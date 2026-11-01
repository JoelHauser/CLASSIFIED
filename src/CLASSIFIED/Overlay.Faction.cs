using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// What makes a BEAR terminal look like BEAR's and a USEC one like USEC's, rather than either
    /// like TerraGroup's: the picture behind it, the frame round the insignia, the progress bar
    /// and the chrome round the screen's edge.
    ///
    /// BEAR is Soviet hardware: a staff map, aerial film or typed orders behind; a PSO-1 scope's
    /// reticle, an instrument gauge or a stamped, riveted plate round the insignia; a row of panel
    /// lamps or a punched tape for the bar; a riveted border with a red secrecy stamp across it.
    /// USEC is a modern tactical display: an MGRS map, satellite imagery or an operation order
    /// behind; a holographic HUD, a mil-dot scope or a target lock; a ruler or clean segments; a
    /// chamfered frame under classification banners.
    /// </summary>
    internal partial class Overlay
    {
        // ---- backdrops ----------------------------------------------------------------------

        /// <summary>The faction pictures (Resources/backdrop_*.png, made by scripts/make_backdrops.py), loaded as shown, let go on hide.</summary>
        private static class Backdrops
        {
            private static readonly Texture2D[] Loaded = new Texture2D[6];

            internal static Texture2D Get(Faction faction, BackdropKind kind)
            {
                if (kind == BackdropKind.Plain || kind == BackdropKind.Random || faction == Faction.Unknown)
                {
                    return null;
                }
                int slot = (faction == Faction.Bear ? 0 : 3) + (kind == BackdropKind.Map ? 0 : kind == BackdropKind.Imagery ? 1 : 2);
                if (Loaded[slot] != null)
                {
                    return Loaded[slot];
                }
                string resource = $"CLASSIFIED.backdrop_{(faction == Faction.Bear ? "bear" : "usec")}_{kind.ToString().ToLowerInvariant()}.png";
                try
                {
                    using (Stream stream = typeof(Overlay).Assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null)
                        {
                            ClassifiedPlugin.Log.LogWarning("Backdrop: " + resource + " is missing from the DLL.");
                            return null;
                        }
                        var bytes = new byte[stream.Length];
                        int read = 0;
                        while (read < bytes.Length)
                        {
                            int n = stream.Read(bytes, read, bytes.Length - read);
                            if (n <= 0)
                            {
                                break;
                            }
                            read += n;
                        }
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(texture, bytes, false))
                        {
                            UnityEngine.Object.Destroy(texture);
                            return null;
                        }
                        // A quarter of the memory, and these are only ever faint.
                        texture.Compress(true);
                        texture.Apply(false, true);
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.filterMode = FilterMode.Bilinear;
                        texture.hideFlags = HideFlags.HideAndDontSave;
                        Loaded[slot] = texture;
                    }
                }
                catch (Exception e)
                {
                    ClassifiedPlugin.Log.LogWarning("Backdrop: could not load " + resource + ": " + e.Message);
                    return null;
                }
                return Loaded[slot];
            }

            internal static void Release()
            {
                for (int i = 0; i < Loaded.Length; i++)
                {
                    if (Loaded[i] != null)
                    {
                        UnityEngine.Object.Destroy(Loaded[i]);
                        Loaded[i] = null;
                    }
                }
            }
        }

        private Vector2 _backdropPan;

        private void ApplyBackdrop()
        {
            Texture2D picture = Backdrops.Get(_variant.Faction, _variant.Backdrop);
            _grid.texture = picture;
            _grid.gameObject.SetActive(picture != null);
            // Maps are line work and take more strength; the imagery and the papers are full of tone.
            byte strength = _variant.Backdrop == BackdropKind.Map ? (byte)58 : _variant.Backdrop == BackdropKind.Imagery ? (byte)34 : (byte)40;
            _grid.color = Theme.WithAlpha(Accent, strength);
            _backdropPan = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f) * 0.03f;
        }

        /// <summary>The picture fills the screen's height, centred, drifting a little and slowly closing in over the load.</summary>
        private void AnimateBackdrop(float t)
        {
            Theme theme = _variant.Theme;
            Texture texture = _grid.texture;
            if (texture != null && _laidOutSize.y > 0f)
            {
                float zoom = 1f + 0.05f * _shownFraction + 0.01f * Mathf.Sin(t * 0.05f);
                float h = 1f / zoom;
                float w = (_laidOutSize.x / _laidOutSize.y) / (texture.width / (float)texture.height) * h;
                Vector2 drift = _backdropPan + new Vector2(Mathf.Sin(t * 0.021f), Mathf.Cos(t * 0.017f)) * 0.006f;
                _grid.uvRect = new Rect(0.5f - w * 0.5f + drift.x * w, 0.5f - h * 0.5f + drift.y * h, w, h);
                _grid.canvasRenderer.SetAlpha(Mathf.Lerp(0.55f, 1f, _mood));
            }

            if (theme.Scanlines)
            {
                // A faint bright band rolls down the screen every four seconds.
                float sweep = (t / 4f) % 1f;
                _band.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(_laidOutSize.y * 0.6f, -_laidOutSize.y * 0.6f, sweep));
            }
            _glow.canvasRenderer.SetAlpha((0.75f + 0.1f * Mathf.Sin(t * 0.8f)) * Mathf.Lerp(0.7f, 1f, _mood));
        }

        // ---- the parts the faction look adds ------------------------------------------------

        private readonly TextMeshProUGUI[] _frameLabels = new TextMeshProUGUI[10];
        private int _frameLabelCount;
        private float _pointerStart;
        private float _pointerSweep = 360f;
        private int _lockPart = -1;

        private Shape _barDeco;
        private TextMeshProUGUI[] _lampLabels;

        private Image[] _banners;
        private TextMeshProUGUI[] _bannerTexts;
        private TextMeshProUGUI _stamp;
        private Shape _stampBox;

        private void BuildFaction(Transform content, Transform readout)
        {
            for (int i = 0; i < _frameLabels.Length; i++)
            {
                _frameLabels[i] = MakeText("FrameLabel" + i, _emblem, TextAlignmentOptions.Center, Accent);
                _frameLabels[i].gameObject.SetActive(false);
            }
            _barDeco = Shape.Create("BarDeco", readout);
            _lampLabels = new TextMeshProUGUI[StartupTracker.StepCount];
            for (int i = 0; i < _lampLabels.Length; i++)
            {
                _lampLabels[i] = MakeText("Lamp" + i, readout, TextAlignmentOptions.Center, Dim);
                _lampLabels[i].text = (i + 1).ToString("00");
            }
            _banners = new Image[2];
            _bannerTexts = new TextMeshProUGUI[2];
            for (int i = 0; i < 2; i++)
            {
                _banners[i] = MakeImage("Banner" + i, content, Warn);
                _bannerTexts[i] = MakeText("BannerText" + i, _banners[i].transform, TextAlignmentOptions.Center, Ink);
                Stretch(_bannerTexts[i].rectTransform);
            }
            _stampBox = Shape.Create("StampBox", content);
            _stamp = MakeText("Stamp", _stampBox.transform, TextAlignmentOptions.Center, Warn);
        }

        // ---- chrome -------------------------------------------------------------------------

        /// <summary>The screen's edge: BEAR's riveted border and stamp, USEC's chamfered frame and banners, plain brackets before either.</summary>
        private void DrawChrome(Vector2 size, float inset, float arm, float width)
        {
            float u = _u;
            float hx = Mathf.Round(size.x * 0.5f) - inset;
            float hy = Mathf.Round(size.y * 0.5f) - inset;
            Faction faction = _variant.Faction;
            _corners.Clear();
            foreach (Image banner in _banners)
            {
                banner.gameObject.SetActive(false);
            }
            _stampBox.gameObject.SetActive(false);

            if (faction == Faction.Bear)
            {
                // Two borders, the outer heavier, with rivets along them.
                Color32 edge = Theme.WithAlpha(Dim, 150);
                Rectangle(_corners, hx, hy, width * 1.5f, edge);
                Rectangle(_corners, hx - 9f * u, hy - 9f * u, Mathf.Max(1f, u), Theme.WithAlpha(Dim, 60));
                float spacing = 240f * u;
                for (int side = 0; side < 4; side++)
                {
                    bool horizontal = side < 2;
                    float length = horizontal ? hx : hy;
                    int count = Mathf.Max(1, Mathf.RoundToInt(length * 2f / spacing));
                    for (int k = 0; k <= count; k++)
                    {
                        float along = -length + 2f * length * k / count;
                        Vector2 at = horizontal ? new Vector2(along, side == 0 ? hy - 4.5f * u : -hy + 4.5f * u)
                            : new Vector2(side == 2 ? hx - 4.5f * u : -hx + 4.5f * u, along);
                        Rivet(_corners, at, 4f * u);
                    }
                }
                // The secrecy stamp, struck across the top border, a little crooked.
                _stampBox.gameObject.SetActive(true);
                Color32 ink = new Color32(214, 48, 36, 150);
                _stampBox.rectTransform.anchoredPosition = new Vector2(Mathf.Round(hx * 0.42f), hy);
                _stampBox.rectTransform.localEulerAngles = new Vector3(0f, 0f, -4f);
                _stampBox.Clear();
                float sw = 300f * u;
                float sh = 38f * u;
                Rectangle(_stampBox, sw * 0.5f, sh * 0.5f, 2.5f * u, ink);
                Rectangle(_stampBox, sw * 0.5f - 5f * u, sh * 0.5f - 5f * u, Mathf.Max(1f, u), Theme.WithAlpha(ink, 100));
                _stampBox.Commit();
                _stamp.text = Words("СОВЕРШЕННО СЕКРЕТНО", "TOP SECRET");
                _stamp.color = ink;
                SetText(_stamp, 0f, 0f, sw, sh, 17f * u, 8f);
                _stamp.fontStyle = FontStyles.Bold;
            }
            else if (faction == Faction.Usec)
            {
                // A frame with its corners cut, a scale down each side.
                Color32 edge = Theme.WithAlpha(Dim, 140);
                float cut = 30f * u;
                Vector2[] outline =
                {
                    new Vector2(-hx + cut, hy), new Vector2(hx - cut, hy), new Vector2(hx, hy - cut), new Vector2(hx, -hy + cut),
                    new Vector2(hx - cut, -hy), new Vector2(-hx + cut, -hy), new Vector2(-hx, -hy + cut), new Vector2(-hx, hy - cut)
                };
                for (int i = 0; i < outline.Length; i++)
                {
                    _corners.Line(outline[i], outline[(i + 1) % outline.Length], Mathf.Max(1f, width * 0.6f), edge);
                }
                // Heavier on the cut corners.
                for (int i = 1; i < outline.Length; i += 2)
                {
                    _corners.Line(outline[i], outline[(i + 1) % outline.Length], width * 1.5f, Theme.WithAlpha(Accent, 170));
                }
                for (int k = -6; k <= 6; k++)
                {
                    float y = k * 22f * u;
                    float len = (k % 3 == 0 ? 12f : 6f) * u;
                    _corners.Line(new Vector2(-hx, y), new Vector2(-hx + len, y), Mathf.Max(1f, u), edge);
                    _corners.Line(new Vector2(hx, y), new Vector2(hx - len, y), Mathf.Max(1f, u), edge);
                }
                // Classification banners on the top and bottom edges.
                for (int i = 0; i < 2; i++)
                {
                    Image banner = _banners[i];
                    banner.gameObject.SetActive(true);
                    banner.color = new Color32(186, 22, 38, 215);
                    Place(banner.rectTransform, 0f, i == 0 ? hy : -hy, 330f * u, 20f * u);
                    _bannerTexts[i].text = "SECRET//REL TO USEC";
                    _bannerTexts[i].color = new Color32(255, 240, 240, 255);
                    _bannerTexts[i].fontSize = Mathf.Round(12f * u);
                    _bannerTexts[i].characterSpacing = 10f;
                }
            }
            else
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        var corner = new Vector2(sx * hx, sy * hy);
                        var half = new Vector2(sx * width * 0.5f, sy * width * 0.5f);
                        _corners.Line(corner + new Vector2(half.x, 0f), corner - new Vector2(sx * arm, 0f), width, Dim);
                        _corners.Line(corner - new Vector2(0f, sy * width * 0.5f), corner - new Vector2(0f, sy * arm), width, Dim);
                    }
                }
            }
            _corners.Commit();
        }

        private static void Rectangle(Shape s, float hx, float hy, float width, Color32 color)
        {
            float h = width * 0.5f;
            s.Line(new Vector2(-hx - h, hy), new Vector2(hx + h, hy), width, color);
            s.Line(new Vector2(-hx - h, -hy), new Vector2(hx + h, -hy), width, color);
            s.Line(new Vector2(-hx, hy), new Vector2(-hx, -hy), width, color);
            s.Line(new Vector2(hx, hy), new Vector2(hx, -hy), width, color);
        }

        /// <summary>A rivet: a dome with its highlight up and to the left.</summary>
        private void Rivet(Shape s, Vector2 at, float r)
        {
            s.Disc(at, r, Theme.WithAlpha(Color32.Lerp(Ink, Dim, 0.55f), 230));
            s.Disc(at + new Vector2(-r * 0.3f, r * 0.3f), r * 0.45f, Theme.WithAlpha(Text, 70));
        }

        // ---- frames -------------------------------------------------------------------------

        private TextMeshProUGUI FrameLabel(string text, Vector2 at, float size, Color32 color)
        {
            if (_frameLabelCount >= _frameLabels.Length)
            {
                return null;
            }
            TextMeshProUGUI label = _frameLabels[_frameLabelCount++];
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            SetText(label, at.x, at.y, 120f * _u, size * 1.6f, size, 2f);
            return label;
        }

        /// <summary>The frame round the insignia, in the faction's own hardware.</summary>
        private void LayoutFrame(float u)
        {
            foreach (Shape part in _frameParts)
            {
                part.Clear();
                part.Commit();
                part.gameObject.SetActive(false);
                part.rectTransform.localScale = Vector3.one;
            }
            foreach (TextMeshProUGUI label in _frameLabels)
            {
                label.gameObject.SetActive(false);
            }
            _frameLabelCount = 0;
            _partCount = 0;
            _dialPointer = -1;
            _recDot = -1;
            _lockPart = -1;
            _pointerStart = 0f;
            _pointerSweep = 360f;
            _recLabel.gameObject.SetActive(false);
            Color32 accent = Accent;
            float thin = Mathf.Max(1f, 1.2f * u);

            switch (_variant.Frame)
            {
                case FrameKind.Reticle:
                {
                    // A PSO-1's reticle: the aiming chevron over the logo, the bullet-drop chevrons
                    // under it, the windage scale either side, the rangefinder's curve below left.
                    Shape p = Part(0f);
                    p.Arc(116f * u, Mathf.Max(1.5f, 2f * u), 0f, 360f, Theme.WithAlpha(accent, 120));
                    Chevron(p, new Vector2(0f, 84f * u), 11f * u, Mathf.Max(1.5f, 2.2f * u), Theme.WithAlpha(Text, 230));
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -(80f + i * 11f) * u;
                        Chevron(p, new Vector2(0f, y), (7f - i * 1.5f) * u, thin, Theme.WithAlpha(accent, 200));
                    }
                    for (int s = -1; s <= 1; s += 2)
                    {
                        p.Line(new Vector2(s * 78f * u, 0f), new Vector2(s * 112f * u, 0f), thin, Theme.WithAlpha(accent, 170));
                        for (int k = 0; k <= 10; k++)
                        {
                            float x = s * (78f + k * 3.4f) * u;
                            float len = (k % 5 == 0 ? 7f : 3.5f) * u;
                            p.Line(new Vector2(x, 0f), new Vector2(x, len), thin, Theme.WithAlpha(accent, 170));
                        }
                    }
                    FrameLabel("10", new Vector2(-95f * u, 13f * u), 9f * u, Theme.WithAlpha(accent, 200));
                    FrameLabel("10", new Vector2(95f * u, 13f * u), 9f * u, Theme.WithAlpha(accent, 200));
                    // The rangefinder: a curve over a baseline, marked for a man's height.
                    Vector2 origin = new Vector2(-104f * u, -52f * u);
                    p.Line(origin, origin + new Vector2(46f * u, 0f), thin, Theme.WithAlpha(accent, 190));
                    Vector2 last = origin + new Vector2(0f, 22f * u);
                    for (int k = 1; k <= 12; k++)
                    {
                        float f = k / 12f;
                        Vector2 next = origin + new Vector2(f * 46f * u, (22f - 18f * Mathf.Sqrt(f)) * u);
                        p.Line(last, next, thin, Theme.WithAlpha(accent, 190));
                        last = next;
                    }
                    FrameLabel("2  4  6  8  10", origin + new Vector2(23f * u, -9f * u), 7f * u, Theme.WithAlpha(accent, 170));
                    // A ring of fine marks turning slowly outside, as the elevation drum.
                    p = Part(_variant.DashSpin * 0.25f);
                    for (int i = 0; i < 60; i++)
                    {
                        p.Line(Polar(120f * u, i * 6f), Polar((i % 5 == 0 ? 128f : 124f) * u, i * 6f), thin, Theme.WithAlpha(accent, (byte)(i % 5 == 0 ? 140 : 70)));
                    }
                    break;
                }
                case FrameKind.Gauge:
                {
                    // A Soviet panel instrument: a heavy bezel with rivets, a scale of 0 to 10
                    // over three quarters of the dial, and a needle on the load.
                    Shape p = Part(0f);
                    p.Arc(122f * u, 6f * u, 0f, 360f, Theme.WithAlpha(Color32.Lerp(Ink, Dim, 0.4f), 255));
                    p.Arc(118f * u, Mathf.Max(1f, u), 0f, 360f, Theme.WithAlpha(Dim, 150));
                    p.Arc(126f * u, Mathf.Max(1f, u), 0f, 360f, Theme.WithAlpha(Dim, 120));
                    for (int i = 0; i < 4; i++)
                    {
                        Rivet(p, Polar(122f * u, 45f + i * 90f), 3.2f * u);
                    }
                    for (int i = 0; i <= 50; i++)
                    {
                        float angle = -135f + 270f * i / 50f;
                        bool major = i % 5 == 0;
                        p.Line(Polar((major ? 98f : 103f) * u, angle), Polar(110f * u, angle), Mathf.Max(1f, (major ? 2.2f : 1.1f) * u),
                            Theme.WithAlpha(i >= 40 ? Warn : accent, (byte)(major ? 220 : 120)));
                    }
                    p.Arc(110f * u, 3f * u, 81f, 54f, Theme.WithAlpha(Warn, 90));
                    for (int i = 0; i <= 10; i += 2)
                    {
                        FrameLabel(i.ToString(), Polar(88f * u, -135f + 27f * i), 9f * u, Theme.WithAlpha(Text, 200));
                    }
                    FrameLabel(Words("ЗАГРУЗКА", "LOAD"), new Vector2(0f, -100f * u), 8f * u, Theme.WithAlpha(Dim, 220));
                    p = Part(0f);
                    _dialPointer = _partCount - 1;
                    _pointerStart = 135f;
                    _pointerSweep = 270f;
                    p.Tri(new Vector2(0f, 112f * u), new Vector2(-3.5f * u, 92f * u), new Vector2(3.5f * u, 92f * u), Text);
                    break;
                }
                case FrameKind.Plate:
                {
                    // A stamped steel plate: an octagon with rivets at its corners, stencilled
                    // lettering top and bottom, the insignia mounted in the middle.
                    Shape p = Part(0f);
                    float r = 124f * u;
                    Color32 steel = Theme.WithAlpha(Color32.Lerp(Ink, Dim, 0.3f), 180);
                    var corners = new Vector2[8];
                    for (int i = 0; i < 8; i++)
                    {
                        corners[i] = Polar(r, 22.5f + i * 45f);
                    }
                    p.Polygon(corners, Theme.WithAlpha(Color32.Lerp(Ink, Primary, 0.07f), 200));
                    for (int i = 0; i < 8; i++)
                    {
                        p.Line(corners[i], corners[(i + 1) % 8], 3f * u, steel);
                        p.Line(corners[i] * 0.92f, corners[(i + 1) % 8] * 0.92f, thin, Theme.WithAlpha(Dim, 90));
                        Rivet(p, corners[i] * 0.96f, 3.4f * u);
                    }
                    FrameLabel(Words("ЧВК «BEAR»", "BEAR PMC"), new Vector2(0f, 98f * u), 10f * u, Theme.WithAlpha(Text, 200));
                    FrameLabel(Words("ИЗД. ", "MOD. ") + $"{(_variant.ChartSeed & 0xFF):000}-{(_variant.ChartSeed >> 8) & 0xFF:00}", new Vector2(0f, -100f * u), 8f * u, Theme.WithAlpha(Dim, 220));
                    break;
                }
                case FrameKind.Hud:
                {
                    // A holographic sight's display: a heading tape turning round the top, a fixed
                    // caret on it, the pitch ladder either side, a bracket on each corner.
                    Shape p = Part(_variant.ArcSpin * 0.4f);
                    for (int i = 0; i < 72; i++)
                    {
                        bool major = i % 6 == 0;
                        p.Line(Polar(114f * u, i * 5f), Polar((major ? 124f : 119f) * u, i * 5f), Mathf.Max(1f, (major ? 1.8f : 1f) * u), Theme.WithAlpha(accent, (byte)(major ? 200 : 90)));
                    }
                    p = Part(0f);
                    p.Tri(new Vector2(0f, 110f * u), new Vector2(-6f * u, 102f * u), new Vector2(6f * u, 102f * u), Text);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        for (int k = -2; k <= 2; k++)
                        {
                            if (k == 0)
                            {
                                continue;
                            }
                            float y = k * 26f * u;
                            float x0 = s * 80f * u;
                            float x1 = s * 100f * u;
                            if (k < 0)
                            {
                                // Below the horizon, dashed.
                                p.Line(new Vector2(x0, y), new Vector2(Mathf.Lerp(x0, x1, 0.4f), y), thin, Theme.WithAlpha(accent, 160));
                                p.Line(new Vector2(Mathf.Lerp(x0, x1, 0.6f), y), new Vector2(x1, y), thin, Theme.WithAlpha(accent, 160));
                            }
                            else
                            {
                                p.Line(new Vector2(x0, y), new Vector2(x1, y), thin, Theme.WithAlpha(accent, 160));
                            }
                            p.Line(new Vector2(x0, y), new Vector2(x0, y - Mathf.Sign(k) * 5f * u), thin, Theme.WithAlpha(accent, 160));
                        }
                        p.Line(new Vector2(s * 78f * u, 0f), new Vector2(s * 108f * u, 0f), Mathf.Max(1.5f, 2f * u), Theme.WithAlpha(Text, 200));
                    }
                    FrameLabel("10", new Vector2(-114f * u, 26f * u), 8f * u, Theme.WithAlpha(accent, 180));
                    FrameLabel("10", new Vector2(114f * u, 26f * u), 8f * u, Theme.WithAlpha(accent, 180));
                    FrameLabel("HDG", new Vector2(0f, 134f * u), 8f * u, Theme.WithAlpha(Dim, 200));
                    break;
                }
                case FrameKind.Scope:
                {
                    // A mil-dot scope: the tube's ring, thick posts closing in, fine crosshairs
                    // stopping short of the logo, dots a mil apart along them.
                    Shape p = Part(0f);
                    p.Arc(118f * u, Mathf.Max(1.5f, 2f * u), 0f, 360f, Theme.WithAlpha(accent, 150));
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * 90f;
                        p.Line(Polar(118f * u, angle), Polar(100f * u, angle), 5f * u, Theme.WithAlpha(Text, 200));
                        p.Line(Polar(100f * u, angle), Polar(76f * u, angle), thin, Theme.WithAlpha(Text, 200));
                        for (int k = 0; k < 4; k++)
                        {
                            p.Disc(Polar((78f + k * 6.5f) * u, angle), 1.6f * u, Theme.WithAlpha(accent, 230));
                        }
                    }
                    p = Part(_variant.DashSpin * 0.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        p.Arc(126f * u, Mathf.Max(1f, u), i * 90f + 10f, 70f, Theme.WithAlpha(accent, 80));
                    }
                    break;
                }
                default:
                {
                    // A target lock: four brackets close in on the insignia as the load goes on,
                    // a small diamond at each side, the track readout over the corner.
                    Shape p = Part(0f);
                    _lockPart = _partCount - 1;
                    float s = 104f * u;
                    float arm = 22f * u;
                    float w = Mathf.Max(2f, 2.6f * u);
                    for (int sx = -1; sx <= 1; sx += 2)
                    {
                        for (int sy = -1; sy <= 1; sy += 2)
                        {
                            var corner = new Vector2(sx * s, sy * s);
                            p.Line(corner + new Vector2(sx * w * 0.5f, 0f), corner - new Vector2(sx * arm, 0f), w, Theme.WithAlpha(accent, 220));
                            p.Line(corner, corner - new Vector2(0f, sy * arm), w, Theme.WithAlpha(accent, 220));
                        }
                    }
                    p = Part(-_variant.ArcSpin * 0.3f);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 at = Polar(92f * u, i * 90f + 45f);
                        Vector2 out1 = at.normalized * 5f * u;
                        Vector2 side = new Vector2(-out1.y, out1.x);
                        p.Polygon(new[] { at + out1, at + side, at - out1, at - side }, Theme.WithAlpha(accent, 160));
                    }
                    p = Part(0f);
                    _recDot = _partCount - 1;
                    p.Disc(new Vector2(s - 4f * u, s + 16f * u), 3.5f * u, Warn);
                    _recLabel.gameObject.SetActive(true);
                    _recLabel.text = "TRK";
                    SetText(_recLabel, s - 40f * u, s + 16f * u, 60f * u, 16f * u, 11f * u, 6f);
                    break;
                }
            }

            for (int i = 0; i < _partCount; i++)
            {
                _frameParts[i].Commit();
            }
        }

        private static void Chevron(Shape p, Vector2 tip, float size, float width, Color32 color)
        {
            p.Line(tip, tip + new Vector2(-size, -size * 1.1f), width, color);
            p.Line(tip, tip + new Vector2(size, -size * 1.1f), width, color);
        }

        /// <summary>The pointer's angle, and the lock's brackets closing in. Called by AnimateFrame.</summary>
        private float PointerAngle() => _pointerStart - _pointerSweep * _shownFraction;

        private void AnimateLock(float t)
        {
            if (_lockPart < 0)
            {
                return;
            }
            // Wide to begin with, settling in as the load goes on, a beat on each step.
            float close = 1f - _shownFraction;
            float scale = 1f + 0.22f * close * close + 0.04f * _beat + 0.01f * Mathf.Sin(t * 7f) * (1f - _shownFraction);
            _frameParts[_lockPart].rectTransform.localScale = new Vector3(scale, scale, 1f);
            _recLabel.text = _shownFraction > 0.85f ? "LOCK" : "TRK";
        }

        // ---- intros ---------------------------------------------------------------------------

        private TextMeshProUGUI _stampText;
        private TextMeshProUGUI _bootText;

        /// <summary>Over the intro's covers: created after them.</summary>
        private void BuildIntros()
        {
            _stampText = MakeText("IntroStamp", transform, TextAlignmentOptions.Center, Warn);
            _stampText.fontStyle = FontStyles.Bold;
            _bootText = MakeText("IntroBoot", transform, TextAlignmentOptions.TopLeft, Accent);
            _bootText.enableWordWrapping = false;
            _stampText.gameObject.SetActive(false);
            _bootText.gameObject.SetActive(false);
        }

        private void ApplyIntroText()
        {
            string unitName = BearRussian ? Cyr(_variant.Theme.Native ?? string.Empty) : _variant.Theme.Division;
            _stampText.text = $"<size=40%>{Words("ЧВК «BEAR»  //  ЭКЗ. № 1", "BEAR PMC  //  COPY NO. 1")}</size>\n{Words("СОВЕРШЕННО СЕКРЕТНО", "TOP SECRET")}\n<size=40%>{unitName}</size>";
            _stampText.color = new Color32(214, 48, 36, 255);
            string unit = _variant.Theme.Division;
            int session = StartupTracker.Launches;
            _bootText.text =
                $"USEC TACNET  //  SECURE BOOT  //  BUILD 4.1.{session % 1000:000}\n\n"
                + "> HARDWARE KEY ...................... <color=#FFFFFF>OK</color>\n"
                + "> CERTIFICATE CHAIN ................. <color=#FFFFFF>VALID</color>\n"
                + "> CRYPTO FILL ....................... <color=#FFFFFF>LOADED</color>\n"
                + $"> NET {unit.Replace(" ", "-"),-31}<color=#FFFFFF>JOINED</color>\n"
                + "> BLUE FORCE TRACKER ................ <color=#FFFFFF>SYNCED</color>\n"
                + "> OPERATOR .......................... <color=" + _variant.Theme.WarnHex + ">PENDING</color>";
            _bootText.color = Accent;
        }

        private void LayoutIntros(Vector2 size)
        {
            float u = _u;
            SetText(_stampText, 0f, 0f, 900f * u, 220f * u, 58f * u, 10f);
            _stampText.lineSpacing = -10f;
            // A block of console lines, left-aligned, the block itself centred.
            SetText(_bootText, 0f, 20f * u, 640f * u, 220f * u, 15f * u, 4f);
            _bootText.lineSpacing = 18f;
        }

        /// <summary>BEAR's opening: on black, the secrecy stamp comes down hard, crooked, and the terminal comes up behind it.</summary>
        private void AnimateStamp(float clock, float w, float h)
        {
            // Hold the black a beat, slam, settle, then lift the black and the stamp together.
            const float Slam = 0.18f;
            const float Hit = 0.24f;
            float down = Mathf.Clamp01((clock - Slam + 0.12f) / 0.12f);
            float scale = Mathf.Lerp(2.4f, 1f, down * down);
            float shake = clock > Slam && clock < Slam + 0.12f ? Mathf.Sin(clock * 140f) * 4f * _u * (1f - (clock - Slam) / 0.12f) : 0f;
            float lift = Mathf.Clamp01((clock - 0.75f) / 0.45f);
            float liftEase = lift * lift * (3f - 2f * lift);
            PlaceShutters(0f, w, h, 1f - liftEase);
            _stampText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _stampText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -7f);
            _stampText.rectTransform.anchoredPosition = new Vector2(shake, -shake * 0.5f);
            _stampText.canvasRenderer.SetAlpha(down * (1f - liftEase) * (clock < Hit + 0.05f ? 1f : 0.92f));
            // The ink's flash as it lands.
            float flash = clock > Slam - 0.02f ? Mathf.Clamp01(1f - (clock - Slam) / 0.25f) : 0f;
            Place(_powerLine.rectTransform, 0f, 0f, w, h);
            _powerLine.color = new Color32(214, 48, 36, 255);
            _powerLine.canvasRenderer.SetAlpha(flash * 0.12f * (1f - liftEase));
        }

        /// <summary>USEC's opening: a secure-boot console checks its keys and joins the net, then a scan line opens the picture.</summary>
        private void AnimateBoot(float clock, float w, float h)
        {
            const float Typing = 1.15f;
            int total = _bootText.textInfo != null && _bootText.textInfo.characterCount > 0 ? _bootText.textInfo.characterCount : 400;
            _bootText.maxVisibleCharacters = Mathf.FloorToInt(total * Mathf.Clamp01(clock / Typing));
            float open = Mathf.Clamp01((clock - Typing - 0.1f) / 0.4f);
            float openEase = open * open * (3f - 2f * open);
            _bootText.canvasRenderer.SetAlpha(1f - openEase);
            PlaceShutters(Mathf.Round(h * 0.5f * openEase), w, h, 1f);
            Place(_powerLine.rectTransform, 0f, 0f, w, Mathf.Max(2f, Mathf.Round(2f * _u)));
            _powerLine.color = Color32.Lerp(Accent, new Color32(255, 255, 255, 255), 0.5f);
            _powerLine.canvasRenderer.SetAlpha(open > 0f ? 1f - openEase : 0f);
        }

        // ---- bars ---------------------------------------------------------------------------

        private void LayoutBar(float centre, float barWidth, float barTop)
        {
            BarKind kind = _variant.Bar;
            int first = StartupTracker.FirstStep;
            int shown = _segments.Length - first;
            float u = _u;
            bool lamps = kind == BarKind.Lamps;
            bool ruler = kind == BarKind.Ruler;
            bool tape = kind == BarKind.Tape;
            float segmentGap = Mathf.Round((lamps ? 6f : ruler ? 3f : shown > 12 ? 4f : 6f) * u);
            float segmentWidth = Mathf.Floor((barWidth - segmentGap * (shown - 1)) / shown);
            float segmentHeight = lamps ? Mathf.Round(10f * u) : ruler ? Mathf.Max(2f, Mathf.Round(2f * u)) : Mathf.Max(3f, Mathf.Round(4f * u));
            float rowWidth = segmentWidth * shown + segmentGap * (shown - 1);
            _barDeco.Clear();
            for (int i = 0; i < _segments.Length; i++)
            {
                bool on = !tape && i >= first;
                _segments[i].gameObject.SetActive(on);
                float x = centre - rowWidth * 0.5f + (i - first) * (segmentWidth + segmentGap) + segmentWidth * 0.5f;
                Place(_segments[i].rectTransform, x, barTop, segmentWidth, segmentHeight);
                _lampLabels[i].gameObject.SetActive(lamps && on);
                if (lamps && on)
                {
                    // Each lamp in its bezel, numbered under it.
                    float hw = segmentWidth * 0.5f + 2f * u;
                    float hh = segmentHeight * 0.5f + 2f * u;
                    Color32 bezel = Theme.WithAlpha(Dim, 110);
                    _barDeco.Line(new Vector2(x - hw, barTop + hh), new Vector2(x + hw, barTop + hh), Mathf.Max(1f, u), bezel);
                    _barDeco.Line(new Vector2(x - hw, barTop - hh), new Vector2(x + hw, barTop - hh), Mathf.Max(1f, u), bezel);
                    _barDeco.Line(new Vector2(x - hw, barTop + hh), new Vector2(x - hw, barTop - hh), Mathf.Max(1f, u), bezel);
                    _barDeco.Line(new Vector2(x + hw, barTop + hh), new Vector2(x + hw, barTop - hh), Mathf.Max(1f, u), bezel);
                    SetText(_lampLabels[i], x, barTop - 15f * u, segmentWidth + 10f * u, 12f * u, 8f * u, 2f);
                    _lampLabels[i].color = Theme.WithAlpha(Dim, 200);
                }
            }

            _solidTrack.gameObject.SetActive(tape);
            float tapeHeight = Mathf.Round(12f * u);
            Place(_solidTrack.rectTransform, centre, barTop, barWidth, tape ? tapeHeight : Mathf.Max(3f, Mathf.Round(4f * u)));
            if (tape)
            {
                // Punched tape: a row of sprocket holes down the middle, the edges ruled.
                Color32 hole = Theme.WithAlpha(Ink, 255);
                for (float x = centre - barWidth * 0.5f + 7f * u; x < centre + barWidth * 0.5f - 3f * u; x += 12f * u)
                {
                    _barDeco.Disc(new Vector2(x, barTop), 1.8f * u, hole);
                }
                _barDeco.Line(new Vector2(centre - barWidth * 0.5f, barTop + tapeHeight * 0.5f + 3f * u), new Vector2(centre + barWidth * 0.5f, barTop + tapeHeight * 0.5f + 3f * u), Mathf.Max(1f, u), Theme.WithAlpha(Dim, 90));
                _barDeco.Line(new Vector2(centre - barWidth * 0.5f, barTop - tapeHeight * 0.5f - 3f * u), new Vector2(centre + barWidth * 0.5f, barTop - tapeHeight * 0.5f - 3f * u), Mathf.Max(1f, u), Theme.WithAlpha(Dim, 90));
            }

            if (ruler)
            {
                // A ruler under the bar: forty divisions, every tenth longer. Short, so it stays
                // clear of the step name under it.
                for (int k = 0; k <= 40; k++)
                {
                    float x = centre - rowWidth * 0.5f + rowWidth * k / 40f;
                    float len = (k % 10 == 0 ? 6f : k % 5 == 0 ? 4f : 2f) * u;
                    _barDeco.Line(new Vector2(x, barTop - 4f * u), new Vector2(x, barTop - 4f * u - len), Mathf.Max(1f, u), Theme.WithAlpha(Dim, (byte)(k % 10 == 0 ? 180 : 90)));
                }
            }
            _barDeco.Commit();
            _solidDrawn = -1f;
        }

        private void UpdateSolidBar()
        {
            if (_variant.Bar != BarKind.Tape)
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
    }
}
