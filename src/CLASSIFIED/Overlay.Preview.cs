using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The preview: from the main menu, the terminal plays over a pretend load (see
    /// <see cref="StartupTracker.BeginPreview"/>) so every unit and every part can be looked at
    /// without relaunching. Three lines along the bottom say what is on screen, where the load
    /// is, and which keys do what:
    ///
    ///   Left/Right unit, Up/Down layout; 1 to 6 pin a part (backdrop, frame, intro, bar, ending,
    ///   glitches; Shift goes back), 0 unpins them all, N re-rolls what is not pinned. These change
    ///   the terminal in place, the load carrying on.
    ///   R replays from the start, I replays just the intro, E jumps to the ending, L loops.
    ///   Space holds the load where it is, -/+ change the speed (down to a freeze), [ and ] step.
    ///   M brings in a notice, G a glitch, D turns the fake denial on or off, C crossfades to the
    ///   other faction as a first launch does.
    ///   V shows less of the screen, down to the logo alone; H hides these lines; Esc closes.
    ///
    /// The showreel uses the same clock, view and caption (Overlay.Showreel.cs).
    /// </summary>
    internal partial class Overlay
    {
        private static readonly LayoutKind[] Layouts =
        {
            LayoutKind.Centered, LayoutKind.Split, LayoutKind.SplitMirror, LayoutKind.Sidebar, LayoutKind.Dock
        };

        /// <summary>How much of the screen the preview shows.</summary>
        private enum View
        {
            /// <summary>Everything.</summary>
            All,

            /// <summary>No text: the backdrop, the drawing, the insignia and its frame, the wordmark.</summary>
            Art,

            /// <summary>The insignia in its frame, on the background.</summary>
            Emblem,

            /// <summary>The insignia alone.</summary>
            Logo
        }

        /// <summary>Preview speeds, slowest first; 0 freezes everything.</summary>
        private static readonly float[] Speeds = { 0f, 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };
        private const int NormalSpeed = 4;

        private bool _previewing;
        private Variant _previewVariant;
        private Pins _previewPins;
        private GlitchLevel _previewGlitches;
        private bool _previewHold;
        private int _previewSpeed = NormalSpeed;
        private bool _previewLoop;
        private View _view;
        private bool _captionHidden;

        private Image _captionBack;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _captionState;
        private TextMeshProUGUI _captionHelp;
        private string _captionKey;
        private readonly StringBuilder _captionBuilder = new StringBuilder();

        /// <summary>How fast the preview's animations and load run; 1 outside a preview.</summary>
        private float PreviewSpeed => _previewing ? Speeds[_previewSpeed] : 1f;

        /// <summary>
        /// Opens the preview with a fresh roll. Main menu only, so it can never cover a raid, and
        /// ignored while the game is really loading.
        /// </summary>
        internal static void StartPreview()
        {
            if (!CanPlayOverMenu())
            {
                return;
            }
            if (_instance == null)
            {
                FindBootFont(null, out TMP_FontAsset font, out Material material);
                Create(font, material);
            }
            Overlay o = _instance;
            o.ResetPreviewControls(captionHidden: false);
            o._previewPins = Pins.FromConfig();
            o._previewGlitches = ClassifiedPlugin.Glitches.Value;
            o.PlayPreview(o.RollPreview(ClassifiedPlugin.Theme.Value));
        }

        /// <summary>Normal speed, the whole screen, the load running. Kept through replays; reset when a preview or reel starts.</summary>
        private void ResetPreviewControls(bool captionHidden)
        {
            _previewHold = false;
            _previewSpeed = NormalSpeed;
            _previewLoop = false;
            _view = View.All;
            _captionHidden = captionHidden;
            _captionKey = null;
        }

        /// <summary>The preview's keys. Called every frame by the plugin; does nothing unless a preview is showing.</summary>
        internal static void PreviewInput()
        {
            if (_instance == null || !_instance._previewing)
            {
                return;
            }
            Overlay o = _instance;
            bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shared by the preview and the reel.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StartupTracker.EndRun();
                o.Hide();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                o._previewHold = !o._previewHold;
            }
            else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                o._previewSpeed = Mathf.Max(0, o._previewSpeed - 1);
            }
            else if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                o._previewSpeed = Mathf.Min(Speeds.Length - 1, o._previewSpeed + 1);
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                o._view = (View)Wrap((int)o._view + (back ? -1 : 1), 4);
                o.ApplyView();
            }
            else if (Input.GetKeyDown(KeyCode.H))
            {
                o._captionHidden = !o._captionHidden;
            }
            else if (o._showreel)
            {
                o.ReelInput();
            }
            else if (o._phase == Phase.Loading)
            {
                o.PreviewKeys(back);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                // Once the ending has started, only a replay makes sense.
                o.PlayPreview(o._previewVariant);
            }
        }

        private void PreviewKeys(bool back)
        {
            Variant v = _previewVariant;
            int unit = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
            int layout = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 0;
            int step = back ? -1 : 1;
            if (unit != 0)
            {
                // The next unit in the same layout, so the two can be compared.
                int i = Wrap(Array.IndexOf(Theme.All, v.Theme) + unit, Theme.All.Length);
                ShowLive(RollPreview(Theme.All[i].Id, v.Layout));
            }
            else if (layout != 0)
            {
                _previewPins.Layout = v.Layout = Layouts[Wrap(Array.IndexOf(Layouts, v.Layout) + layout, Layouts.Length)];
                ShowLive(v);
            }
            else if (Pressed(KeyCode.Alpha1, KeyCode.Keypad1))
            {
                _previewPins.Backdrop = v.Backdrop = Next(v.Backdrop, step);
                ShowLive(v);
            }
            else if (Pressed(KeyCode.Alpha2, KeyCode.Keypad2))
            {
                _previewPins.Frame = v.Frame = Next(v.Frame, step);
                ShowLive(v);
            }
            else if (Pressed(KeyCode.Alpha3, KeyCode.Keypad3))
            {
                // An intro is only worth changing to watch it.
                _previewPins.Intro = v.Intro = Next(v.Intro, step);
                ShowLive(v);
                _introClock = 0f;
            }
            else if (Pressed(KeyCode.Alpha4, KeyCode.Keypad4))
            {
                _previewPins.Bar = v.Bar = Next(v.Bar, step);
                ShowLive(v);
            }
            else if (Pressed(KeyCode.Alpha5, KeyCode.Keypad5))
            {
                // Seen with E, or at the end of the load.
                _previewPins.Outro = v.Outro = Next(v.Outro, step);
            }
            else if (Pressed(KeyCode.Alpha6, KeyCode.Keypad6))
            {
                _previewGlitches = v.Glitches = (GlitchLevel)Wrap((int)v.Glitches + step, 3);
                _nextGlitchAt = 0f;
            }
            else if (Pressed(KeyCode.Alpha0, KeyCode.Keypad0))
            {
                _previewPins = new Pins();
                _previewGlitches = ClassifiedPlugin.Glitches.Value;
                ShowLive(RollPreview(v.Theme.Id));
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                ShowLive(RollPreview(v.Theme.Id));
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                PlayPreview(v);
            }
            else if (Input.GetKeyDown(KeyCode.I))
            {
                _introClock = 0f;
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                StartupTracker.PreviewSeek(StartupTracker.PreviewTotal);
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                _previewLoop = !_previewLoop;
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                StartupTracker.PreviewSeek(StartupTracker.PreviewStepStart(StartupTracker.Current + 1));
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                // To the start of this step, or of the one before if this one has only just begun.
                int to = StartupTracker.StepElapsed > 0.4f ? StartupTracker.Current : StartupTracker.Current - 1;
                StartupTracker.PreviewSeek(StartupTracker.PreviewStepStart(Mathf.Max(0, to)));
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                ShowNoticeNow();
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                if (_fx == Fx.None)
                {
                    RandomGlitch(_animClock);
                }
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                v.FakeDenial = !v.FakeDenial;
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                // What a first launch does when the profile turns out to be the other side's.
                Faction other = v.Faction == Faction.Bear ? Faction.Usec : Faction.Bear;
                var units = Theme.UnitsOf(other);
                CrossfadeTo(RollPreview(units[_random.Next(units.Count)].Id, v.Layout));
            }
        }

        private static bool Pressed(KeyCode key, KeyCode keypad) => Input.GetKeyDown(key) || Input.GetKeyDown(keypad);

        /// <summary>The next value of a part, skipping Random.</summary>
        private static T Next<T>(T current, int step) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, current);
            do
            {
                i = Wrap(i + step, values.Length);
            }
            while (values[i].ToString() == "Random");
            return values[i];
        }

        private static int Wrap(int i, int count) => ((i % count) + count) % count;

        /// <summary>
        /// A roll of <paramref name="theme"/> with the preview's pins. An unpinned layout is
        /// <paramref name="layout"/> if given, else rolled.
        /// </summary>
        private Variant RollPreview(ThemeChoice theme, LayoutKind layout = LayoutKind.Random)
        {
            if (_random == null)
            {
                _random = new System.Random(Environment.TickCount);
            }
            Faction faction = theme != ThemeChoice.Random ? Theme.Get(theme).Faction : PlayerFaction();
            if (faction == Faction.Unknown && theme == ThemeChoice.Random)
            {
                // No profile seen yet: the preview shows a faction anyway.
                faction = Faction.Bear;
            }
            Pins pins = _previewPins;
            if (pins.Layout == LayoutKind.Random)
            {
                pins.Layout = layout;
            }
            return Variant.Roll(_random, theme, faction, _previewGlitches, StartupTracker.Launches, pins, preview: true);
        }

        /// <summary>Plays <paramref name="variant"/> from the intro, over a fresh pretend load.</summary>
        private void PlayPreview(Variant variant)
        {
            if (_phase != Phase.Hidden)
            {
                StartupTracker.EndRun();
                Hide();
            }
            if (!StartupTracker.BeginPreview(ClassifiedPlugin.PreviewSeconds.Value))
            {
                return;
            }
            _previewVariant = variant;
            _previewing = true;
            StartLoading(null);
            ApplyView();
        }

        /// <summary>
        /// Puts <paramref name="variant"/> on screen in place of what is showing, the load carrying
        /// on. The insignia assembles again; notices already past stay past.
        /// </summary>
        private void ShowLive(Variant variant)
        {
            float done = StartupTracker.Fraction();
            variant.Notices = Array.FindAll(variant.Notices, n => n.At > done);
            EndCrossfade();
            SwitchShot(variant);
        }

        /// <summary>Brings in the next notice now, or the first again once all have shown.</summary>
        private void ShowNoticeNow()
        {
            if (_variant.Notices == null || _variant.Notices.Length == 0)
            {
                // All gone past with live changes: borrow a fresh roll's.
                _variant.Notices = RollPreview(_variant.Theme.Id).Notices;
                _noticeIndex = 0;
            }
            if (_memoShownAt >= 0f)
            {
                // Clear the one showing, so the next comes straight in.
                _memoShownAt = -1f;
                _memo.gameObject.SetActive(false);
            }
            if (_noticeIndex >= _variant.Notices.Length)
            {
                _noticeIndex = 0;
            }
            _variant.Notices[_noticeIndex].At = 0f;
        }

        // ---- what is on screen --------------------------------------------------------------

        /// <summary>Hides the parts of the screen the view leaves out; the whole screen outside a preview.</summary>
        private void ApplyView()
        {
            View view = _previewing ? _view : View.All;
            bool text = view == View.All;
            bool art = view <= View.Art;
            bool frame = view <= View.Emblem;

            Show(_readoutRoot, text);
            Show(_tickerMask, text);
            Show(_tickerTop, text);
            Show(_tickerBottom, text);
            Show(_memo, text);
            Show(_topLeft, text);
            Show(_topRight, text);
            Show(_bottomRight, text);
            Show(_subtitle, text);
            Show(_solidTrack, text);

            Show(_grid, art);
            Show(_ambientRoot, art);
            Show(_corners, art);
            Show(_panel, art);
            Show(_panelEdge, art);
            Show(_sheen, art);
            Show(_scanlines, art);
            Show(_band, art);
            Show(_wordmark, art);
            Show(_wordmarkGhost, art);
            Show(_wordmarkSplit, art);

            Show(_ringTrack, frame);
            Show(_ringProgress, frame);
            Show(_recLabel, frame);
            Show(_emblemGlow, frame);
            Show(_alertGlow, frame);
            foreach (Shape part in _frameParts)
            {
                Show(part, frame);
            }
            foreach (TextMeshProUGUI label in _frameLabels)
            {
                Show(label, frame);
            }
            Show(_stampBox, art);
            foreach (Image banner in _banners)
            {
                Show(banner, art);
            }
        }

        /// <summary>Shows or hides a part through a group of its own, which nothing else touches.</summary>
        private static void Show(Component part, bool visible)
        {
            if (part == null)
            {
                return;
            }
            CanvasGroup group = part.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (visible)
                {
                    return;
                }
                group = part.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            group.alpha = visible ? 1f : 0f;
        }

        // ---- the caption --------------------------------------------------------------------

        private const string PreviewHelp =
            "LEFT/RIGHT  UNIT      UP/DOWN  LAYOUT      1-6  PIN PART (SHIFT: BACK)      0  UNPIN ALL      N  RE-ROLL      R  REPLAY      I  INTRO      E  ENDING      L  LOOP\n"
            + "SPACE  HOLD      - / +  SPEED      [ / ]  STEP      M  NOTICE      G  GLITCH      D  DENIAL      C  CROSSFADE      V  VIEW      H  HIDE THIS      ESC  CLOSE";

        private const string ReelHelp =
            "LEFT/RIGHT  UNIT      SPACE  HOLD      - / +  SPEED      V  VIEW      H  HIDE THIS      ESC  STOP";

        private void BuildCaption()
        {
            _captionBack = MakeImage("PreviewBack", transform, new Color32(0, 0, 0, 200));
            _caption = MakeText("PreviewCaption", transform, TextAlignmentOptions.Center, Accent);
            _captionState = MakeText("PreviewState", transform, TextAlignmentOptions.Center, Text);
            _captionHelp = MakeText("PreviewHelp", transform, TextAlignmentOptions.Center, Dim);
            _caption.overflowMode = _captionState.overflowMode = TextOverflowModes.Ellipsis;
            SetCaptionActive(false);
        }

        /// <summary>Along the very bottom, under the corner brackets, on a dark band so it reads over anything.</summary>
        private void LayoutCaption(Vector2 size)
        {
            float bottom = -size.y * 0.5f;
            Place(_captionBack.rectTransform, 0f, bottom + 41f * _u, size.x, 82f * _u);
            SetText(_caption, 0f, bottom + 68f * _u, size.x * 0.96f, 18f * _u, 13f * _u, 4f);
            SetText(_captionState, 0f, bottom + 49f * _u, size.x * 0.96f, 16f * _u, 11f * _u, 4f);
            SetText(_captionHelp, 0f, bottom + 20f * _u, size.x * 0.96f, 30f * _u, 10f * _u, 4f);
        }

        private void SetCaptionActive(bool active)
        {
            _captionBack.gameObject.SetActive(active);
            _caption.gameObject.SetActive(active);
            _captionState.gameObject.SetActive(active);
            _captionHelp.gameObject.SetActive(active);
        }

        /// <summary>Rewrites the caption when what it shows has changed. Called every frame.</summary>
        private void UpdateCaption()
        {
            bool show = _previewing && !_captionHidden;
            if (_caption.gameObject.activeSelf != show)
            {
                SetCaptionActive(show);
                _captionKey = null;
            }
            if (!show)
            {
                return;
            }

            Variant v = _variant;
            string key = string.Join("|", v.GetHashCode(), v.Layout, v.Backdrop, v.Frame, v.Intro, v.Bar, v.Outro, v.Glitches,
                v.FakeDenial, StartupTracker.Current, Mathf.FloorToInt(StartupTracker.Elapsed * 10f), _phase, _previewHold,
                _previewSpeed, _previewLoop, _view, _shot);
            if (key == _captionKey)
            {
                return;
            }
            _captionKey = key;

            Theme theme = v.Theme;
            _caption.color = theme.Accent;
            _captionState.color = theme.Text;
            _captionHelp.color = theme.Dim;
            _captionBack.color = Theme.WithAlpha(Color32.Lerp(theme.Ink, new Color32(0, 0, 0, 255), 0.5f), 215);

            StringBuilder b = _captionBuilder.Clear();
            b.Append(_showreel ? $"SHOWREEL  {_shot + 1} / {_reel.Length}" : "PREVIEW").Append("  //  ");
            b.Append(Theme.Name(v.Faction)).Append("  ").Append(theme.Division).Append("  //  ");
            Part(b, Name(v.Layout.ToString()) + " LAYOUT", _previewPins.Layout != LayoutKind.Random);
            Part(b, "1 " + Name(v.Backdrop.ToString()), _previewPins.Backdrop != BackdropKind.Random);
            Part(b, "2 " + Name(v.Frame.ToString()) + " FRAME", _previewPins.Frame != FrameKind.Random);
            Part(b, "3 " + Name(v.Intro.ToString()) + " INTRO", _previewPins.Intro != IntroKind.Random);
            Part(b, "4 " + Name(v.Bar.ToString()) + " BAR", _previewPins.Bar != BarKind.Random);
            Part(b, "5 " + Name(v.Outro.ToString()) + " ENDING", _previewPins.Outro != OutroKind.Random);
            b.Append("6 GLITCHES ").Append(v.Glitches.ToString().ToUpperInvariant());
            _caption.text = b.ToString();

            b.Clear();
            if (_phase == Phase.Loading)
            {
                b.Append($"STEP {Mathf.Max(0, StartupTracker.Current) + 1:00} / {StartupTracker.StepCount:00}  ");
                b.Append(StartupTracker.Labels[Mathf.Max(0, StartupTracker.Current)].ToUpperInvariant());
                b.Append($"  //  {StartupTracker.Elapsed:0.0}s OF {StartupTracker.PreviewTotal:0.0}s");
            }
            else
            {
                b.Append("ENDING");
            }
            string warn = theme.WarnHex;
            b.Append("  //  ").Append(_previewHold ? $"<color={warn}>HOLDING</color>" : "RUNNING");
            float speed = Speeds[_previewSpeed];
            b.Append("  //  ").Append(speed <= 0f ? $"<color={warn}>FROZEN</color>"
                : speed == 1f ? "SPEED x1" : $"<color={warn}>SPEED x{speed:0.##}</color>");
            b.Append("  //  VIEW ").Append(_view.ToString().ToUpperInvariant());
            if (!_showreel)
            {
                b.Append("  //  LOOP ").Append(_previewLoop ? "ON" : "OFF");
                b.Append("  //  DENIAL ").Append(v.FakeDenial ? "ON" : "OFF");
            }
            _captionState.text = b.ToString();
            _captionHelp.text = _showreel ? ReelHelp : PreviewHelp;
        }

        /// <summary>One part for the caption: a pinned one in brackets.</summary>
        private static void Part(StringBuilder b, string text, bool pinned)
        {
            b.Append(pinned ? "[" + text + "]" : text).Append("  /  ");
        }

        /// <summary>SplitMirror becomes SPLIT MIRROR.</summary>
        private static string Name(string identifier)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < identifier.Length; i++)
            {
                if (i > 0 && char.IsUpper(identifier[i]))
                {
                    builder.Append(' ');
                }
                builder.Append(char.ToUpperInvariant(identifier[i]));
            }
            return builder.ToString();
        }

        /// <summary>The ending finished: a looping preview plays again from the start. True if it does.</summary>
        private bool LoopPreview()
        {
            if (!_previewing || _showreel || !_previewLoop)
            {
                return false;
            }
            PlayPreview(_previewVariant);
            return true;
        }

        private void EndPreview()
        {
            _previewing = false;
            SetCaptionActive(false);
            ApplyView();
        }
    }
}
