using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>How fast the terminal must keep drawing while a raid loads, against how much of the raid the game loads in each frame.</summary>
    public enum LoadFrameRate
    {
        Unlocked,
        Fps30,
        Fps20,
        Fps15
    }

    /// <summary>
    /// What the game keeps doing under the raid-loading terminal that nobody can see, switched
    /// off while the terminal covers it, and, if the player allows it, more of each frame given
    /// to the load itself. Everything is put back as it was when the terminal leaves.
    ///
    /// Always, while the terminal covers the load:
    /// - The deploy screen's player model (a turntable view of the PMC, loaded once the raid's
    ///   pools are ready) is never loaded.
    /// - Once the terminal's intro is over, the main menu's 3D background and its camera go off,
    ///   and so do the game's own deploy and countdown screens and the menu bar under them. They
    ///   stop being drawn: their canvases are switched off, never the screens themselves, which
    ///   keep running exactly as before.
    ///
    /// With a frame rate limit chosen (Unlocked, the default, changes neither):
    /// - The game loads a raid through its job scheduler, which each frame runs loading work for
    ///   a set time (1.5 frames at the menu's frame cap) and then draws. A limit raises that time
    ///   to most of a frame at the limit, so more of each second goes to loading.
    /// - Unity finishes loaded assets for a set time each frame (2 ms on Low, up to 50 ms on High),
    ///   and the game's map streamer leaves it on Low after a raid. A limit raises it.
    ///
    /// When the terminal leaves: if the raid started, or the game went back to its menus, the
    /// game has already set the background as it wants it. If the deploy screen is still up (the
    /// terminal left early), the background and the player model come back too.
    /// </summary>
    internal static class DeployScene
    {
        private static readonly AccessTools.FieldRef<EnvironmentUI, EnvironmentUIRoot> CurrentEnvironment =
            Field<EnvironmentUI, EnvironmentUIRoot>("_currentEnvironment");

        /// <summary>How much of a frame at the limit the loading work may take; the rest is for drawing.</summary>
        private const float LoadShare = 0.75f;

        private static MatchmakerTimeHasCome _screen;
        private static MatchmakerFinalCountdown _countdown;
        private static bool _covering;
        private static bool _skippedModel;
        private static bool _sceneOff;
        private static bool _environmentWasShown;
        private static bool _cameraWasOn;

        private static float _boost = -1f;
        private static bool _priorityRaised;
        private static ThreadPriority _priorPriority;
        private static ThreadPriority _raisedPriority;

        private struct HiddenCanvas
        {
            public GameObject Owner;
            public Canvas Canvas;
            public bool Added;
            public bool WasEnabled;
        }

        private static readonly List<HiddenCanvas> Hidden = new List<HiddenCanvas>(4);

        internal static void Apply(Harmony harmony)
        {
            MethodInfo showModel = AccessTools.Method(typeof(MatchmakerTimeHasCome), nameof(MatchmakerTimeHasCome.ShowPlayerModel));
            if (showModel != null)
            {
                harmony.Patch(showModel, prefix: new HarmonyMethod(typeof(DeployScene), nameof(SkipModel)));
            }
            else
            {
                ClassifiedPlugin.Log.LogWarning("MatchmakerTimeHasCome.ShowPlayerModel not found; the deploy screen's player model still loads under the terminal.");
            }

            MethodInfo forceMode = AccessTools.Method(typeof(JobScheduler), nameof(JobScheduler.SetForceMode));
            if (forceMode != null)
            {
                harmony.Patch(forceMode, prefix: new HarmonyMethod(typeof(DeployScene), nameof(KeepBoost)));
            }
            else
            {
                ClassifiedPlugin.Log.LogWarning("JobScheduler.SetForceMode not found; a raid-load frame rate limit only applies until the game resets it.");
            }
        }

        /// <summary>The terminal has come up over this deploy screen.</summary>
        internal static void Cover(MatchmakerTimeHasCome screen, LoadFrameRate limit)
        {
            _screen = screen;
            _countdown = null;
            _covering = true;
            _skippedModel = false;
            Boost(limit);
        }

        /// <summary>The final countdown showed: it goes the same way as the deploy screen.</summary>
        internal static void CountdownShown(MatchmakerFinalCountdown countdown)
        {
            if (_covering)
            {
                _countdown = countdown;
            }
        }

        /// <summary>
        /// Every frame of a raid load once the terminal's intro is over and it covers the whole
        /// screen: what is under it stops being drawn. Does nothing once done.
        /// </summary>
        internal static void Covered()
        {
            if (!_covering)
            {
                return;
            }
            if (!_sceneOff)
            {
                _sceneOff = true;
                SceneOff();
                HideCanvas(_screen != null ? _screen.gameObject : null);
                PreloaderUI preloader = MonoBehaviourSingleton<PreloaderUI>.Instantiated ? MonoBehaviourSingleton<PreloaderUI>.Instance : null;
                if (preloader != null)
                {
                    HideCanvas(preloader.MenuTaskBar != null ? preloader.MenuTaskBar.gameObject : null);
                    HideCanvas(preloader._bottomPanelBackground);
                }
                ClassifiedPlugin.Log.LogInfo("Raid load: the game's screens under the terminal are no longer drawn.");
            }
            if (_countdown != null)
            {
                HideCanvas(_countdown.gameObject);
                _countdown = null;
            }
        }

        /// <summary>
        /// The terminal is leaving: everything goes back as it was. The background and the model
        /// only if the deploy screen is still up to be seen; the game sets them itself anywhere else.
        /// </summary>
        internal static void Uncover(bool raidStarted)
        {
            if (!_covering)
            {
                return;
            }
            _covering = false;
            MatchmakerTimeHasCome screen = _screen;
            _screen = null;
            _countdown = null;
            bool sceneOff = _sceneOff;
            bool skippedModel = _skippedModel;
            _sceneOff = false;
            _skippedModel = false;

            Unboost();
            ShowCanvases();

            EftScreenManager manager = EftScreenManager._instance;
            EEftScreenType current = manager != null && manager.CurrentScreenController != null
                ? manager.CurrentScreenController.ScreenType
                : EEftScreenType.None;
            if (raidStarted || (current != EEftScreenType.TimeHasCome && current != EEftScreenType.FinalCountdown))
            {
                return;
            }
            try
            {
                if (sceneOff && MonoBehaviourSingleton<EnvironmentUI>.Instantiated)
                {
                    EnvironmentUI environment = MonoBehaviourSingleton<EnvironmentUI>.Instance;
                    if (_environmentWasShown)
                    {
                        environment.ShowEnvironment(true);
                    }
                    EnvironmentUIRoot root = CurrentEnvironment != null ? CurrentEnvironment(environment) : null;
                    if (_cameraWasOn && root != null)
                    {
                        root.SetCameraActive(true);
                    }
                    ClassifiedPlugin.Log.LogInfo("Raid load: the terminal left early; the menu background is back.");
                }
                if (skippedModel && current == EEftScreenType.TimeHasCome && screen != null && screen.isActiveAndEnabled)
                {
                    Task load = screen.ShowPlayerModel();
                    load.ContinueWith(t => ClassifiedPlugin.Log.LogWarning("Raid load: the player model did not come back: " + t.Exception?.GetBaseException().Message),
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Raid load: could not give the deploy screen back what the terminal switched off: " + e.Message);
            }
        }

        /// <summary>The menu's 3D background and its camera go off.</summary>
        private static void SceneOff()
        {
            _environmentWasShown = false;
            _cameraWasOn = false;
            if (!MonoBehaviourSingleton<EnvironmentUI>.Instantiated)
            {
                return;
            }
            try
            {
                EnvironmentUI environment = MonoBehaviourSingleton<EnvironmentUI>.Instance;
                EnvironmentUIRoot root = CurrentEnvironment != null ? CurrentEnvironment(environment) : null;
                _environmentWasShown = environment.IsActive;
                _cameraWasOn = root != null && root.CameraContainer != null && root.CameraContainer.gameObject.activeSelf;
                // Not EnvironmentUI.ShowCameraContainer: that would also switch the game's own camera on.
                if (_cameraWasOn)
                {
                    root.SetCameraActive(false);
                }
                if (_environmentWasShown)
                {
                    environment.ShowEnvironment(false);
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Raid load: could not switch the menu background off: " + e.Message);
            }
        }

        /// <summary>
        /// Stops a piece of the game's interface being drawn, without touching the object itself:
        /// its own canvas is switched off, or, if it has none, a switched-off one is added (a
        /// canvas that is off draws nothing under it) and taken away again afterwards.
        /// </summary>
        private static void HideCanvas(GameObject owner)
        {
            if (owner == null)
            {
                return;
            }
            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i].Owner == owner)
                {
                    return;
                }
            }
            try
            {
                Canvas canvas = owner.GetComponent<Canvas>();
                bool added = canvas == null;
                if (added)
                {
                    canvas = owner.AddComponent<Canvas>();
                }
                Hidden.Add(new HiddenCanvas { Owner = owner, Canvas = canvas, Added = added, WasEnabled = added || canvas.enabled });
                canvas.enabled = false;
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning($"Raid load: {owner.name} is still drawn under the terminal: {e.Message}");
            }
        }

        private static void ShowCanvases()
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                HiddenCanvas hidden = Hidden[i];
                try
                {
                    if (hidden.Canvas == null)
                    {
                        continue;
                    }
                    if (hidden.Added)
                    {
                        UnityEngine.Object.Destroy(hidden.Canvas);
                    }
                    else
                    {
                        hidden.Canvas.enabled = hidden.WasEnabled;
                    }
                }
                catch (Exception e)
                {
                    ClassifiedPlugin.Log.LogWarning("Raid load: could not give a game screen its canvas back: " + e.Message);
                }
            }
            Hidden.Clear();
        }

        /// <summary>With a frame rate limit: more of each frame for the load, up to about the limit. Never less than the game gives it.</summary>
        private static void Boost(LoadFrameRate limit)
        {
            _boost = -1f;
            _priorityRaised = false;
            int fps = limit == LoadFrameRate.Fps30 ? 30 : limit == LoadFrameRate.Fps20 ? 20 : limit == LoadFrameRate.Fps15 ? 15 : 0;
            if (fps <= 0)
            {
                return;
            }
            try
            {
                if (Singleton<JobScheduler>.Instantiated)
                {
                    JobScheduler scheduler = Singleton<JobScheduler>.Instance;
                    long loop = scheduler.LoopTicks;
                    float wanted = loop > 0 ? TimeSpan.TicksPerSecond / (float)fps * LoadShare / loop : -1f;
                    if (wanted > scheduler.DefaultForceModeMultiplier)
                    {
                        _boost = wanted;
                        if (scheduler.IsForceModeEnabled)
                        {
                            scheduler.SetForceMode(true, _boost);
                        }
                    }
                }

                // Unity's own steps: Low 2 ms, Below Normal 4, Normal 10, High 50 a frame.
                ThreadPriority wantedPriority = fps <= 20 ? ThreadPriority.High : ThreadPriority.Normal;
                _priorPriority = Application.backgroundLoadingPriority;
                if (wantedPriority > _priorPriority)
                {
                    Application.backgroundLoadingPriority = wantedPriority;
                    _raisedPriority = wantedPriority;
                    _priorityRaised = true;
                }
                ClassifiedPlugin.Log.LogInfo($"Raid load: frame rate limit {fps}: loading budget x{(_boost > 0f ? _boost : (Singleton<JobScheduler>.Instantiated ? Singleton<JobScheduler>.Instance.DefaultForceModeMultiplier : 0f)):0.0}, "
                    + $"asset loading {(_priorityRaised ? _priorPriority + " -> " + _raisedPriority : _priorPriority + " (kept)")}.");
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Raid load: could not raise the loading budget: " + e.Message);
            }
        }

        /// <summary>The game's own budget and Unity's setting back, unless the game has changed them since.</summary>
        private static void Unboost()
        {
            try
            {
                if (_boost > 0f && Singleton<JobScheduler>.Instantiated)
                {
                    JobScheduler scheduler = Singleton<JobScheduler>.Instance;
                    _boost = -1f;
                    if (scheduler.IsForceModeEnabled)
                    {
                        scheduler.SetForceMode(true, scheduler.DefaultForceModeMultiplier);
                    }
                }
                _boost = -1f;
                if (_priorityRaised && Application.backgroundLoadingPriority == _raisedPriority)
                {
                    Application.backgroundLoadingPriority = _priorPriority;
                }
                _priorityRaised = false;
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Raid load: could not put the loading budget back: " + e.Message);
            }
        }

        /// <summary>If the game switches its loading mode on again while the terminal covers the load, it keeps the raised budget.</summary>
        private static void KeepBoost(bool enable, ref float forceModeMultiplier)
        {
            if (_covering && _boost > 0f && enable && forceModeMultiplier < _boost)
            {
                forceModeMultiplier = _boost;
            }
        }

        private static bool SkipModel(ref Task __result)
        {
            if (!_covering)
            {
                return true;
            }
            if (!_skippedModel)
            {
                ClassifiedPlugin.Log.LogInfo("Raid load: the player model is not loaded while the terminal covers the deploy screen.");
            }
            _skippedModel = true;
            __result = Task.CompletedTask;
            return false;
        }

        private static AccessTools.FieldRef<TObject, TField> Field<TObject, TField>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<TObject, TField>(name);
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning($"{typeof(TObject).Name}.{name} not found ({e.Message}); the menu background stays on under the terminal.");
                return null;
            }
        }
    }
}
