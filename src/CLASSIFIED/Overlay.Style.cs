using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The parts of the screen that change with the unit: backdrop, insignia frame, intro,
    /// progress bar and ending, plus the news ticker, the memo, the stamp, and the live
    /// reactions every unit shares.
    ///
    /// Reactions: progress builds intensity (frames turn faster, glitches come sooner); each
    /// finished step is a beat (the emblem pulses); the boot half is dimmer than the sign-in half;
    /// a step running well over its usual time puts the screen in alert (a warning glow, faster
    /// frames) until it ends.
    ///
    /// All of it is built at layout time and animated through transforms and alphas. The few
    /// things rebuilt while loading (the chart, the solid bar) are rebuilt only when they move.
    /// </summary>
    internal partial class Overlay
    {
        private const float TickerSpeed = 60f;
        private const float MemoIn = 0.35f;
        private const float MemoHold = 7f;
        private const float SheenTime = 0.9f;

        private LayoutKind _layout;
        private float _brandScale = 1f;
        private Image _panel;
        private Image _panelEdge;
        private Vector2 _memoSpot;
        private float _memoWidth;
        private Shape _watermark;
        private RawImage _sheen;
        private RawImage _alertGlow;
        private Vector2 _backdropOffset;

        private readonly Shape[] _frameParts = new Shape[4];
        private readonly float[] _partSpin = new float[4];
        private readonly float[] _partAngle = new float[4];
        private int _partCount;
        private int _dialPointer = -1;
        private int _recDot = -1;
        private TextMeshProUGUI _recLabel;

        private Image _solidTrack;
        private Image _solidFill;
        private float _solidDrawn = -1f;

        private RectTransform _tickerMask;
        private TextMeshProUGUI _ticker;
        private Image _tickerTop;
        private Image _tickerBottom;
        private float _tickerLoop;
        private float _tickerOffset;

        private RectTransform _memo;
        private Image _memoPanel;
        private Image _memoStripe;
        private TextMeshProUGUI _memoTitle;
        private TextMeshProUGUI _memoBody;
        private float _memoShownAt = -1f;
        private int _noticeIndex;
        private float _memoX;
        private float _memoOff;


        private Image _flash;
        private TextMeshProUGUI _introTitle;
        private TextMeshProUGUI _introSub;
        private float _sheenAt = -1f;


        private float _beatAt = -10f;
        private float _beat;
        private float _mood;
        private float _alert;
        private float _spinScale = 1f;
        private float _nextItemPoll;

        private float IntroLength
        {
            get
            {
                switch (_variant.Intro)
                {
                    case IntroKind.TitleCard: return 1.25f;
                    case IntroKind.Wipe: return 0.5f;
                    case IntroKind.Crt: return 0.55f;
                    case IntroKind.Doors: return 0.75f;
                    default: return 0.35f;
                }
            }
        }

        private void BuildBackdropExtras(Transform c)
        {
            _panel = MakeImage("Panel", c, Ink);
            _panelEdge = MakeImage("PanelEdge", c, Accent);
            _watermark = Shape.Create("Watermark", c);
            BuildSignatureRoot(c);
            _sheen = MakeRaw("Sheen", c, Art.Band, Accent);
            Isolate(_sheen);
            _sheen.gameObject.SetActive(false);
        }

        private void BuildSolidBar(Transform c)
        {
            _solidTrack = MakeImage("SolidTrack", c, Faint);
            _solidFill = MakeImage("SolidFill", _solidTrack.transform, Accent);
            RectTransform fill = _solidFill.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        private void BuildTicker(Transform c)
        {
            _tickerMask = MakeRect("Ticker", c);
            _tickerMask.gameObject.AddComponent<RectMask2D>();
            _ticker = MakeText("TickerText", _tickerMask, TextAlignmentOptions.Left, Dim);
            Isolate(_ticker);
            RectTransform rect = _ticker.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            _tickerTop = MakeImage("TickerTop", c, Faint);

            _tickerBottom = MakeImage("TickerBottom", c, Faint);
        }

        private void BuildMemo(Transform c)
        {
            _memo = MakeRect("Memo", c);
            Isolate(_memo);
            _memoPanel = MakeImage("Panel", _memo, Ink);
            Stretch(_memoPanel.rectTransform);
            _memoStripe = MakeImage("Stripe", _memo, Accent);
            _memoTitle = MakeText("Title", _memo, TextAlignmentOptions.TopLeft, Accent);
            // A long unit name on a narrow screen must not run off the card.
            _memoTitle.overflowMode = TextOverflowModes.Ellipsis;
            _memoBody = MakeText("Body", _memo, TextAlignmentOptions.TopLeft, Text);
            _memoBody.enableWordWrapping = true;
            _memo.gameObject.SetActive(false);
        }

        /// <summary>The unit's own colours, textures and words.</summary>
        private void ApplyStyle()
        {
            Theme theme = _variant.Theme;
            _grid.texture = BackdropTexture(_variant.Backdrop);
            _grid.color = Theme.WithAlpha(Accent, BackdropAlpha(_variant.Backdrop));
            _grid.gameObject.SetActive(_variant.Backdrop != BackdropKind.Plain);
            _panel.color = Theme.WithAlpha(Color32.Lerp(Ink, Primary, 0.1f), 215);
            _panelEdge.color = Theme.WithAlpha(Accent, 90);
            _sheen.color = Theme.WithAlpha(Accent, 45);
            _recLabel.color = Warn;

            _solidTrack.color = Faint;
            _solidFill.color = Accent;
            _ticker.color = Dim;
            _ticker.text = _variant.Ticker + _variant.Ticker;
            _tickerTop.color = _tickerBottom.color = Faint;

            _memoPanel.color = Color32.Lerp(Ink, Primary, 0.16f);
            _memoStripe.color = Accent;
            _memoTitle.color = Accent;
            _memoBody.color = Text;


            Color32 cover = _variant.Intro == IntroKind.Wipe ? Primary
                : _variant.Intro == IntroKind.TitleCard ? Ink
                : _variant.Intro == IntroKind.Doors ? Color32.Lerp(Ink, Primary, 0.22f)
                : new Color32(0, 0, 0, 255);
            _shutterTop.GetComponent<Image>().color = cover;
            _shutterBottom.GetComponent<Image>().color = cover;
            _powerLine.color = _variant.Intro == IntroKind.Crt ? Color32.Lerp(Accent, new Color32(255, 255, 255, 255), 0.6f) : Accent;
            _introTitle.color = Text;
            _introSub.color = Accent;
            _introSub.text = theme.TitleCard;
            _flash.color = Accent;
        }

        /// <summary>Per-run state of the style parts.</summary>
        private void ResetStyle()
        {
            _beatAt = -10f;
            _beat = 0f;
            _mood = StartupTracker.FirstStep >= (int)StepId.Connect ? 1f : 0f;
            _alert = 0f;
            _memoShownAt = -1f;
            _noticeIndex = 0;
            _memo.gameObject.SetActive(false);
            _sheenAt = -1f;
            _solidDrawn = -1f;
            _content.anchoredPosition = Vector2.zero;
            _content.localScale = Vector3.one;
            _flash.gameObject.SetActive(false);
        }

        private static Texture BackdropTexture(BackdropKind kind)
        {
            switch (kind)
            {
                case BackdropKind.Blueprint: return Art.Blueprint;
                case BackdropKind.Plain: return Art.Dots;
                case BackdropKind.Grid: return Art.Grid;
                case BackdropKind.Hex: return Art.Hex;
                case BackdropKind.Pinstripe: return Art.Pinstripe;
                case BackdropKind.Ruled: return Art.Ruled;
                default: return Art.Dots;
            }
        }

        private static byte BackdropAlpha(BackdropKind kind)
        {
            switch (kind)
            {
                case BackdropKind.Blueprint: return 22;
                case BackdropKind.Plain: return 0;
                case BackdropKind.Grid: return 16;
                case BackdropKind.Hex: return 26;
                case BackdropKind.Pinstripe: return 12;
                case BackdropKind.Ruled: return 24;
                default: return 34;
            }
        }

        /// <summary>Drift speed of the backdrop, in pixels a second.</summary>
        private static Vector2 BackdropDrift(BackdropKind kind)
        {
            switch (kind)
            {
                case BackdropKind.Blueprint: return new Vector2(1.5f, 1f);
                case BackdropKind.Grid: return new Vector2(0.8f, 1.3f);
                case BackdropKind.Hex: return new Vector2(3f, 0f);
                case BackdropKind.Pinstripe: return new Vector2(8f, 0f);
                case BackdropKind.Ruled: return new Vector2(0f, 4f);
                default: return new Vector2(4f, 2.5f);
            }
        }

        /// <summary>
        /// Where the brand (logo, wordmark, subtitle), the readout (bar, current step, log,
        /// chatter), the two dashboard panels and the memo go for this launch's arrangement.
        /// Everything is in 1080p units scaled to the screen height, so only the width varies
        /// between resolutions: columns stay within 2.1 screen-heights so ultrawide screens do
        /// not push them apart, the panels shrink (or drop out) when a narrow screen leaves less
        /// room, and the memo always hugs the top-right edge.
        /// </summary>
        private void PlanLayout(Vector2 size, float inset, out Vector2 brand, out float brandScale,
            out float rx, out float ry, out float barWidth, out Vector2 log, out Vector2 chatter)
        {
            float u = _u;
            float w = size.x;
            float h = size.y;
            float cw = Mathf.Min(w, h * 2.1f);
            LayoutKind kind = _variant.Layout;
            if ((kind == LayoutKind.Split || kind == LayoutKind.SplitMirror) && w / h < 1.5f)
            {
                kind = LayoutKind.Centered;
            }
            _layout = kind;

            brand = Vector2.zero;
            brandScale = 1f;
            rx = 0f;
            ry = 0f;
            barWidth = 600f * u;
            float panelWidth = 0f;
            float logX = 0f;

            switch (kind)
            {
                case LayoutKind.Split:
                case LayoutKind.SplitMirror:
                {
                    float side = kind == LayoutKind.Split ? 1f : -1f;
                    brand = new Vector2(-side * cw * 0.23f, -90f * u);
                    rx = side * cw * 0.2f;
                    ry = 210f * u;
                    break;
                }
                case LayoutKind.Sidebar:
                {
                    panelWidth = Mathf.Round(620f * u);
                    float centre = -w * 0.5f + panelWidth * 0.5f;
                    brand = new Vector2(centre, 200f * u);
                    brandScale = 0.75f;
                    rx = centre;
                    ry = 160f * u;
                    barWidth = panelWidth - 120f * u;
                    break;
                }
                case LayoutKind.Dock:
                {
                    brand = new Vector2(0f, 140f * u);
                    brandScale = 1.15f;
                    barWidth = Mathf.Min(600f * u, (w - 2f * inset - 120f * u) * 0.5f);
                    rx = -(barWidth * 0.5f + 40f * u);
                    logX = barWidth * 0.5f + 40f * u;
                    break;
                }
                default:
                    break;
            }

            // Under the current step's detail line, with room so the two never touch.
            log = new Vector2(rx, ry - 224f * u);
            chatter = new Vector2(rx, ry - 300f * u);
            if (kind == LayoutKind.Dock)
            {
                log = new Vector2(logX, -184f * u);
                chatter = new Vector2(logX, -268f * u);
            }

            _panel.gameObject.SetActive(kind == LayoutKind.Sidebar || kind == LayoutKind.Dock);
            _panelEdge.gameObject.SetActive(_panel.gameObject.activeSelf);
            float edge = Mathf.Max(1f, Mathf.Round(u));
            if (kind == LayoutKind.Sidebar)
            {
                Place(_panel.rectTransform, -w * 0.5f + panelWidth * 0.5f, 0f, panelWidth, h + 4f);
                Place(_panelEdge.rectTransform, -w * 0.5f + panelWidth, 0f, edge, h + 4f);
            }
            else if (kind == LayoutKind.Dock)
            {
                Place(_panel.rectTransform, 0f, -205f * u, w + 4f, 230f * u);
                Place(_panelEdge.rectTransform, 0f, -90f * u, w + 4f, edge);
            }

            // The memo hugs the top-right edge, under the corner text, in every layout.
            _memoWidth = Mathf.Min(380f * u, w * 0.5f - inset - 40f * u);
            _memoSpot = new Vector2(w * 0.5f - inset - 10f * u - _memoWidth * 0.5f, h * 0.5f - inset - 50f * u);
        }

        /// <summary>Sizes the memo to its text: the title, then the body, with even padding.</summary>
        private void FitMemo()
        {
            float u = _u;
            float width = _memoWidth;
            float textWidth = width - 44f * u;
            _memoBody.fontSize = Mathf.Round(15f * u);
            float bodyHeight = _memoBody.GetPreferredValues(_memoBody.text, textWidth, 0f).y;
            float height = Mathf.Round(18f * u + 18f * u + 8f * u + bodyHeight + 18f * u);
            _memo.sizeDelta = new Vector2(Mathf.Round(width), height);
            Place(_memoStripe.rectTransform, -width * 0.5f + 2f * u, 0f, 4f * u, height);
            SetText(_memoTitle, 10f * u, height * 0.5f - 18f * u - 9f * u, textWidth, 18f * u, 12f * u, 6f);
            SetText(_memoBody, 10f * u, height * 0.5f - 44f * u - bodyHeight * 0.5f, textWidth, bodyHeight + 2f, 15f * u, 2f);
            // Its top edge stays put whatever its height.
            _memo.anchoredPosition = new Vector2(_memo.anchoredPosition.x, Mathf.Round(_memoSpot.y - height * 0.5f));
        }

        private void LayoutStyle(Vector2 size, float inset, float halfH)
        {
            float u = _u;
            Theme theme = _variant.Theme;


            // Like the ambient drawings, this keeps to the space the layout leaves free of text.
            bool room = ArtZones(out Rect left, out Rect right, out _);
            Rect tall = left.height >= right.height ? left : right;
            _watermark.gameObject.SetActive(theme.Watermark && room);

            // Command: the insignia again, large and faint, beside the terminal.
            _watermark.Clear();
            if (theme.Watermark && room)
            {
                float r = Mathf.Min(Mathf.Min(tall.width, tall.height) * 0.42f, 300f) * u;
                Color32 tint = Theme.WithAlpha(Primary, 18);
                Insignia.DrawAll(_variant.Faction, _watermark, r, tint, Theme.WithAlpha(Primary, 28), Theme.WithAlpha(Ink, 220));
                _watermark.rectTransform.anchoredPosition = tall.center * u;
            }
            _watermark.Commit();

            Place(_sheen.rectTransform, 0f, 0f, size.y * 2.6f, size.y * 0.22f);
            _sheen.rectTransform.localEulerAngles = new Vector3(0f, 0f, 65f);

            LayoutFrame(u);

            // The ticker runs between the bottom corners.
            float tickerWidth = size.x - 2f * (inset + 70f * u);
            float tickerY = -halfH + 44f * u;
            Place(_tickerMask, 0f, tickerY, tickerWidth, 22f * u);
            _ticker.fontSize = Mathf.Round(12f * u);
            _ticker.characterSpacing = 6f;
            float textWidth = _ticker.GetPreferredValues(_ticker.text).x;
            _ticker.rectTransform.sizeDelta = new Vector2(textWidth, 22f * u);
            _tickerLoop = textWidth * 0.5f;
            Place(_tickerTop.rectTransform, 0f, tickerY + 13f * u, tickerWidth, 1f);
            Place(_tickerBottom.rectTransform, 0f, tickerY - 13f * u, tickerWidth, 1f);

            // The memo, where the layout left room for it; left out if there is none.
            _memoOff = _memoWidth >= 240f * u ? 1f : -1f;
            if (_memoOff > 0f)
            {
                _memoX = Mathf.Round(_memoSpot.x);
                // How far right the memo starts, to be off the screen.
                _memoOff = size.x * 0.5f - _memoX + _memoWidth * 0.5f + 20f * u;
                _memo.anchoredPosition = new Vector2(_memoX + _memoOff, 0f);
                FitMemo();
            }

            LayoutCaption(size);

            LayoutSignature();

            // The title card.
            SetText(_introTitle, 0f, 18f * u, size.x, 80f * u, 64f * u, 30f);
            SetText(_introSub, 0f, -40f * u, size.x, 30f * u, 17f * u, 12f);
        }

        /// <summary>The ring of parts around the logo, one design per unit.</summary>
        private void LayoutFrame(float u)
        {
            foreach (Shape part in _frameParts)
            {
                part.Clear();
                part.Commit();
                part.gameObject.SetActive(false);
            }
            _partCount = 0;
            _dialPointer = -1;
            _recDot = -1;
            _recLabel.gameObject.SetActive(false);

            switch (_variant.Frame)
            {
                case FrameKind.Rings:
                {
                    int dashes = _variant.DashCount;
                    Shape p = Part(_variant.DashSpin);
                    for (int i = 0; i < dashes; i++)
                    {
                        p.Arc(96f * u, (i % _variant.DashMajorEvery == 0 ? 7f : 3f) * u, i * 360f / dashes, 2.2f * 48f / dashes, Theme.WithAlpha(Accent, 130));
                    }
                    p = Part(_variant.ArcSpin);
                    for (int i = 0; i < _variant.ArcCount; i++)
                    {
                        p.Arc(110f * u, 2.5f * u, i * 360f / _variant.ArcCount + _variant.ArcStart, _variant.ArcSweep, Theme.WithAlpha(Primary, 220));
                    }
                    break;
                }
                case FrameKind.Brackets:
                {
                    // A camera's framing brackets, a slow ring inside them, a blinking REC.
                    Shape p = Part(0f);
                    float s = 108f * u;
                    float arm = 26f * u;
                    float w = Mathf.Max(2f, 3f * u);
                    Color32 col = Theme.WithAlpha(Accent, 210);
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sy = -1; sy <= 1; sy += 2)
                        {
                            var corner = new Vector2(sx * s, sy * s);
                            p.Line(corner + new Vector2(sx * w * 0.5f, 0f), corner - new Vector2(sx * arm, 0f), w, col);
                            p.Line(corner, corner - new Vector2(0f, sy * arm), w, col);
                        }
                    }
                    p = Part(_variant.DashSpin * 0.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        p.Arc(92f * u, 1.5f * u, i * 90f + 8f, 74f, Theme.WithAlpha(Accent, 90));
                    }
                    p = Part(0f);
                    _recDot = _partCount - 1;
                    p.Disc(new Vector2(s - 4f * u, s + 16f * u), 4f * u, Warn);
                    _recLabel.gameObject.SetActive(true);
                    SetText(_recLabel, s - 40f * u, s + 16f * u, 60f * u, 16f * u, 11f * u, 6f);
                    break;
                }
                case FrameKind.Molecule:
                {
                    // Six atoms in a ring, bonded, with a dashed inner shell.
                    Shape p = Part(_variant.ArcSpin * 0.4f);
                    float r = 104f * u;
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 at = Polar(r, i * 60f);
                        Vector2 next = Polar(r, (i + 1) * 60f);
                        p.Line(at, next, Mathf.Max(1.5f, 1.5f * u), Theme.WithAlpha(Accent, 110));
                        if (i % 2 == 0)
                        {
                            Vector2 outer = Polar(r + 16f * u, i * 60f);
                            p.Line(at, outer, Mathf.Max(1f, 1.2f * u), Theme.WithAlpha(Accent, 90));
                            p.Disc(outer, 3f * u, Theme.WithAlpha(Accent, 200));
                        }
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        p.Disc(Polar(r, i * 60f), 5.5f * u, Theme.WithAlpha(Accent, 235));
                    }
                    p = Part(_variant.DashSpin * 0.6f);
                    for (int i = 0; i < 24; i++)
                    {
                        p.Arc(86f * u, 2f * u, i * 15f, 6f, Theme.WithAlpha(Primary, 170));
                    }
                    break;
                }
                case FrameKind.Dial:
                {
                    // A gauge: sixty ticks, a slow outer arc, and a needle that tracks progress.
                    Shape p = Part(0f);
                    for (int i = 0; i < 60; i++)
                    {
                        bool major = i % 5 == 0;
                        Vector2 from = Polar(96f * u, i * 6f);
                        Vector2 to = Polar((major ? 107f : 101f) * u, i * 6f);
                        p.Line(from, to, Mathf.Max(1f, (major ? 2f : 1.2f) * u), Theme.WithAlpha(Accent, (byte)(major ? 210 : 90)));
                    }
                    p = Part(_variant.ArcSpin * 0.3f);
                    p.Arc(113f * u, 1.5f * u, _variant.ArcStart, 120f, Theme.WithAlpha(Primary, 160));
                    p.Arc(113f * u, 1.5f * u, _variant.ArcStart + 180f, 120f, Theme.WithAlpha(Primary, 160));
                    p = Part(0f);
                    _dialPointer = _partCount - 1;
                    p.Tri(new Vector2(0f, 110f * u), new Vector2(-5f * u, 120f * u), new Vector2(5f * u, 120f * u), Text);
                    break;
                }
                case FrameKind.Seal:
                {
                    // A company seal: two fine rings with a band of dots between.
                    Shape p = Part(0f);
                    p.Arc(90f * u, 1.5f * u, 0f, 360f, Theme.WithAlpha(Accent, 130));
                    p.Arc(114f * u, 1.5f * u, 0f, 360f, Theme.WithAlpha(Accent, 130));
                    p = Part(_variant.DashSpin * 0.4f);
                    for (int i = 0; i < 36; i++)
                    {
                        p.Disc(Polar(102f * u, i * 10f), (i % 9 == 0 ? 3f : 1.8f) * u, Theme.WithAlpha(Accent, 160));
                    }
                    break;
                }
                case FrameKind.Ornate:
                {
                    // Double ring, a turning band of gold beads, four diamonds turning the other way.
                    Shape p = Part(0f);
                    p.Arc(88f * u, 2f * u, 0f, 360f, Theme.WithAlpha(Primary, 210));
                    p.Arc(117f * u, 1.2f * u, 0f, 360f, Theme.WithAlpha(Accent, 140));
                    p = Part(_variant.ArcSpin * 0.3f);
                    for (int i = 0; i < 12; i++)
                    {
                        p.Disc(Polar(102f * u, i * 30f), 3f * u, Theme.WithAlpha(Accent, 220));
                    }
                    p = Part(-_variant.ArcSpin * 0.3f);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 at = Polar(117f * u, i * 90f + 45f);
                        Vector2 out1 = at.normalized * 7f * u;
                        Vector2 side = new Vector2(-out1.y, out1.x) * 0.6f;
                        p.Polygon(new[] { at + out1, at + side, at - out1, at - side }, Accent);
                    }
                    break;
                }
                case FrameKind.Minimal:
                {
                    // One fine ring with compass ticks, and a single highlight easing round it.
                    Shape p = Part(0f);
                    p.Arc(100f * u, Mathf.Max(1f, 1.2f * u), 0f, 360f, Theme.WithAlpha(Accent, 100));
                    for (int i = 0; i < 8; i++)
                    {
                        bool major = i % 2 == 0;
                        p.Line(Polar((major ? 90f : 95f) * u, i * 45f), Polar((major ? 110f : 105f) * u, i * 45f),
                            Mathf.Max(1f, (major ? 2f : 1.2f) * u), Theme.WithAlpha(Accent, (byte)(major ? 200 : 110)));
                    }
                    p = Part(_variant.ArcSpin * 0.5f);
                    p.Arc(100f * u, 3f * u, 0f, 40f, Theme.WithAlpha(Primary, 230));
                    break;
                }
                case FrameKind.Squares:
                {
                    // Two squares, one turned to echo the diamond, rotating against each other.
                    float r = 104f * u;
                    float w = Mathf.Max(1.5f, 1.5f * u);
                    for (int s = 0; s < 2; s++)
                    {
                        Shape p = Part((s == 0 ? 1f : -1f) * _variant.DashSpin * 0.35f);
                        float turn = s == 0 ? 0f : 45f;
                        Color32 col = Theme.WithAlpha(s == 0 ? Accent : Primary, (byte)(s == 0 ? 130 : 200));
                        for (int k = 0; k < 4; k++)
                        {
                            Vector2 a = Polar(r, turn + k * 90f);
                            Vector2 b = Polar(r, turn + (k + 1) * 90f);
                            Vector2 along = (b - a).normalized * (w * 0.5f);
                            p.Line(a - along, b + along, w, col);
                        }
                    }
                    break;
                }
                case FrameKind.Orbit:
                {
                    // A faint ring with one bright point going round it, trailing light.
                    Shape p = Part(0f);
                    p.Arc(106f * u, Mathf.Max(1f, u), 0f, 360f, Theme.WithAlpha(Accent, 60));
                    float spin = _variant.ArcSpin * 2.2f;
                    p = Part(spin);
                    // Rotation is anticlockwise for a positive spin, and arcs run clockwise, so a
                    // trail behind the point lies clockwise of it then. One continuous arc,
                    // thinning and fading from the point back.
                    float side = spin > 0f ? 1f : -1f;
                    p.ArcGradient(106f * u, 0f, 75f * side, 4f * u, 0.5f * u, Theme.WithAlpha(Accent, 230), Theme.WithAlpha(Accent, 0));
                    p.Disc(Vector2.up * 106f * u, 5f * u, Text);
                    break;
                }
            }

            for (int i = 0; i < _partCount; i++)
            {
                _frameParts[i].Commit();
            }
        }

        private Shape Part(float spin)
        {
            Shape part = _frameParts[_partCount];
            _partSpin[_partCount] = spin;
            _partAngle[_partCount] = 0f;
            part.rectTransform.localEulerAngles = Vector3.zero;
            part.gameObject.SetActive(true);
            _partCount++;
            return part;
        }

        private static Vector2 Polar(float radius, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
        }

        private void LayoutBar(float centre, float barWidth, float barTop)
        {
            BarKind kind = _variant.Bar;
            int first = StartupTracker.FirstStep;
            int shown = _segments.Length - first;
            bool thin = kind == BarKind.Thin;
            float segmentGap = Mathf.Round((thin ? 3f : shown > 12 ? 4f : 6f) * _u);
            float segmentWidth = Mathf.Floor((barWidth - segmentGap * (shown - 1)) / shown);
            float segmentHeight = thin ? Mathf.Max(2f, Mathf.Round(2f * _u)) : Mathf.Max(3f, Mathf.Round(4f * _u));
            float rowWidth = segmentWidth * shown + segmentGap * (shown - 1);
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].gameObject.SetActive(kind != BarKind.Solid && i >= first);
                float x = centre - rowWidth * 0.5f + (i - first) * (segmentWidth + segmentGap) + segmentWidth * 0.5f;
                Place(_segments[i].rectTransform, x, barTop, segmentWidth, segmentHeight);
            }

            _solidTrack.gameObject.SetActive(kind == BarKind.Solid);
            Place(_solidTrack.rectTransform, centre, barTop, barWidth, Mathf.Max(3f, Mathf.Round(4f * _u)));
            _solidDrawn = -1f;
        }

        private void UpdateSolidBar()
        {
            if (_variant.Bar != BarKind.Solid)
            {
                return;
            }
            float width = Mathf.Round(_barWidth * _shownFraction);
            if (width != _solidDrawn)
            {
                _solidDrawn = width;
                _solidFill.rectTransform.sizeDelta = new Vector2(width, 0f);
            }
        }

        private void PollItems(float t)
        {
            int current = StartupTracker.Current;
            if (t < _nextItemPoll || (current != (int)StepId.ItemDatabase && current != (int)StepId.IconCache))
            {
                return;
            }
            _nextItemPoll = t + 0.25f;
            Patches.NoteItemCount();
        }

        private void Beat(float t)
        {
            _beatAt = t;
        }

        private void UpdateDynamics(float t)
        {
            bool loading = _phase == Phase.Loading;
            float mood = !loading || StartupTracker.Current >= (int)StepId.Connect ? 1f : 0f;
            _mood = Mathf.MoveTowards(_mood, mood, _frameDt * 1.2f);
            _alert = Mathf.MoveTowards(_alert, loading && _slow ? 1f : 0f, _frameDt * 2f);
            _beat = Mathf.Exp(-(t - _beatAt) * 5f);
            _spinScale = 1f + 0.8f * _shownFraction + 1.2f * _alert;
        }

        private void AnimateBackdrop(float t)
        {
            Theme theme = _variant.Theme;
            Texture texture = _grid.texture;
            Vector2 drift = BackdropDrift(_variant.Backdrop) * (1f + 0.5f * _shownFraction);
            _backdropOffset += drift * _frameDt;
            // Whole texels only: the patterns are point-sampled one texel per pixel, and a
            // fractional offset made their thin lines shimmer (worst on the pinstripes).
            _grid.uvRect = new Rect(
                Mathf.Round(_backdropOffset.x) / texture.width, Mathf.Round(_backdropOffset.y) / texture.height,
                _laidOutSize.x / texture.width, _laidOutSize.y / texture.height);
            _grid.canvasRenderer.SetAlpha(Mathf.Lerp(0.45f, 1f, _mood));

            if (theme.Scanlines)
            {
                // A faint bright band rolls down the screen every four seconds.
                float sweep = (t / 4f) % 1f;
                _band.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(_laidOutSize.y * 0.6f, -_laidOutSize.y * 0.6f, sweep));
            }
            _glow.canvasRenderer.SetAlpha((0.75f + 0.1f * Mathf.Sin(t * 0.8f)) * Mathf.Lerp(0.7f, 1f, _mood));
        }

        private void AnimateFrame(float t, float alpha)
        {
            for (int i = 0; i < _partCount; i++)
            {
                _partAngle[i] += _partSpin[i] * _frameDt * _spinScale;
                float z = i == _dialPointer ? -360f * _shownFraction : _partAngle[i] + (i < 2 ? RingKick(t, i) : 0f);
                _frameParts[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, z);
                float a = alpha;
                if (i == _recDot)
                {
                    a *= (t % 1f) < 0.6f ? 1f : 0.15f;
                    _recLabel.canvasRenderer.SetAlpha(alpha);
                }
                _frameParts[i].canvasRenderer.SetAlpha(a);
            }
        }

        /// <summary>How the screen comes up, one way per unit.</summary>
        private void AnimateIntro()
        {
            IntroKind kind = _variant.Intro;
            float clock = _introClock;
            float length = IntroLength;
            bool done = clock >= length + 0.1f;
            bool covers = !done && kind != IntroKind.Fade && kind != IntroKind.Sheen;
            SetActive(_shutterTop.gameObject, covers);
            SetActive(_shutterBottom.gameObject, covers && kind != IntroKind.Wipe);
            SetActive(_powerLine.gameObject, covers);
            SetActive(_introTitle.gameObject, covers && kind == IntroKind.TitleCard);
            SetActive(_introSub.gameObject, covers && kind == IntroKind.TitleCard);
            if (!covers)
            {
                return;
            }

            float w = _laidOutSize.x;
            float h = _laidOutSize.y;
            switch (kind)
            {
                case IntroKind.Crt:
                {
                    // A CRT switching on: a line flicks across the middle, the picture opens from it.
                    float line = Mathf.Clamp01(clock / 0.18f);
                    float lineEase = 1f - Mathf.Pow(1f - line, 3f);
                    float open = Mathf.Clamp01((clock - 0.2f) / 0.35f);
                    float openEase = open * open * (3f - 2f * open);
                    PlaceShutters(Mathf.Round(h * 0.5f * openEase), w, h, 1f);
                    float thickness = Mathf.Max(2f, Mathf.Round(3f * _u));
                    Place(_powerLine.rectTransform, 0f, 0f, w * lineEase, thickness);
                    _powerLine.canvasRenderer.SetAlpha(1f - openEase);
                    break;
                }
                case IntroKind.TitleCard:
                {
                    // The company name on a plain card, then the card fades off the screen.
                    float textIn = Mathf.Clamp01(clock / 0.25f);
                    float textOut = 1f - Mathf.Clamp01((clock - 0.8f) / 0.2f);
                    float cover = 1f - Mathf.Clamp01((clock - 0.9f) / 0.35f);
                    PlaceShutters(0f, w, h, cover);
                    _introTitle.canvasRenderer.SetAlpha(textIn * textOut);
                    _introSub.canvasRenderer.SetAlpha(Mathf.Clamp01((clock - 0.12f) / 0.25f) * textOut);
                    float rule = 1f - Mathf.Pow(1f - Mathf.Clamp01(clock / 0.6f), 3f);
                    Place(_powerLine.rectTransform, 0f, -14f * _u, 360f * _u * rule, Mathf.Max(1f, Mathf.Round(2f * _u)));
                    _powerLine.canvasRenderer.SetAlpha(textOut);
                    break;
                }
                case IntroKind.Wipe:
                {
                    // A panel in the unit's colour slides off to the right, an accent edge leading.
                    float p = Mathf.Clamp01(clock / length);
                    float ease = p * p * (3f - 2f * p);
                    float x = w * ease;
                    Place(_shutterTop, x, 0f, w + 4f, h + 4f);
                    _shutterTop.GetComponent<Image>().canvasRenderer.SetAlpha(1f);
                    Place(_powerLine.rectTransform, x - w * 0.5f, 0f, Mathf.Max(3f, Mathf.Round(6f * _u)), h + 4f);
                    _powerLine.canvasRenderer.SetAlpha(1f);
                    break;
                }
                case IntroKind.Doors:
                {
                    // Lift doors: closed a moment, a light at the seam, then they slide apart.
                    float p = Mathf.Clamp01((clock - 0.2f) / (length - 0.2f));
                    float ease = p * p * (3f - 2f * p);
                    float half = w * 0.5f;
                    float open = Mathf.Round(half * ease);
                    Place(_shutterTop, -half * 0.5f - open, 0f, half + 2f, h + 4f);
                    Place(_shutterBottom, half * 0.5f + open, 0f, half + 2f, h + 4f);
                    _shutterTop.GetComponent<Image>().canvasRenderer.SetAlpha(1f);
                    _shutterBottom.GetComponent<Image>().canvasRenderer.SetAlpha(1f);
                    float seam = Mathf.Clamp01(clock / 0.2f) * (1f - ease);
                    Place(_powerLine.rectTransform, 0f, 0f, Mathf.Max(2f, Mathf.Round(2f * _u)), h * Mathf.Clamp01(clock / 0.2f));
                    _powerLine.canvasRenderer.SetAlpha(seam);
                    break;
                }
            }
        }

        /// <summary>Two covers, opened <paramref name="gap"/> each way from the middle.</summary>
        private void PlaceShutters(float gap, float w, float h, float alpha)
        {
            float shutter = h * 0.5f - gap + 2f;
            Place(_shutterTop, 0f, gap + shutter * 0.5f - 1f, w + 4f, shutter);
            Place(_shutterBottom, 0f, -(gap + shutter * 0.5f - 1f), w + 4f, shutter);
            _shutterTop.GetComponent<Image>().canvasRenderer.SetAlpha(alpha);
            _shutterBottom.GetComponent<Image>().canvasRenderer.SetAlpha(alpha);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        /// <summary>A diagonal shine across the screen: Executive's intro and ending.</summary>
        private void AnimateSheen(float t)
        {
            // The intro's runs on the intro clock, the ending's on the animation clock.
            float age = -1f;
            if (_sheenAt >= 0f)
            {
                age = t - _sheenAt;
            }
            else if (_variant.Intro == IntroKind.Sheen && _phase == Phase.Loading)
            {
                age = _introClock - IntroLength;
            }
            bool on = age >= 0f && age < SheenTime;
            SetActive(_sheen.gameObject, on);
            if (!on)
            {
                return;
            }
            float p = age / SheenTime;
            float x = Mathf.Lerp(-0.75f, 0.75f, p * p * (3f - 2f * p)) * _laidOutSize.x;
            _sheen.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x), 0f);
        }

        private void AnimateTicker()
        {
            if (_tickerLoop <= 0f)
            {
                return;
            }
            _tickerOffset = (_tickerOffset + _frameDt * TickerSpeed * _u * (1f + 0.3f * _shownFraction)) % _tickerLoop;
            _ticker.rectTransform.anchoredPosition = new Vector2(-Mathf.Round(_tickerOffset), 0f);
        }

        /// <summary>
        /// The launch's notices, one at a time: each slides in from the right edge once the load
        /// passes its point, sits a few seconds, and leaves. Orders carry the accent colour,
        /// priority intel the warning colour, the other side's radio intercepts plain text, the odd
        /// unsigned note a dim one.
        /// </summary>
        private void AnimateMemo(float t)
        {
            Notice[] notices = _variant.Notices;
            if (_memoShownAt < 0f && notices != null && _noticeIndex < notices.Length && _phase == Phase.Loading
                && _memoOff > 0f && _shownFraction >= notices[_noticeIndex].At && _introClock > IntroLength + 1f)
            {
                Notice notice = notices[_noticeIndex++];
                _memoShownAt = t;
                Color32 tone = notice.Kind == NoticeKind.Intel ? Warn
                    : notice.Kind == NoticeKind.Note ? Dim
                    : notice.Kind == NoticeKind.Intercept ? Theme.WithAlpha(Text, 220)
                    : Accent;
                string kind = notice.Kind == NoticeKind.Intel ? "PRIORITY INTEL"
                    : notice.Kind == NoticeKind.Note ? "MESSAGE"
                    : notice.Kind == NoticeKind.Intercept ? "INTERCEPT"
                    : "ORDER";
                _memoTitle.text = kind + "  //  " + notice.From;
                _memoTitle.color = tone;
                _memoStripe.color = tone;
                _memoBody.text = Lore.Fill(notice.Body, _random)
                    + (string.IsNullOrEmpty(notice.Signature) ? string.Empty : "\n<alpha=#80><size=80%>" + notice.Signature + "</size>");
                _memo.gameObject.SetActive(true);
                FitMemo();
            }
            if (_memoShownAt < 0f)
            {
                return;
            }
            float age = t - _memoShownAt;
            float into = Mathf.Clamp01(age / MemoIn);
            float out1 = Mathf.Clamp01((age - MemoHold) / MemoIn);
            float slide = (1f - Mathf.Pow(1f - into, 3f)) * (1f - out1 * out1);
            _memo.anchoredPosition = new Vector2(Mathf.Round(_memoX + _memoOff * (1f - slide)), _memo.anchoredPosition.y);
            if (age >= MemoHold + MemoIn)
            {
                _memoShownAt = -1f;
                _memo.gameObject.SetActive(false);
            }
        }

        /// <summary>How the screen leaves, one way per unit. <paramref name="fade"/> runs 0 to 1.</summary>
        private void AnimateOutro(float fade)
        {
            float ease = fade * fade * (3f - 2f * fade);
            Vector2 offset = Vector2.zero;
            float emblemScale = 1f;
            float everything = 1f - ease;
            float rest = 1f;
            OutroKind outro = _variant.Outro;
            switch (outro)
            {
                case OutroKind.Lift:
                    // The picture slides up and away, like a security shutter going up.
                    offset = new Vector2(0f, Mathf.Round(ease * ease * _laidOutSize.y * 0.45f));
                    break;
                case OutroKind.Slide:
                    // Off to the left, like the next slide of a presentation.
                    offset = new Vector2(-Mathf.Round(ease * ease * _laidOutSize.x * 0.35f), 0f);
                    break;
                case OutroKind.Sting:
                {
                    // Everything but the logo and wordmark fades first; they hold a beat, then go.
                    float first = Mathf.Clamp01(fade / 0.45f);
                    rest = 1f - first * first * (3f - 2f * first);
                    float second = Mathf.Clamp01((fade - 0.6f) / 0.4f);
                    everything = 1f - second * second * (3f - 2f * second);
                    emblemScale = 1f + 0.05f * ease;
                    break;
                }
                case OutroKind.Flash:
                {
                    float flash = fade <= 0f ? 0f : fade < 0.12f ? fade / 0.12f * 0.45f : 0.45f * (1f - (fade - 0.12f) / 0.88f);
                    SetActive(_flash.gameObject, flash > 0.01f);
                    _flash.canvasRenderer.SetAlpha(flash);
                    emblemScale = 1f + 0.12f * ease;
                    break;
                }
                default:
                    emblemScale = 1f + 0.08f * ease;
                    break;
            }
            _content.anchoredPosition = offset;
            _brand.anchoredPosition = offset;
            _contentGroup.alpha = rest;
            if (_phase != Phase.Loading)
            {
                _group.alpha = everything;
                _emblem.localScale = Vector3.one * (_brandScale * emblemScale);
            }
        }

        /// <summary>How long the fade-out takes; the sting holds on the logo, so it runs longer.</summary>
        private float OutroTime => _variant.Outro == OutroKind.Sting ? 1f : FadeTime;
    }
}
