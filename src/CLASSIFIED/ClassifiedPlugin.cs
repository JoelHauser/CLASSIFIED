using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CLASSIFIED
{
    /// <summary>The language BEAR's terminal speaks.</summary>
    public enum BearText
    {
        Russian,
        English
    }

    /// <summary>What the raid-loading terminal does when Deploy Screen Overhaul is installed too.</summary>
    public enum DeployScreenMode
    {
        StandAside,
        TakeTurns,
        AlwaysTerminal
    }

    /// <summary>Whose units the showreel plays.</summary>
    public enum ReelFactions
    {
        Both,
        Bear,
        Usec
    }

    /// <summary>What CLASSIFIED does when ACCESS GRANTED is installed too.</summary>
    public enum AccessGrantedMode
    {
        TakeTurns,
        AlwaysClassified,
        AlwaysAccessGranted
    }

    /// <summary>
    /// Replaces the bare "Loading profile data..." wait with your faction's terminal, BEAR or
    /// USEC, showing what the game is actually doing: each load step as it starts, how long the
    /// finished ones took, and progress timed from the last launch. See <see cref="StartupTracker"/>
    /// for the steps and <see cref="Overlay"/> for the drawing. A sister mod of ACCESS GRANTED,
    /// built from the same code; the two take turns when both are installed.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(AccessGrantedGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class ClassifiedPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.mybutthasarash.classified";
        public const string PluginName = "CLASSIFIED";

        /// <summary>Must match the csproj's Version. Two places, and they have to agree.</summary>
        public const string PluginVersion = "0.1.0";

        private const string AccessGrantedGuid = "com.mybutthasarash.accessgranted";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<FactionChoice> FactionSetting;
        internal static ConfigEntry<BearText> BearLanguage;
        internal static ConfigEntry<AccessGrantedMode> WithAccessGranted;
        internal static ConfigEntry<ThemeChoice> Theme;
        internal static ConfigEntry<GlitchLevel> Glitches;
        internal static ConfigEntry<LayoutKind> Layout;
        internal static ConfigEntry<BackdropKind> Backdrop;
        internal static ConfigEntry<FrameKind> Frame;
        internal static ConfigEntry<IntroKind> Intro;
        internal static ConfigEntry<BarKind> Bar;
        internal static ConfigEntry<OutroKind> Ending;
        internal static ConfigEntry<KeyboardShortcut> PreviewKey;
        internal static ConfigEntry<float> PreviewSeconds;
        internal static ConfigEntry<float> ShotSeconds;
        internal static ConfigEntry<ReelFactions> ReelUnits;
        internal static ConfigEntry<RunKind> PreviewLoad;
        internal static ConfigEntry<bool> RaidLoading;
        internal static ConfigEntry<DeployScreenMode> WithDeployScreen;
        internal static ConfigEntry<bool> ShowMapName;

        private const string DeployScreenGuid = "com.mybutthasarash.deployscreen";

        /// <summary>Whether Deploy Screen Overhaul is installed: it draws the same screen. Asked when a raid loads, since it may load after this plugin.</summary>
        internal static bool DeployScreenInstalled => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(DeployScreenGuid);

        /// <summary>
        /// A raid is loading: whether this terminal shows it. Off unless switched on. With Deploy
        /// Screen Overhaul installed it stands aside by default, or they take turns. With ACCESS
        /// GRANTED's raid terminal on too, the With ACCESS GRANTED setting decides, raid by raid.
        /// Counts the raid either way, so taking turns stays in step.
        /// </summary>
        internal static bool ShowThisDeploy()
        {
            StartupTracker.CountDeploy();
            if (!Enabled.Value || !RaidLoading.Value)
            {
                return false;
            }
            if (DeployScreenInstalled)
            {
                switch (WithDeployScreen.Value)
                {
                    case DeployScreenMode.StandAside:
                        return false;
                    case DeployScreenMode.TakeTurns:
                        if (StartupTracker.Deploys % 2 == 0)
                        {
                            Log.LogInfo("Deploy Screen Overhaul is installed: this raid load is its turn.");
                            return false;
                        }
                        break;
                }
            }
            if (AccessGrantedInstalled && AccessGrantedShowsRaids())
            {
                switch (WithAccessGranted.Value)
                {
                    case AccessGrantedMode.AlwaysAccessGranted:
                        return false;
                    case AccessGrantedMode.TakeTurns:
                        // Over the deploys this terminal is allowed, every other one is ACCESS GRANTED's.
                        _raidTurn++;
                        if (_raidTurn % 2 == 0)
                        {
                            Log.LogInfo("ACCESS GRANTED shows raid loads too: this one is its turn.");
                            return false;
                        }
                        break;
                }
            }
            return true;
        }

        private static int _raidTurn;

        /// <summary>Whether ACCESS GRANTED has its own raid-loading terminal switched on, from its settings.</summary>
        private static bool AccessGrantedShowsRaids()
        {
            try
            {
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(AccessGrantedGuid, out PluginInfo info) && info.Instance != null
                    && info.Instance.Config.TryGetEntry(new ConfigDefinition("Raid Loading", "Show While Loading Raids"), out ConfigEntry<bool> entry))
                {
                    return entry.Value;
                }
            }
            catch (System.Exception e)
            {
                Log.LogDebug("Could not read ACCESS GRANTED's raid setting: " + e.Message);
            }
            return false;
        }

        /// <summary>Whether ACCESS GRANTED is installed: then the two share the launches.</summary>
        internal static bool AccessGrantedInstalled { get; private set; }

        /// <summary>True on the launches this mod sits out for ACCESS GRANTED.</summary>
        private static bool _standingAside;

        /// <summary>Whether this launch's loading screen is CLASSIFIED's.</summary>
        internal static bool Showing => Enabled.Value && !_standingAside;

        /// <summary>When the showreel asked for from F12 starts; below zero, none is waiting.</summary>
        private static float _showreelAt = -1f;
        private static bool _showreelAsked;

        /// <summary>Gives the F12 window time to close, so none of it is in the recording.</summary>
        private const float ShowreelDelay = 0.5f;

        private void Update()
        {
            Overlay.RestoreOther();
            if (PreviewKey.Value.IsDown())
            {
                Overlay.StartPreview();
            }
            if (_showreelAsked)
            {
                // Handled here rather than in the button: closing the window from inside its own
                // drawing would upset its layout.
                _showreelAsked = false;
                if (Overlay.CanPlayOverMenu())
                {
                    CloseConfigWindow();
                    _showreelAt = UnityEngine.Time.unscaledTime + ShowreelDelay;
                }
                else
                {
                    Log.LogInfo("Showreel: plays from the main menu only.");
                }
            }
            if (_showreelAt >= 0f && UnityEngine.Time.unscaledTime >= _showreelAt)
            {
                _showreelAt = -1f;
                Overlay.StartShowreel();
            }
            Overlay.PreviewInput();
            Overlay.DeployInput();
        }

        private static void DrawShowreelButton(ConfigEntryBase entry)
        {
            if (UnityEngine.GUILayout.Button("Play Showreel", UnityEngine.GUILayout.ExpandWidth(true)))
            {
                _showreelAsked = true;
            }
        }

        /// <summary>Closes the F12 window (BetterBepInEx's follows the stock one).</summary>
        private static void CloseConfigWindow()
        {
            try
            {
                foreach (PluginInfo info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                {
                    if (info.Metadata.GUID == "com.bepis.bepinex.configurationmanager" && info.Instance != null)
                    {
                        info.Instance.GetType().GetProperty("DisplayingWindow")?.SetValue(info.Instance, false, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.LogWarning("Showreel: could not close the F12 window: " + e.Message);
            }
        }

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Show your faction's loading terminal instead of the plain loading text.");
            FactionSetting = Config.Bind("General", "Faction", FactionChoice.Profile,
                "Whose terminal to show. Profile follows your profile's faction (the very first launch, before the profile loads, picks a side at random); BEAR or USEC shows that side's whatever your profile.");
            BearLanguage = Config.Bind("General", "BEAR Language", BearText.Russian,
                "BEAR's terminal speaks Russian: its steps, orders, chatter and labels. English shows all of it in English instead.");
            WithAccessGranted = Config.Bind("General", "With ACCESS GRANTED", AccessGrantedMode.TakeTurns,
                "Only matters when ACCESS GRANTED is installed too. Take Turns alternates launches between the TerraGroup terminal and yours; or always show one of the two.");
            Theme = Config.Bind("Style", "Unit", ThemeChoice.Random,
                "The unit the terminal belongs to, with its own colours, drawing and orders. Random picks one of your faction's each launch, never one shown in the last few launches.");
            Glitches = Config.Bind("Style", "Glitches", GlitchLevel.Subtle,
                "How often the terminal glitches: tears, colour splits, garbled text. Off for none.");
            Layout = Config.Bind("Style", "Layout", LayoutKind.Random,
                "How the insignia and the readout are arranged. Random picks a different one each launch; pick one to use it every launch.");
            Backdrop = Config.Bind("Style", "Background", BackdropKind.Random,
                "The pattern behind everything. Random lets each unit pick one of its own.");
            Frame = Config.Bind("Style", "Insignia Frame", FrameKind.Random,
                "What surrounds the insignia. Random lets each unit pick one of its own.");
            Intro = Config.Bind("Style", "Intro", IntroKind.Random,
                "How the screen comes on. Random lets each unit pick one of its own.");
            Bar = Config.Bind("Style", "Progress Bar", BarKind.Random,
                "The progress bar. Random lets each unit pick one of its own.");
            Ending = Config.Bind("Style", "Ending", OutroKind.Random,
                "How the screen leaves for the main menu. Random lets each unit pick one of its own.");
            PreviewKey = Config.Bind("Preview", "Preview Key", new KeyboardShortcut(UnityEngine.KeyCode.F6, UnityEngine.KeyCode.LeftControl),
                "In the main menu, plays the terminal with a pretend load. The keys are listed along the bottom while it "
                + "shows: Left/Right unit, Up/Down layout, 1 to 6 pin a part, Space holds the load, -/+ speed, "
                + "[ and ] step, E jumps to the ending, V hides parts of the screen, H hides the key list, Esc closes.");
            PreviewSeconds = Config.Bind("Preview", "Preview Length", 12f, new ConfigDescription(
                "How long the preview's pretend load takes, in seconds, before its ending plays.",
                new AcceptableValueRange<float>(5f, 60f)));
            ShotSeconds = Config.Bind("Preview", "Showreel Shot Length", 6f, new ConfigDescription(
                "How long each unit stays on screen in the showreel, in seconds. The first holds a little longer for its title card.",
                new AcceptableValueRange<float>(2f, 20f)));
            PreviewLoad = Config.Bind("Preview", "Preview Load", RunKind.Launch,
                "Which load the preview pretends to be when it opens: the game launching, or a raid loading. T switches while it shows.");
            RaidLoading = Config.Bind("Raid Loading", "Show While Loading Raids", false,
                "Also show the terminal while a raid loads, from the deploy screen to the raid: the game's real load steps, "
                + "the countdown, then into the raid. One notice at most. Esc cancels the raid while the game allows it.");
            WithDeployScreen = Config.Bind("Raid Loading", "With Deploy Screen Overhaul", DeployScreenMode.StandAside,
                "Only matters when Deploy Screen Overhaul is installed too, since both draw the raid loading screen. "
                + "Stand Aside leaves raid loads to it; Take Turns alternates raids; Always Terminal shows this terminal every raid.");
            ShowMapName = Config.Bind("Raid Loading", "Show Map Name", false,
                "Name the map on the raid-loading terminal. Off gives it a site code instead.");
            ReelUnits = Config.Bind("Preview", "Showreel Units", ReelFactions.Both,
                "Whose units the showreel plays: all twelve, BEAR and USEC in turn, or one faction's six.");
            // A button, not a setting: the value is never read.
            Config.Bind("Preview", "Showreel", false, new ConfigDescription(
                "Closes this menu and plays every unit in turn over one pretend load, crossfading from one to the next, "
                + "then hands over to the main menu. Nothing on screen but the terminal, for recording; while it plays, "
                + "Left/Right skip units, Space holds one, -/+ change speed, H shows what is on screen, Esc stops it. Main menu only.",
                null, new ConfigurationManagerAttributes { CustomDrawer = DrawShowreelButton, HideDefaultButton = true }));

            StartupTracker.LoadTimings();
            DecideTurn();
            Patches.Apply(new Harmony(PluginGuid));

            // If the splash woke before this plugin loaded, its Awake patch never ran.
            var splash = FindObjectOfType<EFT.UI.SplashScreenPanel>();
            if (splash != null)
            {
                Patches.SplashAwake(splash);
            }
        }

        /// <summary>
        /// With ACCESS GRANTED installed, both mods would draw over the same loading screen. Taking
        /// turns, CLASSIFIED shows on every other launch and sits out the rest; on its own
        /// launches it keeps ACCESS GRANTED's terminal out of sight (see Overlay.StandInFor). Neither
        /// mod refuses to load, so nothing shows as a failed plugin.
        /// </summary>
        private static void DecideTurn()
        {
            AccessGrantedInstalled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(AccessGrantedGuid);
            if (!AccessGrantedInstalled || !Enabled.Value)
            {
                return;
            }
            switch (WithAccessGranted.Value)
            {
                case AccessGrantedMode.AlwaysAccessGranted:
                    _standingAside = true;
                    break;
                case AccessGrantedMode.TakeTurns:
                    _standingAside = StartupTracker.Launches % 2 == 1;
                    if (_standingAside)
                    {
                        // This launch is ACCESS GRANTED's; count it, so the next one is ours.
                        StartupTracker.SkipLaunch();
                    }
                    break;
            }
            Log.LogInfo(_standingAside
                ? "ACCESS GRANTED is installed: this launch is its turn."
                : "ACCESS GRANTED is installed: this launch is CLASSIFIED's turn.");
        }
    }
}
