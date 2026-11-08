using System;
using System.Collections.Generic;
using System.Text;
using EFT.UI;
using EFT.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// A full-screen faction terminal over the game's whole launch: it comes up in place of the
    /// game's splash, then shows the faction's insignia inside a frame and a progress ring, the
    /// wordmark, a bar with a segment per load step, the running step typed out, and a log of the
    /// finished ones.
    ///
    /// It lives on its own top-level canvas rather than inside the game's screen, because the game
    /// switches that screen off two steps before the menu is ready. The overlay carries on through
    /// those, shows the closing line when the main menu replaces the loading screen, and fades out
    /// over the live menu (it never takes input, so the menu is usable during the fade).
    ///
    /// The canvas has no scaler, so one unit is one screen pixel: shapes get a one-pixel soft edge
    /// and the patterns land exactly on pixels. Everything that moves is a transform or an alpha;
    /// text is rewritten only when what it shows changes. All motion follows the clock, so a
    /// hitch in the game's own loading skips frames instead of slowing the animation.
    ///
    /// Each launch is one unit of the player's faction (<see cref="Theme"/>), with its own
    /// backdrop, frame, intro, bar and ending (Overlay.Style.cs), and the terminal glitches now
    /// and then (Overlay.Glitch.cs). Until the profile loads, the terminal wears the faction of the
    /// last profile it saw; if that turns out wrong (or there was none), it crossfades to the right
    /// one when the profile is picked (Overlay.Crossfade.cs).
    /// </summary>
    internal partial class Overlay : MonoBehaviour
    {
        private enum Phase
        {
            Hidden,
            Loading,
            Granted
        }

        // The launch's palette; see Theme.
        private Color32 Ink => _variant.Theme.Ink;
        private Color32 Glow => _variant.Theme.Glow;
        private Color32 Primary => _variant.Theme.Primary;
        private Color32 Accent => _variant.Theme.Accent;
        private Color32 Text => _variant.Theme.Text;
        private Color32 Dim => _variant.Theme.Dim;
        private Color32 Faint => _variant.Theme.Faint;
        private Color32 Warn => _variant.Theme.Warn;

        private const int LogLines = 4;
        private const float TypeSpeed = 70f;
        private const float GrantedHold = 0.4f;
        private const float FadeTime = 0.7f;

        /// <summary>A frame quicker than this counts as smooth.</summary>
        private const float SmoothFrame = 0.05f;
        private const int SmoothFramesNeeded = 10;

        /// <summary>Fade regardless after this long on ACCESS GRANTED, however rough the frames.</summary>
        private const float MaxHold = 2.5f;

        /// <summary>If the game hides its screen and the menu never comes, give up after this long.</summary>
        private const float GiveUpAfter = 15f;

        /// <summary>
        /// A raid load and the way back cover the game's screen from the first frame. Fading in,
        /// the game's own screen showed through, and the long frames as those loads start (the
        /// intro runs on drawn frames) stretched the fade to a second or more. A launch fades in
        /// over the splash, which is already dark.
        /// </summary>
        private static bool CoversAtOnce => StartupTracker.Kind != RunKind.Launch;

        /// <summary>The way back from a raid has no screen to lose: if the menu never comes, give up after this long.</summary>
        private const float ReturnGiveUpAfter = 120f;

        /// <summary>Over this far past its usual time, a step's readout turns to the warning colour.</summary>
        private const float SlowAfter = 1.5f;

        private static Overlay _instance;

        private Variant _variant;
        private System.Random _random;
        private Chatter _chatterSource;
        private string _grantText;

        private Phase _phase = Phase.Hidden;
        private ProfileLoadingScreen _screen;
        private TextMeshProUGUI _status;
        private bool _sawLoadingScreen;
        private float _screenGoneFor;
        private float _introClock;

        /// <summary>
        /// What every animation runs on: it advances by the frame's time, but never by more than
        /// a frame at 30 fps. The launch is full of long frames while the game loads on the main
        /// thread; on the real clock each one made the rings, grid and band jump ahead, which is
        /// what looked laggy. On this clock a stall slows the motion for a moment instead.
        /// </summary>
        private float _animClock;
        private float _frameDt;
        private const float MaxFrameStep = 1f / 30f;
        private int _laidOutFirstStep = -1;
        private float _grantedAt;
        private bool _fading;
        private float _fadeProgress;
        private float _held;
        private int _smoothFrames;

        private TMP_FontAsset _font;
        private Material _material;
        private RectTransform _rect;
        private RectTransform _content;
        private CanvasGroup _contentGroup;

        /// <summary>The logo and wordmark, in a layer of their own over the content, so an ending can keep them a moment longer.</summary>
        private RectTransform _brand;

        /// <summary>The readout: bar, current step, log, chatter.</summary>
        private RectTransform _readoutRoot;
        private CanvasGroup _group;
        private Vector2 _laidOutSize;
        private float _u = 1f;
        private float _barWidth;

        private Image _background;
        private RawImage _glow;
        private RawImage _grid;
        private Image _dropout;
        private RawImage _scanlines;
        private RawImage _band;
        private Shape _corners;
        private RectTransform _shutterTop;
        private RectTransform _shutterBottom;
        private Image _powerLine;

        private RectTransform _emblem;
        private InsigniaView _insignia;
        private RawImage _emblemGlow;
        // Screen-sized glows are big (20 MB at 3440x1440): keep one. The logo's glow changes
        // size with the layout and is small: keep a few.
        private readonly DitheredGlow _glowBake = new DitheredGlow(1);
        private readonly DitheredGlow _emblemBake = new DitheredGlow(4);
        private Shape _ringTrack;
        private Shape _ringProgress;
        private float _ringProgressDrawn = -1f;

        private TextMeshProUGUI _wordmark;
        private TextMeshProUGUI _wordmarkGhost;
        private TextMeshProUGUI _wordmarkSplit;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _stepCounter;
        private TextMeshProUGUI _percent;
        private Image[] _segments;
        private Image[] _segmentFills;
        private TextMeshProUGUI _currentLabel;
        private TextMeshProUGUI _labelGhost;
        private TextMeshProUGUI _labelSplit;
        private TextMeshProUGUI _currentTime;
        private TextMeshProUGUI _currentDetail;
        private TextMeshProUGUI _logLabels;
        private TextMeshProUGUI _logTimes;
        private TextMeshProUGUI _chatter;
        private float _chatterNextAt;
        private float _chatterShownAt;
        private TextMeshProUGUI _topLeft;
        private TextMeshProUGUI _topRight;
        private TextMeshProUGUI _bottomLeft;
        private TextMeshProUGUI _bottomRight;

        private bool _bootSubtitle;

        /// <summary>The overrun last written into a slow step's note, in tenths of a second.</summary>
        private int _shownOverTenths = -1;
        private int _shownStep = -2;
        private string _shownDetail;
        private float _stepShownAt;
        private int _shownStepTenths = -1;
        private bool _slow;
        private int _shownElapsedTenths = -1;
        private int _shownPercent = -1;
        private float _shownFraction;
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly StringBuilder _timesBuilder = new StringBuilder();

        /// <summary>Called when the game's splash wakes: the start of a launch.</summary>
        internal static void BeginBoot(SplashScreenPanel splash)
        {
            if (_instance == null)
            {
                FindBootFont(splash, out TMP_FontAsset font, out Material material);
                Create(font, material);
            }
            if (_instance._phase == Phase.Hidden)
            {
                _instance.StartLoading(null);
            }
        }

        /// <summary>Called when the game shows its loading screen in loading mode.</summary>
        internal static void Begin(ProfileLoadingScreen screen)
        {
            TextMeshProUGUI status = screen._statusField;
            if (status == null)
            {
                return;
            }
            if (_instance == null)
            {
                Create(status.font, status.fontSharedMaterial);
            }
            else
            {
                // The boot font was a guess; the status text's is the one the game uses here.
                _instance.ApplyFont(status.font, status.fontSharedMaterial);
            }

            if (_instance._previewing)
            {
                // The game is loading for real: the preview gives way.
                _instance.Hide();
            }
            if (_instance._phase == Phase.Loading)
            {
                // A launch already under way from the splash: just take charge of the screen.
                _instance.TakeScreen(screen);
            }
            else
            {
                _instance.StartLoading(screen);
            }
        }

        /// <summary>
        /// Called when the game leaves the after-action report for the main menu: the terminal
        /// covers the raid unloading and the menu loading, and signs in when the menu shows.
        /// </summary>
        internal static void BeginReturn()
        {
            if (_instance == null)
            {
                FindBootFont(null, out TMP_FontAsset font, out Material material);
                Create(font, material);
            }
            if (_instance._previewing)
            {
                _instance.Hide();
            }
            if (_instance._phase == Phase.Hidden)
            {
                _instance.StartLoading(null);
                ClassifiedPlugin.Log.LogInfo("Back from the raid: the terminal is showing the way to the main menu.");
            }
        }

        private static void Create(TMP_FontAsset font, Material material)
        {
            var go = new GameObject("ClassifiedLoading", typeof(RectTransform), typeof(CanvasGroup));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Overlay>();
            // Build colours from a palette; each run rolls its own anyway. Not recorded as seen:
            // nothing from this roll is shown.
            _instance.Roll(record: false);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            _instance.Build(font, material);
            ClassifiedPlugin.Log.LogInfo($"Terminal built in {clock.Elapsed.TotalMilliseconds:0.0} ms.");
        }

        /// <summary>
        /// The game's own font, if anything loaded so far uses it: the splash's text first, then
        /// any loaded font named Bender. Null leaves TextMeshPro's default.
        /// </summary>
        private static void FindBootFont(SplashScreenPanel splash, out TMP_FontAsset font, out Material material)
        {
            font = null;
            material = null;
            TMP_Text text = splash != null ? splash.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null && text.font != null)
            {
                font = text.font;
                material = text.fontSharedMaterial;
                return;
            }
            foreach (TMP_FontAsset candidate in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (candidate.name.IndexOf("bender", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    font = candidate;
                    material = candidate.material;
                    return;
                }
            }
        }

        private void ApplyFont(TMP_FontAsset font, Material material)
        {
            if (font == null)
            {
                return;
            }
            _font = font;
            _material = material;
            CheckCyrillic();
            foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font != font)
                {
                    text.font = font;
                    text.fontSharedMaterial = material;
                }
            }
            // The ticker's length depends on the font.
            _laidOutSize = Vector2.zero;
        }

        /// <summary>BEAR's terminal speaks Russian (spelt in Latin letters if the font has no Cyrillic); the others English.</summary>
        private string Words(string russian, string english) => _variant != null && _variant.Faction == Faction.Bear && BearRussian ? Cyr(russian) : english;

        /// <summary>A load step's name for the readout, in the terminal's language.</summary>
        private string StepName(int step) => _variant != null && _variant.Faction == Faction.Bear && BearRussian
            ? Cyr(StartupTracker.LabelRu(step).ToUpperInvariant())
            : StartupTracker.Label(step).ToUpperInvariant();

        /// <summary>The game is showing an error: get out of its way at once.</summary>
        internal static void HideNow()
        {
            if (_instance != null && _instance._phase != Phase.Hidden)
            {
                StartupTracker.EndRun();
                _instance.Hide();
            }
        }

        private void Build(TMP_FontAsset font, Material material)
        {
            _rect = (RectTransform)transform;
            _font = font;
            _material = material;
            CheckCyrillic();

            // Top-level, over everything, one unit per pixel.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Over ACCESS GRANTED's terminal (32000), on the launches it stands aside for this one.
            canvas.sortingOrder = 32001;
            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            // Everything but the intro's covers sits in one layer, so an ending can move or
            // squash the whole picture at once.
            _content = MakeRect("Content", transform);
            Stretch(_content);
            _contentGroup = _content.gameObject.AddComponent<CanvasGroup>();
            _contentGroup.blocksRaycasts = false;
            _contentGroup.interactable = false;
            _brand = MakeRect("Brand", transform);
            Stretch(_brand);
            Transform c = _content;
            Transform brand = _brand;

            _background = MakeImage("Background", c, Ink);
            Stretch(_background.rectTransform);
            _glow = MakeRaw("Glow", c, Art.Radial, Glow);
            _grid = MakeRaw("Backdrop", c, null, Accent);
            Stretch(_grid.rectTransform);
            Isolate(_glow);
            Isolate(_grid);
            BuildBackdropExtras(c);
            _corners = Shape.Create("Corners", c);

            _emblem = MakeRect("Emblem", brand);
            Isolate(_emblem);
            _emblemGlow = MakeRaw("EmblemGlow", _emblem, Art.Radial, Primary);
            _alertGlow = MakeRaw("AlertGlow", _emblem, Art.Radial, Warn);
            _ringTrack = Shape.Create("RingTrack", _emblem);
            _ringProgress = Shape.Create("RingProgress", _emblem);
            for (int i = 0; i < _frameParts.Length; i++)
            {
                _frameParts[i] = Shape.Create("Frame" + i, _emblem);
            }
            _insignia = InsigniaView.Create("Insignia", _emblem);
            _recLabel = MakeText("Rec", _emblem, TextAlignmentOptions.Right, Warn);
            _recLabel.text = "REC";

            // The RGB split's two copies sit behind the wordmark, hidden until a glitch.
            _wordmarkSplit = MakeText("WordmarkSplit", brand, TextAlignmentOptions.Center, Text);
            _wordmarkGhost = MakeText("WordmarkGhost", brand, TextAlignmentOptions.Center, Text);
            _wordmark = MakeText("Wordmark", brand, TextAlignmentOptions.Center, Text);
            _wordmark.text = _wordmarkGhost.text = _wordmarkSplit.text = "CLASSIFIED";
            _wordmarkGhost.enabled = _wordmarkSplit.enabled = false;
            _subtitle = MakeText("Subtitle", c, TextAlignmentOptions.Center, Accent);

            // The readout changes several times a second (typing, timers, the running
            // segment), so it gets a canvas of its own too.
            RectTransform readout = _readoutRoot = MakeRect("Readout", c);
            Stretch(readout);
            Isolate(readout);
            Transform r = readout;

            _stepCounter = MakeText("StepCounter", r, TextAlignmentOptions.Left, Dim);
            _percent = MakeText("Percent", r, TextAlignmentOptions.Right, Dim);
            _segments = new Image[StartupTracker.StepCount];
            _segmentFills = new Image[StartupTracker.StepCount];
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i] = MakeImage("Segment" + i, r, Faint);
                _segmentFills[i] = MakeImage("Fill", _segments[i].transform, Accent);
                RectTransform fill = _segmentFills[i].rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0f, 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
            }
            BuildSolidBar(r);

            _labelSplit = MakeText("LabelSplit", r, TextAlignmentOptions.Left, Text);
            _labelGhost = MakeText("LabelGhost", r, TextAlignmentOptions.Left, Text);
            _labelGhost.enabled = _labelSplit.enabled = false;
            _currentLabel = MakeText("CurrentLabel", r, TextAlignmentOptions.Left, Text);
            _currentTime = MakeText("CurrentTime", r, TextAlignmentOptions.Right, Text);
            _currentDetail = MakeText("CurrentDetail", r, TextAlignmentOptions.Left, Dim);
            _logLabels = MakeText("LogLabels", r, TextAlignmentOptions.TopLeft, Dim);
            _logTimes = MakeText("LogTimes", r, TextAlignmentOptions.TopRight, Dim);
            _chatter = MakeText("Chatter", r, TextAlignmentOptions.Left, Dim);
            _chatter.overflowMode = TextOverflowModes.Ellipsis;
            BuildFaction(c, r);

            _topLeft = MakeText("TopLeft", c, TextAlignmentOptions.TopLeft, Dim);
            _topRight = MakeText("TopRight", c, TextAlignmentOptions.TopRight, Dim);
            _bottomLeft = MakeText("BottomLeft", r, TextAlignmentOptions.BottomLeft, Dim);
            _bottomRight = MakeText("BottomRight", c, TextAlignmentOptions.BottomRight, Dim);

            BuildTicker(c);
            BuildMemo(c);

            for (int b = 0; b < TearBands; b++)
            {
                _tearFills[b] = MakeImage("TearFill" + b, c, Accent);
                _tearEdges[b * 2] = MakeImage("TearEdge" + b + "a", c, Accent);
                _tearEdges[b * 2 + 1] = MakeImage("TearEdge" + b + "b", c, Accent);
                _tearFills[b].gameObject.SetActive(false);
                _tearEdges[b * 2].gameObject.SetActive(false);
                _tearEdges[b * 2 + 1].gameObject.SetActive(false);
            }
            _dropout = MakeImage("Dropout", c, Color.black);
            Stretch(_dropout.rectTransform);
            _dropout.gameObject.SetActive(false);

            _scanlines = MakeRaw("Scanlines", c, Art.Scanlines, new Color32(0, 0, 0, 70));
            Stretch(_scanlines.rectTransform);
            _band = MakeRaw("Band", c, Art.Band, Accent);
            Isolate(_band);
            BuildCountdownAlert(r, c);

            // The intro's covers, over the content.
            _shutterTop = MakeImage("ShutterTop", transform, Color.black).rectTransform;
            _shutterBottom = MakeImage("ShutterBottom", transform, Color.black).rectTransform;
            _powerLine = MakeImage("PowerLine", transform, Color.white);
            _introTitle = MakeText("IntroTitle", transform, TextAlignmentOptions.Center, Text);
            _introTitle.text = "CLASSIFIED";
            _introSub = MakeText("IntroSub", transform, TextAlignmentOptions.Center, Accent);
            BuildIntros();
            _flash = MakeImage("Flash", transform, Color.white);
            Stretch(_flash.rectTransform);
            _flash.gameObject.SetActive(false);
            BuildCaption();
            BuildSnapshot();

            // What a tear can shove sideways: everything in the middle column.
            var tearables = new List<RectTransform>
            {
                _emblem, _wordmark.rectTransform, _subtitle.rectTransform, _stepCounter.rectTransform,
                _percent.rectTransform, _currentLabel.rectTransform, _currentTime.rectTransform,
                _currentDetail.rectTransform, _logLabels.rectTransform, _logTimes.rectTransform,
                _chatter.rectTransform, _solidTrack.rectTransform
            };
            foreach (Image segment in _segments)
            {
                tearables.Add(segment.rectTransform);
            }
            _tearables = tearables.ToArray();
            _tearBase = new Vector2[_tearables.Length];
            _tearHalf = new float[_tearables.Length];

            gameObject.SetActive(false);
        }

        /// <summary>
        /// The terminal the player signed in to this session, once a launch has reached the menu.
        /// Every later load (a raid, the sign-in after one) keeps its unit and look, so the
        /// session reads as one terminal; only the words are new each time.
        /// </summary>
        private Variant _sessionLook;

        /// <summary>Rolls this launch's unit, rings, header, ending and surprises.</summary>
        /// <param name="record">False for a roll that is never shown: its memos and headlines are not marked as seen.</param>
        private void Roll(bool record = true)
        {
            if (_random == null)
            {
                _random = new System.Random(Environment.TickCount);
            }
            // A preview brings its own roll, picked with the preview's keys.
            if (_previewing && _previewVariant != null)
            {
                _variant = _previewVariant;
            }
            else
            {
                Variant look = _sessionLook;
                ThemeChoice theme = look != null ? look.Theme.Id : ClassifiedPlugin.Theme.Value;
                Faction faction = look != null ? look.Faction : PlayerFaction();
                _variant = Variant.Roll(_random, theme, faction, ClassifiedPlugin.Glitches.Value, StartupTracker.Launches, Pins.FromConfig(), preview: !record);
                if (look != null)
                {
                    _variant.KeepLook(look);
                }
            }
            _chatterSource = new Chatter(_random, _variant.Theme);
            DeployRoll();
        }

        /// <summary>Recolours and retexts everything that is not rebuilt by <see cref="Layout"/>.</summary>
        private void ApplyVariant()
        {
            Theme theme = _variant.Theme;
            _background.color = Ink;
            _glow.color = Glow;
            _emblemGlow.color = Primary;
            _alertGlow.color = Warn;
            ColourCountdownAlert();
            _wordmark.color = Text;
            _wordmarkGhost.color = _labelGhost.color = Theme.WithAlpha(Accent, 180);
            _wordmarkSplit.color = _labelSplit.color = Theme.WithAlpha(theme.Split, 150);
            _subtitle.color = Accent;
            _stepCounter.color = _percent.color = Dim;
            _currentLabel.color = _currentTime.color = Text;
            _currentDetail.color = _logLabels.color = _logTimes.color = Dim;
            _chatter.color = Theme.WithAlpha(Dim, 170);
            _topLeft.color = _topRight.color = _bottomLeft.color = _bottomRight.color = Dim;
            foreach (Image fill in _segmentFills)
            {
                fill.color = Accent;
            }
            foreach (Image segment in _segments)
            {
                segment.color = Faint;
            }
            foreach (Image fill in _tearFills)
            {
                fill.color = Theme.WithAlpha(Accent, 14);
            }
            foreach (Image edge in _tearEdges)
            {
                edge.color = Theme.WithAlpha(Accent, 110);
            }
            _scanlines.gameObject.SetActive(theme.Scanlines);
            _band.gameObject.SetActive(theme.Scanlines);
            _band.color = Theme.WithAlpha(Accent, 10);
            _wordmark.text = _wordmarkGhost.text = _wordmarkSplit.text = theme.Wordmark;
            _introTitle.text = theme.Wordmark;
            _topLeft.text = Cyr(_variant.Header);
            _topRight.text = Cyr(_variant.TopRight);
            _bootSubtitle = !(StartupTracker.Current >= (int)StepId.Connect);
            _subtitle.text = Subtitle(_bootSubtitle);
            ApplyStyle();
            ClassifiedPlugin.Log.LogInfo(
                $"Terminal: {theme.Id} ({_variant.Layout} layout, {_variant.Backdrop}, {_variant.Frame} frame, {_variant.Intro} intro, {_variant.Bar} bar, {_variant.Outro} ending), glitches {_variant.Glitches}, line '{_variant.GrantText}'"
                + (_variant.FakeDenial ? ", fake denial" : string.Empty)
                + $", {_variant.Notices.Length} notices.");
        }

        private void StartLoading(ProfileLoadingScreen screen)
        {
            if (_phase == Phase.Loading && _screen == screen)
            {
                return;
            }

            _screen = null;
            _status = null;
            _sawLoadingScreen = false;
            _screenGoneFor = 0f;
            _introClock = 0f;
            _phase = Phase.Loading;

            _shownStep = -2;
            _shownDetail = null;
            _shownStepTenths = -1;
            _shownElapsedTenths = -1;
            _shownPercent = -1;
            _shownFraction = 0f;
            _ringProgressDrawn = -1f;
            _slow = false;
            _emblem.localScale = Vector3.one;
            _laidOutSize = Vector2.zero;
            _group.alpha = CoversAtOnce && !_showreel ? 1f : 0f;
            ResetFx();
            Roll();
            _debriefAt = _animClock;
            ApplyVariant();
            ResetStyle();
            _currentLabel.text = string.Empty;
            _logLabels.text = _logTimes.text = string.Empty;
            _chatter.text = string.Empty;
            _chatterNextAt = 0f;

            TakeScreen(screen);
            gameObject.SetActive(true);
        }

        private void TakeScreen(ProfileLoadingScreen screen)
        {
            if (screen == null || screen == _screen)
            {
                return;
            }
            _screen = screen;
            _status = screen._statusField;
            _screenGoneFor = 0f;
            // The game's own "Loading profile data..." would show through the overlay's fade-in.
            if (_status != null)
            {
                _status.enabled = false;
            }
        }

        /// <param name="restarting">True when another show follows at once (a preview or reel starting over).</param>
        private void Hide(bool restarting = false)
        {
            EndFx();
            EndShowreel();
            EndCrossfade();
            StandInFor(Phase.Hidden);
            EndPreview();
            _phase = Phase.Hidden;
            if (_status != null)
            {
                _status.enabled = true;
            }
            _screen = null;
            _status = null;
            _deployScreen = null;
            CanCancel = false;
            // The logos are only needed on screen; the next show lays out again and reloads them.
            _insignia.Clear();
            Insignia.Release();
            _grid.texture = null;
            Backdrops.Release();
            if (!restarting && !ClassifiedPlugin.RaidLoading.Value)
            {
                // Nothing shows again this session unless a preview is asked for, so the glows
                // (one is the size of the screen) and the table's models go too. With raid loads
                // on they are kept, so a raid's terminal comes up as finished as the launch's.
                // A preview starting over keeps them: it would only make them again.
                _glow.texture = null;
                _emblemGlow.texture = null;
                _glowBake.Release();
                _emblemBake.Release();
                WeaponModel.ClearCache();
            }
            gameObject.SetActive(false);
        }

        private void Grant(float realTime)
        {
            // Close the run first: the times are how long it took to reach the menu.
            float elapsed = StartupTracker.Elapsed;
            StartupTracker.EndRun();
            EndFx();
            _ringKick = 0f;
            if (!_previewing && !_showreel && !Deploying && _sessionLook == null)
            {
                // The terminal the player signed in to (after any crossfade to their faction):
                // the rest of the session keeps it.
                _sessionLook = _variant;
            }
            _phase = Phase.Granted;
            _grantedAt = realTime;
            _fading = false;
            _fadeProgress = 0f;
            _held = 0f;
            _smoothFrames = 0;
            _revealAt = -1f;
            _revealStage = 0;
            if (_status != null)
            {
                _status.enabled = true;
            }

            string nickname = StartupTracker.Nickname;
            _grantText = Cyr(_variant.Greet && !string.IsNullOrEmpty(nickname)
                ? Words("С ВОЗВРАЩЕНИЕМ, БОЕЦ ", "WELCOME BACK, OPERATOR ") + nickname.ToUpperInvariant()
                : _variant.GrantText);

            int steps = StartupTracker.RunSteps;
            _stepCounter.text = Words("ЭТАП", "STEP") + $" <mspace=0.62em>{steps:00}</mspace> / {steps:00}";
            _percent.text = "<mspace=0.62em>100</mspace>%";
            // A run headed for a fake denial keeps everyone guessing until the menu settles.
            if (_variant.FakeDenial)
            {
                ShowLabel(Words("> ПРОВЕРКА ДОПУСКА", "> VERIFYING CLEARANCE"), Dim);
            }
            else
            {
                ShowLabel("> " + _grantText, Accent);
            }
            _currentTime.text = string.Empty;
            _currentTime.color = Text;
            _currentDetail.text = string.Empty;
            _chatter.text = Words(":: ЖЕЛАЕМ УСПЕХОВ В СЛУЖБЕ", ":: HAVE A PRODUCTIVE DAY");
            _chatter.maxVisibleCharacters = 0;
            _chatterShownAt = _animClock;
            RebuildLog(StartupTracker.LastStep + 1);
            for (int i = 0; i < _segments.Length; i++)
            {
                _segmentFills[i].enabled = false;
            }
            if (Deploying)
            {
                GrantDeploy();
            }
            SetSegments(Accent);
            _bottomLeft.text = Words("ПРОШЛО", "ELAPSED") + $" <mspace=0.62em>{elapsed:0.0}</mspace>" + Words("с", "s");
        }

        private void LateUpdate()
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            Tick();
            StartupTracker.NoteTerminal((System.Diagnostics.Stopwatch.GetTimestamp() - start) / (float)System.Diagnostics.Stopwatch.Frequency);
        }

        private void Tick()
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            float real = Time.unscaledTime;
            // A preview can run slower or faster, or freeze.
            _frameDt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep) * PreviewSpeed;
            _animClock += _frameDt;
            float t = _animClock;
            StandInFor(_phase);
            if (_phase == Phase.Loading)
            {
                StartupTracker.NoteFrame(Time.unscaledDeltaTime);
                // A pretend load runs on the preview's clock, which can also hold it where it is.
                StartupTracker.PreviewAdvance(_previewHold ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f) * PreviewSpeed);
                CompleteCrossfade();
                WatchGame(real);
                if (_phase == Phase.Hidden)
                {
                    return;
                }
            }

            // A dithered glow finished on its worker thread: swap it in.
            if (_glowBake.Collect())
            {
                ApplyGlow(true);
            }
            if (_emblemBake.Collect())
            {
                ApplyEmblemGlow(true);
            }

            Vector2 size = _rect.rect.size;
            if ((size - _laidOutSize).sqrMagnitude > 0.25f || _laidOutFirstStep != StartupTracker.FirstStep)
            {
                _laidOutSize = size;
                _laidOutFirstStep = StartupTracker.FirstStep;
                EndFx();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                Layout(size);
                ClassifiedPlugin.Log.LogInfo($"Terminal laid out ({_variant.Theme.Id}, {_layout}) in {clock.Elapsed.TotalMilliseconds:0.0} ms.");
            }

            // The intro runs on drawn frames, not the clock: the first seconds of a launch are full
            // of long frames, and a clock-timed intro would be over before anyone saw it.
            _introClock += _frameDt * IntroSpeed;
            if (_showreel)
            {
                // Nothing to point at in a recording.
                Cursor.visible = false;
            }

            UpdateDynamics(t);
            AnimateBackdrop(t);
            AnimateIntro();
            AnimateSnapshot();
            AnimateEmblem(t, _introClock - IntroLength);
            AnimateTicker();
            AnimateMemo(t);
            AnimateSheen(t);
            AnimatePanels(t);
            UpdateCaption();
            if (_previewing && !_showreel)
            {
                // Up while the preview loops; off as soon as looping is switched off, so the
                // ending then fades to the menu as a real one does.
                ShowCurtain(_previewLoop);
            }

            UpdateCountdownAlert(t);
            if (_phase == Phase.Loading)
            {
                _group.alpha = _showreel ? ShowreelAlpha()
                    : CoversAtOnce ? 1f
                    : Mathf.Clamp01(_introClock * (_variant.Intro == IntroKind.Fade ? 3f : 8f));
                UpdateProgress(StartupTracker.Fraction());
                UpdateSteps(t);
                UpdateChatter(t);
                UpdateClock(StartupTracker.Elapsed);
                PollItems(t);
                UpdateFx(t);
                AnimateOutro(0f);
            }
            else
            {
                UpdateProgress(1f);
                TypeOut(t);
                TypeChatter(t);
                float fade = AdvanceFade(real);
                AnimateOutro(fade);
                // A looping preview plays its ending without fading off the menu, then starts again.
                if (fade >= 1f && !LoopPreview())
                {
                    Hide();
                }
            }
        }

        /// <summary>
        /// How far the fade-out has gone, 0 to 1. Not by the clock: the menu's first frames are a
        /// burst of work from the game and other mods that can freeze it for a second, and a
        /// clock-timed fade would finish inside that freeze and look like a hard cut. So ACCESS
        /// GRANTED holds until frames come smoothly again, plays its closing sequence, and the
        /// fade then moves only as far per frame as a frame at 30 fps would take it.
        /// </summary>
        private float AdvanceFade(float realTime)
        {
            float dt = Time.unscaledDeltaTime;
            if (!_fading)
            {
                if (_revealAt < 0f)
                {
                    _smoothFrames = dt < SmoothFrame ? _smoothFrames + 1 : 0;
                    _held += Mathf.Min(dt, SmoothFrame);
                    bool settled = _smoothFrames >= SmoothFramesNeeded && _held >= GrantedHold;
                    if (!settled && realTime - _grantedAt <= MaxHold)
                    {
                        UpdateFx(_animClock);
                        return 0f;
                    }
                    _revealAt = _animClock;
                    if (!settled)
                    {
                        // Never settled: no show, just leave.
                        FinishReveal();
                        _fading = true;
                        _fadeProgress = 0f;
                        return 0f;
                    }
                }

                // A backstop in case the frames turn rough again partway through.
                bool done = UpdateReveal(_animClock) || realTime - _grantedAt > MaxHold + 3f;
                UpdateFx(_animClock);
                if (done)
                {
                    FinishReveal();
                    _fading = true;
                    _fadeProgress = 0f;
                }
                return 0f;
            }

            _fadeProgress += _frameDt / OutroTime;
            return Mathf.Clamp01(_fadeProgress);
        }

        /// <summary>Decides when loading is over, or when to stand aside for the game.</summary>
        private void WatchGame(float realTime)
        {
            if (!ClassifiedPlugin.Enabled.Value)
            {
                StartupTracker.EndRun();
                Hide();
                return;
            }

            if (StartupTracker.Preview)
            {
                // A preview has no game to watch: it ends when its pretend steps have run (a
                // showreel, once its last unit is up).
                if (_showreel)
                {
                    ShowreelTick();
                }
                if (StartupTracker.PreviewTick() && (!_showreel || _shot == _reel.Length - 1))
                {
                    Grant(realTime);
                }
                return;
            }
            if (Deploying)
            {
                WatchDeploy(realTime);
                return;
            }

            bool haveScreen = _screen != null;
            bool screenUp = haveScreen && _screen.gameObject.activeInHierarchy;
            if (screenUp && _screen._loadingPanel != null && !_screen._loadingPanel.gameObject.activeSelf)
            {
                // The screen switched to its error panel.
                StartupTracker.EndRun();
                Hide();
                return;
            }

            // _instance, not Instance: the property creates a manager if there is none.
            EftScreenManager manager = EftScreenManager._instance;
            bool loadingIsCurrent = manager != null && manager.CheckCurrentScreen(EEftScreenType.ProfileLoading);
            if (loadingIsCurrent)
            {
                _sawLoadingScreen = true;
            }
            else if (manager != null && manager.CurrentScreenController != null
                     && (_sawLoadingScreen || manager.CheckCurrentScreen(EEftScreenType.MainMenu)))
            {
                // Another screen, normally the main menu, has taken over. A moment with no
                // screen at all, between two, does not count.
                Grant(realTime);
                return;
            }

            // Before the profile screen first shows (the boot steps) there is nothing to lose.
            _screenGoneFor = !haveScreen || screenUp ? 0f : _screenGoneFor + Time.unscaledDeltaTime;
            if (_screenGoneFor > GiveUpAfter)
            {
                Grant(realTime);
            }
            else if (StartupTracker.Kind == RunKind.Return && StartupTracker.Elapsed > ReturnGiveUpAfter)
            {
                ClassifiedPlugin.Log.LogWarning("Back from the raid: the main menu never showed; stepping aside.");
                Grant(realTime);
            }
        }

        /// <summary>Everything that depends only on the screen size. Units are 1080p pixels scaled to the screen.</summary>
        private void Layout(Vector2 size)
        {
            float u = _u = size.y / 1080f;

            // One texel per pixel, so the lines stay one pixel wide.
            _scanlines.uvRect = new Rect(0f, 0f, 1f, size.y / 3f);
            Place(_band.rectTransform, 0f, 0f, size.x, 260f * u);

            float inset = Mathf.Round(48f * u);
            float arm = Mathf.Round(46f * u);
            float width = Mathf.Max(2f, Mathf.Round(2f * u));
            DrawChrome(size, inset, arm, width);

            PlanLayout(size, inset, out Vector2 brand, out float bs,
                out float rx, out float ry, out float barWidth, out Vector2 log, out Vector2 chatter);

            // Emblem: the faction's insignia, in pieces that fly together.
            _brandScale = bs;
            _emblem.anchoredPosition = new Vector2(Mathf.Round(brand.x), Mathf.Round(brand.y + 175f * u * bs));
            LayoutGlows(size, u);
            Place(_alertGlow.rectTransform, 0f, 0f, 520f * u, 520f * u);
            // Its farthest corner 70 units out: clear of the innermost frame, at 86.
            _insignia.Show(_variant.Faction, 70f * u, Primary);

            _ringTrack.Clear();
            _ringTrack.Arc(124f * u, 2f * u, 0f, 360f, Theme.WithAlpha(Accent, 32));
            _ringTrack.Commit();
            _ringProgressDrawn = -1f;

            float wordWidth = 900f * u * bs;
            float wordY = brand.y - 2f * u * bs;
            float subY = brand.y - 42f * u * bs;
            SetText(_wordmark, brand.x, wordY, wordWidth, 60f * u * bs, 50f * u * bs, 26f);
            SetText(_wordmarkGhost, brand.x, wordY, wordWidth, 60f * u * bs, 50f * u * bs, 26f);
            SetText(_wordmarkSplit, brand.x, wordY, wordWidth, 60f * u * bs, 50f * u * bs, 26f);
            SetText(_subtitle, brand.x, subY, wordWidth, 24f * u, 15f * u, 10f);

            barWidth = _barWidth = Mathf.Round(barWidth);
            float barTop = ry - 98f * u;
            SetText(_stepCounter, rx - barWidth * 0.25f, barTop + 20f * u, barWidth * 0.5f, 18f * u, 13f * u, 8f);
            SetText(_percent, rx + barWidth * 0.25f, barTop + 20f * u, barWidth * 0.5f, 18f * u, 13f * u, 8f);
            LayoutBar(rx, barWidth, barTop);

            SetText(_currentLabel, rx - barWidth * 0.15f, ry - 136f * u, barWidth * 0.7f, 30f * u, 21f * u, 4f);
            LayoutCountdownAlert(rx, barWidth, ry - 136f * u, u);
            SetText(_labelGhost, rx - barWidth * 0.15f, ry - 136f * u, barWidth * 0.7f, 30f * u, 21f * u, 4f);
            SetText(_labelSplit, rx - barWidth * 0.15f, ry - 136f * u, barWidth * 0.7f, 30f * u, 21f * u, 4f);
            SetText(_currentTime, rx + barWidth * 0.35f, ry - 136f * u, barWidth * 0.3f, 30f * u, 21f * u, 4f);
            SetText(_currentDetail, rx, ry - 162f * u, barWidth, 20f * u, 13f * u, 6f);
            SetText(_logLabels, log.x - barWidth * 0.15f, log.y, barWidth * 0.7f, LogLines * 22f * u, 13f * u, 6f);
            SetText(_logTimes, log.x + barWidth * 0.35f, log.y, barWidth * 0.3f, LogLines * 22f * u, 13f * u, 6f);
            _logLabels.lineSpacing = _logTimes.lineSpacing = 18f;
            SetText(_chatter, chatter.x, chatter.y, barWidth, 18f * u, 12f * u, 6f);

            float textInset = inset + 16f * u;
            float halfW = size.x * 0.5f - textInset;
            float halfH = size.y * 0.5f - textInset;
            float cornerWidth = size.x * 0.45f;
            SetText(_topLeft, -halfW + cornerWidth * 0.5f, halfH - 9f * u, cornerWidth, 18f * u, 12f * u, 8f);
            SetText(_topRight, halfW - cornerWidth * 0.5f, halfH - 9f * u, cornerWidth, 18f * u, 12f * u, 8f);
            SetText(_bottomLeft, -halfW + cornerWidth * 0.5f, -halfH + 9f * u, cornerWidth, 18f * u, 12f * u, 8f);
            SetText(_bottomRight, halfW - cornerWidth * 0.5f, -halfH + 9f * u, cornerWidth, 18f * u, 12f * u, 8f);
            bool returning = StartupTracker.Kind == RunKind.Return;
            float last = Deploying ? StartupTracker.LastDeployTotal : returning ? StartupTracker.LastReturnTotal : StartupTracker.LastTotal;
            string lastName = Deploying ? Words("ПРЕД. ВЫХОД", "LAST DEPLOY")
                : returning ? Words("ПРЕД. ВОЗВРАТ", "LAST RETURN")
                : Words("ПРЕД. СЕАНС", "LAST SYNC");
            string lastSync = last > 0f
                ? lastName + $" <mspace=0.62em>{last:0.0}</mspace>" + Words("с", "s")
                : lastName + "  --";
            _bottomRight.text = Words("КЛИЕНТ", "CLIENT") + $" {Application.version}  //  {lastSync}";

            LayoutStyle(size, inset, halfH);
            CaptureTearBase();
        }

        /// <summary>
        /// The two big glows, from screen-resolution textures with the dither built in once they
        /// are ready (see DitheredGlow); the stretched Art.Radial until then.
        /// </summary>
        private void LayoutGlows(Vector2 size, float u)
        {
            int w = Mathf.RoundToInt(size.x);
            int h = Mathf.RoundToInt(size.y);
            // The screen glow: centred 60 units above the middle, 1.3 by 1.5 screens across.
            // Six alpha steps of noise: drawn at about 0.8 strength, the units' glows move
            // the screen by 0.18 to 0.32 of a step per alpha step, so this is one to two steps.
            bool glow = _glowBake.Request(w, h, w * 0.5f, h * 0.5f + 60f * u, size.x * 0.65f, size.y * 0.75f, GlowNoise);
            ApplyGlow(glow);
            // The logo's glow, brighter against what is under it: 0.5 to 0.8 of a step per alpha step.
            int e = Mathf.Max(2, Mathf.RoundToInt(460f * u * _brandScale));
            bool emblem = _emblemBake.Request(e, e, e * 0.5f, e * 0.5f, e * 0.5f, e * 0.5f, EmblemNoise);
            ApplyEmblemGlow(emblem);
        }

        private const int GlowNoise = 6;
        private const int EmblemNoise = 3;

        private void ApplyGlow(bool dithered)
        {
            Vector2 size = _laidOutSize;
            if (dithered && _glowBake.Texture != null)
            {
                _glow.texture = _glowBake.Texture;
                Place(_glow.rectTransform, 0f, 0f, size.x, size.y);
            }
            else
            {
                _glow.texture = Art.Radial;
                Place(_glow.rectTransform, 0f, 60f * _u, size.x * 1.3f, size.y * 1.5f);
            }
        }

        private void ApplyEmblemGlow(bool dithered)
        {
            float local = 460f * _u;
            if (dithered && _emblemBake.Texture != null)
            {
                _emblemGlow.texture = _emblemBake.Texture;
                // Sized so one texel is one screen pixel at the logo's scale.
                RectTransform rect = _emblemGlow.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                local = _emblemBake.Texture.width / Mathf.Max(0.01f, _brandScale);
                rect.sizeDelta = new Vector2(local, local);
            }
            else
            {
                _emblemGlow.texture = Art.Radial;
                Place(_emblemGlow.rectTransform, 0f, 0f, local, local);
            }
        }

        private void AnimateEmblem(float t, float elapsed)
        {
            // The pieces fly in from outside, one after another, and lock together over the
            // first second.
            int count = _insignia.Count;
            float stagger = count > 1 ? 0.36f / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                float own = Mathf.Clamp01((elapsed - i * stagger) / 0.7f);
                float ease = 1f - Mathf.Pow(1f - own, 3f);
                _insignia.Place(i, 70f * _u * (1f - ease), ease);
            }

            float frame = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.9f), 3f);
            AnimateFrame(t, frame);
            float glow = 0.22f + 0.08f * Mathf.Sin(t * 2.2f) + 0.25f * _beat;
            _emblemGlow.canvasRenderer.SetAlpha(glow * frame * Mathf.Lerp(0.6f, 1f, _mood));
            _alertGlow.canvasRenderer.SetAlpha(_alert * (0.28f + 0.12f * Mathf.Sin(t * 5f)));
            if (_phase == Phase.Loading)
            {
                _emblem.localScale = Vector3.one * (_brandScale * (1f + 0.035f * _beat));
            }
        }

        private void UpdateProgress(float fraction)
        {
            _shownFraction = Mathf.Lerp(_shownFraction, fraction, 1f - Mathf.Exp(-_frameDt * 6f));
            if (fraction >= 1f && 1f - _shownFraction < 0.002f)
            {
                _shownFraction = 1f;
            }

            // The ring is redrawn only when it has visibly moved.
            if (Mathf.Abs(_shownFraction - _ringProgressDrawn) > 0.002f || (_shownFraction >= 1f && _ringProgressDrawn < 1f))
            {
                _ringProgressDrawn = _shownFraction;
                _ringProgress.Clear();
                _ringProgress.Arc(124f * _u, 3f * _u, 0f, 360f * _shownFraction, Accent);
                _ringProgress.Commit();
            }
            UpdateSolidBar();

            if (_phase != Phase.Loading)
            {
                return;
            }
            int percent = Mathf.FloorToInt(_shownFraction * 100f);
            if (percent != _shownPercent)
            {
                _shownPercent = percent;
                _percent.text = $"<mspace=0.62em>{percent}</mspace>%";
            }
        }

        private void UpdateSteps(float t)
        {
            int current = StartupTracker.Current;
            if (current < 0)
            {
                return;
            }
            if (current != _shownStep)
            {
                bool first = _shownStep < 0;
                _shownStep = current;
                _stepShownAt = t;
                _shownStepTenths = -1;
                if (!first)
                {
                    Beat(t);
                }
                int firstStep = StartupTracker.FirstStep;
                _stepCounter.text = Words("ЭТАП", "STEP") + $" <mspace=0.62em>{current - firstStep + 1:00}</mspace> / {StartupTracker.RunSteps:00}";
                bool boot = current < (int)StepId.Connect;
                if (boot != _bootSubtitle)
                {
                    _bootSubtitle = boot;
                    _subtitle.text = Subtitle(boot);
                }
                _currentLabel.text = "> " + StepName(current);
                _currentLabel.color = Text;
                _currentLabel.maxVisibleCharacters = 0;
                RebuildLog(current);
                _slow = false;
                _currentTime.color = Text;
                _solidFill.color = Accent;
                for (int i = 0; i < _segments.Length; i++)
                {
                    _segments[i].color = i < current ? (Color)Accent : (Color)Faint;
                    _segmentFills[i].color = Accent;
                    _segmentFills[i].rectTransform.anchorMax = new Vector2(0f, 1f);
                    _segmentFills[i].enabled = i == current;
                }
            }

            TypeOut(t);

            // The running segment fills toward where this step usually ends, and pulses.
            RectTransform fill = _segmentFills[current].rectTransform;
            float target = StartupTracker.CurrentStepFraction();
            fill.anchorMax = new Vector2(Mathf.Lerp(fill.anchorMax.x, target, 1f - Mathf.Exp(-_frameDt * 6f)), 1f);
            _segmentFills[current].canvasRenderer.SetAlpha(0.65f + 0.35f * Mathf.Sin(t * 6f));

            string detail = StartupTracker.Detail(current);
            int tenths = Mathf.FloorToInt(StartupTracker.StepElapsed * 10f);
            bool detailChanged = detail != _shownDetail;
            if (tenths == _shownStepTenths && !detailChanged)
            {
                return;
            }
            _shownDetail = detail;
            if (tenths != _shownStepTenths)
            {
                _shownStepTenths = tenths;
                _currentTime.text = StepTimeText(current, tenths);
            }

            // Running well past this machine's usual time for the step: say so, in the warning
            // colour. Only once a full launch has set the usual times; the defaults are guesses.
            float expected = StartupTracker.ExpectedSeconds(current);
            float over = StartupTracker.StepElapsed - expected;
            bool slow = StartupTracker.HasBaseline && CanRunSlow(current) && over > Mathf.Max(SlowAfter, expected * 0.5f);
            bool wasSlow = _slow;
            if (slow != _slow)
            {
                _slow = slow;
                _currentTime.color = slow ? Warn : Text;
                _segmentFills[current].color = slow ? Warn : Accent;
                _solidFill.color = slow ? Warn : Accent;
            }

            string warnHex = _variant.Theme.WarnHex;
            int overTenths = slow ? Mathf.FloorToInt(over * 10f) : -1;
            if (slow && (overTenths != _shownOverTenths || detailChanged || !wasSlow))
            {
                _shownOverTenths = overTenths;
                string late = Cyr(Lang.Bear(_variant.Theme.SlowNote)) + $"  +{over:0.0}" + Words("С", "S");
                _currentDetail.text = string.IsNullOrEmpty(detail)
                    ? $"  <color={warnHex}>{late}</color>"
                    : $"  {detail.ToUpperInvariant()}   <color={warnHex}>{late}</color>";
            }
            else if (!slow && (detailChanged || wasSlow))
            {
                _shownOverTenths = -1;
                _currentDetail.text = string.IsNullOrEmpty(detail) ? string.Empty : "  " + detail.ToUpperInvariant();
            }
        }

        /// <summary>Every second or two a new line of made-up radio chatter types itself out.</summary>
        private void UpdateChatter(float t)
        {
            if (_introClock < IntroLength + 1f)
            {
                return;
            }
            if (t >= _chatterNextAt)
            {
                _chatter.text = Cyr(_chatterSource.Next());
                _chatter.maxVisibleCharacters = 0;
                _chatterShownAt = t;
                _chatterNextAt = t + Range(1.2f, 2.6f);
            }
            TypeChatter(t);
        }

        private void TypeChatter(float t)
        {
            if (_chatter.maxVisibleCharacters < _chatter.text.Length)
            {
                _chatter.maxVisibleCharacters = Mathf.FloorToInt((t - _chatterShownAt) * TypeSpeed) + 1;
            }
        }

        /// <summary>The current line types itself out.</summary>
        private void TypeOut(float t)
        {
            if (_currentLabel.maxVisibleCharacters < _currentLabel.text.Length)
            {
                _currentLabel.maxVisibleCharacters = Mathf.FloorToInt((t - _stepShownAt) * TypeSpeed) + 2;
            }
        }

        /// <summary>The steps before <paramref name="current"/>, newest first, older lines fading out.</summary>
        private void RebuildLog(int current)
        {
            _builder.Length = 0;
            _timesBuilder.Length = 0;
            int lines = 0;
            for (int i = current - 1; i >= StartupTracker.FirstStep && lines < LogLines; i--)
            {
                // Steps the game skipped (SPT turns some off) are left out rather than listed empty.
                if (StartupTracker.WasSkipped(i))
                {
                    continue;
                }
                int alpha = 255 - lines * 50;
                lines++;
                string open = $"<alpha=#{alpha:X2}>";
                _builder.Append(open).Append("<color=").Append(_variant.Theme.AccentHex).Append('>')
                    .Append(Cyr(Lang.Bear(_variant.Theme.LogTag))).Append("</color>   ").Append(StepName(i));
                string detail = StartupTracker.Detail(i);
                if (!string.IsNullOrEmpty(detail))
                {
                    _builder.Append("   <alpha=#").Append((alpha / 2).ToString("X2")).Append('>').Append(detail.ToUpperInvariant());
                }
                _builder.Append('\n');
                _timesBuilder.Append(open)
                    .Append($"<mspace=0.62em>{StartupTracker.TookSeconds(i):0.0}</mspace>s")
                    .Append('\n');
            }
            _logLabels.text = _builder.ToString();
            _logTimes.text = _timesBuilder.ToString();
        }

        private void UpdateClock(float elapsed)
        {
            int tenths = Mathf.FloorToInt(elapsed * 10f);
            if (tenths != _shownElapsedTenths)
            {
                _shownElapsedTenths = tenths;
                _bottomLeft.text = ClockText(tenths);
            }
        }

        private TextMeshProUGUI MakeText(string name, Transform parent, TextAlignmentOptions alignment, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSharedMaterial = _material;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        private static Image MakeImage(string name, Transform parent, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage MakeRaw(string name, Transform parent, Texture texture, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Gives a part that moves or changes often a canvas of its own. Unity redraws a canvas's
        /// whole batch when anything in it changes; isolated, a turning ring or a ticking timer
        /// costs only its own redraw, not the whole screen's, every frame.
        /// </summary>
        private static void Isolate(Component part)
        {
            part.gameObject.AddComponent<Canvas>();
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre at (x, y) from the middle of the screen, snapped to whole pixels.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            width = Mathf.Round(width);
            height = Mathf.Round(height);
            rect.sizeDelta = new Vector2(width, height);
            // An odd size centres on a half pixel; offset it so its edges still land on pixels.
            rect.anchoredPosition = new Vector2(
                Mathf.Round(x) + (width % 2f) * 0.5f,
                Mathf.Round(y) + (height % 2f) * 0.5f);
        }

        private static void SetText(TextMeshProUGUI text, float x, float y, float width, float height, float fontSize, float spacing)
        {
            Place(text.rectTransform, x, y, width, height);
            text.fontSize = Mathf.Round(fontSize);
            text.characterSpacing = spacing;
        }
    }
}
