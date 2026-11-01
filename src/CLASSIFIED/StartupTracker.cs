using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CLASSIFIED
{
    /// <summary>The load steps, in the order the game runs them.</summary>
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
        MainMenu
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
            "Opening main menu"
        };

        /// <summary>
        /// For a first launch, in seconds: one real SPT 4.1 launch (the four boot steps are
        /// guesses). Replaced by this machine's own times after one.
        /// </summary>
        private static readonly float[] DefaultSeconds = { 0.5f, 6f, 1f, 3f, 0.1f, 9f, 0.4f, 0.7f, 0.6f, 0.5f, 2.7f, 0.2f, 1.6f, 1f, 1.5f };

        internal static int StepCount => Labels.Length;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly float[] Expected = (float[])DefaultSeconds.Clone();
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

        /// <summary>The step this run started at: 0 for a launch, <see cref="StepId.Connect"/> otherwise.</summary>
        internal static int FirstStep { get; private set; }

        /// <summary>Total of the last launch that ran from the splash to the main menu, or 0 if none is known.</summary>
        internal static float LastTotal { get; private set; }

        /// <summary>Launches from the splash so far, this one included.</summary>
        internal static int Launches { get; private set; }

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

        /// <summary>True once a full launch has replaced the guessed step times with real ones.</summary>
        internal static bool HasBaseline => LastTotal > 0f || Preview;

        /// <summary>
        /// A pretend run, started from the main menu to show the terminal: the steps advance on
        /// a short schedule instead of the game's hooks, and nothing about it is saved.
        /// </summary>
        internal static bool Preview { get; private set; }

        /// <summary>The preview's step times: the real ones squeezed into about ten seconds.</summary>
        private static readonly float[] PreviewPlan = new float[Labels.Length];

        /// <summary>What the preview treats as each step's usual time; one step is meant to run over it.</summary>
        private static readonly float[] PreviewExpected = new float[Labels.Length];

        private const float PreviewLength = 10f;

        /// <summary>How long the running pretend run's steps add up to, in seconds.</summary>
        internal static float PreviewTotal { get; private set; }
        private const StepId PreviewSlowStep = StepId.WeaponBuilds;

        private static float Usual(int step) => Preview ? PreviewExpected[step] : Expected[step];

        private static string TimingsPath =>
            // Qualified: Assembly-CSharp has a global Paths class of its own.
            Path.Combine(BepInEx.Paths.ConfigPath, ClassifiedPlugin.PluginGuid + ".timings.txt");

        private static float Now => (float)Clock.Elapsed.TotalSeconds;

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
        internal static bool BeginPreview(float length = PreviewLength, bool overrun = true)
        {
            if (Active && !Preview)
            {
                return false;
            }
            Active = false;

            float total = 0f;
            for (int i = 0; i < StepCount; i++)
            {
                total += Math.Max(Expected[i], 0.05f);
            }
            for (int i = 0; i < StepCount; i++)
            {
                PreviewPlan[i] = Math.Max(0.25f, Math.Max(Expected[i], 0.05f) * length / total);
                PreviewExpected[i] = PreviewPlan[i];
            }
            if (overrun)
            {
                // One step overruns, so the slow-step warning shows too.
                int slow = (int)PreviewSlowStep;
                PreviewExpected[slow] = Math.Max(Expected[slow], 0.25f);
                PreviewPlan[slow] = PreviewExpected[slow] + Math.Max(1.5f, PreviewExpected[slow] * 0.5f) + 1.2f;
            }
            PreviewTotal = 0f;
            for (int i = 0; i < StepCount; i++)
            {
                PreviewTotal += PreviewPlan[i];
            }

            Preview = true;
            Clear(0);
            Advance(0);
            return true;
        }

        /// <summary>Moves the preview along. True once its last step has run its time.</summary>
        internal static bool PreviewTick()
        {
            if (!Preview || !Active || Current < 0 || StepElapsed < PreviewPlan[Current])
            {
                return false;
            }
            if (Current == StepCount - 1)
            {
                return true;
            }
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
            return false;
        }

        private static void Clear(int first)
        {
            Array.Clear(Took, 0, Took.Length);
            Array.Clear(Skipped, 0, Skipped.Length);
            Array.Clear(Details, 0, Details.Length);
            FirstStep = first;
            Current = FirstStep - 1;
            _shownFraction = 0f;
            _runStart = Now;
            _runTerminal = 0f;
            _runFrames = 0;
            Active = true;
        }

        internal static void Reach(StepId id)
        {
            // A preview runs on its own schedule; the game calls some of these again in the menu.
            if (!Preview)
            {
                Advance((int)id);
            }
        }

        private static void Advance(int step)
        {
            if (!Active || step <= Current)
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
            ClassifiedPlugin.Log.LogInfo($"Loading took {total:0.00}s from '{Labels[FirstStep]}', reached '{Labels[Math.Max(Current, 0)]}'.");
            ClassifiedPlugin.Log.LogInfo(
                $"Terminal: {_runTerminal * 1000f:0} ms of its own work over {_runFrames} frames ({(_runFrames > 0 ? _runTerminal * 1000f / _runFrames : 0f):0.00} ms/frame).");

            // Only a run that reached the menu is a fair yardstick for the next one.
            if (Current == StepCount - 1)
            {
                for (int i = FirstStep; i < StepCount; i++)
                {
                    if (!Skipped[i])
                    {
                        Expected[i] = Took[i];
                    }
                }
                if (FirstStep == 0)
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
            for (int i = FirstStep; i < StepCount; i++)
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

        /// <summary>Time the terminal's own per-frame work took, in seconds.</summary>
        internal static void NoteTerminal(float seconds)
        {
            if (!Active)
            {
                return;
            }
            _stepTerminal += seconds;
            _runTerminal += seconds;
            _runFrames++;
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
                $"{Labels[Current]}: {Took[Current]:0.00}s ({_stepFrames} frames, {_stepLongFrames} over {LongFrame * 1000f:0} ms, worst {_stepWorstFrame * 1000f:0} ms, "
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
                    string[] parts = line.Split('=');
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
                    else if (parts[0] == "Launches")
                    {
                        Launches = (int)seconds;
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
                    "Launches=" + Launches.ToString(CultureInfo.InvariantCulture)
                };
                if (LastSide != null)
                {
                    lines.Add("Side=" + LastSide);
                }
                for (int i = 0; i < StepCount; i++)
                {
                    lines.Add((StepId)i + "=" + Expected[i].ToString("0.000", CultureInfo.InvariantCulture));
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
