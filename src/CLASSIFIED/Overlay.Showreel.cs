using System.Collections.Generic;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The showreel, for recording a video of the mod or looking over every unit: from the main
    /// menu (the F12 button), the menu fades to a title card, then one pretend load runs through
    /// all twelve units (or one faction's six, see the Showreel Units setting), BEAR and USEC in
    /// turn, each in a different layout, frame, background and bar from its own choices,
    /// crossfading from one to the next (Overlay.Crossfade.cs). Each stays on screen for the
    /// Showreel Shot Length setting. The last one finishes the load and leaves for the main menu
    /// with its usual ending.
    ///
    /// Nothing but the terminal shows, for recording. While it plays, Left/Right skip back and on
    /// a unit, Space holds the current one, -/+ change the speed, V and H work as in the preview
    /// (H shows what is on screen), Esc stops it.
    /// </summary>
    internal partial class Overlay
    {
        private struct Shot
        {
            internal ThemeChoice Theme;
            internal Pins Pins;

            /// <summary>Whether a notice slides in during this shot.</summary>
            internal bool Notice;

            internal Shot(ThemeChoice theme, LayoutKind layout, FrameKind frame, BackdropKind backdrop, BarKind bar, bool notice = false)
            {
                Theme = theme;
                Pins = new Pins { Layout = layout, Frame = frame, Backdrop = backdrop, Bar = bar };
                Notice = notice;
            }
        }

        // BEAR and USEC in turn, every layout two or three times, a different frame and background
        // on every cut, each from that unit's own choices.
        private static readonly Shot[] Shots =
        {
            new Shot(ThemeChoice.BearCommand, LayoutKind.Centered, FrameKind.Plate, BackdropKind.Map, BarKind.Lamps),
            new Shot(ThemeChoice.UsecOperations, LayoutKind.Split, FrameKind.Hud, BackdropKind.Map, BarKind.Ruler),
            new Shot(ThemeChoice.BearSignals, LayoutKind.Sidebar, FrameKind.Gauge, BackdropKind.Map, BarKind.Lamps, notice: true),
            new Shot(ThemeChoice.UsecIntelligence, LayoutKind.Dock, FrameKind.Scope, BackdropKind.Imagery, BarKind.Ruler),
            new Shot(ThemeChoice.BearReconnaissance, LayoutKind.SplitMirror, FrameKind.Reticle, BackdropKind.Imagery, BarKind.Tape),
            new Shot(ThemeChoice.UsecSignals, LayoutKind.Centered, FrameKind.Hud, BackdropKind.Map, BarKind.Segments, notice: true),
            new Shot(ThemeChoice.BearCounterintelligence, LayoutKind.Split, FrameKind.Plate, BackdropKind.Document, BarKind.Tape),
            new Shot(ThemeChoice.UsecLogistics, LayoutKind.Sidebar, FrameKind.Lock, BackdropKind.Document, BarKind.Segments),
            new Shot(ThemeChoice.BearSupply, LayoutKind.Dock, FrameKind.Gauge, BackdropKind.Document, BarKind.Lamps, notice: true),
            new Shot(ThemeChoice.UsecMedical, LayoutKind.SplitMirror, FrameKind.Scope, BackdropKind.Map, BarKind.Ruler),
            new Shot(ThemeChoice.BearMedical, LayoutKind.Sidebar, FrameKind.Gauge, BackdropKind.Map, BarKind.Tape, notice: true),
            new Shot(ThemeChoice.UsecCommand, LayoutKind.Centered, FrameKind.Lock, BackdropKind.Document, BarKind.Segments)
        };

        /// <summary>The menu fading to the title card, before the card's words come up.</summary>
        private const float ReelPreRoll = 0.9f;
        private const float ReelFadeIn = 0.7f;

        /// <summary>The title card plays at this speed, so it reads as an opening rather than a flash.</summary>
        private const float TitleCardSpeed = 0.7f;

        /// <summary>How much longer the first shot stays, for its title card.</summary>
        private const float TitleCardExtra = 2.6f;

        /// <summary>Left goes back to the start of the shot showing until it has been up this long, then to the one before.</summary>
        private const float RestartWithin = 1.5f;

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

        /// <summary>The shots the Showreel Units setting asks for, in order.</summary>
        private static List<Shot> ReelShots()
        {
            ReelFactions which = ClassifiedPlugin.ReelUnits.Value;
            var shots = new List<Shot>();
            foreach (Shot shot in Shots)
            {
                Faction faction = Theme.Get(shot.Theme).Faction;
                if (which == ReelFactions.Both || (which == ReelFactions.Bear) == (faction == Faction.Bear))
                {
                    shots.Add(shot);
                }
            }
            return shots;
        }

        private void PlayShowreel()
        {
            if (_phase != Phase.Hidden)
            {
                StartupTracker.EndRun();
                Hide(restarting: true);
            }
            if (_random == null)
            {
                _random = new System.Random(System.Environment.TickCount);
            }

            List<Shot> shots = ReelShots();
            float length = ClassifiedPlugin.ShotSeconds.Value;
            _reel = new Variant[shots.Count];
            _cuts = new float[shots.Count];
            float planned = 0f;
            for (int i = 0; i < shots.Count; i++)
            {
                _reel[i] = RollShot(shots[i], first: i == 0, last: i == shots.Count - 1);
                planned += length + (i == 0 ? TitleCardExtra : 0f);
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

            ResetPreviewControls(captionHidden: true);
            _previewPins = new Pins();
            _showreel = true;
            _shot = 0;
            _previewVariant = _reel[0];
            _previewing = true;
            StartLoading(null);
            ApplyView();
            _introClock = -ReelPreRoll;
            ClassifiedPlugin.Log.LogInfo($"Showreel: {_reel.Length} units over {StartupTracker.PreviewTotal:0.0}s.");
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
            v.GrantText = Variant.ClearanceLine(v.Faction);

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

        /// <summary>When shot <paramref name="i"/> comes on, in seconds of the reel's load.</summary>
        private float ShotStart(int i) => i <= 0 ? 0f : _cuts[i - 1];

        /// <summary>Cuts to whichever unit the load has reached, either way. Called each frame of the load.</summary>
        private void ShowreelTick()
        {
            if (_capturing)
            {
                return;
            }
            float at = StartupTracker.Elapsed;
            int shot = 0;
            while (shot < _reel.Length - 1 && at >= _cuts[shot])
            {
                shot++;
            }
            if (shot != _shot)
            {
                _shot = shot;
                CrossfadeTo(_reel[_shot]);
            }
        }

        /// <summary>The reel's own keys: Left/Right skip a unit back or on.</summary>
        private void ReelInput()
        {
            if (_phase != Phase.Loading)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                // On the last unit, on to the ending.
                StartupTracker.PreviewSeek(_shot < _reel.Length - 1 ? ShotStart(_shot + 1) : StartupTracker.PreviewTotal);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                bool restart = StartupTracker.Elapsed - ShotStart(_shot) > RestartWithin || _shot == 0;
                StartupTracker.PreviewSeek(ShotStart(restart ? _shot : _shot - 1));
                if (restart)
                {
                    // Same unit from its start: show it come on again.
                    CrossfadeTo(_reel[_shot]);
                }
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
