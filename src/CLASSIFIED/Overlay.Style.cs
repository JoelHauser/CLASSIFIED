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
        private RawImage _sheen;
        private RawImage _alertGlow;

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
                    case IntroKind.Stamp: return 1.25f;
                    case IntroKind.Boot: return 1.7f;
                    default: return 0.35f;
                }
            }
        }

        private void BuildBackdropExtras(Transform c)
        {
            _panel = MakeImage("Panel", c, Ink);
            _panelEdge = MakeImage("PanelEdge", c, Accent);
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
            ApplyBackdrop();
            _panel.color = Theme.WithAlpha(Color32.Lerp(Ink, Primary, 0.1f), 215);
            _panelEdge.color = Theme.WithAlpha(Accent, 90);
            _sheen.color = Theme.WithAlpha(Accent, 45);
            _recLabel.color = Warn;

            _solidTrack.color = Faint;
            _solidFill.color = Accent;
            _ticker.color = Dim;
            _ticker.text = Cyr(_variant.Ticker + _variant.Ticker);
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
            _introSub.text = Cyr(Lang.Bear(theme.TitleCard));
            ApplyIntroText();
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
            LayoutIntros(size);
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

        private void AnimateFrame(float t, float alpha)
        {
            for (int i = 0; i < _partCount; i++)
            {
                _partAngle[i] += _partSpin[i] * _frameDt * _spinScale;
                float z = i == _dialPointer ? PointerAngle() : _partAngle[i] + (i < 2 ? RingKick(t, i) : 0f);
                _frameParts[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, z);
                float a = alpha;
                if (i == _recDot)
                {
                    a *= (t % 1f) < 0.6f ? 1f : 0.15f;
                    _recLabel.canvasRenderer.SetAlpha(alpha);
                }
                _frameParts[i].canvasRenderer.SetAlpha(a);
            }
            AnimateLock(t);
            foreach (TMPro.TextMeshProUGUI label in _frameLabels)
            {
                label.canvasRenderer.SetAlpha(alpha);
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
            SetActive(_stampText.gameObject, covers && kind == IntroKind.Stamp);
            SetActive(_bootText.gameObject, covers && kind == IntroKind.Boot);
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
                case IntroKind.Stamp:
                    AnimateStamp(clock, w, h);
                    break;
                case IntroKind.Boot:
                    AnimateBoot(clock, w, h);
                    break;
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
                string kind = notice.Kind == NoticeKind.Intel ? Words("СРОЧНАЯ РАЗВЕДСВОДКА", "PRIORITY INTEL")
                    : notice.Kind == NoticeKind.Note ? Words("СООБЩЕНИЕ", "MESSAGE")
                    : notice.Kind == NoticeKind.Intercept ? Words("РАДИОПЕРЕХВАТ", "INTERCEPT")
                    : Words("ПРИКАЗ", "ORDER");
                _memoTitle.text = kind + "  //  " + Cyr(notice.From);
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
