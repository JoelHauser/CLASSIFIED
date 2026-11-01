using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The showreel, for recording a video of the mod: from the main menu (the F12 button), the
    /// menu fades to a title card, then one pretend load runs quickly through a few units of both
    /// factions (not all: the rest are for players to find), each in a different layout, frame,
    /// background and bar, crossfading from one to the next (Overlay.Crossfade.cs). The last one
    /// finishes the load and leaves for the main menu with its usual ending. Esc stops it.
    /// </summary>
    internal partial class Overlay
    {
        private struct Shot
        {
            internal ThemeChoice Theme;
            internal Pins Pins;

            /// <summary>How long it is on screen, in seconds.</summary>
            internal float Length;

            /// <summary>Whether a notice slides in during this shot.</summary>
            internal bool Notice;

            internal Shot(ThemeChoice theme, float length, LayoutKind layout, FrameKind frame, BackdropKind backdrop, BarKind bar, bool notice = false)
            {
                Theme = theme;
                Length = length;
                Pins = new Pins { Layout = layout, Frame = frame, Backdrop = backdrop, Bar = bar };
                Notice = notice;
            }
        }

        // BEAR and USEC in turn, every layout, a different frame and background on every cut, each
        // from that unit's own choices. The first shot also carries the title card, so it stays
        // longest.
        private static readonly Shot[] Shots =
        {
            new Shot(ThemeChoice.BearCommand, 4.6f, LayoutKind.Centered, FrameKind.Seal, BackdropKind.Grid, BarKind.Segments),
            new Shot(ThemeChoice.UsecOperations, 2f, LayoutKind.Split, FrameKind.Brackets, BackdropKind.Blueprint, BarKind.Thin),
            new Shot(ThemeChoice.BearSignals, 2.6f, LayoutKind.Sidebar, FrameKind.Orbit, BackdropKind.Dots, BarKind.Segments, notice: true),
            new Shot(ThemeChoice.UsecIntelligence, 2f, LayoutKind.Dock, FrameKind.Squares, BackdropKind.Ruled, BarKind.Thin),
            new Shot(ThemeChoice.BearReconnaissance, 2f, LayoutKind.SplitMirror, FrameKind.Dial, BackdropKind.Grid, BarKind.Segments),
            new Shot(ThemeChoice.UsecCommand, 2.4f, LayoutKind.Centered, FrameKind.Ornate, BackdropKind.Pinstripe, BarKind.Solid)
        };

        /// <summary>The menu fading to the title card, before the card's words come up.</summary>
        private const float ReelPreRoll = 0.9f;
        private const float ReelFadeIn = 0.7f;

        /// <summary>The title card plays at this speed, so it reads as an opening rather than a flash.</summary>
        private const float TitleCardSpeed = 0.7f;

        private bool _showreel;
        private Variant[] _reel;
        private float[] _cuts;
        private int _shot;

        /// <summary>The preview and the showreel play over the main menu only, so they can never cover a raid.</summary>
        internal static bool CanPlayOverMenu()
        {
            EFT.UI.Screens.EftScreenManager manager = EFT.UI.Screens.EftScreenManager._instance;
            return manager != null && manager.CheckCurrentScreen(EFT.UI.Screens.EEftScreenType.MainMenu)
                && !(StartupTracker.Active && !StartupTracker.Preview);
        }

        internal static void StartShowreel()
        {
            if (!CanPlayOverMenu())
            {
                return;
            }
            if (_instance == null)
            {
                FindBootFont(null, out TMPro.TMP_FontAsset font, out Material material);
                Create(font, material);
            }
            _instance.PlayShowreel();
        }

        private void PlayShowreel()
        {
            if (_phase != Phase.Hidden)
            {
                StartupTracker.EndRun();
                Hide();
            }
            if (_random == null)
            {
                _random = new System.Random(System.Environment.TickCount);
            }

            _reel = new Variant[Shots.Length];
            _cuts = new float[Shots.Length];
            float planned = 0f;
            for (int i = 0; i < Shots.Length; i++)
            {
                _reel[i] = RollShot(Shots[i], first: i == 0, last: i == Shots.Length - 1);
                planned += Shots[i].Length;
                _cuts[i] = planned;
            }

            // One load across the whole reel, so every unit picks up where the last left off.
            if (!StartupTracker.BeginPreview(planned, overrun: false))
            {
                return;
            }
            // Steps have a minimum length, so the run can come out a little longer than asked;
            // spread the difference over the cuts so the last unit still gets its time.
            float stretch = StartupTracker.PreviewTotal / planned;
            for (int i = 0; i < _cuts.Length; i++)
            {
                _cuts[i] *= stretch;
            }

            _showreel = true;
            _shot = 0;
            _previewVariant = _reel[0];
            _previewing = true;
            StartLoading(null);
            _introClock = -ReelPreRoll;
            ClassifiedPlugin.Log.LogInfo($"Showreel: {Shots.Length} units over {StartupTracker.PreviewTotal:0.0}s.");
        }

        private Variant RollShot(Shot shot, bool first, bool last)
        {
            Pins pins = shot.Pins;
            if (first)
            {
                pins.Intro = IntroKind.TitleCard;
            }
            if (last)
            {
                // Everything else fades, the insignia holds a beat, then the menu.
                pins.Outro = OutroKind.Sting;
            }
            Variant v = Variant.Roll(_random, shot.Theme, Theme.Get(shot.Theme).Faction, GlitchLevel.Off, StartupTracker.Launches, pins, preview: true);
            v.FakeDenial = false;
            v.Greet = false;
            v.GrantText = "CLEARANCE CONFIRMED";

            // At most one notice, straight away: a shot is too short for the usual spacing. Not
            // the unsigned notes, which only make sense to someone who has seen the others.
            Notice notice = null;
            if (shot.Notice)
            {
                foreach (Notice candidate in v.Notices)
                {
                    if (candidate.Kind != NoticeKind.Note)
                    {
                        notice = candidate;
                        break;
                    }
                }
            }
            if (notice != null)
            {
                notice.At = 0f;
                v.Notices = new[] { notice };
            }
            else
            {
                v.Notices = new Notice[0];
            }
            return v;
        }

        /// <summary>Cuts to the next unit when its time comes. Called each frame of the load.</summary>
        private void ShowreelTick()
        {
            if (_shot < _reel.Length - 1 && !_capturing && StartupTracker.Elapsed >= _cuts[_shot])
            {
                _shot++;
                CrossfadeTo(_reel[_shot]);
            }
        }

        /// <summary>How much of the menu the reel's opening covers: a slow fade to the title card.</summary>
        private float ShowreelAlpha()
        {
            float p = Mathf.Clamp01((_introClock + ReelPreRoll) / ReelFadeIn);
            return p * p * (3f - 2f * p);
        }

        /// <summary>The title card opens the reel at its own, slower pace; everything else runs at speed.</summary>
        private float IntroSpeed => _showreel && _shot == 0 && _introClock < IntroLength ? TitleCardSpeed : 1f;

        private void EndShowreel()
        {
            if (!_showreel)
            {
                return;
            }
            _showreel = false;
            Cursor.visible = true;
        }
    }
}
