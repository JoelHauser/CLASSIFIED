using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// Each unit's ambient drawing: one large, faint, slow thing behind the terminal that belongs
    /// to that unit and, where it can, follows the load: the route on the map advances, the
    /// receiver locks on, the seal's ring closes. No widgets; faint, so the insignia and readout
    /// stay the subject.
    ///
    /// Nothing is drawn under text. Each layout names the areas its text leaves free (ArtZones):
    /// two zones, and a strip under the readout above the ticker that both zones reach down to,
    /// so a drawing can run from one zone along the strip into the other without crossing text.
    /// The drawings size themselves to those areas. Built on the full-screen layer, in 1080p
    /// units around the screen's centre.
    /// </summary>
    internal partial class Overlay
    {
        private const float ArtFloor = -405f;
        private const float ArtStripTop = -322f;
        private const float ArtStripBottom = -408f;
        /// <summary>Under the top corner lines.</summary>
        private const float ArtTop = 400f;
        /// <summary>Under the memo in the top-right corner.</summary>
        private const float ArtUnderMemo = 170f;

        /// <summary>
        /// The text-free areas of the current layout, in 1080p units, y up: a left and a right
        /// zone, both reaching down to the strip. False where the layout leaves no room (the dock,
        /// and screens too narrow for zones beside the text).
        /// </summary>
        private bool ArtZones(out Rect left, out Rect right, out Rect strip)
        {
            float w = _laidOutSize.x / _u;
            float edge = w * 0.5f - 72f;
            float stripHalf = w * 0.5f - 118f;
            left = right = strip = default;
            strip = Rect.MinMaxRect(-stripHalf, ArtStripBottom, stripHalf, ArtStripTop);
            switch (_layout)
            {
                case LayoutKind.Centered:
                    // Either side of the logo and readout column (within 320 of the centre line).
                    if (edge - 360f < 180f)
                    {
                        return false;
                    }
                    left = Rect.MinMaxRect(-edge, ArtFloor, -360f, ArtTop);
                    right = Rect.MinMaxRect(360f, ArtFloor, edge, ArtUnderMemo);
                    return true;
                case LayoutKind.Sidebar:
                {
                    // The open area right of the sign-in panel, in two halves.
                    float open = -w * 0.5f + 620f + 40f;
                    if (edge - open < 440f)
                    {
                        return false;
                    }
                    float middle = (open + edge) * 0.5f;
                    left = Rect.MinMaxRect(open, ArtFloor, middle - 20f, ArtTop);
                    right = Rect.MinMaxRect(middle + 20f, ArtFloor, edge, ArtUnderMemo);
                    strip = Rect.MinMaxRect(open, ArtStripBottom, Mathf.Min(edge, stripHalf), ArtStripTop);
                    return true;
                }
                case LayoutKind.Split:
                case LayoutKind.SplitMirror:
                {
                    // Under the logo and wordmark on one side, under the readout on the other.
                    bool brandLeft = _layout == LayoutKind.Split;
                    const float UnderBrand = -160f;
                    const float UnderReadout = -120f;
                    left = Rect.MinMaxRect(-edge, ArtFloor, -30f, brandLeft ? UnderBrand : UnderReadout);
                    right = Rect.MinMaxRect(30f, ArtFloor, edge, brandLeft ? UnderReadout : UnderBrand);
                    return true;
                }
                default:
                    return false;
            }
        }

        private bool AmbientRoom => ArtZones(out _, out _, out _);

        /// <summary>The unit's ambient drawing, or null where the backdrop already has one (the watermark).</summary>
        private static Signature CreateAmbient(ThemeChoice id)
        {
            switch (id)
            {
                case ThemeChoice.BearReconnaissance: return new TacticalMap { Recon = true };
                case ThemeChoice.UsecOperations: return new TacticalMap();
                case ThemeChoice.BearSignals: return new Spectrum();
                case ThemeChoice.BearCounterintelligence: return new Fingerprint();
                case ThemeChoice.BearSupply:
                case ThemeChoice.UsecLogistics: return new ShippingRoute();
                case ThemeChoice.BearMedical:
                case ThemeChoice.UsecMedical: return new Heartbeat();
                case ThemeChoice.UsecIntelligence: return new RedactedText();
                case ThemeChoice.UsecSignals: return new NetworkMap();
                case ThemeChoice.UsecCommand: return new UnitSeal();
                default: return null;
            }
        }

        /// <summary>Shared strengths, shapes and the text-free areas for the ambient drawings.</summary>
        private abstract class Ambient : Signature
        {
            private bool _zoned;
            private Rect _left;
            private Rect _right;
            private Rect _strip;

            private void Zones()
            {
                if (!_zoned)
                {
                    O.ArtZones(out _left, out _right, out _strip);
                    _zoned = true;
                }
            }

            /// <summary>The left zone.</summary>
            protected Rect Lz { get { Zones(); return _left; } }
            /// <summary>The right zone.</summary>
            protected Rect Rz { get { Zones(); return _right; } }
            /// <summary>The strip under the readout, above the ticker.</summary>
            protected Rect Sz { get { Zones(); return _strip; } }
            protected Rect Zone(int side) => side == 0 ? Lz : Rz;
            /// <summary>The taller zone, for the one big piece; the other gets the rest.</summary>
            protected Rect Tall => Lz.height >= Rz.height ? Lz : Rz;
            protected Rect Other => Lz.height >= Rz.height ? Rz : Lz;

            protected override bool Seeded => true;

            protected Color32 Line => A(T.Accent, 70);
            protected Color32 Soft => A(T.Accent, 30);
            protected Color32 Fill => A(T.Accent, 10);

            /// <summary>A masked area: what is drawn in it stops at its edges. Draw in it relative to its centre.</summary>
            protected RectTransform Clip(Rect area)
            {
                RectTransform rect = MakeRect("Clip", R);
                Place(rect, area.center.x * U, area.center.y * U, area.width * U, area.height * U);
                rect.gameObject.AddComponent<RectMask2D>();
                return rect;
            }

            /// <summary>A head-and-shoulders silhouette filling a photo h tall, centred at (x, y).</summary>
            protected void Person(Shape shape, float x, float y, float h, Color32 color)
            {
                shape.Disc(P(x, y + 0.16f * h), 0.2f * h * U, color);
                float r = 0.17f * h * U;
                shape.RoundedRect(P(x, y - 0.3f * h), 0.72f * h * U, 0.34f * h * U, r, r, 0f, 0f, color);
            }
        }

        /// <summary>Medical: a heartbeat on ECG paper along the strip, quickening when a step runs slow.</summary>
        private sealed class Heartbeat : Ambient
        {
            private const float Period = 340f;
            private const float Baseline = -12f;
            private Shape _trace;
            private float _offset;

            protected override void Build()
            {
                Rect strip = Sz;
                float half = strip.width * 0.5f;
                float h = strip.height * 0.5f;
                RectTransform clip = Clip(strip);
                Shape paper = Draw(0f, 0f, clip);
                for (float x = -half; x <= half; x += 40f)
                {
                    paper.Line(P(x, -h), P(x, h), U, A(T.Accent, 9));
                }
                for (float y = -40f; y <= 40f; y += 20f)
                {
                    paper.Line(P(-half, y), P(half, y), U, A(T.Accent, 9));
                }
                paper.Commit();

                _trace = Draw(0f, Baseline, clip);
                float start = -half - Period;
                Vector2 last = Vector2.zero;
                for (float x = start; x <= half + Period; x += 5f)
                {
                    float p = ((x - start) % Period) / Period;
                    Vector2 point = P(x, HeartWave(p) * 0.72f);
                    if (x > start)
                    {
                        _trace.Line(last, point, 2f * U, Line);
                    }
                    last = point;
                }
                _trace.Commit();
            }

            internal override void Animate(float t)
            {
                float bpm = 60f + 40f * O._alert + 6f * O._beat;
                _offset = (_offset + Dt * Period * bpm / 60f) % Period;
                _trace.rectTransform.anchoredPosition = P(-_offset + Period * 0.5f, Baseline);
            }
        }

        /// <summary>
        /// Logistics: a dotted shipping route down the left zone, along the strip and up the right
        /// one, a marker travelling it as the load goes.
        /// </summary>
        private sealed class ShippingRoute : Ambient
        {
            private Vector2[] _points;
            private float[] _at;
            private Shape _marker;
            private Shape _done;
            private float _drawn = -1f;

            protected override void Build()
            {
                Rect l = Lz;
                Rect r = Rz;
                float y = Sz.center.y;
                float jl = l.width * 0.28f;
                float jr = r.width * 0.28f;
                // A stop where a zone meets the strip is in both, so every leg stays in one of them.
                _points = new[]
                {
                    new Vector2(l.center.x + Range(-jl, jl), Mathf.Lerp(l.yMin, l.yMax, 0.92f)),
                    new Vector2(l.center.x + Range(-jl, jl), Mathf.Lerp(l.yMin, l.yMax, 0.62f)),
                    new Vector2(l.center.x + Range(-jl, jl), Mathf.Lerp(l.yMin, l.yMax, 0.32f)),
                    new Vector2(l.center.x, y + Range(-20f, 20f)),
                    new Vector2(Mathf.Lerp(l.center.x, r.center.x, 1f / 3f), y + Range(-25f, 25f)),
                    new Vector2(Mathf.Lerp(l.center.x, r.center.x, 2f / 3f), y + Range(-25f, 25f)),
                    new Vector2(r.center.x, y + Range(-20f, 20f)),
                    new Vector2(r.center.x + Range(-jr, jr), Mathf.Lerp(r.yMin, r.yMax, 0.45f)),
                    new Vector2(r.center.x + Range(-jr, jr), Mathf.Lerp(r.yMin, r.yMax, 0.92f)),
                };
                _at = new float[_points.Length];
                Shape dashes = Draw();
                for (int i = 0; i < _points.Length - 1; i++)
                {
                    Vector2 a = _points[i];
                    Vector2 b = _points[i + 1];
                    float length = Vector2.Distance(a, b);
                    _at[i + 1] = _at[i] + length;
                    for (float d = 0f; d < length; d += 14f)
                    {
                        Vector2 from = Vector2.Lerp(a, b, d / length);
                        Vector2 to = Vector2.Lerp(a, b, Mathf.Min(d + 7f, length) / length);
                        dashes.Line(P(from.x, from.y), P(to.x, to.y), 1.5f * U, Soft);
                    }
                }
                foreach (Vector2 stop in _points)
                {
                    dashes.Disc(P(stop.x, stop.y), 5f * U, Line);
                }
                dashes.Commit();
                _done = Draw();
                _marker = Draw();
                _marker.Disc(Vector2.zero, 6f * U, A(T.Accent, 170));
                _marker.Commit();
            }

            internal override void Animate(float t)
            {
                float total = _at[_at.Length - 1];
                float d = Progress * total;
                int leg = 0;
                while (leg < _points.Length - 2 && d > _at[leg + 1])
                {
                    leg++;
                }
                float share = Mathf.Clamp01((d - _at[leg]) / Mathf.Max(1f, _at[leg + 1] - _at[leg]));
                Vector2 at = Vector2.Lerp(_points[leg], _points[leg + 1], share);
                _marker.rectTransform.anchoredPosition = P(at.x, at.y);
                if (Mathf.Abs(d - _drawn) < 4f)
                {
                    return;
                }
                _drawn = d;
                _done.Clear();
                for (int i = 0; i < leg; i++)
                {
                    _done.Line(P(_points[i].x, _points[i].y), P(_points[i + 1].x, _points[i + 1].y), 2f * U, Line);
                }
                _done.Line(P(_points[leg].x, _points[leg].y), P(at.x, at.y), 2f * U, Line);
                _done.Commit();
            }
        }

        /// <summary>Intelligence: a page of report text in each zone, a redaction bar landing on another as the load goes.</summary>
        private sealed class RedactedText : Ambient
        {
            private readonly List<Vector4> _bars = new List<Vector4>();
            private Shape _redactions;
            private int _drawn = -1;

            protected override void Build()
            {
                Shape text = Draw();
                for (int s = 0; s < 2; s++)
                {
                    Rect zone = Zone(s);
                    float left = zone.xMin + 10f;
                    float right = zone.xMax - 10f;
                    for (float y = zone.yMax - 20f; y > zone.yMin + 10f; y -= 26f)
                    {
                        // Now and then a short line ends a paragraph.
                        float end = Rng.NextDouble() < 0.15 ? Mathf.Lerp(left, right, Range(0.3f, 0.7f)) : right;
                        float x = left;
                        while (x < end - 30f)
                        {
                            float wordEnd = Mathf.Min(x + Range(30f, 100f), end);
                            text.Line(P(x, y), P(wordEnd, y), 7f * U, A(T.Accent, 16));
                            if (Rng.NextDouble() < 0.12)
                            {
                                _bars.Add(new Vector4(x - 3f, y, wordEnd + 3f, 0f));
                            }
                            x = wordEnd + 14f;
                        }
                    }
                }
                text.Commit();
                // Shuffled, so the censor works across the pages rather than down them.
                for (int i = _bars.Count - 1; i > 0; i--)
                {
                    int j = Rng.Next(i + 1);
                    (_bars[i], _bars[j]) = (_bars[j], _bars[i]);
                }
                _redactions = Draw();
            }

            internal override void Animate(float t)
            {
                int count = Mathf.FloorToInt(Progress * _bars.Count);
                if (count == _drawn)
                {
                    return;
                }
                _drawn = count;
                _redactions.Clear();
                for (int i = 0; i < count; i++)
                {
                    Vector4 bar = _bars[i];
                    _redactions.Line(P(bar.x, bar.y), P(bar.z, bar.y), 13f * U, A(new Color32(0, 0, 0, 255), 170));
                }
                _redactions.Commit();
            }
        }

        /// <summary>
        /// USEC Corporate Command: the company seal, embossed faintly in the taller zone. Its outer
        /// ring presses in round the circle as the load goes, and the lettering on its rim appears
        /// with it.
        /// </summary>
        private sealed class UnitSeal : Ambient
        {
            private const string Rim = "UNITED SECURITY INTERNATIONAL  //  CORPORATE COMMAND  //  ";
            private Shape _ring;
            private TMPro.TextMeshProUGUI[] _letters;
            private float[] _angles;
            private int _drawn = -1;
            private float _r;

            protected override void Build()
            {
                // Sized to the zone; everything on it scales with the radius.
                Rect zone = Tall;
                _r = Mathf.Min(200f, zone.width * 0.4f, zone.height * 0.4f);
                float k = _r / 220f;
                float x = zone.center.x;
                float y = zone.center.y;
                Shape seal = Draw(x, y);
                seal.Arc((_r + 26f * k) * U, 1.5f * U, 0f, 360f, Soft);
                seal.Arc((_r - 30f * k) * U, 2f * U, 0f, 360f, Soft);
                seal.Arc((_r - 40f * k) * U, 1f * U, 0f, 360f, Soft);
                for (int i = 0; i < 72; i++)
                {
                    seal.Line(Polar((_r - 56f * k) * U, i * 5f), Polar((_r - (i % 6 == 0 ? 70f : 62f) * k) * U, i * 5f), (i % 6 == 0 ? 2f : 1f) * U, Soft);
                }
                Color32 mark = A(T.Accent, 40);
                Insignia.DrawAll(T.Faction, seal, 80f * k * U, mark, A(T.Accent, 60), A(T.Ink, 200));
                seal.Commit();
                _ring = Draw(x, y);

                // The rim lettering, one character at a time round the circle, upright to it.
                _letters = new TMPro.TextMeshProUGUI[Rim.Length];
                _angles = new float[Rim.Length];
                for (int i = 0; i < Rim.Length; i++)
                {
                    float angle = i * 360f / Rim.Length;
                    Vector2 at = Polar(_r * U, angle);
                    TMPro.TextMeshProUGUI letter = _letters[i] = Label(Rim[i].ToString(), 0f, 0f, 30f * k, 22f * k, A(T.Accent, 60), TMPro.TextAlignmentOptions.Center, null, 0f);
                    letter.rectTransform.anchoredPosition = P(x, y) + at;
                    letter.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
                    letter.canvasRenderer.SetAlpha(0f);
                    _angles[i] = angle;
                }
            }

            internal override void Animate(float t)
            {
                int degrees = Mathf.FloorToInt(Progress * 360f);
                if (degrees == _drawn)
                {
                    return;
                }
                _drawn = degrees;
                _ring.Clear();
                _ring.Arc((_r + 34f * _r / 220f) * U, 4f * U, 0f, degrees, A(T.Accent, 60));
                _ring.Commit();
                for (int i = 0; i < _letters.Length; i++)
                {
                    _letters[i].canvasRenderer.SetAlpha(_angles[i] <= degrees ? 1f : 0f);
                }
            }
        }

        /// <summary>
        /// Internal Affairs: a fingerprint in the taller zone, a scan line passing over it, and an
        /// investigation board in the other: photos pinned up round the subject, string run
        /// between them as the load goes, the subject circled at the end. The print is a picture
        /// (Resources/fingerprint.png, made by scripts/fingerprint.py the way synthetic
        /// fingerprint datasets are, not taken from a real person), white with the ridges in its
        /// alpha, tinted to the unit.
        /// </summary>
        private sealed class Fingerprint : Ambient
        {
            private const float Aspect = 440f / 560f;
            private const float CardW = 72f;
            private const float CardH = 90f;
            private static Texture2D _texture;
            private Image _scan;
            private float _x;
            private float _y;
            private float _h;
            private Vector2[] _pins;
            private int[][] _links;
            private Shape _strings;
            private Shape _ring;
            private float _ringR;
            private int _drawn = -1;

            protected override void Build()
            {
                BuildPrint(Tall);
                BuildBoard(Other);
            }

            private void BuildPrint(Rect zone)
            {
                _x = zone.center.x;
                _y = zone.center.y;
                _h = Mathf.Min(520f, zone.height * 0.75f, zone.width * 0.9f / Aspect);
                float w = _h * Aspect;
                Texture2D texture = Texture();
                if (texture != null)
                {
                    RawImage print = MakeRaw("Print", R, texture, A(T.Accent, 72));
                    Place(print.rectTransform, _x * U, _y * U, w * U, _h * U);
                }
                _scan = Box(_x, _y, w * 1.15f, 2f, A(T.Accent, 70));
            }

            /// <summary>Five photos, the subject larger in the middle, each with a pin and a redacted name.</summary>
            private void BuildBoard(Rect zone)
            {
                float cx = zone.center.x;
                float cy = zone.center.y;
                float dx = zone.width * 0.3f;
                float dy = zone.height * 0.3f;
                // Smaller cards in a smaller zone, so none overlap.
                float scale = Mathf.Clamp(Mathf.Min(zone.height / 560f, zone.width / 500f), 0.55f, 1.1f);
                float jx = 15f * scale;
                float jy = 15f * scale;
                var cards = new[]
                {
                    new Vector2(cx, cy),
                    new Vector2(cx - dx + Range(-jx, jx), cy + dy + Range(-jy, 0f)),
                    new Vector2(cx + dx + Range(-jx, jx), cy + dy + Range(-jy, 0f)),
                    new Vector2(cx + dx + Range(-jx, jx), cy - dy + Range(0f, jy)),
                    new Vector2(cx - dx + Range(-jx, jx), cy - dy + Range(0f, jy)),
                };
                Shape board = Draw();
                _pins = new Vector2[cards.Length];
                for (int i = 0; i < cards.Length; i++)
                {
                    float k = (i == 0 ? 1.25f : 1f) * scale;
                    float w = CardW * k;
                    float h = CardH * k;
                    Vector2 c = cards[i];
                    board.RoundedRect(P(c.x, c.y), w * U, h * U, 2f * U, A(T.Accent, 12));
                    Outline(board, c.x, c.y, w, h, 1.5f, Soft);
                    float photo = w - 12f * k;
                    float photoY = c.y + h * 0.5f - 6f * k - photo * 0.5f;
                    board.RoundedRect(P(c.x, photoY), photo * U, photo * U, U, A(T.Accent, 14));
                    Person(board, c.x, photoY, photo, A(T.Accent, 40));
                    // The name, redacted.
                    float nameY = c.y - h * 0.5f + 11f * k;
                    board.Line(P(c.x - w * 0.3f, nameY), P(c.x + w * 0.3f, nameY), 7f * k * U, A(new Color32(0, 0, 0, 255), 150));
                    _pins[i] = new Vector2(c.x, c.y + h * 0.5f - 3f);
                    board.Disc(P(_pins[i].x, _pins[i].y), 4f * U, A(T.Warn, 150));
                }
                board.Commit();

                // Every photo to the subject, then two between the others.
                _links = new[] { new[] { 0, 1 }, new[] { 0, 3 }, new[] { 0, 2 }, new[] { 1, 2 }, new[] { 0, 4 }, new[] { 3, 4 } };
                _strings = Draw();
                _ring = Draw(cards[0].x, cards[0].y);
                _ringR = CardH * 1.25f * scale * 0.68f;
            }

            /// <summary>The print, read from the plugin once and kept.</summary>
            private static Texture2D Texture()
            {
                if (_texture != null)
                {
                    return _texture;
                }
                try
                {
                    using (Stream stream = typeof(Overlay).Assembly.GetManifestResourceStream("CLASSIFIED.fingerprint.png"))
                    {
                        if (stream == null)
                        {
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
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if (!ImageConversion.LoadImage(texture, bytes, true))
                        {
                            UnityEngine.Object.Destroy(texture);
                            return null;
                        }
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.filterMode = FilterMode.Trilinear;
                        texture.hideFlags = HideFlags.HideAndDontSave;
                        _texture = texture;
                    }
                }
                catch (Exception)
                {
                    return null;
                }
                return _texture;
            }

            internal override void Animate(float t)
            {
                _scan.rectTransform.anchoredPosition = P(_x, _y + Mathf.Sin(t * 0.6f) * _h * 0.46f);

                // The string runs out link by link over the first 85% of the load; then the subject is circled.
                int step = Mathf.FloorToInt(Progress * 400f);
                if (step == _drawn)
                {
                    return;
                }
                _drawn = step;
                float strung = Mathf.Clamp01(Progress / 0.85f) * _links.Length;
                _strings.Clear();
                for (int i = 0; i < _links.Length && i < strung; i++)
                {
                    Vector2 a = _pins[_links[i][0]];
                    Vector2 b = Vector2.Lerp(a, _pins[_links[i][1]], Mathf.Clamp01(strung - i));
                    _strings.Line(P(a.x, a.y), P(b.x, b.y), 1.5f * U, A(T.Warn, 90));
                }
                _strings.Commit();
                float ring = Mathf.Clamp01((Progress - 0.85f) / 0.15f);
                _ring.Clear();
                if (ring > 0f)
                {
                    _ring.Arc(_ringR * U, 2.5f * U, -40f, 360f * ring, A(T.Warn, 120));
                }
                _ring.Commit();
            }
        }

        /// <summary>IT: a network in each zone and along the strip, packets travelling between its nodes.</summary>
        private sealed class NetworkMap : Ambient
        {
            private const int Packets = 10;
            private readonly List<Vector2> _nodes = new List<Vector2>();
            private readonly List<int[]> _links = new List<int[]>();
            private readonly Shape[] _packets = new Shape[Packets];
            private readonly int[] _from = new int[Packets];
            private readonly int[] _to = new int[Packets];
            private readonly float[] _along = new float[Packets];

            protected override void Build()
            {
                // Three separate networks, one per area, so no link crosses the text; a node
                // where each zone meets the strip belongs to both and joins them up.
                int left = Area(Lz, 20f);
                int right = Area(Rz, 20f);
                int stripEnd = Area(Sz, 14f);
                _nodes.Add(new Vector2(Lz.center.x, Sz.center.y));
                _nodes.Add(new Vector2(Rz.center.x, Sz.center.y));
                Link(stripEnd, Nearest(_nodes[stripEnd], 0, left));
                Link(stripEnd + 1, Nearest(_nodes[stripEnd + 1], left, right));
                Link(stripEnd, Nearest(_nodes[stripEnd], right, stripEnd));
                Link(stripEnd + 1, Nearest(_nodes[stripEnd + 1], right, stripEnd));
                Join(0, left);
                Join(left, right);
                Join(right, stripEnd);

                Shape map = Draw();
                foreach (int[] link in _links)
                {
                    map.Line(P(_nodes[link[0]].x, _nodes[link[0]].y), P(_nodes[link[1]].x, _nodes[link[1]].y), 1.2f * U, Soft);
                }
                foreach (Vector2 node in _nodes)
                {
                    map.Disc(P(node.x, node.y), 4f * U, Line);
                }
                map.Commit();
                for (int p = 0; p < Packets; p++)
                {
                    _packets[p] = Draw();
                    _packets[p].Disc(Vector2.zero, 3.5f * U, A(T.Accent, 170));
                    _packets[p].Commit();
                    NewTrip(p);
                    _along[p] = Range(0f, 1f);
                }
            }

            /// <summary>Scatters nodes in an area, by its size; returns the index after the last.</summary>
            private int Area(Rect area, float inset)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(area.width * area.height / 32000f), 4, 10);
                for (int i = 0; i < count; i++)
                {
                    _nodes.Add(new Vector2(Range(area.xMin + inset, area.xMax - inset), Range(area.yMin + inset, area.yMax - inset)));
                }
                return _nodes.Count;
            }

            /// <summary>Links each node in [from, to) to its two nearest neighbours in the same range.</summary>
            private void Join(int from, int to)
            {
                for (int i = from; i < to; i++)
                {
                    int a = -1, b = -1;
                    float da = float.MaxValue, db = float.MaxValue;
                    for (int j = from; j < to; j++)
                    {
                        if (j == i)
                        {
                            continue;
                        }
                        float d = Vector2.Distance(_nodes[i], _nodes[j]);
                        if (d < da) { db = da; b = a; da = d; a = j; }
                        else if (d < db) { db = d; b = j; }
                    }
                    Link(i, a);
                    Link(i, b);
                }
            }

            private int Nearest(Vector2 to, int from, int end)
            {
                int best = -1;
                float distance = float.MaxValue;
                for (int j = from; j < end; j++)
                {
                    float d = Vector2.Distance(to, _nodes[j]);
                    if (d < distance)
                    {
                        distance = d;
                        best = j;
                    }
                }
                return best;
            }

            private void Link(int a, int b)
            {
                if (a >= 0 && b >= 0 && a != b)
                {
                    _links.Add(new[] { a, b });
                }
            }

            private void NewTrip(int p)
            {
                int[] link = _links[Rng.Next(_links.Count)];
                bool flip = Rng.Next(2) == 0;
                _from[p] = flip ? link[1] : link[0];
                _to[p] = flip ? link[0] : link[1];
                _along[p] = 0f;
            }

            internal override void Animate(float t)
            {
                float speed = 0.4f + 0.8f * O._shownFraction + O._alert;
                for (int p = 0; p < Packets; p++)
                {
                    _along[p] += Dt * speed;
                    if (_along[p] >= 1f)
                    {
                        NewTrip(p);
                    }
                    Vector2 at = Vector2.Lerp(_nodes[_from[p]], _nodes[_to[p]], _along[p]);
                    _packets[p].rectTransform.anchoredPosition = P(at.x, at.y);
                }
            }
        }


        /// <summary>
        /// Operations and reconnaissance: a contour map in the taller zone, a faint grid over it.
        /// Operations draws the axis of advance, an arrow that pushes from the start line to the
        /// objective with the load, past friendly and enemy positions. Reconnaissance flies a drone
        /// out along a dashed track and circles the objective, its camera footprint turning with it.
        /// </summary>
        private sealed class TacticalMap : Ambient
        {
            internal bool Recon;
            private const int Cells = 44;
            private Rect _area;
            private Vector2 _start;
            private Vector2 _objective;
            private Vector2 _bend;
            private Shape _route;
            private Shape _head;
            private Shape _drone;
            private float _drawn = -1f;
            private float _orbit;

            protected override void Build()
            {
                Rect zone = Tall;
                float side = Mathf.Min(zone.width, zone.height);
                _area = new Rect(zone.center.x - side * 0.5f, zone.center.y - side * 0.5f, side, side);

                Shape map = Draw();
                Contours(map);
                for (int i = 1; i < 6; i++)
                {
                    float gx = Mathf.Lerp(_area.xMin, _area.xMax, i / 6f);
                    float gy = Mathf.Lerp(_area.yMin, _area.yMax, i / 6f);
                    map.Line(P(gx, _area.yMin), P(gx, _area.yMax), U, A(T.Accent, 14));
                    map.Line(P(_area.xMin, gy), P(_area.xMax, gy), U, A(T.Accent, 14));
                }
                Outline(map, _area.center.x, _area.center.y, _area.width, _area.height, 1.5f, Soft);

                _start = new Vector2(Mathf.Lerp(_area.xMin, _area.xMax, 0.12f), Mathf.Lerp(_area.yMin, _area.yMax, Range(0.15f, 0.3f)));
                _objective = new Vector2(Mathf.Lerp(_area.xMin, _area.xMax, Range(0.62f, 0.78f)), Mathf.Lerp(_area.yMin, _area.yMax, Range(0.62f, 0.8f)));
                _bend = Vector2.Lerp(_start, _objective, 0.5f) + new Vector2(Range(-0.12f, 0.12f), Range(-0.12f, 0.12f)) * side;

                // The objective: a ring with a cross through it.
                float ring = side * 0.07f;
                map.Line(P(_objective.x - ring * 1.4f, _objective.y), P(_objective.x + ring * 1.4f, _objective.y), U, Soft);
                map.Line(P(_objective.x, _objective.y - ring * 1.4f), P(_objective.x, _objective.y + ring * 1.4f), U, Soft);
                // Arc draws round the shape's own centre: move that ring to the objective.
                Shape ringShape = Draw(_objective.x, _objective.y);
                ringShape.Arc(ring * U, 1.5f * U, 0f, 360f, Line);
                ringShape.Commit();

                // Friendly positions are boxes, enemy positions diamonds.
                for (int i = 0; i < 3; i++)
                {
                    Vector2 at = Vector2.Lerp(_start, _bend, 0.3f + 0.3f * i) + new Vector2(Range(-30f, 30f), Range(-30f, 30f));
                    Outline(map, at.x, at.y, 22f, 14f, 1.5f, Line);
                    map.Line(P(at.x - 11f, at.y - 7f), P(at.x + 11f, at.y + 7f), U, Soft);
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector2 at = Vector2.Lerp(_bend, _objective, 0.45f + 0.35f * i) + new Vector2(Range(-45f, 45f), Range(-45f, 45f));
                    float d = 11f;
                    Color32 enemy = A(T.Warn, 70);
                    map.Line(P(at.x - d, at.y), P(at.x, at.y + d), 1.5f * U, enemy);
                    map.Line(P(at.x, at.y + d), P(at.x + d, at.y), 1.5f * U, enemy);
                    map.Line(P(at.x + d, at.y), P(at.x, at.y - d), 1.5f * U, enemy);
                    map.Line(P(at.x, at.y - d), P(at.x - d, at.y), 1.5f * U, enemy);
                }
                map.Commit();

                _route = Draw();
                _head = Draw();
                if (Recon)
                {
                    _drone = Draw();
                    _drone.Polygon(new[] { P(0f, 9f), P(6f, -6f), P(0f, -2f), P(-6f, -6f) }, A(T.Accent, 170));
                    // The camera's footprint on the ground, ahead of the drone.
                    _drone.Polygon(new[] { P(-10f, 26f), P(10f, 26f), P(26f, 70f), P(-26f, 70f) }, A(T.Accent, 16));
                    _drone.Commit();
                }
                else
                {
                    _head.Polygon(new[] { P(0f, 10f), P(9f, -8f), P(-9f, -8f) }, A(T.Accent, 140));
                    _head.Commit();
                }
            }

            /// <summary>Contours of a few seeded hills, by marching squares over a coarse grid.</summary>
            private void Contours(Shape map)
            {
                int hills = 5;
                var centres = new Vector2[hills];
                var sizes = new float[hills];
                var heights = new float[hills];
                for (int i = 0; i < hills; i++)
                {
                    centres[i] = new Vector2(Range(0.1f, 0.9f), Range(0.1f, 0.9f));
                    sizes[i] = Range(0.12f, 0.3f);
                    heights[i] = Range(0.5f, 1f) * (i == 0 ? 1.3f : 1f);
                }
                var field = new float[Cells + 1, Cells + 1];
                for (int gx = 0; gx <= Cells; gx++)
                {
                    for (int gy = 0; gy <= Cells; gy++)
                    {
                        var p = new Vector2(gx / (float)Cells, gy / (float)Cells);
                        float h = 0f;
                        for (int i = 0; i < hills; i++)
                        {
                            float d = (p - centres[i]).sqrMagnitude / (sizes[i] * sizes[i]);
                            h += heights[i] * Mathf.Exp(-d);
                        }
                        field[gx, gy] = h;
                    }
                }
                float cell = _area.width / Cells;
                for (int level = 1; level <= 7; level++)
                {
                    float iso = level * 0.16f;
                    Color32 color = A(T.Accent, (byte)(level % 5 == 0 ? 34 : 20));
                    for (int gx = 0; gx < Cells; gx++)
                    {
                        for (int gy = 0; gy < Cells; gy++)
                        {
                            float a = field[gx, gy], b = field[gx + 1, gy], c = field[gx + 1, gy + 1], d = field[gx, gy + 1];
                            int code = (a > iso ? 1 : 0) | (b > iso ? 2 : 0) | (c > iso ? 4 : 0) | (d > iso ? 8 : 0);
                            if (code == 0 || code == 15)
                            {
                                continue;
                            }
                            // Where the level crosses each edge of the cell.
                            Vector2 o = new Vector2(_area.xMin + gx * cell, _area.yMin + gy * cell);
                            Vector2 bottom = o + new Vector2(cell * Cross(a, b, iso), 0f);
                            Vector2 right = o + new Vector2(cell, cell * Cross(b, c, iso));
                            Vector2 top = o + new Vector2(cell * Cross(d, c, iso), cell);
                            Vector2 left = o + new Vector2(0f, cell * Cross(a, d, iso));
                            switch (code)
                            {
                                case 1: case 14: Seg(map, left, bottom, color); break;
                                case 2: case 13: Seg(map, bottom, right, color); break;
                                case 3: case 12: Seg(map, left, right, color); break;
                                case 4: case 11: Seg(map, right, top, color); break;
                                case 6: case 9: Seg(map, bottom, top, color); break;
                                case 7: case 8: Seg(map, left, top, color); break;
                                case 5: Seg(map, left, top, color); Seg(map, bottom, right, color); break;
                                case 10: Seg(map, left, bottom, color); Seg(map, right, top, color); break;
                            }
                        }
                    }
                }
            }

            private static float Cross(float from, float to, float iso) => Mathf.Clamp01((iso - from) / (to - from));

            private void Seg(Shape map, Vector2 a, Vector2 b, Color32 color) => map.Line(P(a.x, a.y), P(b.x, b.y), U, color);

            /// <summary>The route, as far as <paramref name="p"/> of it: start line, the bend, the objective.</summary>
            private Vector2 Along(float p) => p < 0.5f ? Vector2.Lerp(_start, _bend, p * 2f) : Vector2.Lerp(_bend, _objective, (p - 0.5f) * 2f);

            internal override void Animate(float t)
            {
                float p = Progress;
                if (Recon)
                {
                    // Out along the track for the first part of the load, then round the objective.
                    const float Outbound = 0.4f;
                    Vector2 at;
                    float heading;
                    float radius = _area.width * 0.13f;
                    if (p < Outbound)
                    {
                        Vector2 entry = _objective + new Vector2(-radius, 0f);
                        at = Vector2.Lerp(_start, entry, p / Outbound);
                        Vector2 d = entry - _start;
                        heading = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                        _orbit = 180f;
                    }
                    else
                    {
                        _orbit -= Dt * 40f;
                        at = _objective + new Vector2(Mathf.Cos(_orbit * Mathf.Deg2Rad), Mathf.Sin(_orbit * Mathf.Deg2Rad)) * radius;
                        heading = _orbit - 180f;
                    }
                    _drone.rectTransform.anchoredPosition = P(at.x, at.y);
                    _drone.rectTransform.localEulerAngles = new Vector3(0f, 0f, heading);
                    if (Mathf.Abs(p - _drawn) < 0.01f)
                    {
                        return;
                    }
                    _drawn = p;
                    _route.Clear();
                    Vector2 end = Vector2.Lerp(_start, _objective + new Vector2(-radius, 0f), Mathf.Min(p / Outbound, 1f));
                    float length = Vector2.Distance(_start, end);
                    for (float d = 0f; d < length; d += 16f)
                    {
                        Vector2 a = Vector2.Lerp(_start, end, d / Mathf.Max(1f, length));
                        Vector2 b = Vector2.Lerp(_start, end, Mathf.Min(d + 8f, length) / Mathf.Max(1f, length));
                        _route.Line(P(a.x, a.y), P(b.x, b.y), 1.5f * U, Line);
                    }
                    _route.Commit();
                    return;
                }

                Vector2 head = Along(p);
                Vector2 ahead = Along(Mathf.Min(p + 0.02f, 1f)) - head;
                _head.rectTransform.anchoredPosition = P(head.x, head.y);
                if (ahead.sqrMagnitude > 0.01f)
                {
                    _head.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg - 90f);
                }
                if (Mathf.Abs(p - _drawn) < 0.01f)
                {
                    return;
                }
                _drawn = p;
                // The axis of advance: a broad, faint band with a firm edge, drawn as far as the load.
                _route.Clear();
                const int Steps = 24;
                int shown = Mathf.CeilToInt(p * Steps);
                for (int i = 0; i < shown; i++)
                {
                    Vector2 a = Along(i / (float)Steps);
                    Vector2 b = Along(Mathf.Min((i + 1f) / Steps, p));
                    _route.Line(P(a.x, a.y), P(b.x, b.y), 10f * U, A(T.Accent, 18));
                    _route.Line(P(a.x, a.y), P(b.x, b.y), 2f * U, Line);
                }
                _route.Commit();
            }
        }

        /// <summary>
        /// Signals: a receiver's spectrum across the taller zone, the trace breathing with noise
        /// round a few carriers, and a waterfall of the last seconds scrolling down under it. The
        /// tuning marker sweeps the band with the load and locks onto the strongest carrier.
        /// </summary>
        private sealed class Spectrum : Ambient
        {
            private const int Bins = 72;
            private const int Rows = 48;
            private const float TickRate = 10f;

            private Rect _scope;
            private Rect _fall;
            private float[] _carriers;
            private float[] _strength;
            private float[] _level = new float[Bins];
            private Shape _trace;
            private Shape _marker;
            /// <summary>One texture for every rebuild: the drawing is rebuilt with the layout, and a texture per rebuild would never be freed.</summary>
            private static Texture2D _waterfall;
            private Color32[] _pixels;
            private float _nextTick;
            private int _strongest;

            protected override void Build()
            {
                Rect zone = Tall;
                float w = Mathf.Min(zone.width, 520f);
                float h = Mathf.Min(zone.height, 560f);
                float x = zone.center.x - w * 0.5f;
                float top = zone.center.y + h * 0.5f;
                _scope = new Rect(x, top - h * 0.38f, w, h * 0.38f);
                _fall = new Rect(x, top - h, w, h * 0.58f);

                _carriers = new float[3];
                _strength = new float[3];
                for (int i = 0; i < _carriers.Length; i++)
                {
                    _carriers[i] = Range(0.12f, 0.88f);
                    _strength[i] = Range(0.45f, 1f);
                }
                _strongest = 0;
                for (int i = 1; i < _carriers.Length; i++)
                {
                    if (_strength[i] > _strength[_strongest])
                    {
                        _strongest = i;
                    }
                }

                Shape frame = Draw();
                Outline(frame, _scope.center.x, _scope.center.y, _scope.width, _scope.height, 1.5f, Soft);
                Outline(frame, _fall.center.x, _fall.center.y, _fall.width, _fall.height, 1.5f, Soft);
                for (int i = 1; i < 8; i++)
                {
                    float gx = Mathf.Lerp(_scope.xMin, _scope.xMax, i / 8f);
                    frame.Line(P(gx, _scope.yMin), P(gx, _scope.yMax), U, A(T.Accent, 12));
                }
                for (int i = 1; i < 4; i++)
                {
                    float gy = Mathf.Lerp(_scope.yMin, _scope.yMax, i / 4f);
                    frame.Line(P(_scope.xMin, gy), P(_scope.xMax, gy), U, A(T.Accent, 12));
                }
                frame.Commit();

                if (_waterfall == null)
                {
                    _waterfall = new Texture2D(Bins, Rows, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        // Held only by this static field; EFT unloads unused assets between scenes.
                        hideFlags = HideFlags.DontUnloadUnusedAsset
                    };
                }
                _pixels = new Color32[Bins * Rows];
                _waterfall.SetPixels32(_pixels);
                _waterfall.Apply(false);
                RawImage fall = MakeRaw("Waterfall", R, _waterfall, A(T.Accent, 90));
                Place(fall.rectTransform, _fall.center.x * U, _fall.center.y * U, (_fall.width - 4f) * U, (_fall.height - 4f) * U);

                _trace = Draw();
                _marker = Draw();
                _marker.Line(P(0f, _fall.yMin), P(0f, _scope.yMax), 1.5f * U, A(T.Accent, 80));
                _marker.Polygon(new[] { P(-6f, _scope.yMax + 10f), P(6f, _scope.yMax + 10f), P(0f, _scope.yMax) }, A(T.Accent, 150));
                _marker.Commit();
            }

            internal override void Animate(float t)
            {
                // The marker sweeps the band, then settles on the strongest carrier as the load ends.
                float p = Progress;
                float sweep = Mathf.PingPong(t * 0.18f, 1f);
                float tuned = Mathf.Lerp(sweep, _carriers[_strongest], Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.5f) / 0.45f)));
                _marker.rectTransform.anchoredPosition = P(Mathf.Lerp(_scope.xMin, _scope.xMax, tuned), 0f);

                if (t < _nextTick)
                {
                    return;
                }
                _nextTick = t + 1f / TickRate;

                for (int b = 0; b < Bins; b++)
                {
                    float f = b / (Bins - 1f);
                    float level = 0.08f + 0.06f * (float)Rng.NextDouble();
                    for (int i = 0; i < _carriers.Length; i++)
                    {
                        float d = (f - _carriers[i]) / 0.025f;
                        float fade = 0.75f + 0.25f * Mathf.Sin(t * (1.3f + i) + i * 2f);
                        level += _strength[i] * fade * Mathf.Exp(-d * d);
                    }
                    _level[b] = Mathf.Clamp01(level);
                }

                _trace.Clear();
                for (int b = 0; b < Bins - 1; b++)
                {
                    float x0 = Mathf.Lerp(_scope.xMin, _scope.xMax, b / (Bins - 1f));
                    float x1 = Mathf.Lerp(_scope.xMin, _scope.xMax, (b + 1) / (Bins - 1f));
                    float y0 = Mathf.Lerp(_scope.yMin + 4f, _scope.yMax - 6f, _level[b]);
                    float y1 = Mathf.Lerp(_scope.yMin + 4f, _scope.yMax - 6f, _level[b + 1]);
                    _trace.Polygon(new[] { P(x0, _scope.yMin + 1f), P(x0, y0), P(x1, y1), P(x1, _scope.yMin + 1f) }, A(T.Accent, 10));
                    _trace.Line(P(x0, y0), P(x1, y1), 1.5f * U, Line);
                }
                _trace.Commit();

                // The waterfall scrolls one row down; the newest row goes on top.
                System.Array.Copy(_pixels, Bins, _pixels, 0, Bins * (Rows - 1));
                int row = Bins * (Rows - 1);
                for (int b = 0; b < Bins; b++)
                {
                    _pixels[row + b] = new Color32(255, 255, 255, (byte)(_level[b] * _level[b] * 255f));
                }
                _waterfall.SetPixels32(_pixels);
                _waterfall.Apply(false);
            }
        }
    }
}
