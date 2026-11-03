using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;

namespace CLASSIFIED
{
    /// <summary>
    /// Prefixes on the start of each load step. All the async ones are patched on the method that
    /// returns the Task, which runs synchronously up to its first await, so the prefix fires the
    /// moment the step begins.
    ///
    /// DebugTimeMeasurer.StartWithToken would name every step for free, but it is a one-line
    /// static the JIT can inline, so a patch on it is not guaranteed to run.
    /// </summary>
    internal static class Patches
    {
        internal static void Apply(Harmony harmony)
        {
            MethodInfo splashAwake = AccessTools.Method(typeof(SplashScreenPanel), nameof(SplashScreenPanel.Awake));
            if (splashAwake != null)
            {
                harmony.Patch(splashAwake, postfix: new HarmonyMethod(typeof(Patches), nameof(SplashAwake)));
            }
            else
            {
                ClassifiedPlugin.Log.LogWarning("SplashScreenPanel.Awake not found; the game's splash will show before the terminal.");
            }

            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication).BaseType, nameof(TarkovApplication.RunFilesChecking)), nameof(VerifyFiles));
            Prefix(harmony, AccessTools.Method(typeof(ClientApplicationInitOperation), nameof(ClientApplicationInitOperation.Execute)), nameof(MountBundles));
            Prefix(harmony, AccessTools.Method(typeof(UiPools), nameof(UiPools.Init)), nameof(BuildInterface));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.LoadLoginScenes)), nameof(LoginScenes));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.CreateBackend)), nameof(Connect));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.PrepareGameJob)), nameof(ItemDatabase));
            // ItemIconCreator inherits Init from IconCreatorBase<Item, ItemIcon>.
            Prefix(harmony, AccessTools.Method(typeof(ItemIconCreator).BaseType, nameof(ItemIconCreator.Init)), nameof(IconCache));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.GetGlobalConfigAndClientSettingsConfig)), nameof(GameSettings));
            Prefix(harmony, AccessTools.Method(typeof(EftClientBackendSession), nameof(EftClientBackendSession.GetProfiles)), nameof(Profiles));
            Prefix(harmony, AccessTools.Method(typeof(DataPrepareOperation), nameof(DataPrepareOperation.SelectProfile)), nameof(SelectProfile));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.LoadHandbook)), nameof(Handbook));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.RequestBuildList)), nameof(WeaponBuilds));
            Prefix(harmony, AccessTools.Method(typeof(PlayerVoiceLoader), nameof(PlayerVoiceLoader.LoadVoice)), nameof(Voice));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.LoadLocations)), nameof(World));
            Prefix(harmony, AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.MainMenu)), nameof(MainMenu));

            // The overlay draws over everything, so any error the game raises during loading
            // takes it down at once rather than sitting hidden behind it.
            foreach (MethodInfo method in typeof(PreloaderUI).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.Name == nameof(PreloaderUI.ShowErrorScreen) || method.Name == nameof(PreloaderUI.ShowCriticalErrorScreen))
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(Patches), nameof(ErrorShown)));
                }
            }

            // ACCESS GRANTED carries the same fix; once is enough.
            if (!ClassifiedPlugin.AccessGrantedInstalled)
            {
                MenuBackgroundFix.Apply(harmony);
            }
            ApplyDeploy(harmony);

            // After the after-action report (or a raid that ended without one) the game unloads
            // the raid and loads the menu again; the launch's own steps from the item database on
            // mark the rest of the way.
            MethodInfo comeback = AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.ComebackToMainMenu));
            if (comeback != null)
            {
                harmony.Patch(comeback, prefix: new HarmonyMethod(typeof(Patches), nameof(ComingBack)));
            }
            else
            {
                ClassifiedPlugin.Log.LogWarning("TarkovApplication.ComebackToMainMenu not found; the terminal will not show after raids.");
            }

            MethodInfo show = AccessTools.Method(typeof(ProfileLoadingScreen), nameof(ProfileLoadingScreen.Show), new[] { typeof(bool) });
            if (show == null)
            {
                ClassifiedPlugin.Log.LogError("ProfileLoadingScreen.Show(bool) not found; the readout will not appear.");
                return;
            }
            harmony.Patch(show, postfix: new HarmonyMethod(typeof(Patches), nameof(ScreenShown)));
        }

        private static void Prefix(Harmony harmony, MethodInfo target, string prefix)
        {
            if (target == null)
            {
                // A missing step is only a gap in the list, not a reason to lose the rest.
                ClassifiedPlugin.Log.LogWarning($"No method found for step '{prefix}'; it will show as skipped.");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patches), prefix));
        }

        private static void ScreenShown(ProfileLoadingScreen __instance, bool loading)
        {
            if (!loading || !ClassifiedPlugin.Showing)
            {
                return;
            }
            // On a launch the run is already going from the splash; this only picks up the screen.
            StartupTracker.BeginRun(StepId.Connect);
            Overlay.Begin(__instance);
        }

        /// <summary>
        /// The game's splash is the first thing it draws. Take over from it: hide it and start the
        /// terminal, so a launch is one unbroken sequence. Also called from the plugin's Awake in
        /// case the splash woke before the plugin loaded.
        /// </summary>
        internal static void SplashAwake(SplashScreenPanel __instance)
        {
            if (!ClassifiedPlugin.Showing || __instance == null)
            {
                return;
            }
            if (__instance._canvas != null)
            {
                __instance._canvas.enabled = false;
            }
            StartupTracker.BeginRun(StepId.VerifyFiles);
            Overlay.BeginBoot(__instance);
        }

        private static void VerifyFiles() => StartupTracker.Reach(StepId.VerifyFiles);
        private static void MountBundles() => StartupTracker.Reach(StepId.MountBundles);
        private static void BuildInterface() => StartupTracker.Reach(StepId.BuildInterface);
        private static void LoginScenes() => StartupTracker.Reach(StepId.LoginScenes);
        private static void Connect() => StartupTracker.Reach(StepId.Connect);

        private static void ErrorShown() => Overlay.HideNow();

        private static void ItemDatabase() => StartupTracker.Reach(StepId.ItemDatabase);

        private static void IconCache(object __instance)
        {
            // Init is shared by the clothing and player icon creators; only the first one, the
            // item icons, marks the step.
            if (__instance is ItemIconCreator)
            {
                NoteItemCount();
                StartupTracker.Reach(StepId.IconCache);
            }
        }

        private static void GameSettings()
        {
            NoteItemCount();
            StartupTracker.Reach(StepId.GameSettings);
        }

        private static void Profiles() => StartupTracker.Reach(StepId.Profiles);

        private static void SelectProfile(IEftSession backendSession)
        {
            StartupTracker.Reach(StepId.SelectProfile);
            try
            {
                Profile profile = backendSession?.Profile;
                if (profile?.Info != null)
                {
                    StartupTracker.SetDetail(StepId.SelectProfile, $"{profile.Info.Nickname}, level {profile.Info.Level}");
                    StartupTracker.NoteProfile(profile.Info.Nickname,
                        profile.Info.Side == EPlayerSide.Bear ? "BEAR" : profile.Info.Side == EPlayerSide.Usec ? "USEC" : null,
                        profile.Info.Level);
                    StartupTracker.NoteQuests(ActiveQuests(profile));
                    // The terminal may be wearing last launch's faction, or none: put it right.
                    Overlay.ProfileKnown();
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogDebug($"No profile detail: {e.Message}");
            }
        }

        /// <summary>The names of the profile's tasks under way, in the game's language; none it cannot name.</summary>
        private static List<string> ActiveQuests(Profile profile)
        {
            var names = new List<string>();
            if (profile.QuestsData == null)
            {
                return names;
            }
            foreach (EFT.Quests.QuestDataClass quest in profile.QuestsData)
            {
                if (quest == null || string.IsNullOrEmpty(quest.Id)
                    || (quest.Status != EFT.Quests.EQuestStatus.Started && quest.Status != EFT.Quests.EQuestStatus.AvailableForFinish))
                {
                    continue;
                }
                string key = quest.Id + " name";
                string name;
                try
                {
                    name = key.Localized();
                }
                catch (Exception)
                {
                    continue;
                }
                // An unloaded locale hands the key back.
                if (string.IsNullOrEmpty(name) || name == key || name.EndsWith(" name") || name.Length > 60)
                {
                    continue;
                }
                names.Add(name.Trim());
                if (names.Count >= 12)
                {
                    break;
                }
            }
            ClassifiedPlugin.Log.LogInfo($"Profile: {names.Count} tasks under way known to the terminal.");
            return names;
        }

        /// <summary>
        /// The raid load: the deploy screen showing (the run starts in a prefix, since the screen
        /// sends its first status from inside Show), its statuses, whether it can be cancelled, the
        /// final countdown, and the raid starting. Each hook stands alone: a missing one is logged
        /// and the rest still work.
        /// </summary>
        private static void ApplyDeploy(Harmony harmony)
        {
            MethodInfo show = null;
            foreach (MethodInfo method in typeof(MatchmakerTimeHasCome).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.Name == nameof(MatchmakerTimeHasCome.Show) && method.GetParameters().Length == 3)
                {
                    show = method;
                }
            }
            if (show == null)
            {
                ClassifiedPlugin.Log.LogWarning("MatchmakerTimeHasCome.Show not found; the terminal will not show while raids load.");
                return;
            }
            harmony.Patch(show, prefix: new HarmonyMethod(typeof(Patches), nameof(DeployShown)));
            DeployHook(harmony, AccessTools.Method(typeof(MatchmakerTimeHasCome), nameof(MatchmakerTimeHasCome.ChangeStatus)), prefix: nameof(DeployStatus));
            DeployHook(harmony, AccessTools.Method(typeof(MatchmakerTimeHasCome), nameof(MatchmakerTimeHasCome.ChangeCancelButtonVisibility)), postfix: nameof(CancelAvailable));
            DeployHook(harmony, AccessTools.Method(typeof(MatchmakerFinalCountdown), nameof(MatchmakerFinalCountdown.Show), new[] { typeof(Profile), typeof(DateTime) }), postfix: nameof(CountdownShown));
            DeployHook(harmony, AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted)), postfix: nameof(RaidStarted));
        }

        private static void DeployHook(Harmony harmony, MethodInfo target, string prefix = null, string postfix = null)
        {
            if (target == null)
            {
                ClassifiedPlugin.Log.LogWarning($"No method found for raid-load hook '{prefix ?? postfix}'; the raid-loading terminal works without it.");
                return;
            }
            harmony.Patch(target,
                prefix: prefix != null ? new HarmonyMethod(typeof(Patches), prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(typeof(Patches), postfix) : null);
        }

        private static void DeployShown(MatchmakerTimeHasCome __instance, RaidSettings raidSettings)
        {
            try
            {
                if (!ClassifiedPlugin.ShowThisDeploy())
                {
                    return;
                }
                StartupTracker.BeginDeploy();
                Overlay.BeginDeploy(__instance, raidSettings);
            }
            catch (Exception e)
            {
                // Never let the terminal stand between the player and a raid.
                ClassifiedPlugin.Log.LogError("Raid-loading terminal failed to start: " + e);
                Overlay.HideNow();
            }
        }

        private static void ComingBack()
        {
            try
            {
                if (ClassifiedPlugin.ShowThisReturn() && StartupTracker.BeginReturn())
                {
                    Overlay.BeginReturn();
                }
            }
            catch (Exception e)
            {
                // Never let the terminal stand between the player and the menu.
                ClassifiedPlugin.Log.LogError("After-raid terminal failed to start: " + e);
                Overlay.HideNow();
            }
        }

        private static void DeployStatus(string status, float? progress) => StartupTracker.DeployStatus(status, progress);

        private static void CancelAvailable(bool available) => Overlay.CanCancel = available;

        private static void CountdownShown(DateTime gameStartTime)
        {
            float seconds = (float)(gameStartTime - EFT.DateTimeExtensions.Now).TotalSeconds;
            StartupTracker.ReachCountdown(seconds);
            Overlay.Countdown(seconds);
        }

        private static void RaidStarted() => Overlay.RaidStarted();

        private static void Handbook() => StartupTracker.Reach(StepId.Handbook);
        private static void WeaponBuilds() => StartupTracker.Reach(StepId.WeaponBuilds);
        private static void Voice() => StartupTracker.Reach(StepId.Voice);
        private static void World() => StartupTracker.Reach(StepId.World);
        private static void MainMenu() => StartupTracker.Reach(StepId.MainMenu);

        private static int _itemCount;

        /// <summary>
        /// Puts the item count on the item database step. Also polled by the overlay while the
        /// step runs, so the number climbs if the game fills the table as it goes; the text is
        /// only rebuilt when the count moves.
        /// </summary>
        internal static void NoteItemCount()
        {
            if (Singleton<ItemFactory>.Instantiated && Singleton<ItemFactory>.Instance.ItemTemplates != null)
            {
                int count = Singleton<ItemFactory>.Instance.ItemTemplates.Count;
                if (count != _itemCount || StartupTracker.Detail((int)StepId.ItemDatabase) == null)
                {
                    _itemCount = count;
                    StartupTracker.SetDetail(StepId.ItemDatabase, $"{count:N0} items");
                }
            }
        }
    }
}
