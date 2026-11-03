using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CLASSIFIED
{
    /// <summary>
    /// The load steps, in the order the game runs them: the launch's, then the raid load's. A run
    /// covers one stretch of them (see <see cref="StartupTracker.FirstStep"/> and
    /// <see cref="StartupTracker.LastStep"/>).
    /// </summary>
    public enum StepId
    {
        VerifyFiles,
        MountBundles,
        BuildInterface,
        LoginScenes,
        Connect,
        ItemDatabase,
        IconCache,
        GameSettings,
        Profiles,
        SelectProfile,
        Handbook,
        WeaponBuilds,
        Voice,
        World,
        MainMenu,

        // The raid load, from the deploy screen to the raid.
        DeployPrepare,
        DeployBundles,
        DeployMap,
        DeployCache,
        DeployMatch,
        DeployObjects,
        DeployStart,
        DeployCountdown
    }

    /// <summary>Which load a run is: the game starting up, a raid loading, or the game going back to the menu after a raid.</summary>
    public enum RunKind
    {
        Launch,
        Deploy,
        Return
    }

    /// <summary>
    /// One pass of loading, until the main menu replaces it. A launch starts at the game's splash
    /// screen and runs every step; a later visit to the profile loading screen starts at
    /// <see cref="StepId.Connect"/>, and the steps before it are not part of that run.
    /// Steps only move forward: several hooked methods (GetProfiles, LoadVoice) run again later,
    /// and a later call must not drag the readout back.
    ///
    /// How long each step took is kept between launches, so the bar can be weighted by real time
    /// instead of step count.
    /// </summary>
    internal static class StartupTracker
    {
        internal static readonly string[] Labels =
        {
            "Verifying game files",
            "Mounting asset bundles",
            "Building interface",
            "Loading login scenes",
            "Connecting to server",
            "Building item database",
            "Reading icon cache",
            "Fetching game settings",
            "Loading profiles",
            "Selecting profile",
            "Loading handbook",
            "Loading weapon builds",
            "Loading voice",
            "Loading weather and maps",
            "Opening main menu",

            "Preparing the raid",
            "Loading asset bundles",
            "Loading the area",
            "Caching data",
            "Matching",
            "Loading objects",
            "Starting the raid",
            "Deploying"
        };

        /// <summary>The same steps in Russian, for BEAR's terminal.</summary>
        internal static readonly string[] LabelsRu =
        {
            "Проверка файлов игры",
            "Подключение пакетов ресурсов",
            "Построение интерфейса",
            "Загрузка сцен входа",
            "Соединение с сервером",
            "Построение базы предметов",
            "Чтение кэша значков",
            "Получение настроек",
            "Загрузка профилей",
            "Выбор профиля",
            "Загрузка справочника",
            "Загрузка сборок оружия",
            "Загрузка озвучки",
            "Загрузка погоды и карт",
            "Открытие главного меню",

            "Подготовка к выходу",
            "Загрузка пакетов ресурсов",
            "Загрузка района",
            "Кэширование данных",
            "Сбор группы",
            "Загрузка объектов",
            "Начало рейда",
            "Выдвижение"
        };

        /// <summary>
        /// What the way back from a raid calls its first step, which stands in the place of
        /// <see cref="StepId.Connect"/>: the raid unloading, the clean-up after it, and the menu
        /// scene loading. The rest of the way back is the launch's own steps from the item
        /// database on.
        /// </summary>
        private const string LeaveRaidLabel = "Leaving the raid";
        private const string LeaveRaidLabelRu = "Выход из рейда";

        /// <summary>For a first way back, in seconds, before this machine's own are known: guesses.</summary>
        private static readonly float[] DefaultReturnSeconds =
        {
            0f, 0f, 0f, 0f, 6f, 1f, 0.4f, 0.7f, 0.6f, 0.5f, 2.7f, 0.2f, 1.6f, 1f, 1.5f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f
        };

        /// <summary>
        /// For a first run, in seconds: one real SPT 4.1 launch (the four boot steps are guesses),
        /// and the medians of 165 raid loads on a 12700K. Replaced by this machine's own times
        /// after one.
        /// </summary>
        private static readonly float[] DefaultSeconds =
        {
            0.5f, 6f, 1f, 3f, 0.1f, 9f, 0.4f, 0.7f, 0.6f, 0.5f, 2.7f, 0.2f, 1.6f, 1f, 1.5f,
            0.8f, 1.5f, 17f, 11f, 0.1f, 1.1f, 55f, 9f
        };

        /// <summary>The raid load's statuses, as the game sends them to its deploy screen (keys, not translated text).</summary>
        private static readonly Dictionary<string, StepId> DeployStatuses = new Dictionary<string, StepId>
        {
            { "Preparing the game...", StepId.DeployPrepare },
            { "LoadingBundles", StepId.DeployBundles },
            { "CreatingPools", StepId.DeployBundles },
            { "Map loading...", StepId.DeployMap },
            { "Caching data...", StepId.DeployCache },
            { "Matching...", StepId.DeployMatch },
            { "Loading objects...", StepId.DeployObjects },
            { "Local game starting...", StepId.DeployStart }
        };

        /// <summary>Every step there is: the size of the per-step arrays, not of a run.</summary>
        internal static int StepCount => Labels.Length;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly float[] Expected = (float[])DefaultSeconds.Clone();

        /// <summary>The way back's own step times: the steps it shares with a launch run differently after a raid.</summary>
        private static readonly float[] ReturnExpected = (float[])DefaultReturnSeconds.Clone();
        private static readonly float[] Took = new float[Labels.Length];
        private static readonly bool[] Skipped = new bool[Labels.Length];
        private static readonly string[] Details = new string[Labels.Length];

        // Frame times within the running step, for the log: where the game itself stalls.
        private static int _stepFrames;
        private static int _stepLongFrames;
        private static float _stepWorstFrame;

        // What drawing the terminal itself cost, per step and over the run, so the log can show
        // it adds next to nothing to a launch.
        private static float _stepTerminal;
        private static float _runTerminal;
        private static int _runFrames;
        private const float LongFrame = 0.05f;

        private static float _runStart;
        private static float _stepStart;
        private static float _shownFraction;

        internal static bool Active { get; private set; }
        internal static int Current { get; private set; } = -1;

        /// <summary>What the running (or last) run is loading.</summary>
        internal static RunKind Kind { get; private set; }

        /// <summary>The step this run started at: 0 for a launch, <see cref="StepId.Connect"/> for a later profile load, the first deploy step for a raid.</summary>
        internal static int FirstStep { get; private set; }

        /// <summary>The step this run ends on.</summary>
        internal static int LastStep { get; private set; } = (int)StepId.MainMenu;

        /// <summary>How many steps this run shows.</summary>
        internal static int RunSteps => LastStep - FirstStep + 1;

        /// <summary>Total of the last launch that ran from the splash to the main menu, or 0 if none is known.</summary>
        internal static float LastTotal { get; private set; }

        /// <summary>Total of the last raid load that reached the raid, or 0 if none is known.</summary>
        internal static float LastDeployTotal { get; private set; }

        /// <summary>Launches from the splash so far, this one included.</summary>
        internal static int Launches { get; private set; }

        /// <summary>Total of the last way back from a raid that reached the main menu, or 0 if none is known.</summary>
        internal static float LastReturnTotal { get; private set; }

        /// <summary>Raid loads so far, this one included, shown or not.</summary>
        internal static int Deploys { get; private set; }

        /// <summary>The selected profile's nickname, once the game has picked it; null before.</summary>
        internal static string Nickname { get; set; }

        /// <summary>The selected profile's faction, BEAR or USEC; null before the game picks a profile.</summary>
        internal static string Side { get; private set; }

        /// <summary>The selected profile's level; 0 before the game picks a profile.</summary>
        internal static int Level { get; private set; }

        /// <summary>
        /// The faction of the last profile seen, from an earlier launch: what the terminal shows
        /// before this launch's profile is known. Null on a first launch.
        /// </summary>
        internal static string LastSide { get; private set; }

        /// <summary>The game picked its profile: remember who it is, and the faction for next launch.</summary>
        internal static void NoteProfile(string nickname, string side, int level)
        {
            Nickname = nickname;
            Side = side;
            Level = level;
            if (side != null && side != LastSide && !Preview)
            {
                LastSide = side;
                SaveTimings();
            }
        }

        /// <summary>The names of the tasks the profile has under way, as the game names them: this launch's, or the last one's until the profile loads.</summary>
        internal static readonly List<string> Quests = new List<string>();

        /// <summary>The profile's tasks under way. Remembered, so the next launch knows them from the start.</summary>
        internal static void NoteQuests(List<string> names)
        {
            if (Preview || names == null)
            {
                return;
            }
            bool same = names.Count == Quests.Count && names.TrueForAll(Quests.Contains);
            Quests.Clear();
            Quests.AddRange(names);
            if (!same)
            {
                SaveTimings();
            }
        }

        /// <summary>True once a full launch has replaced the guessed step times with real ones.</summary>
        internal static bool HasBaseline =>
            (Kind == RunKind.Deploy ? LastDeployTotal : Kind == RunKind.Return ? LastReturnTotal : LastTotal) > 0f || Preview;

        /// <summary>
        /// A pretend run, started from the main menu to show the terminal: the steps advance on
        /// a short schedule instead of the game's hooks, and nothing about it is saved.
        /// </summary>
        internal static bool Preview { get; private set; }

        /// <summary>The preview's step times: the real ones squeezed into the preview's length.</summary>
        private static readonly float[] PreviewPlan = new float[Labels.Length];

        /// <summary>What the preview treats as each step's usual time; one step is meant to run over it.</summary>
        private static readonly float[] PreviewExpected = new float[Labels.Length];

        internal const float PreviewLength = 10f;

        /// <summary>How long the running pretend run's steps add up to, in seconds.</summary>
        internal static float PreviewTotal { get; private set; }
        private const StepId PreviewSlowStep = StepId.WeaponBuilds;

        private static float Usual(int step) => Preview ? PreviewExpected[step] : Times[step];

        /// <summary>The step times this run is weighed against.</summary>
        private static float[] Times => Kind == RunKind.Return ? ReturnExpected : Expected;

        /// <summary>A step's name in this run: the way back's first step has its own.</summary>
        internal static string Label(int step) =>
            Kind == RunKind.Return && step == (int)StepId.Connect ? LeaveRaidLabel : Labels[step];

        /// <summary>A step's Russian name in this run.</summary>
        internal static string LabelRu(int step) =>
            Kind == RunKind.Return && step == (int)StepId.Connect ? LeaveRaidLabelRu : LabelsRu[step];

        /// <summary>Marks the way back's step times in the timings file.</summary>
        private const string ReturnPrefix = "Return.";

        private static string TimingsPath =>
            // Qualified: Assembly-CSharp has a global Paths class of its own.
            Path.Combine(BepInEx.Paths.ConfigPath, ClassifiedPlugin.PluginGuid + ".timings.txt");

        /// <summary>
        /// A pretend run's clock, in seconds. The preview moves it each frame (see
        /// <see cref="PreviewAdvance"/>), so it can hold the run, slow it down or jump it to
        /// any point; a real run always goes by the stopwatch.
        /// </summary>
        private static float _previewNow;

        private static float Now => Preview ? _previewNow : (float)Clock.Elapsed.TotalSeconds;

        internal static float Elapsed => Active ? Now - _runStart : 0f;
        internal static float StepElapsed => Active && Current >= 0 ? Now - _stepStart : 0f;

        internal static float TookSeconds(int step) => Took[step];
        internal static float ExpectedSeconds(int step) => Usual(step);
        internal static bool WasSkipped(int step) => Skipped[step];
        internal static string Detail(int step) => Details[step];

        internal static void BeginRun(StepId first)
        {
            if (Active)
            {
                if (!Preview)
                {
                    return;
                }
                // The game is loading for real: that beats a preview.
                EndRun();
            }

            Range(RunKind.Launch, (int)first);
            Clear((int)first);
            if (first == StepId.VerifyFiles)
            {
                Launches++;
                SaveTimings();
            }
            Advance((int)first);
        }

        /// <summary>
        /// Counts a launch this mod sat out (its turn went to ACCESS GRANTED), so the next one is
        /// its turn again.
        /// </summary>
        internal static void SkipLaunch()
        {
            Launches++;
            SaveTimings();
        }

        /// <summary>Starts a pretend run from the first step. Does nothing while a real one is going.</summary>
        /// <param name="length">Roughly how long the run takes, in seconds.</param>
        /// <param name="overrun">Whether one step runs over its time, to show the slow-step warning.</param>
        internal static bool BeginPreview(float length = PreviewLength, bool overrun = true, RunKind kind = RunKind.Launch)
        {
            if (Active && !Preview)
            {
                return false;
            }
            Active = false;
            Range(kind, kind == RunKind.Deploy ? (int)StepId.DeployPrepare : 0);

            float total = 0f;
            for (int i = FirstStep; i <= LastStep; i++)
            {
                total += Math.Max(Expected[i], 0.05f);
            }
            for (int i = 0; i < StepCount; i++)
            {
                bool inRun = i >= FirstStep && i <= LastStep;
                PreviewPlan[i] = inRun ? Math.Max(0.25f, Math.Max(Expected[i], 0.05f) * length / total) : 0f;
                PreviewExpected[i] = PreviewPlan[i];
            }
            if (kind == RunKind.Deploy)
            {
                // The countdown counts down whole seconds, whatever the squeeze.
                PreviewPlan[(int)StepId.DeployCountdown] = PreviewExpected[(int)StepId.DeployCountdown] = Math.Min(5f, Math.Max(3f, length * 0.25f));
            }
            if (overrun)
            {
                // One step overruns, so the slow-step warning shows too.
                int slow = (int)(kind == RunKind.Deploy ? StepId.DeployCache : PreviewSlowStep);
                PreviewExpected[slow] = Math.Max(Expected[slow] * length / total, 0.25f);
                PreviewPlan[slow] = PreviewExpected[slow] + Math.Max(1.5f, PreviewExpected[slow] * 0.5f) + 1.2f;
            }
            PreviewTotal = 0f;
            for (int i = FirstStep; i <= LastStep; i++)
            {
                PreviewTotal += PreviewPlan[i];
            }

            Preview = true;
            _previewNow = 0f;
            Clear(FirstStep);
            Advance(FirstStep);
            return true;
        }

        private static void Range(RunKind kind, int first)
        {
            Kind = kind;
            FirstStep = first;
            LastStep = kind == RunKind.Deploy ? (int)StepId.DeployCountdown : (int)StepId.MainMenu;
        }

        /// <summary>
        /// The after-action report is done and the game is going back to the main menu. False,
        /// and nothing started, while another real run is going (a raid load that is stopping).
        /// </summary>
        internal static bool BeginReturn()
        {
            if (Active)
            {
                if (!Preview)
                {
                    return false;
                }
                EndRun();
            }
            Range(RunKind.Return, (int)StepId.Connect);
            Clear(FirstStep);
            Advance(FirstStep);
            return true;
        }

        /// <summary>A raid is loading: the deploy screen came up.</summary>
        internal static void BeginDeploy()
        {
            if (Active)
            {
                if (!Preview && Kind == RunKind.Deploy)
                {
                    return;
                }
                // A raid load beats a preview, and closes any launch run left open.
                EndRun();
            }
            Range(RunKind.Deploy, (int)StepId.DeployPrepare);
            Clear(FirstStep);
            Advance(FirstStep);
        }

        /// <summary>Counts a raid load, whether or not the terminal shows it (taking turns goes by this).</summary>
        internal static void CountDeploy()
        {
            Deploys++;
            SaveTimings();
        }

        /// <summary>
        /// The deploy screen's status changed. Statuses it does not know are kept as the running
        /// step's detail, so anything the game (or a mod) adds still shows.
        /// </summary>
        internal static void DeployStatus(string status, float? progress)
        {
            if (!Active || Preview || Kind != RunKind.Deploy || string.IsNullOrEmpty(status))
            {
                return;
            }
            if (DeployStatuses.TryGetValue(status, out StepId id))
            {
                Advance((int)id);
                if (progress.HasValue && (int)id == Current)
                {
                    Details[Current] = $"{Math.Round(progress.Value * 100f):0}%";
                }
            }
            else
            {
                ClassifiedPlugin.Log.LogInfo($"Raid load: status '{status}' is not one of the known steps; shown as a detail.");
                Details[Math.Max(Current, FirstStep)] = status;
            }
        }

        /// <summary>The final countdown screen came up: the last step runs exactly as long as it shows.</summary>
        internal static void ReachCountdown(float seconds)
        {
            if (!Active || Preview || Kind != RunKind.Deploy)
            {
                return;
            }
            Advance((int)StepId.DeployCountdown);
            Expected[(int)StepId.DeployCountdown] = Math.Max(0.5f, seconds);
        }

        /// <summary>Runs the pretend clock on by <paramref name="seconds"/>: the frame's time, scaled by the preview's speed, or 0 to hold.</summary>
        internal static void PreviewAdvance(float seconds)
        {
            if (Preview && Active)
            {
                _previewNow += Math.Max(0f, seconds);
            }
        }

        /// <summary>Moves the preview along. True once its last step has run its time.</summary>
        internal static bool PreviewTick()
        {
            if (!Preview || !Active || Current < 0)
            {
                return false;
            }
            if (Current == (int)StepId.DeployMap)
            {
                // The game reports how far the map has loaded; so does the pretend one.
                Details[Current] = $"{Math.Min(99f, StepElapsed / Math.Max(PreviewPlan[Current], 0.05f) * 100f):0}%";
            }
            if (StepElapsed < PreviewPlan[Current])
            {
                return false;
            }
            if (Current == LastStep)
            {
                return true;
            }
            PreviewNext();
            return false;
        }

        private static void PreviewNext()
        {
            int next = Current + 1;
            Advance(next);
            if (next == (int)StepId.ItemDatabase)
            {
                Details[next] = "16,412 items";
            }
            else if (next == (int)StepId.SelectProfile)
            {
                Details[next] = $"{(string.IsNullOrEmpty(Nickname) ? "Operator" : Nickname)}, level 42";
            }
        }

        /// <summary>
        /// Jumps the pretend run to <paramref name="seconds"/> from its start, every step before
        /// that finished on its planned time. Going back runs it again from the start.
        /// </summary>
        internal static void PreviewSeek(float seconds)
        {
            if (!Preview)
            {
                return;
            }
            seconds = Math.Max(0f, Math.Min(seconds, PreviewTotal));
            if (!Active || seconds < Elapsed)
            {
                _previewNow = 0f;
                Clear(FirstStep);
                Advance(FirstStep);
            }
            while (Current < LastStep && _stepStart - _runStart + PreviewPlan[Current] <= seconds)
            {
                _previewNow = _stepStart + PreviewPlan[Current];
                PreviewNext();
            }
            _previewNow = _runStart + seconds;
        }

        /// <summary>When step <paramref name="step"/> of the pretend run starts, in seconds from its start.</summary>
        internal static float PreviewStepStart(int step)
        {
            float at = 0f;
            for (int i = FirstStep; i < step && i <= LastStep; i++)
            {
                at += PreviewPlan[i];
            }
            return at;
        }

        private static void Clear(int first)
        {
            Array.Clear(Took, 0, Took.Length);
            Array.Clear(Skipped, 0, Skipped.Length);
            Array.Clear(Details, 0, Details.Length);
            Current = first - 1;
            _shownFraction = 0f;
            _runStart = Now;
            _runTerminal = 0f;
            _runFrames = 0;
            Active = true;
        }

        internal static void Reach(StepId id)
        {
            // A preview runs on its own schedule; the game calls some of these again in the menu,
            // and during a raid load, where they are not this run's steps.
            if (!Preview && Kind != RunKind.Deploy && (int)id <= LastStep)
            {
                Advance((int)id);
            }
        }

        private static void Advance(int step)
        {
            if (!Active || step <= Current || step > LastStep)
            {
                return;
            }

            float now = Now;
            if (Current >= 0)
            {
                FinishStep(now);
            }
            for (int i = Current + 1; i < step; i++)
            {
                Skipped[i] = true;
            }
            Current = step;
            _stepStart = now;
            _stepFrames = 0;
            _stepLongFrames = 0;
            _stepWorstFrame = 0f;
            _stepTerminal = 0f;
        }

        internal static void SetDetail(StepId id, string detail)
        {
            if (Active && !Preview)
            {
                Details[(int)id] = detail;
            }
        }

        /// <summary>Ends a run that did not finish (a cancelled raid load): logged, but nothing is kept as a yardstick.</summary>
        internal static void AbortRun(string why)
        {
            if (!Active)
            {
                return;
            }
            if (!Preview)
            {
                ClassifiedPlugin.Log.LogInfo($"Loading stopped after {Elapsed:0.00}s at '{Label(Math.Max(Current, FirstStep))}': {why}.");
                Archive.Save();
            }
            Active = false;
            Preview = false;
        }

        internal static void EndRun()
        {
            if (!Active)
            {
                return;
            }

            // What the chatter showed this run (a preview records nothing, so this is a no-op then).
            Archive.Save();

            float now = Now;
            if (Preview)
            {
                // Nothing from a preview is kept: not its times, not the launch count.
                Active = false;
                Preview = false;
                return;
            }
            if (Current >= 0)
            {
                FinishStep(now);
            }
            Active = false;

            float total = now - _runStart;
            ClassifiedPlugin.Log.LogInfo($"Loading took {total:0.00}s from '{Label(FirstStep)}', reached '{Label(Math.Max(Current, 0))}'.");
            ClassifiedPlugin.Log.LogInfo(
                $"Terminal: {_runTerminal * 1000f:0} ms of its own work over {_runFrames} frames ({(_runFrames > 0 ? _runTerminal * 1000f / _runFrames : 0f):0.00} ms/frame).");

            // Only a run that reached its end is a fair yardstick for the next one.
            if (Current == LastStep)
            {
                for (int i = FirstStep; i <= LastStep; i++)
                {
                    if (!Skipped[i])
                    {
                        Times[i] = Took[i];
                    }
                }
                if (Kind == RunKind.Deploy)
                {
                    LastDeployTotal = total;
                }
                else if (Kind == RunKind.Return)
                {
                    LastReturnTotal = total;
                }
                else if (FirstStep == 0)
                {
                    LastTotal = total;
                }
                SaveTimings();
            }
        }

        /// <summary>
        /// How full the bar is: finished steps count at their expected time, the running step
        /// counts up to just short of its own. Never moves backwards.
        /// </summary>
        internal static float Fraction()
        {
            if (!Active || Current < 0)
            {
                return _shownFraction;
            }

            float total = 0f;
            float done = 0f;
            for (int i = FirstStep; i <= LastStep; i++)
            {
                float expected = Math.Max(Usual(i), 0.05f);
                total += expected;
                if (i < Current)
                {
                    done += expected;
                }
                else if (i == Current)
                {
                    done += Math.Min(StepElapsed, expected * 0.9f);
                }
            }

            _shownFraction = Math.Max(_shownFraction, Math.Min(done / total, 0.99f));
            return _shownFraction;
        }

        /// <summary>How far the running step is toward its expected time, held short of the end.</summary>
        internal static float CurrentStepFraction()
        {
            if (!Active || Current < 0)
            {
                return 0f;
            }
            return Math.Min(StepElapsed / Math.Max(Usual(Current), 0.05f), 0.95f);
        }

        /// <summary>Time the terminal's own work took, in seconds: once a frame for its update (frame), and again for what it draws later in the frame.</summary>
        internal static void NoteTerminal(float seconds, bool frame = true)
        {
            if (!Active)
            {
                return;
            }
            _stepTerminal += seconds;
            _runTerminal += seconds;
            if (frame)
            {
                _runFrames++;
            }
        }

        /// <summary>Called once per drawn frame while a run is going.</summary>
        internal static void NoteFrame(float seconds)
        {
            if (!Active)
            {
                return;
            }
            _stepFrames++;
            if (seconds > LongFrame)
            {
                _stepLongFrames++;
            }
            _stepWorstFrame = Math.Max(_stepWorstFrame, seconds);
        }

        private static void FinishStep(float now)
        {
            Took[Current] = now - _stepStart;
            if (Preview)
            {
                return;
            }
            ClassifiedPlugin.Log.LogInfo(
                $"{Label(Current)}: {Took[Current]:0.00}s ({_stepFrames} frames, {_stepLongFrames} over {LongFrame * 1000f:0} ms, worst {_stepWorstFrame * 1000f:0} ms, "
                + $"terminal {(_stepFrames > 0 ? _stepTerminal * 1000f / _stepFrames : 0f):0.00} ms/frame)");
        }

        internal static void LoadTimings()
        {
            try
            {
                if (!File.Exists(TimingsPath))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(TimingsPath))
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && parts[0] == "Quest")
                    {
                        if (parts[1].Length > 0 && Quests.Count < 12)
                        {
                            Quests.Add(parts[1]);
                        }
                        continue;
                    }
                    if (parts.Length == 2 && parts[0] == "Side")
                    {
                        LastSide = parts[1] == "BEAR" || parts[1] == "USEC" ? parts[1] : null;
                        continue;
                    }
                    if (parts.Length != 2
                        || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
                    {
                        continue;
                    }
                    // "Total" (before 0.2) timed only the profile part, so it is not read.
                    if (parts[0] == "LaunchTotal")
                    {
                        LastTotal = seconds;
                    }
                    else if (parts[0] == "DeployTotal")
                    {
                        LastDeployTotal = seconds;
                    }
                    else if (parts[0] == "ReturnTotal")
                    {
                        LastReturnTotal = seconds;
                    }
                    else if (parts[0].StartsWith(ReturnPrefix, StringComparison.Ordinal)
                             && Enum.TryParse(parts[0].Substring(ReturnPrefix.Length), out StepId returnId))
                    {
                        ReturnExpected[(int)returnId] = seconds;
                    }
                    else if (parts[0] == "Launches")
                    {
                        Launches = (int)seconds;
                    }
                    else if (parts[0] == "Deploys")
                    {
                        Deploys = (int)seconds;
                    }
                    else if (Enum.TryParse(parts[0], out StepId id))
                    {
                        Expected[(int)id] = seconds;
                    }
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning($"Could not read {TimingsPath}: {e.Message}");
            }
        }

        private static void SaveTimings()
        {
            try
            {
                var lines = new List<string>
                {
                    "LaunchTotal=" + LastTotal.ToString("0.000", CultureInfo.InvariantCulture),
                    "DeployTotal=" + LastDeployTotal.ToString("0.000", CultureInfo.InvariantCulture),
                    "ReturnTotal=" + LastReturnTotal.ToString("0.000", CultureInfo.InvariantCulture),
                    "Launches=" + Launches.ToString(CultureInfo.InvariantCulture),
                    "Deploys=" + Deploys.ToString(CultureInfo.InvariantCulture)
                };
                if (LastSide != null)
                {
                    lines.Add("Side=" + LastSide);
                }
                foreach (string quest in Quests)
                {
                    lines.Add("Quest=" + quest.Replace('\n', ' ').Replace('\r', ' '));
                }
                for (int i = 0; i < StepCount; i++)
                {
                    lines.Add((StepId)i + "=" + Expected[i].ToString("0.000", CultureInfo.InvariantCulture));
                }
                for (int i = (int)StepId.Connect; i <= (int)StepId.MainMenu; i++)
                {
                    lines.Add(ReturnPrefix + (StepId)i + "=" + ReturnExpected[i].ToString("0.000", CultureInfo.InvariantCulture));
                }
                File.WriteAllLines(TimingsPath, lines);
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning($"Could not write {TimingsPath}: {e.Message}");
            }
        }
    }
}
