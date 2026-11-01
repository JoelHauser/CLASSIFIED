using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The debrief's second panel, beside the kit: one of several, picked each launch from the
    /// ones that suit the unit, so the screen is not the same twice running.
    ///
    ///   Map        the operation, drawn in the faction's conventions (Overlay.Debrief.cs).
    ///   Dossier    a personal file on someone wanted: a photo being matched, the record redacted.
    ///   Intercept  a radio spectrum and its waterfall, the transcript typing out under it.
    ///   Imagery    a reconnaissance photo, enhanced step by step onto its target as the load runs.
    ///   Readiness  the unit's state in bars: strength, ammunition, fuel, stores.
    ///   Tasking    the operator's own tasks under way, when the profile is known.
    /// </summary>
    internal partial class Overlay
    {
        private enum Module
        {
            Map,
            Dossier,
            Intercept,
            Imagery,
            Readiness,
            Tasking
        }

        private static Module[] ModulesOf(ThemeChoice unit)
        {
            switch (unit)
            {
                case ThemeChoice.BearCommand:
                case ThemeChoice.UsecCommand: return new[] { Module.Map, Module.Readiness, Module.Dossier };
                case ThemeChoice.BearReconnaissance:
                case ThemeChoice.UsecIntelligence: return new[] { Module.Map, Module.Imagery, Module.Dossier };
                case ThemeChoice.BearSignals:
                case ThemeChoice.UsecSignals: return new[] { Module.Map, Module.Intercept };
                case ThemeChoice.BearCounterintelligence: return new[] { Module.Dossier, Module.Intercept, Module.Map };
                case ThemeChoice.BearSupply:
                case ThemeChoice.UsecLogistics: return new[] { Module.Map, Module.Readiness };
                case ThemeChoice.BearMedical:
                case ThemeChoice.UsecMedical: return new[] { Module.Map, Module.Readiness };
                default: return new[] { Module.Map, Module.Imagery, Module.Intercept };
            }
        }

        private sealed partial class Debrief
        {
            private Module _module;
            private Rect _panel;

            /// <summary>The panel for this launch, built in <paramref name="zone"/>.</summary>
            private void BuildModule(Rect zone)
            {
                Module[] choices = ModulesOf(T.Id);
                _module = choices[Rng.Next(choices.Length)];
                // The operator's own tasks, now and then, when they are known.
                if (StartupTracker.Quests.Count > 0 && Rng.Next(100) < 35)
                {
                    _module = Module.Tasking;
                }
                switch (_module)
                {
                    case Module.Dossier: BuildDossier(zone); break;
                    case Module.Intercept: BuildIntercept(zone); break;
                    case Module.Imagery: BuildImagery(zone); break;
                    case Module.Readiness: BuildReadiness(zone); break;
                    case Module.Tasking: BuildTasking(zone); break;
                    default: BuildMap(zone); break;
                }
            }

            private void AnimateModule(float t)
            {
                switch (_module)
                {
                    case Module.Dossier: AnimateDossier(t); break;
                    case Module.Intercept: AnimateIntercept(t); break;
                    case Module.Imagery: AnimateImagery(t); break;
                    case Module.Readiness: AnimateReadiness(t); break;
                    case Module.Tasking: AnimateTasking(t); break;
                    default:
                        if (_live != null)
                        {
                            AnimateMap(t);
                        }
                        break;
                }
            }

            /// <summary>A panel's frame: an outline with heavy corners, a title over it on the left and a reading on the right.</summary>
            private Rect PanelFrame(Rect zone, string title, out TextMeshProUGUI reading)
            {
                const float Header = 22f;
                Rect area = Rect.MinMaxRect(zone.xMin + 6f, zone.yMin + 6f, zone.xMax - 6f, zone.yMax - Header - 6f);
                Shape frame = Draw();
                Outline(frame, area.center.x, area.center.y, area.width, area.height, 1.5f, A(T.Accent, 70));
                const float Tick = 10f;
                foreach (var corner in new[] { new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin), new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMax) })
                {
                    float sx = corner.x < area.center.x ? 1f : -1f;
                    float sy = corner.y < area.center.y ? 1f : -1f;
                    frame.Line(P(corner.x, corner.y), P(corner.x + sx * Tick, corner.y), 3f * U, A(T.Accent, 200));
                    frame.Line(P(corner.x, corner.y), P(corner.x, corner.y + sy * Tick), 3f * U, A(T.Accent, 200));
                }
                frame.Commit();
                const float ReadingWidth = 110f;
                Fit(Label(title, area.xMin + (area.width - ReadingWidth) * 0.5f, area.yMax + 13f, area.width - ReadingWidth - 8f, 10f, A(T.Dim, 220), TextAlignmentOptions.Left), 10f);
                reading = Label("", area.xMax - ReadingWidth * 0.5f, area.yMax + 13f, ReadingWidth, 10f, A(T.Accent, 220), TextAlignmentOptions.Right);
                return area;
            }

            private string Code(int digits) => Rng.Next((int)Mathf.Pow(10, digits - 1), (int)Mathf.Pow(10, digits)).ToString();

            // ==== Dossier ========================================================================

            private Shape _scan;
            private TextMeshProUGUI _dossierReading;
            private TextMeshProUGUI _match;
            private Rect _photo;
            private int _shownMatch = -1;

            private static readonly string[] Callsigns = { "GRANIT", "SHAMAN", "VOLK", "KOSTYL", "SAPPER", "BARMAN", "TOURIST", "NOMAD", "DEACON", "LYNX", "HATCHET", "MOTH" };

            private void BuildDossier(Rect zone)
            {
                _panel = PanelFrame(zone, Ru($"ЛИЧНОЕ ДЕЛО  //  № {Code(4)}  //  СОВ. СЕКРЕТНО", $"PERSONNEL FILE  //  NO. {Code(4)}  //  EYES ONLY"), out _dossierReading);
                Rect a = _panel;
                // The photo, on the left, as tall as the panel allows.
                float ph = Mathf.Min(a.height - 30f, 190f);
                float pw = ph * 0.78f;
                _photo = new Rect(a.xMin + 14f, a.yMax - 14f - ph, pw, ph);
                Shape photo = Draw();
                photo.RoundedRect(P(_photo.center.x, _photo.center.y), pw * U, ph * U, 0f, A(T.Accent, 14));
                Outline(photo, _photo.center.x, _photo.center.y, pw, ph, 1.2f, A(T.Accent, 90));
                Person(photo, _photo.center.x, _photo.center.y - ph * 0.08f, ph * 0.8f, A(T.Accent, 60));
                // A measuring scale down the photo's side, as police photos have.
                for (int k = 0; k <= 10; k++)
                {
                    float y = Mathf.Lerp(_photo.yMin, _photo.yMax, k / 10f);
                    photo.Line(P(_photo.xMax - 6f, y), P(_photo.xMax, y), U, A(T.Accent, 90));
                }
                photo.Commit();
                _scan = Draw();
                _match = Label("", _photo.center.x, _photo.yMin - 10f, pw + 40f, 9f, A(T.Accent, 220), TextAlignmentOptions.Center);

                // The record, on the right.
                float x = _photo.xMax + 18f;
                float w = a.xMax - 10f - x;
                Faction enemy = Theme.Opposite(T.Faction);
                string[,] rows =
                {
                    { Ru("ФАМИЛИЯ", "NAME"), "" },
                    { Ru("ПОЗЫВНОЙ", "CALLSIGN"), "«" + Callsigns[Rng.Next(Callsigns.Length)] + "»" },
                    { Ru("ПРИНАДЛЕЖНОСТЬ", "AFFILIATION"), Rng.Next(3) == 0 ? Ru("ДИКИЕ", "SCAV") : Theme.Name(enemy) },
                    { Ru("ПОСЛЕДНИЙ РАЗ", "LAST SEEN"), Lore.Fill("{place}", Rng).ToUpperInvariant() },
                    { Ru("СТЕПЕНЬ УГРОЗЫ", "THREAT"), Ru(Rng.Next(2) == 0 ? "ВЫСОКАЯ" : "КРАЙНЕ ВЫСОКАЯ", Rng.Next(2) == 0 ? "HIGH" : "EXTREME") },
                    { Ru("СТАТУС", "STATUS"), Ru("В РОЗЫСКЕ", "WANTED") }
                };
                Shape bars = Draw();
                for (int i = 0; i < rows.GetLength(0); i++)
                {
                    float y = a.yMax - 22f - i * 26f;
                    if (y < a.yMin + 14f)
                    {
                        break;
                    }
                    Fit(Label(rows[i, 0], x + w * 0.5f, y + 6f, w, 7f, A(T.Dim, 200), TextAlignmentOptions.Left), 7f);
                    if (i == 0)
                    {
                        // The name is blacked out.
                        bars.RoundedRect(P(x + w * 0.3f, y - 6f), w * 0.6f * U, 9f * U, 0f, A(T.Text, 170));
                    }
                    else
                    {
                        bool warn = i >= 4;
                        Fit(Label(rows[i, 1], x + w * 0.5f, y - 6f, w, 10f, A(warn ? T.Warn : T.Text, 220), TextAlignmentOptions.Left), 10f);
                    }
                    bars.Line(P(x, y - 13f), P(x + w, y - 13f), U, A(T.Accent, 30));
                }
                bars.Commit();

                // A rubber stamp across the corner.
                TextMeshProUGUI stamp = Label(Ru("РОЗЫСК", "WANTED"), a.xMax - 70f, a.yMin + 26f, 140f, 18f, new Color32(214, 48, 36, 170), TextAlignmentOptions.Center);
                stamp.fontStyle = FontStyles.Bold;
                stamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, 12f);
                Shape box = Draw(a.xMax - 70f, a.yMin + 26f);
                box.rectTransform.localEulerAngles = new Vector3(0f, 0f, 12f);
                Outline(box, 0f, 0f, 130f, 30f, 2f, new Color32(214, 48, 36, 150));
                box.Commit();
            }

            private void AnimateDossier(float t)
            {
                float p = Progress;
                // The match box closes in on the face, the score climbing, as the load runs.
                _scan.Clear();
                Vector2 face = new Vector2(_photo.center.x, _photo.yMax - _photo.height * 0.28f);
                float size = Mathf.Lerp(_photo.width * 0.9f, _photo.width * 0.42f, Mathf.Clamp01(p * 1.4f));
                float arm = size * 0.25f;
                Color32 c = A(p > 0.7f ? T.Warn : T.Accent, 220);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        Vector2 corner = face + new Vector2(sx, sy) * size * 0.5f;
                        _scan.Line(P(corner.x, corner.y), P(corner.x - sx * arm, corner.y), 1.6f * U, c);
                        _scan.Line(P(corner.x, corner.y), P(corner.x, corner.y - sy * arm), 1.6f * U, c);
                    }
                }
                // A scan line going up and down the photo.
                float y = Mathf.Lerp(_photo.yMin + 2f, _photo.yMax - 2f, 0.5f + 0.5f * Mathf.Sin(t * 1.3f));
                _scan.Line(P(_photo.xMin + 2f, y), P(_photo.xMax - 8f, y), U, A(T.Accent, 120));
                _scan.Commit();
                int match = Mathf.Min(99, Mathf.FloorToInt(12f + p * 90f));
                if (match != _shownMatch)
                {
                    _shownMatch = match;
                    _match.text = Ru("СОВПАДЕНИЕ", "MATCH") + $" <mspace=0.62em>{match:00}</mspace>%";
                    _dossierReading.text = match >= 90 ? Ru("ОПОЗНАН", "IDENTIFIED") : Ru("СВЕРКА", "MATCHING");
                }
            }

            // ==== Intercept ======================================================================

            private const int SpectrumBins = 64;
            private const int Waterfall = 10;
            private Shape _spectrum;
            private TextMeshProUGUI _transcript;
            private TextMeshProUGUI _interceptReading;
            private readonly List<float[]> _history = new List<float[]>();
            private float[] _carriers;
            private float _nextTrace;
            private float _lineAt;
            private string[] _lines;
            private int _line;
            private int _typedFrom;
            private Rect _graph;

            private void BuildIntercept(Rect zone)
            {
                float mhz = Rng.Next(30, 88) + Rng.Next(0, 1000) / 1000f;
                _panel = PanelFrame(zone, Ru($"РАДИОПЕРЕХВАТ  //  {mhz:0.000} МГЦ".Replace('.', ','), $"SIGINT  //  INTERCEPT  //  {mhz:0.000} MHZ"), out _interceptReading);
                Rect a = _panel;
                _graph = Rect.MinMaxRect(a.xMin + 10f, a.center.y - 6f, a.xMax - 10f, a.yMax - 12f);
                Shape axes = Draw();
                axes.Line(P(_graph.xMin, _graph.yMin), P(_graph.xMax, _graph.yMin), U, A(T.Accent, 80));
                for (int k = 0; k <= 8; k++)
                {
                    float x = Mathf.Lerp(_graph.xMin, _graph.xMax, k / 8f);
                    axes.Line(P(x, _graph.yMin), P(x, _graph.yMin - 4f), U, A(T.Accent, 80));
                    axes.Line(P(x, _graph.yMin), P(x, _graph.yMax), U, A(T.Accent, 14));
                }
                axes.Commit();
                _spectrum = Draw();
                _carriers = new[] { Range(0.15f, 0.35f), Range(0.45f, 0.6f), Range(0.7f, 0.9f) };

                // The transcript: the other side's traffic, a line at a time.
                Faction enemy = Theme.Opposite(T.Faction);
                var lines = new List<string>(Correspondence.Intercepts(enemy));
                for (int i = lines.Count - 1; i > 0; i--)
                {
                    int j = Rng.Next(i + 1);
                    (lines[i], lines[j]) = (lines[j], lines[i]);
                }
                _lines = lines.ToArray();
                float th = _graph.yMin - 12f - (a.yMin + 8f);
                _transcript = Label("", a.center.x, a.yMin + 8f + th * 0.5f, a.width - 20f, 9f, A(T.Text, 210), TextAlignmentOptions.TopLeft);
                _transcript.enableWordWrapping = true;
                SetText(_transcript, a.center.x * U, (a.yMin + 8f + th * 0.5f) * U, (a.width - 20f) * U, th * U, 9f * U, 2f);
                _line = -1;
            }

            private void AnimateIntercept(float t)
            {
                if (t >= _nextTrace)
                {
                    // A new trace a dozen times a second: noise, with the carriers standing up out of it.
                    _nextTrace = t + 1f / 12f;
                    var trace = new float[SpectrumBins];
                    for (int i = 0; i < SpectrumBins; i++)
                    {
                        float f = i / (SpectrumBins - 1f);
                        float v = 0.08f + 0.12f * (float)Rng.NextDouble();
                        for (int c = 0; c < _carriers.Length; c++)
                        {
                            float d = (f - _carriers[c]) / 0.025f;
                            v += (0.5f + 0.3f * Mathf.Sin(t * (1.3f + c) + c)) * Mathf.Exp(-d * d);
                        }
                        trace[i] = Mathf.Clamp01(v);
                    }
                    _history.Insert(0, trace);
                    if (_history.Count > Waterfall)
                    {
                        _history.RemoveAt(_history.Count - 1);
                    }
                    _spectrum.Clear();
                    // The waterfall: the older traces fainter and lower, the newest on top.
                    for (int h = _history.Count - 1; h >= 0; h--)
                    {
                        float[] tr = _history[h];
                        float drop = h * 5f;
                        byte alpha = (byte)(h == 0 ? 220 : 70 - h * 6);
                        for (int i = 0; i + 1 < SpectrumBins; i++)
                        {
                            float x0 = Mathf.Lerp(_graph.xMin, _graph.xMax, i / (SpectrumBins - 1f));
                            float x1 = Mathf.Lerp(_graph.xMin, _graph.xMax, (i + 1) / (SpectrumBins - 1f));
                            float y0 = _graph.yMin + tr[i] * (_graph.height - 4f) - drop;
                            float y1 = _graph.yMin + tr[i + 1] * (_graph.height - 4f) - drop;
                            if (y0 < _graph.yMin && y1 < _graph.yMin)
                            {
                                continue;
                            }
                            _spectrum.Line(P(x0, Mathf.Max(y0, _graph.yMin)), P(x1, Mathf.Max(y1, _graph.yMin)), (h == 0 ? 1.4f : 1f) * U, A(T.Accent, alpha));
                        }
                    }
                    // The receiver's marker on the strongest carrier.
                    float lockX = Mathf.Lerp(_graph.xMin, _graph.xMax, _carriers[1]);
                    _spectrum.Line(P(lockX, _graph.yMin), P(lockX, _graph.yMax), U, A(T.Warn, 140));
                    _spectrum.Commit();
                }

                // A line of transcript every few seconds, typed out, the last few kept.
                if (_line < 0 || t - _lineAt > 6.5f)
                {
                    _line++;
                    _lineAt = t;
                    // The earlier lines dim and whole, the new one typed out after them.
                    var earlier = new System.Text.StringBuilder("<alpha=#70>");
                    for (int k = Mathf.Max(0, _line - 2); k < _line; k++)
                    {
                        earlier.Append("> ").Append(Lore.Fill(_lines[k % _lines.Length], Rng)).Append("\n");
                    }
                    _transcript.text = earlier.ToString();
                    _transcript.ForceMeshUpdate();
                    _typedFrom = _transcript.textInfo.characterCount;
                    _transcript.text = earlier + "<alpha=#FF><color=" + T.AccentHex + ">> </color>" + Lore.Fill(_lines[_line % _lines.Length], Rng);
                    _transcript.ForceMeshUpdate();
                    _interceptReading.text = Ru("ЗАПИСЬ", "REC") + $" {_line + 1:000}";
                }
                _transcript.maxVisibleCharacters = _typedFrom + Mathf.FloorToInt((t - _lineAt) * 45f);
            }

            // ==== Imagery ========================================================================

            private RawImage _image;
            private Shape _reticle;
            private TextMeshProUGUI _imageReading;
            private Vector2 _focus;
            private int _shownZoom = -1;

            private void BuildImagery(Rect zone)
            {
                _panel = PanelFrame(zone, Ru($"АЭРОФОТОСНИМОК  //  КАДР {Code(4)}", $"IMAGERY  //  FRAME {Code(5)}  //  GSD 0.3 M"), out _imageReading);
                Rect a = _panel;
                RectTransform clip = Clip(a);
                _image = MakeRaw("Image", clip, Backdrops.Get(T.Faction, BackdropKind.Imagery), A(T.Accent, 150));
                _image.rectTransform.anchorMin = Vector2.zero;
                _image.rectTransform.anchorMax = Vector2.one;
                _image.rectTransform.offsetMin = _image.rectTransform.offsetMax = Vector2.zero;
                _focus = new Vector2(Range(0.3f, 0.7f), Range(0.3f, 0.7f));
                _reticle = Draw();
            }

            private void AnimateImagery(float t)
            {
                Rect a = _panel;
                Texture texture = _image.texture;
                if (texture == null)
                {
                    return;
                }
                // Zoom in on the target in steps: each step a quick ease, then a hold, like an analyst's "enhance".
                float p = Progress;
                int steps = 4;
                float raw = p * steps;
                int step = Mathf.Min(steps - 1, Mathf.FloorToInt(raw));
                float within = Mathf.Clamp01((raw - step) * 4f);
                float ease = within * within * (3f - 2f * within);
                float zoom = Mathf.Pow(1.7f, step + ease);
                float aspect = (a.width / a.height) / (texture.width / (float)texture.height);
                float h = 0.9f / zoom;
                float w = h * aspect;
                Vector2 drift = new Vector2(Mathf.Sin(t * 0.13f), Mathf.Cos(t * 0.11f)) * 0.004f;
                _image.uvRect = new Rect(_focus.x - w * 0.5f + drift.x, _focus.y - h * 0.5f + drift.y, w, h);

                // The analyst's box on the target, with its crosshair and grid.
                _reticle.Clear();
                Vector2 c = a.center;
                float box = Mathf.Min(a.width, a.height) * 0.28f;
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        Vector2 corner = c + new Vector2(sx, sy) * box * 0.5f;
                        _reticle.Line(P(corner.x, corner.y), P(corner.x - sx * 12f, corner.y), 1.6f * U, A(T.Text, 220));
                        _reticle.Line(P(corner.x, corner.y), P(corner.x, corner.y - sy * 12f), 1.6f * U, A(T.Text, 220));
                    }
                }
                _reticle.Line(P(c.x - 6f, c.y), P(c.x + 6f, c.y), U, A(T.Text, 200));
                _reticle.Line(P(c.x, c.y - 6f), P(c.x, c.y + 6f), U, A(T.Text, 200));
                if (within < 1f)
                {
                    // The flash of a step.
                    _reticle.Line(P(a.xMin, Mathf.Lerp(a.yMax, a.yMin, within)), P(a.xMax, Mathf.Lerp(a.yMax, a.yMin, within)), 1.4f * U, A(T.Accent, 160));
                }
                _reticle.Commit();
                int shown = step * 10 + Mathf.RoundToInt(ease * 9f);
                if (shown != _shownZoom)
                {
                    _shownZoom = shown;
                    _imageReading.text = (within < 1f ? Ru("УВЕЛИЧЕНИЕ", "ENHANCE") : "") + $"  x{zoom:0.0}";
                }
            }

            // ==== Readiness ======================================================================

            private struct Gauge
            {
                internal Image Fill;
                internal TextMeshProUGUI Value;
                internal float From;
                internal float To;
                internal float Width;
            }

            private readonly List<Gauge> _gauges = new List<Gauge>();
            private TextMeshProUGUI _readyReading;

            private void BuildReadiness(Rect zone)
            {
                bool stores = T.Id == ThemeChoice.BearSupply || T.Id == ThemeChoice.UsecLogistics || T.Id == ThemeChoice.BearMedical || T.Id == ThemeChoice.UsecMedical;
                _panel = PanelFrame(zone, stores ? Ru("ОБЕСПЕЧЕННОСТЬ  //  СВОДКА", "SUSTAINMENT  //  STATUS") : Ru("БОЕВОЙ СОСТАВ  //  СТРОЕВАЯ ЗАПИСКА", "ORDER OF BATTLE  //  READINESS"), out _readyReading);
                Rect a = _panel;
                string[] names = stores
                    ? new[] { Ru("БОЕПРИПАСЫ", "AMMUNITION"), Ru("ГОРЮЧЕЕ", "FUEL"), Ru("ПРОДОВОЛЬСТВИЕ", "RATIONS"), Ru("МЕДИКАМЕНТЫ", "MEDICAL"), Ru("ВОДА", "WATER"), Ru("ЗАПЧАСТИ", "SPARES") }
                    : new[] { Ru("1 ГРУППА", "TEAM 1"), Ru("2 ГРУППА", "TEAM 2"), Ru("3 ГРУППА", "TEAM 3"), Ru("РЕЗЕРВ", "RESERVE"), Ru("РАЗВЕДКА", "RECON"), Ru("ОГНЕВАЯ ПОДДЕРЖКА", "FIRE SUPPORT") };
                int rows = Mathf.Clamp(Mathf.FloorToInt((a.height - 20f) / 28f), 2, names.Length);
                float labelW = Mathf.Min(150f, a.width * 0.36f);
                float barX = a.xMin + 14f + labelW;
                float barW = a.xMax - 60f - barX;
                for (int i = 0; i < rows; i++)
                {
                    float y = a.yMax - 22f - i * ((a.height - 30f) / rows);
                    Fit(Label(names[i], a.xMin + 14f + labelW * 0.5f, y, labelW, 9f, A(T.Text, 210), TextAlignmentOptions.Left), 9f);
                    Image track = Box(barX + barW * 0.5f, y, barW, 8f, A(T.Accent, 26));
                    // Ticks along the track at the quarters.
                    Shape ticks = Draw();
                    for (int k = 1; k < 4; k++)
                    {
                        ticks.Line(P(barX + barW * k / 4f, y - 6f), P(barX + barW * k / 4f, y + 6f), U, A(T.Accent, 50));
                    }
                    ticks.Commit();
                    Image fill = Box(barX, y, 0f, 8f, A(T.Accent, 200));
                    fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                    fill.rectTransform.anchoredPosition = P(barX, y);
                    TextMeshProUGUI value = Label("", a.xMax - 30f, y, 50f, 9f, A(T.Text, 220), TextAlignmentOptions.Right);
                    float to = Range(0.35f, 0.98f);
                    _gauges.Add(new Gauge { Fill = fill, Value = value, From = Range(0.1f, 0.3f), To = to, Width = barW });
                    track.transform.SetAsFirstSibling();
                }
            }

            private void AnimateReadiness(float t)
            {
                float p = Progress;
                int ready = 0;
                for (int i = 0; i < _gauges.Count; i++)
                {
                    Gauge g = _gauges[i];
                    // Each fills toward its figure as the load runs, a little behind the one above.
                    float f = Mathf.Clamp01(p * 1.3f - i * 0.06f);
                    float v = Mathf.Lerp(g.From, g.To, f * f * (3f - 2f * f)) + 0.006f * Mathf.Sin(t * 2f + i);
                    g.Fill.rectTransform.sizeDelta = new Vector2(Mathf.Round(g.Width * v * U), Mathf.Round(8f * U));
                    bool low = v < 0.4f;
                    g.Fill.color = A(low ? T.Warn : T.Accent, 200);
                    int pct = Mathf.RoundToInt(v * 100f);
                    string text = $"<mspace=0.62em>{pct:00}</mspace>%";
                    if (g.Value.text != text)
                    {
                        g.Value.text = text;
                    }
                    if (!low)
                    {
                        ready++;
                    }
                }
                string reading = Ru("ГОТОВНОСТЬ", "READY") + $" {ready}/{_gauges.Count}";
                if (_readyReading.text != reading)
                {
                    _readyReading.text = reading;
                }
            }

            // ==== Tasking ========================================================================

            private readonly List<TextMeshProUGUI> _tasks = new List<TextMeshProUGUI>();
            private Shape _taskMarker;
            private TextMeshProUGUI _taskReading;
            private float _taskTop;
            private float _taskRow;

            private void BuildTasking(Rect zone)
            {
                _panel = PanelFrame(zone, Ru("ПОСТАВЛЕННЫЕ ЗАДАЧИ  //  НА КОНТРОЛЕ", "OPERATIONAL TASKING  //  ACTIVE"), out _taskReading);
                Rect a = _panel;
                List<string> quests = StartupTracker.Quests;
                int rows = Mathf.Min(quests.Count, Mathf.Max(1, Mathf.FloorToInt((a.height - 16f) / 30f)));
                _taskTop = a.yMax - 22f;
                _taskRow = 30f;
                Shape lines = Draw();
                for (int i = 0; i < rows; i++)
                {
                    float y = _taskTop - i * _taskRow;
                    Fit(Label($"{i + 1:00}", a.xMin + 24f, y, 30f, 9f, A(T.Dim, 200), TextAlignmentOptions.Left), 9f);
                    TextMeshProUGUI name = Fit(Label(quests[i].ToUpperInvariant(), a.xMin + 40f + (a.width - 150f) * 0.5f, y, a.width - 150f, 10f, A(T.Text, 220), TextAlignmentOptions.Left), 10f);
                    _tasks.Add(name);
                    Fit(Label(Ru("В РАБОТЕ", "IN PROGRESS"), a.xMax - 56f, y, 100f, 8f, A(T.Accent, 200), TextAlignmentOptions.Right), 8f);
                    lines.Line(P(a.xMin + 10f, y - 12f), P(a.xMax - 10f, y - 12f), U, A(T.Accent, 26));
                }
                lines.Commit();
                _taskMarker = Draw();
                _taskReading.text = $"{quests.Count:00} " + Ru("ЗАДАЧ", "OPEN");
            }

            private void AnimateTasking(float t)
            {
                if (_tasks.Count == 0)
                {
                    return;
                }
                // One task at a time comes under the cursor, as if being read.
                int at = Mathf.FloorToInt(t / 3f) % _tasks.Count;
                float y = _taskTop - at * _taskRow;
                _taskMarker.Clear();
                _taskMarker.RoundedRect(P(_panel.center.x, y), (_panel.width - 16f) * U, 24f * U, 0f, A(T.Accent, 22));
                _taskMarker.Line(P(_panel.xMin + 8f, y - 12f), P(_panel.xMin + 8f, y + 12f), 3f * U, A(T.Accent, 220));
                _taskMarker.Commit();
            }
        }
    }
}
