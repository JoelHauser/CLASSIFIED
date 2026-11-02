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
    ///   Imagery    an aerial picture, drawn, enhanced step by step onto the objective as the load runs.
    ///   Readiness  a fighting unit's PERSTAT (its teams reporting in), a supply unit's LOGSTAT (classes of supply).
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
                // A dark ground under the panel, so the backdrop's own map and figures don't show through it.
                Box(area.center.x, area.center.y, area.width, area.height, A(Dark, 235));
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
            private readonly List<TextMeshProUGUI> _sightings = new List<TextMeshProUGUI>();
            private TextMeshProUGUI _bounty;
            private int _bountyTo;
            private int _shownBounty = -1;
            private Vector2 _face;
            private float _faceSize;
            private int _shownMatch = -1;

            private static readonly string[] Callsigns = { "GRANIT", "SHAMAN", "VOLK", "KOSTYL", "SAPPER", "BARMAN", "TOURIST", "NOMAD", "DEACON", "LYNX", "HATCHET", "MOTH" };

            private void BuildDossier(Rect zone)
            {
                _panel = PanelFrame(zone, Ru($"ЛИЧНОЕ ДЕЛО  //  № {Code(4)}  //  СОВ. СЕКРЕТНО", $"PERSONNEL FILE  //  NO. {Code(4)}  //  EYES ONLY"), out _dossierReading);
                Rect a = _panel;
                // The photo, on the left, as tall as the panel.
                float ph = Mathf.Min(a.height - 34f, 280f);
                float pw = ph * 0.78f;
                _photo = new Rect(a.xMin + 14f, a.center.y - ph * 0.5f + 6f, pw, ph);
                Shape photo = Draw();
                photo.RoundedRect(P(_photo.center.x, _photo.center.y), pw * U, ph * U, 0f, A(T.Accent, 14));
                Outline(photo, _photo.center.x, _photo.center.y, pw, ph, 1.2f, A(T.Accent, 90));
                Person(photo, _photo.center.x, _photo.center.y - ph * 0.08f, ph * 0.8f, A(T.Accent, 60));
                // Where Person puts the head (0.16 of its height above its middle), for the match box.
                _face = new Vector2(_photo.center.x, _photo.center.y - ph * 0.08f + 0.16f * ph * 0.8f);
                _faceSize = 0.2f * ph * 0.8f * 2f;
                // A measuring scale down the photo's side, as police photos have.
                for (int k = 0; k <= 10; k++)
                {
                    float y = Mathf.Lerp(_photo.yMin, _photo.yMax, k / 10f);
                    photo.Line(P(_photo.xMax - 6f, y), P(_photo.xMax, y), U, A(T.Accent, 90));
                }
                photo.Commit();
                _scan = Draw();
                _match = Label("", _photo.center.x, _photo.yMin - 10f, pw + 40f, 9f, A(T.Accent, 220), TextAlignmentOptions.Center);

                // The record, in the middle, its rows spread down the panel.
                float x = _photo.xMax + 18f;
                float room = a.xMax - 10f - x;
                bool side = room > 330f;
                float w = side ? Mathf.Min(room * 0.5f, 300f) : room;
                Faction enemy = Theme.Opposite(T.Faction);
                // What they carry: any weapon in the armoury.
                var arms = Kit.All[0];
                for (int k = 0; k < 40; k++)
                {
                    var candidate = Kit.All[Rng.Next(Kit.All.Length)];
                    if (candidate.Weapon)
                    {
                        arms = candidate;
                        break;
                    }
                }
                string[,] rows =
                {
                    { Ru("ФАМИЛИЯ", "NAME"), "" },
                    { Ru("ПОЗЫВНОЙ", "CALLSIGN"), "«" + Callsigns[Rng.Next(Callsigns.Length)] + "»" },
                    { Ru("ПРИНАДЛЕЖНОСТЬ", "AFFILIATION"), Rng.Next(3) == 0 ? Ru("ДИКИЕ", "SCAV") : Theme.Name(enemy) },
                    { Ru("ВООРУЖЕНИЕ", "ARMED WITH"), Russian ? O.Cyr(arms.ShortRu) : arms.Short },
                    { Ru("ПОСЛЕДНИЙ РАЗ", "LAST SEEN"), Lore.Fill("{place}", Rng).ToUpperInvariant() },
                    { Ru("СТЕПЕНЬ УГРОЗЫ", "THREAT"), Ru(Rng.Next(2) == 0 ? "ВЫСОКАЯ" : "КРАЙНЕ ВЫСОКАЯ", Rng.Next(2) == 0 ? "HIGH" : "EXTREME") },
                    { Ru("СТАТУС", "STATUS"), Ru("В РОЗЫСКЕ", "WANTED") }
                };
                int count = rows.GetLength(0);
                float pitch = Mathf.Clamp((a.height - 24f) / count, 22f, 40f);
                Shape bars = Draw();
                for (int i = 0; i < count; i++)
                {
                    float y = a.yMax - 16f - pitch * (i + 0.5f);
                    if (y < a.yMin + 12f)
                    {
                        break;
                    }
                    Fit(Label(rows[i, 0], x + w * 0.5f, y + 7f, w, 7f, A(T.Dim, 200), TextAlignmentOptions.Left), 7f);
                    if (i == 0)
                    {
                        // The name is blacked out.
                        bars.RoundedRect(P(x + w * 0.3f, y - 5f), w * 0.6f * U, 9f * U, 0f, A(T.Text, 170));
                    }
                    else
                    {
                        bool warn = i >= count - 2;
                        Fit(Label(rows[i, 1], x + w * 0.5f, y - 5f, w, 10f, A(warn ? T.Warn : T.Text, 220), TextAlignmentOptions.Left), 10f);
                    }
                    bars.Line(P(x, y - pitch * 0.5f + 2f), P(x + w, y - pitch * 0.5f + 2f), U, A(T.Accent, 30));
                }

                // On the right, where there is room: when and where they were seen, what they are worth, who they run with.
                _sightings.Clear();
                if (side)
                {
                    float sx = x + w + 24f;
                    float sw = a.xMax - 12f - sx;
                    bars.Line(P(sx - 12f, a.yMin + 10f), P(sx - 12f, a.yMax - 10f), U, A(T.Accent, 40));
                    float top = a.yMax - 16f;
                    Fit(Label(Ru("НАБЛЮДЕНИЯ", "SIGHTINGS"), sx + sw * 0.5f, top, sw, 7f, A(T.Dim, 200), TextAlignmentOptions.Left), 7f);
                    int seen = Mathf.Clamp(Mathf.FloorToInt((a.height * 0.55f) / 18f), 2, 6);
                    int minute = Rng.Next(0, 300);
                    for (int i = 0; i < seen; i++)
                    {
                        minute += Rng.Next(25, 140);
                        string when = $"{(minute / 60) % 24:00}:{minute % 60:00}";
                        TextMeshProUGUI line = Fit(Label($"<color={T.AccentHex}>{when}</color>  {Lore.Fill("{place}", Rng).ToUpperInvariant()}", sx + sw * 0.5f, top - 16f - i * 18f, sw, 9f, A(T.Text, 210), TextAlignmentOptions.Left), 9f);
                        line.gameObject.SetActive(false);
                        _sightings.Add(line);
                    }
                    float low = a.yMin + 16f;
                    Fit(Label(Ru("ВОЗНАГРАЖДЕНИЕ", "BOUNTY"), sx + sw * 0.5f, low + 44f, sw, 7f, A(T.Dim, 200), TextAlignmentOptions.Left), 7f);
                    _bountyTo = Rng.Next(5, 60) * 10000;
                    _bounty = Label("", sx + sw * 0.5f, low + 28f, sw, 14f, A(T.Warn, 230), TextAlignmentOptions.Left);
                    Fit(Label(Ru("СВЯЗИ: ", "ASSOCIATES: ") + "«" + Callsigns[Rng.Next(Callsigns.Length)] + "», «" + Callsigns[Rng.Next(Callsigns.Length)] + "»", sx + sw * 0.5f, low + 4f, sw, 8f, A(T.Text, 190), TextAlignmentOptions.Left), 8f);
                }
                else
                {
                    _bounty = null;
                }
                bars.Commit();
            }

            private void AnimateDossier(float t)
            {
                float p = Progress;
                // The match box closes in on the face, the score climbing, as the load runs.
                _scan.Clear();
                // Closing from most of the photo onto the head, a little wider than it.
                Vector2 face = _face;
                float size = Mathf.Lerp(_photo.width * 0.9f, _faceSize * 1.35f, Mathf.Clamp01(p * 1.4f));
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
                // The sightings come in one by one, the bounty counts up.
                for (int i = 0; i < _sightings.Count; i++)
                {
                    bool show = p >= 0.1f + 0.8f * i / Mathf.Max(1, _sightings.Count);
                    if (_sightings[i].gameObject.activeSelf != show)
                    {
                        _sightings[i].gameObject.SetActive(show);
                    }
                }
                if (_bounty != null)
                {
                    int bounty = Mathf.RoundToInt(_bountyTo * Mathf.Clamp01(p * 1.2f) / 1000f) * 1000;
                    if (bounty != _shownBounty)
                    {
                        _shownBounty = bounty;
                        _bounty.text = (T.Faction == Faction.Bear ? "$ " : "₽ ") + bounty.ToString("N0");
                    }
                }
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
                        earlier.Append("> ").Append(O.Cyr(Lore.Fill(_lines[k % _lines.Length], Rng))).Append("\n");
                    }
                    _transcript.text = earlier.ToString();
                    _transcript.ForceMeshUpdate();
                    _typedFrom = _transcript.textInfo.characterCount;
                    _transcript.text = earlier + "<alpha=#FF><color=" + T.AccentHex + ">> </color>" + O.Cyr(Lore.Fill(_lines[_line % _lines.Length], Rng));
                    _transcript.ForceMeshUpdate();
                    _interceptReading.text = Ru("ЗАПИСЬ", "REC") + $" {_line + 1:000}";
                }
                _transcript.maxVisibleCharacters = _typedFrom + Mathf.FloorToInt((t - _lineAt) * 45f);
            }

            // ==== Imagery ========================================================================

            /// <summary>A piece of the aerial scene, in metres, with its bounds so what is out of view is skipped.</summary>
            private struct Patch
            {
                internal Vector2[] Points;
                internal Color32 Color;
                internal Rect Bounds;

                /// <summary>How warm it shows in infrared, 0 to 1; Cold for a shadow, Hidden for what infrared sees through.</summary>
                internal float Heat;
            }

            private const float Cold = -2f;
            private const float Hidden = -1f;

            /// <summary>Someone on the ground: a guard standing (A = B) or a patrol walking between A and B.</summary>
            private struct Walker
            {
                internal Vector2 A;
                internal Vector2 B;
                internal float Speed;
                internal float Phase;
            }

            private struct Vehicle
            {
                internal Vector2 Centre;
                internal Vector2 Forward;
                internal float Length;
                internal float Width;
                internal bool Armoured;
                internal bool Netted;
            }

            // Drawn bottom to top: ground cover, tracks and water, roads, shadows, trees, buildings, vehicles.
            private const int SceneLayers = 7;
            private const int Traffic = -1;
            private readonly List<Patch>[] _layers = new List<Patch>[SceneLayers];
            private readonly List<Patch> _scene = new List<Patch>();
            private readonly List<Vehicle> _parked = new List<Vehicle>();
            private readonly Vector2[][] _scratch = new Vector2[65][];
            private readonly TextMeshProUGUI[] _tags = new TextMeshProUGUI[6];
            private readonly Vector2[] _moverAt = new Vector2[2];
            private Shape _world;
            private Shape _traffic;
            private Shape _reticle;
            private TextMeshProUGUI _imageReading;
            private TextMeshProUGUI _imageData;
            private TextMeshProUGUI _scaleLabel;
            private TextMeshProUGUI _targetLabel;
            private Vector2[] _road;
            private float[] _roadAt;
            private Vector2 _site;
            private Vector2 _siteX;
            private Vector2 _siteY;
            private Vector2 _worldSize;
            private Vector2 _sun;
            private float _metre;
            private float _drawnScale;
            private Vector2 _drawnCentre;
            private float _viewScale;
            private Vector2 _viewCentre;
            private float _moverStart;
            private string _gridRef;
            private int _shownZoom = -1;
            private int _shownScale = -1;
            private float _heat;
            private float _imageryAt = -1f;
            private bool _infrared;
            private bool _drawnInfrared;
            private float _infraredAt = -1f;
            private Image _groundBox;

            // The fight on the ground, drawn as a raid review would: a friendly team closing on the
            // compound, its defenders, tracers both ways, now and then an explosion, the fallen marked.
            private Vector2 _roadSide;
            private Vector2 _teamFrom;
            private readonly Vector2[] _teamTo = new Vector2[4];
            private float[] _fallsAt = new float[0];
            private TextMeshProUGUI _combatLabel;
            private int _shownCombat = -1;
            private static readonly Color32 Tracer = new Color32(255, 214, 120, 235);
            private readonly List<Walker> _walkers = new List<Walker>();
            private readonly List<Vector2> _noteAt = new List<Vector2>();
            private readonly List<TextMeshProUGUI> _notes = new List<TextMeshProUGUI>();

            private Color32 Ground => A(Color32.Lerp(T.Ink, new Color32(0, 0, 0, 255), 0.35f), 255);

            /// <summary>An opaque colour <paramref name="k"/> of the way from the ground to <paramref name="c"/>: overlaps never show.</summary>
            private Color32 Mix(Color32 c, float k) => A(Color32.Lerp(Ground, A(c, 255), k), 255);

            private static readonly Color32 Shadow = new Color32(0, 0, 0, 150);

            private Color32 Pit => A(Color32.Lerp(Ground, new Color32(0, 0, 0, 255), 0.55f), 255);

            /// <summary>White-hot: the warmer, the whiter, with a breath of the unit's colour.</summary>
            private Color32 HeatColour(float heat)
            {
                byte v = (byte)(Mathf.Clamp01(heat) * 255f);
                return A(Color32.Lerp(new Color32(v, v, v, 255), A(T.Accent, 255), 0.14f), 255);
            }

            private void BuildImagery(Rect zone)
            {
                _panel = PanelFrame(zone, Ru($"АЭРОФОТОСНИМОК  //  КАДР {Code(4)}", $"IMAGERY  //  FRAME {Code(5)}  //  MQ-9"), out _imageReading);
                Rect a = _panel;
                _groundBox = Box(a.center.x, a.center.y, a.width, a.height, Ground);
                RectTransform clip = Clip(a);

                // The scene, half a kilometre or so across, the panel's shape.
                float wide = Range(520f, 660f);
                _worldSize = new Vector2(wide, wide * a.height / a.width);
                _metre = a.width / wide;
                _sun = new Vector2(Range(0.5f, 0.8f), -Range(0.4f, 0.7f)).normalized;
                for (int i = 0; i < SceneLayers; i++)
                {
                    _layers[i] = new List<Patch>();
                }
                Scene();
                _scene.Clear();
                foreach (List<Patch> layer in _layers)
                {
                    _scene.AddRange(layer);
                    layer.Clear();
                }
                _world = Draw(-a.center.x, -a.center.y, clip);
                _traffic = Draw(-a.center.x, -a.center.y, clip);

                // The team starts well out on the far side from the road and ends stacked on the back wire.
                Vector2 from = _site - _roadSide * Range(150f, 200f) + _siteX * Range(-60f, 60f);
                _teamFrom = new Vector2(Mathf.Clamp(from.x, 20f, _worldSize.x - 20f), Mathf.Clamp(from.y, 20f, _worldSize.y - 20f));
                for (int i = 0; i < _teamTo.Length; i++)
                {
                    _teamTo[i] = _site - _roadSide * Range(24f, 30f) + _siteX * (-15f + i * 10f);
                }
                // The defenders fall one at a time over the second half of the load.
                _fallsAt = new float[_walkers.Count];
                for (int i = 0; i < _fallsAt.Length; i++)
                {
                    _fallsAt[i] = i < 5 ? 0.45f + i * 0.09f + Range(0f, 0.04f) : 9f;
                }
                for (int i = _fallsAt.Length - 1; i > 0; i--)
                {
                    int j = Rng.Next(i + 1);
                    (_fallsAt[i], _fallsAt[j]) = (_fallsAt[j], _fallsAt[i]);
                }
                _moverStart = Range(0f, 400f);
                _drawnScale = 0f;

                // The frame's fixed marks: fiducial crosses in the corners, ticks along the edges, north.
                Shape marks = Draw();
                Color32 mark = A(T.Text, 120);
                foreach (var corner in new[] { new Vector2(a.xMin + 14f, a.yMin + 14f), new Vector2(a.xMax - 14f, a.yMin + 14f), new Vector2(a.xMin + 14f, a.yMax - 14f), new Vector2(a.xMax - 14f, a.yMax - 14f) })
                {
                    marks.Line(P(corner.x - 5f, corner.y), P(corner.x + 5f, corner.y), U, mark);
                    marks.Line(P(corner.x, corner.y - 5f), P(corner.x, corner.y + 5f), U, mark);
                }
                for (int k = 1; k < 10; k++)
                {
                    float x = Mathf.Lerp(a.xMin, a.xMax, k / 10f);
                    float y = Mathf.Lerp(a.yMin, a.yMax, k / 10f);
                    float len = k == 5 ? 8f : 4f;
                    marks.Line(P(x, a.yMin), P(x, a.yMin + len), U, mark);
                    marks.Line(P(x, a.yMax), P(x, a.yMax - len), U, mark);
                    marks.Line(P(a.xMin, y), P(a.xMin + len, y), U, mark);
                    marks.Line(P(a.xMax, y), P(a.xMax - len, y), U, mark);
                }
                Vector2 north = new Vector2(a.xMax - 26f, a.yMax - 34f);
                marks.Tri(P(north.x, north.y + 9f), P(north.x - 4.5f, north.y - 6f), P(north.x, north.y - 3f), A(T.Text, 210));
                marks.Tri(P(north.x, north.y + 9f), P(north.x, north.y - 3f), P(north.x + 4.5f, north.y - 6f), A(T.Text, 110));
                marks.Commit();
                Label(Ru("С", "N"), north.x, north.y + 17f, 20f, 8f, A(T.Text, 210), TextAlignmentOptions.Center);

                // The analyst's readings, on dark strips so the picture never swallows them.
                _gridRef = $"{Rng.Next(30, 40)}U {(char)('C' + Rng.Next(20))}{(char)('M' + Rng.Next(12))} {Rng.Next(10000, 99999)} {Rng.Next(10000, 99999)}";
                float dataW = Mathf.Min(a.width * 0.62f, 300f);
                Box(a.xMin + 6f + dataW * 0.5f, a.yMin + 12f, dataW, 14f, A(Dark, 200));
                _imageData = Label("", a.xMin + 10f + dataW * 0.5f, a.yMin + 12f, dataW - 8f, 8f, A(T.Text, 210), TextAlignmentOptions.Left);
                _scaleLabel = Label("", a.xMax - 44f, a.yMin + 26f, 60f, 8f, A(T.Text, 210), TextAlignmentOptions.Right);
                _reticle = Draw();
                _targetLabel = Label(Ru($"ОБЪЕКТ {Code(3)}", $"OBJECTIVE {Code(3)}"), 0f, 0f, 120f, 8f, A(T.Text, 220), TextAlignmentOptions.Left);
                _combatLabel = Label("", a.xMin + 12f + 110f, a.yMax - 30f, 220f, 8f, A(T.Warn, 230), TextAlignmentOptions.Left);
                for (int i = 0; i < _tags.Length; i++)
                {
                    bool mover = i >= 4;
                    string text = mover
                        ? Ru($"ДВИЖ {i - 3:00}", $"MOVER {i - 3:00}")
                        : i >= _parked.Count ? ""
                        : _parked[i].Netted ? Ru($"{i + 1:00} МАСКСЕТЬ", $"{i + 1:00} CAMO NET")
                        : _parked[i].Armoured ? Ru($"{i + 1:00} БТР", $"{i + 1:00} APC") : Ru($"{i + 1:00} ГРУЗ", $"{i + 1:00} TRUCK");
                    _tags[i] = Label(text, 0f, 0f, 80f, 7f, A(mover ? T.Accent : T.Warn, 230), TextAlignmentOptions.Left);
                    _tags[i].gameObject.SetActive(false);
                }
                // The analyst's notes on the objective, for the last, closest look.
                string[] notes =
                {
                    Ru("КАЗАРМА", "BARRACKS"), Ru("БОКС ТЕХНИКИ", "VEHICLE SHED"), Ru("СКЛАД", "STORES"),
                    Ru($"Л/С: {_walkers.Count}", $"PAX: {_walkers.Count}"), Ru("АНТЕННА", "ANTENNA MAST")
                };
                for (int i = 0; i < _noteAt.Count && i < notes.Length; i++)
                {
                    TextMeshProUGUI note = Label(notes[i], 0f, 0f, 90f, 7f, A(T.Text, 230), TextAlignmentOptions.Left);
                    note.gameObject.SetActive(false);
                    _notes.Add(note);
                }
            }

            /// <summary>The scene: fields, hedges and a wood, a river, a road through a village, and the objective, a wired compound with its vehicles.</summary>
            private void Scene()
            {
                float W = _worldSize.x, H = _worldSize.y;
                _site = new Vector2(W * Range(0.38f, 0.62f), H * Range(0.4f, 0.6f));
                float turn = Range(-28f, 28f) * Mathf.Deg2Rad;
                _siteX = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn));
                _siteY = new Vector2(-_siteX.y, _siteX.x);
                float side = Rng.Next(2) == 0 ? -1f : 1f;
                _roadSide = _siteY * side;

                // The road, passing the compound a little way off.
                _road = Wander(new Vector2(-20f, _site.y + side * Range(60f, 85f) + Range(-25f, 25f)), new Vector2(W + 20f, _site.y + side * Range(60f, 85f) + Range(-25f, 25f)), 0.05f, 28);
                float off = PathDistance(_road, _site);
                if (off < 58f)
                {
                    for (int i = 0; i < _road.Length; i++)
                    {
                        _road[i].y += side * (58f - off);
                    }
                }
                _roadAt = new float[_road.Length];
                for (int i = 1; i < _road.Length; i++)
                {
                    _roadAt[i] = _roadAt[i - 1] + (_road[i] - _road[i - 1]).magnitude;
                }

                // A river on the far side from the compound, most of the time.
                Vector2[] river = null;
                if (Rng.Next(100) < 75)
                {
                    float rx = _site.x < W * 0.5f ? Range(0.78f, 0.92f) * W : Range(0.08f, 0.22f) * W;
                    river = Wander(new Vector2(rx + Range(-40f, 40f), H + 20f), new Vector2(rx + Range(-40f, 40f), -20f), 0.1f, 22);
                }

                // Fields on a rough lattice, with tracks between them; bare ground round the compound.
                int nx = Mathf.Max(3, Mathf.RoundToInt(W / 95f));
                int ny = Mathf.Max(2, Mathf.RoundToInt(H / 95f));
                var corners = new Vector2[nx + 1, ny + 1];
                for (int i = 0; i <= nx; i++)
                {
                    for (int j = 0; j <= ny; j++)
                    {
                        float x = i == 0 ? -20f : i == nx ? W + 20f : i * W / nx + Range(-22f, 22f);
                        float y = j == 0 ? -20f : j == ny ? H + 20f : j * H / ny + Range(-22f, 22f);
                        corners[i, j] = new Vector2(x, y);
                    }
                }
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < ny; j++)
                    {
                        Vector2[] q = { corners[i, j], corners[i + 1, j], corners[i + 1, j + 1], corners[i, j + 1] };
                        Vector2 m = (q[0] + q[1] + q[2] + q[3]) * 0.25f;
                        for (int k = 0; k < 4; k++)
                        {
                            q[k] = Vector2.MoveTowards(q[k], m, 4f);
                        }
                        if ((m - _site).magnitude < 70f)
                        {
                            // Churned ground round the objective, crossed by tyre tracks.
                            _heat = 0.27f;
                            Add(0, q, Mix(T.Text, Range(0.09f, 0.13f)));
                            _heat = 0.31f;
                            for (int k = Rng.Next(1, 3); k > 0; k--)
                            {
                                Vector2 from = Vector2.Lerp(q[0], q[3], Range(0.2f, 0.8f));
                                Vector2 to = Vector2.Lerp(q[1], q[2], Range(0.2f, 0.8f));
                                Vector2 n = new Vector2(-(to - from).y, (to - from).x).normalized * 1.1f;
                                Vector2[] path = Wander(from, to, 0.06f, 8);
                                for (int e = 0; e < path.Length; e++)
                                {
                                    path[e] += n;
                                }
                                AddPath(0, path, 0.45f, Mix(T.Text, 0.2f));
                                for (int e = 0; e < path.Length; e++)
                                {
                                    path[e] -= n * 2f;
                                }
                                AddPath(0, path, 0.45f, Mix(T.Text, 0.2f));
                            }
                            continue;
                        }
                        float tone = Range(0.07f, 0.2f);
                        Color32 field = Rng.Next(5) == 0 ? Mix(T.Text, tone * 0.6f) : Mix(T.Accent, tone);
                        _heat = Range(0.17f, 0.3f);
                        Add(0, q, field);
                        if (Rng.Next(100) < 50)
                        {
                            // Furrows or tramlines, one way or the other across the field.
                            bool across = Rng.Next(2) == 0;
                            Vector2 a0 = q[0], a1 = across ? q[1] : q[3], b0 = across ? q[3] : q[1], b1 = q[2];
                            int n = Mathf.Clamp(Mathf.RoundToInt((b0 - a0).magnitude / 6f), 4, 16);
                            Color32 furrow = Mix(T.Accent, tone + 0.05f);
                            _heat += 0.04f;
                            for (int k = 1; k < n; k++)
                            {
                                AddLine(0, Vector2.Lerp(a0, b0, k / (float)n), Vector2.Lerp(a1, b1, k / (float)n), 0.5f, furrow);
                            }
                        }
                    }
                }

                // Hedgerows along some of the field edges.
                for (int i = 0; i <= nx; i++)
                {
                    for (int j = 0; j <= ny; j++)
                    {
                        if (i < nx && Rng.Next(100) < 22)
                        {
                            Hedge(corners[i, j], corners[i + 1, j], river);
                        }
                        if (j < ny && Rng.Next(100) < 22)
                        {
                            Hedge(corners[i, j], corners[i, j + 1], river);
                        }
                    }
                }

                // A wood, away from the compound.
                Vector2 wood = new Vector2(_site.x < W * 0.5f ? W * Range(0.6f, 0.85f) : W * Range(0.15f, 0.4f), _site.y < H * 0.5f ? H * Range(0.72f, 0.95f) : H * Range(0.05f, 0.28f));
                for (int k = 0; k < 140; k++)
                {
                    float r = Mathf.Sqrt((float)Rng.NextDouble());
                    float angle = Range(0f, Mathf.PI * 2f);
                    Vector2 p = wood + new Vector2(Mathf.Cos(angle) * 90f, Mathf.Sin(angle) * 60f) * r;
                    if (PathDistance(_road, p) > 8f && (river == null || PathDistance(river, p) > 15f) && (p - _site).magnitude > 60f)
                    {
                        Tree(p, Range(2.8f, 5f));
                    }
                }

                // Dirt tracks from the road out into the fields.
                for (int k = Rng.Next(2, 4); k > 0; k--)
                {
                    Vector2 from = _road[Rng.Next(3, _road.Length - 3)];
                    Vector2 to = new Vector2(Range(0.05f, 0.95f) * W, from.y < _site.y ? Range(-10f, H * 0.2f) : Range(H * 0.8f, H + 10f));
                    if (PathDistance(new[] { from, to }, _site) > 40f)
                    {
                        _heat = 0.27f;
                        AddPath(1, Wander(from, to, 0.08f, 10), 3.2f, Mix(T.Text, 0.16f));
                    }
                }

                // The river and its banks, a bridge where the road crosses.
                if (river != null)
                {
                    _heat = 0.16f;
                    AddPath(1, river, 23f, Mix(T.Accent, 0.24f));
                    _heat = 0.05f;
                    AddPath(1, river, 17f, Pit);
                    for (int i = 0; i + 1 < _road.Length; i++)
                    {
                        for (int j = 0; j + 1 < river.Length; j++)
                        {
                            if (Crossing(_road[i], _road[i + 1], river[j], river[j + 1], out Vector2 at))
                            {
                                Vector2 dir = (_road[i + 1] - _road[i]).normalized;
                                Vector2 n = new Vector2(-dir.y, dir.x);
                                _heat = 0.4f;
                                AddLine(2, at - dir * 15f, at + dir * 15f, 10f, Mix(T.Text, 0.45f));
                                AddLine(2, at - dir * 15f + n * 4.6f, at + dir * 15f + n * 4.6f, 0.7f, Mix(T.Text, 0.85f));
                                AddLine(2, at - dir * 15f - n * 4.6f, at + dir * 15f - n * 4.6f, 0.7f, Mix(T.Text, 0.85f));
                            }
                        }
                    }
                }

                // The road, its centre line dashed.
                _heat = 0.36f;
                AddPath(2, _road, 8f, Mix(T.Text, 0.34f));
                _heat = 0.42f;
                Color32 dash = Mix(T.Text, 0.7f);
                for (float d = 4f; d + 3.5f < _roadAt[_roadAt.Length - 1]; d += 9f)
                {
                    RoadPoint(d, out Vector2 p0, out _);
                    RoadPoint(d + 3.5f, out Vector2 p1, out _);
                    AddLine(2, p0, p1, 0.3f, dash);
                }

                Village(river);
                Compound(side);

                // Old shell craters round the objective.
                if (Rng.Next(100) < 60)
                {
                    for (int k = Rng.Next(8, 15); k > 0; k--)
                    {
                        float angle = Range(0f, Mathf.PI * 2f);
                        Vector2 p = _site + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(42f, 120f);
                        if (PathDistance(_road, p) < 9f || (river != null && PathDistance(river, p) < 16f))
                        {
                            continue;
                        }
                        float r = Range(1.8f, 4.2f);
                        _heat = 0.24f;
                        Add(1, Disc(p, r * 1.5f, 10), Mix(T.Text, 0.22f));
                        _heat = 0.1f;
                        Add(1, Disc(p + _sun * r * 0.15f, r, 10), Pit);
                    }
                }
            }

            private void Hedge(Vector2 from, Vector2 to, Vector2[] river)
            {
                if (((from + to) * 0.5f - _site).magnitude < 75f)
                {
                    return;
                }
                int count = Mathf.FloorToInt((to - from).magnitude / 6.5f);
                for (int k = 0; k <= count; k++)
                {
                    Vector2 p = Vector2.Lerp(from, to, k / (float)Mathf.Max(1, count)) + new Vector2(Range(-1.6f, 1.6f), Range(-1.6f, 1.6f));
                    if (Rng.Next(100) < 80 && PathDistance(_road, p) > 7f && (river == null || PathDistance(river, p) > 14f))
                    {
                        Tree(p, Range(2f, 3.6f));
                    }
                }
            }

            /// <summary>A tree from above: its shadow, its crown, and the sunlit side of the crown.</summary>
            private void Tree(Vector2 p, float r)
            {
                _heat = Cold;
                Add(3, Disc(p + _sun * r * 0.9f, r, 8), Shadow);
                _heat = 0.12f;
                Add(4, Disc(p, r, 8), Mix(T.Accent, Range(0.3f, 0.42f)));
                _heat = 0.15f;
                Add(4, Disc(p - _sun * r * 0.3f, r * 0.55f, 7), Mix(T.Accent, Range(0.5f, 0.62f)));
            }

            /// <summary>A handful of houses with their gardens along the road, somewhere away from the compound.</summary>
            private void Village(Vector2[] river)
            {
                for (int attempt = 0; attempt < 14; attempt++)
                {
                    float at = Range(60f, _roadAt[_roadAt.Length - 1] - 60f);
                    RoadPoint(at, out Vector2 p, out _);
                    if ((p - _site).magnitude < 170f || (river != null && PathDistance(river, p) < 60f) || p.x < 30f || p.x > _worldSize.x - 30f)
                    {
                        continue;
                    }
                    for (int k = Rng.Next(5, 9); k > 0; k--)
                    {
                        RoadPoint(at + Range(-70f, 70f), out Vector2 q, out Vector2 dir);
                        Vector2 n = new Vector2(-dir.y, dir.x) * (Rng.Next(2) == 0 ? 1f : -1f);
                        float back = Range(14f, 22f);
                        Vector2 house = q + n * back;
                        if (river != null && PathDistance(river, house) < 22f)
                        {
                            continue;
                        }
                        _heat = 0.22f;
                        Add(0, Footprint(house + n * 9f, dir, Range(14f, 18f), Range(12f, 16f)), Mix(T.Accent, Range(0.2f, 0.3f)));
                        Building(house, dir, Range(8.5f, 12f), Range(6.5f, 8.5f), true);
                        if (Rng.Next(100) < 50)
                        {
                            Building(house + n * 9f + dir * Range(-4f, 4f), dir, Range(3f, 5f), Range(3f, 4f), false);
                        }
                    }
                    return;
                }
            }

            /// <summary>The objective: a fenced, gravelled yard with a barracks, a garage, containers, a guard hut, and a row of vehicles facing the gate.</summary>
            private void Compound(float side)
            {
                // Its own frame: x along the compound, y toward the road side; the gate in the road side.
                Vector2 L(float x, float y) => _site + _siteX * x + _siteY * (y * side);
                Vector2 up = _siteY * side;
                _heat = 0.3f;
                Add(1, Footprint(_site, _siteX, 52f, 38f), Mix(T.Text, 0.15f));

                // The track in from the road to the gate.
                Vector2 gate = L(0f, 16f);
                Vector2 near = NearestOnPath(_road, gate);
                Vector2 bend = Vector2.Lerp(near, gate, 0.5f) + _siteX * Range(-12f, 12f);
                AddPath(1, Curve(near, bend, gate + up * -4f, 12), 5.5f, Mix(T.Text, 0.26f));

                // Wire, with the gate's gap.
                Color32 wire = Mix(T.Text, 0.75f);
                _heat = 0.35f;
                AddLine(5, L(-23f, -16f), L(23f, -16f), 0.5f, wire);
                AddLine(5, L(-23f, -16f), L(-23f, 16f), 0.5f, wire);
                AddLine(5, L(23f, -16f), L(23f, 16f), 0.5f, wire);
                AddLine(5, L(-23f, 16f), L(-3.5f, 16f), 0.5f, wire);
                AddLine(5, L(3.5f, 16f), L(23f, 16f), 0.5f, wire);
                Building(L(-21f, -14f), _siteX, 2.6f, 2.6f, false);
                Building(L(21f, 14f), _siteX, 2.6f, 2.6f, false);

                // Buildings at the back, a hut by the gate.
                Vector2 barracks = L(Range(-11f, -9f), -10.5f);
                Vector2 shed = L(Range(10f, 12f), -9.5f);
                Building(barracks, _siteX, Range(17f, 20f), 7f, true);
                Building(shed, _siteX, 11f, 9f, false);
                Building(L(-8f, 12.5f), _siteX, 4f, 3f, false);
                _noteAt.Clear();
                _noteAt.Add(barracks);
                _noteAt.Add(shed);
                _noteAt.Add(L(14f, 6f));

                // Outside the wire: a trench along the back, sandbagged posts at two corners.
                Vector2[] trench = new Vector2[12];
                for (int k = 0; k < trench.Length; k++)
                {
                    trench[k] = L(-26f + k * 52f / (trench.Length - 1), -20.5f + (k % 2 == 0 ? -1.6f : 1.6f));
                }
                _heat = 0.14f;
                AddPath(1, trench, 1.3f, Pit);
                foreach (Vector2 post in new[] { L(-27f, 19f), L(27f, 19f) })
                {
                    _heat = 0.32f;
                    Add(1, Disc(post, 2.6f, 12), Mix(T.Text, 0.42f));
                    _heat = 0.15f;
                    Add(1, Disc(post, 1.5f, 10), Pit);
                }

                // USEC fly in: a helipad. BEAR dig in: mortar pits, tubes toward the road.
                Vector2 beyond = L(Range(-6f, 6f), -36f);
                if (T.Faction == Faction.Usec)
                {
                    _heat = 0.34f;
                    Add(1, Disc(beyond, 9f, 24), Mix(T.Text, 0.36f));
                    _heat = 0.3f;
                    Add(1, Disc(beyond, 8.2f, 24), Mix(T.Text, 0.2f));
                    _heat = 0.45f;
                    AddLine(1, beyond + _siteX * -2.6f + _siteY * -3.4f, beyond + _siteX * -2.6f + _siteY * 3.4f, 1f, Mix(T.Text, 0.8f));
                    AddLine(1, beyond + _siteX * 2.6f + _siteY * -3.4f, beyond + _siteX * 2.6f + _siteY * 3.4f, 1f, Mix(T.Text, 0.8f));
                    AddLine(1, beyond + _siteX * -2.6f, beyond + _siteX * 2.6f, 1f, Mix(T.Text, 0.8f));
                }
                else
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        Vector2 pit = beyond + _siteX * (k * 9f);
                        _heat = 0.3f;
                        Add(1, Disc(pit, 3.2f, 14), Mix(T.Text, 0.4f));
                        _heat = 0.12f;
                        Add(1, Disc(pit, 2.2f, 12), Pit);
                        _heat = 0.5f;
                        AddLine(5, pit, pit + up * 1.4f, 0.35f, Mix(T.Text, 0.85f));
                    }
                }

                // An antenna mast in the yard: from above, a dot and a long, thin shadow.
                Vector2 mast = L(-1f, -4f);
                _heat = Cold;
                AddLine(3, mast, mast + _sun * 16f, 0.3f, Shadow);
                _heat = 0.35f;
                for (int k = 0; k < 3; k++)
                {
                    float angle = k * Mathf.PI * 2f / 3f + 0.4f;
                    AddLine(5, mast, mast + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5f, 0.12f, Mix(T.Text, 0.55f));
                }
                _heat = 0.5f;
                Add(5, Disc(mast, 0.6f, 8), Mix(T.Text, 0.9f));

                // Shipping containers in a stack, side by side.
                for (int k = Rng.Next(2, 4); k > 0; k--)
                {
                    Vector2 c = L(14f, 2f + k * 3.1f);
                    _heat = Cold;
                    Add(3, Footprint(c + _sun * 1.6f, _siteX, 6f, 2.4f), Shadow);
                    _heat = 0.42f;
                    Add(5, Footprint(c, _siteX, 6f, 2.4f), Mix(Rng.Next(2) == 0 ? T.Accent : T.Text, Range(0.45f, 0.65f)));
                }

                // The vehicles, nose to the gate.
                _parked.Clear();
                int count = Rng.Next(3, 6);
                bool armoured = Rng.Next(2) == 0;
                int netted = Rng.Next(100) < 60 ? Rng.Next(count) : -1;
                for (int k = 0; k < count; k++)
                {
                    var v = new Vehicle
                    {
                        Centre = L(-18f + k * 5.2f, 5f),
                        Forward = up,
                        Armoured = armoured ? Rng.Next(100) < 70 : Rng.Next(100) < 25,
                        Netted = k == netted
                    };
                    v.Length = v.Armoured ? 7f : 7.4f;
                    v.Width = v.Armoured ? 3f : 2.5f;
                    _parked.Add(v);
                    AddVehicle(3, 6, v);
                    if (v.Netted)
                    {
                        // A camouflage net over it: in the visible a shapeless patch; infrared sees the engine through it.
                        var net = new Vector2[10];
                        for (int e = 0; e < net.Length; e++)
                        {
                            float angle = e * Mathf.PI * 2f / net.Length;
                            net[e] = v.Centre + new Vector2(Mathf.Cos(angle) * Range(4.6f, 5.2f), Mathf.Sin(angle) * Range(4.6f, 5.2f));
                        }
                        _heat = Hidden;
                        Add(6, net, Mix(T.Accent, 0.36f));
                        for (int e = 0; e < 6; e++)
                        {
                            Add(6, Disc(v.Centre + new Vector2(Range(-3f, 3f), Range(-3f, 3f)), Range(0.8f, 1.6f), 7), Mix(T.Accent, Range(0.45f, 0.55f)));
                        }
                    }
                }

                // Its people: guards at the gate, a patrol along the wire, a crew at the vehicles.
                _walkers.Clear();
                _walkers.Add(new Walker { A = L(-2.5f, 17.5f), B = L(-2.5f, 17.5f) });
                _walkers.Add(new Walker { A = L(2.5f, 17.5f), B = L(2.5f, 17.5f) });
                _walkers.Add(new Walker { A = L(-21f, -13.5f), B = L(21f, -13.5f), Speed = 1.3f, Phase = Range(0f, 10f) });
                _walkers.Add(new Walker { A = L(-21f, -12.6f), B = L(21f, -12.6f), Speed = 1.3f, Phase = Range(0f, 10f) });
                for (int k = Rng.Next(1, 4); k > 0; k--)
                {
                    Vector2 at = L(Range(-18f, 4f), Range(0f, 1.5f));
                    _walkers.Add(new Walker { A = at, B = at + _siteX * Range(-6f, 6f), Speed = 0.9f, Phase = Range(0f, 10f) });
                }
                _noteAt.Add(L(-12f, 2f));
                _noteAt.Add(mast);
            }

            /// <summary>A building from above: its shadow, then a pitched roof (one slope in the sun) or a flat one with its parapet.</summary>
            private void Building(Vector2 centre, Vector2 along, float length, float width, bool pitched)
            {
                Vector2 across = new Vector2(-along.y, along.x);
                _heat = Cold;
                Add(3, Footprint(centre + _sun * Mathf.Min(length, width) * 0.4f, along, length, width), Shadow);
                _heat = 0.46f;
                if (pitched)
                {
                    bool firstLit = Vector2.Dot(across, -_sun) > 0f;
                    Add(5, Footprint(centre + across * width * 0.25f, along, length, width * 0.5f), Mix(T.Text, firstLit ? 0.72f : 0.42f));
                    Add(5, Footprint(centre - across * width * 0.25f, along, length, width * 0.5f), Mix(T.Text, firstLit ? 0.42f : 0.72f));
                    AddLine(5, centre - along * length * 0.5f, centre + along * length * 0.5f, 0.35f, Mix(T.Text, 0.9f));
                }
                else
                {
                    _heat = 0.5f;
                    Add(5, Footprint(centre, along, length, width), Mix(T.Text, 0.62f));
                    _heat = 0.56f;
                    Add(5, Footprint(centre, along, length - 1f, width - 1f), Mix(T.Text, 0.5f));
                }
            }

            /// <summary>A truck (cab, tarpaulin) or an armoured carrier (hull, turret, gun), its shadow on <paramref name="shadowLayer"/>.</summary>
            private void AddVehicle(int shadowLayer, int layer, Vehicle v)
            {
                Vector2 f = v.Forward;
                _heat = Cold;
                Add(shadowLayer, Footprint(v.Centre + _sun * 1.4f, f, v.Length, v.Width), Shadow);
                if (v.Armoured)
                {
                    _heat = 0.58f;
                    Add(layer, Footprint(v.Centre, f, v.Length, v.Width), Mix(T.Text, 0.55f));
                    // The engine deck at the back: the hottest thing in the picture.
                    _heat = 0.97f;
                    Add(layer, Footprint(v.Centre - f * (v.Length * 0.5f - 1.1f), f, 2f, v.Width - 0.8f), Mix(T.Text, 0.48f));
                    _heat = 0.62f;
                    Add(layer, Footprint(v.Centre + f * (v.Length * 0.5f - 0.6f), f, 1.2f, v.Width - 0.6f), Mix(T.Text, 0.42f));
                    Add(layer, Disc(v.Centre + f * 0.3f, 1f, 8), Mix(T.Text, 0.85f));
                    AddLine(layer, v.Centre + f * 0.3f, v.Centre + f * 3.8f, 0.25f, Mix(T.Text, 0.85f));
                }
                else
                {
                    _heat = 0.48f;
                    Add(layer, Footprint(v.Centre - f * 0.9f, f, v.Length - 2.2f, v.Width), Mix(T.Accent, 0.55f));
                    _heat = 0.95f;
                    Add(layer, Footprint(v.Centre + f * (v.Length * 0.5f - 1.1f), f, 2f, v.Width * 0.92f), Mix(T.Text, 0.85f));
                }
            }

            // -- the scene's geometry, in metres ---------------------------------------------------

            /// <summary>A rectangle centred on <paramref name="centre"/>, <paramref name="length"/> along <paramref name="along"/>.</summary>
            private static Vector2[] Footprint(Vector2 centre, Vector2 along, float length, float width)
            {
                Vector2 a = along * (length * 0.5f);
                Vector2 b = new Vector2(-along.y, along.x) * (width * 0.5f);
                return new[] { centre - a - b, centre + a - b, centre + a + b, centre - a + b };
            }

            private static Vector2[] Disc(Vector2 centre, float radius, int sides)
            {
                var points = new Vector2[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                return points;
            }

            private void AddLine(int layer, Vector2 a, Vector2 b, float width, Color32 color)
            {
                Vector2 d = b - a;
                if (d.sqrMagnitude < 1e-6f)
                {
                    return;
                }
                Vector2 side = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
                Add(layer, new[] { a - side, b - side, b + side, a + side }, color);
            }

            /// <summary>A path of even width; the colours here are opaque, so its joints are round discs that never show.</summary>
            private void AddPath(int layer, Vector2[] path, float width, Color32 color)
            {
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    AddLine(layer, path[i], path[i + 1], width, color);
                    if (i > 0)
                    {
                        Add(layer, Disc(path[i], width * 0.5f, 10), color);
                    }
                }
            }

            /// <summary>Into the scene, or with <see cref="Traffic"/> straight onto the moving traffic in the current view.</summary>
            private void Add(int layer, Vector2[] points, Color32 color)
            {
                if (layer == Traffic)
                {
                    if (!_infrared || _heat != Hidden)
                    {
                        _traffic.Polygon(Project(points, _viewCentre, _viewScale), Shade(color, _heat));
                    }
                    return;
                }
                Vector2 min = points[0], max = points[0];
                foreach (Vector2 p in points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                _layers[layer].Add(new Patch { Points = points, Color = color, Heat = _heat, Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y) });
            }

            /// <summary>The colour to draw in the current sensor: as seen, or by heat in infrared.</summary>
            private Color32 Shade(Color32 color, float heat) =>
                !_infrared ? color : heat == Cold ? new Color32(0, 0, 0, 90) : HeatColour(heat);

            /// <summary>Scene metres to the shape's units for a view centred on <paramref name="centre"/> at <paramref name="scale"/> panel units a metre.</summary>
            private Vector2[] Project(Vector2[] points, Vector2 centre, float scale)
            {
                Vector2[] out_ = _scratch[points.Length] ?? (_scratch[points.Length] = new Vector2[points.Length]);
                Vector2 middle = _panel.center;
                for (int i = 0; i < points.Length; i++)
                {
                    out_[i] = (middle + (points[i] - centre) * scale) * U;
                }
                return out_;
            }

            private Vector2 ToPanel(Vector2 p) => _panel.center + (p - _viewCentre) * _viewScale;

            private void RoadPoint(float distance, out Vector2 point, out Vector2 direction)
            {
                int i = 1;
                while (i < _roadAt.Length - 1 && _roadAt[i] < distance)
                {
                    i++;
                }
                float span = Mathf.Max(0.001f, _roadAt[i] - _roadAt[i - 1]);
                direction = (_road[i] - _road[i - 1]) / span;
                point = Vector2.Lerp(_road[i - 1], _road[i], Mathf.Clamp01((distance - _roadAt[i - 1]) / span));
            }

            private static Vector2 NearestOnPath(Vector2[] path, Vector2 p)
            {
                Vector2 best = path[0];
                float bestD = float.MaxValue;
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    Vector2 d = path[i + 1] - path[i];
                    float f = Mathf.Clamp01(Vector2.Dot(p - path[i], d) / Mathf.Max(1e-6f, d.sqrMagnitude));
                    Vector2 q = path[i] + d * f;
                    float dist = (q - p).sqrMagnitude;
                    if (dist < bestD)
                    {
                        bestD = dist;
                        best = q;
                    }
                }
                return best;
            }

            private static float PathDistance(Vector2[] path, Vector2 p) => (NearestOnPath(path, p) - p).magnitude;

            private static bool Crossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
            {
                at = default;
                Vector2 r = b - a, s = d - c;
                float den = r.x * s.y - r.y * s.x;
                if (Mathf.Abs(den) < 1e-6f)
                {
                    return false;
                }
                float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / den;
                float u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / den;
                if (t < 0f || t > 1f || u < 0f || u > 1f)
                {
                    return false;
                }
                at = a + r * t;
                return true;
            }

            /// <summary>The scene for a view, crisp at its scale; only what is near the view.</summary>
            private void DrawWorld(Vector2 centre, float scale)
            {
                Rect a = _panel;
                Vector2 half = new Vector2(a.width, a.height) * (0.6f / scale);
                Rect view = Rect.MinMaxRect(centre.x - half.x, centre.y - half.y, centre.x + half.x, centre.y + half.y);
                _world.Clear();
                foreach (Patch patch in _scene)
                {
                    if (patch.Bounds.Overlaps(view) && !(_infrared && patch.Heat == Hidden))
                    {
                        _world.Polygon(Project(patch.Points, centre, scale), Shade(patch.Color, patch.Heat));
                    }
                }
                _world.Commit();
                _drawnInfrared = _infrared;
                _groundBox.color = _infrared ? HeatColour(0.2f) : Ground;
                _world.rectTransform.localScale = Vector3.one;
                _drawnScale = scale;
                _drawnCentre = centre;
            }

            private static readonly int[] ScaleBars = { 5, 10, 20, 25, 50, 100, 200, 250 };

            private void AnimateImagery(float t)
            {
                Rect a = _panel;
                // Zoom in on the objective in steps: each step a quick ease, then a hold, like an analyst's "enhance".
                float p = Progress;
                const int Steps = 4;
                float raw = p * Steps;
                int step = Mathf.Min(Steps - 1, Mathf.FloorToInt(raw));
                float within = Mathf.Clamp01((raw - step) * 4f);
                float ease = within * within * (3f - 2f * within);
                float zoom = Mathf.Pow(1.7f, step + ease);
                float scale = _metre * zoom;
                // The view slides toward the objective as it closes, never past the scene's edges.
                Vector2 half = new Vector2(a.width, a.height) * (0.5f / scale);
                _viewScale = scale;
                _viewCentre = new Vector2(Mathf.Clamp(_site.x, half.x, _worldSize.x - half.x), Mathf.Clamp(_site.y, half.y, _worldSize.y - half.y));

                // The last, closest step goes over to infrared, white-hot, with a flash as the sensor changes.
                bool infrared = step >= Steps - 1;
                if (infrared != _infrared)
                {
                    _infrared = infrared;
                    _infraredAt = infrared ? t : -1f;
                }

                // Drawn crisp whenever a step settles; while it eases the last drawing is scaled, which nobody can see.
                float k = _drawnScale > 0f ? scale / _drawnScale : 0f;
                if (_drawnScale <= 0f || k < 0.97f || _infrared != _drawnInfrared || (within >= 1f && (Mathf.Abs(k - 1f) > 0.001f || (_viewCentre - _drawnCentre).sqrMagnitude > 0.01f)))
                {
                    DrawWorld(_viewCentre, scale);
                    k = 1f;
                }
                _world.rectTransform.localScale = new Vector3(k, k, 1f);
                _world.rectTransform.anchoredPosition = (-a.center * k + (_drawnCentre - _viewCentre) * scale) * U;

                // Held, the traffic and the marks need no more than 30 redraws a second.
                bool flashing = _infraredAt >= 0f && t - _infraredAt < 0.4f;
                if (within >= 1f && !flashing && t - _imageryAt < 1f / 30f)
                {
                    return;
                }
                _imageryAt = t;

                // Traffic on the road, one each way, keeping right.
                _traffic.Clear();
                float length = _roadAt[_roadAt.Length - 1];
                for (int m = 0; m < 2; m++)
                {
                    float d = m == 0 ? Mathf.Repeat(_moverStart + t * 9f, length) : length - Mathf.Repeat(_moverStart * 1.7f + t * 7f, length);
                    RoadPoint(d, out Vector2 at, out Vector2 dir);
                    Vector2 forward = m == 0 ? dir : -dir;
                    var v = new Vehicle { Centre = at + new Vector2(forward.y, -forward.x) * 1.9f, Forward = forward, Length = 7f, Width = 2.5f, Armoured = m == 1 && _parked.Count > 0 && _parked[0].Armoured };
                    _moverAt[m] = v.Centre;
                    AddVehicle(Traffic, Traffic, v);
                }
                // People, too small to see until the picture closes in.
                if (step >= 2)
                {
                    for (int i = 0; i < _walkers.Count; i++)
                    {
                        if (p >= _fallsAt[i])
                        {
                            continue;
                        }
                        Walker w = _walkers[i];
                        float along = w.Speed > 0f ? Mathf.PingPong((t + w.Phase) * w.Speed / Mathf.Max(1f, (w.B - w.A).magnitude), 1f) : 0f;
                        Vector2 at = Vector2.Lerp(w.A, w.B, along);
                        _heat = Cold;
                        Add(Traffic, Disc(at + _sun * 0.7f, 0.4f, 6), Shadow);
                        _heat = 1f;
                        Add(Traffic, Disc(at, 0.42f, 6), Mix(T.Text, 0.95f));
                    }
                }
                Combat(t, p);
                _traffic.Commit();

                // The analyst's marks: the objective boxed, its vehicles and the traffic tagged as the picture closes.
                _reticle.Clear();
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = -lo;
                foreach (Vector2 corner in new[] { _site + _siteX * 23f + _siteY * 16f, _site - _siteX * 23f + _siteY * 16f, _site + _siteX * 23f - _siteY * 16f, _site - _siteX * 23f - _siteY * 16f })
                {
                    Vector2 q = ToPanel(corner);
                    lo = Vector2.Min(lo, q);
                    hi = Vector2.Max(hi, q);
                }
                Vector2 mid = (lo + hi) * 0.5f;
                Vector2 size = Vector2.Max(hi - lo + new Vector2(12f, 12f), new Vector2(30f, 30f));
                size = Vector2.Min(size, new Vector2(a.width - 24f, a.height - 24f));
                Brackets(mid, size, Mathf.Min(12f, size.x * 0.25f), 1.6f, A(T.Text, 220));
                _reticle.Line(P(mid.x - 5f, mid.y), P(mid.x + 5f, mid.y), U, A(T.Text, 160));
                _reticle.Line(P(mid.x, mid.y - 5f), P(mid.x, mid.y + 5f), U, A(T.Text, 160));
                Vector2 tagAt = new Vector2(mid.x - size.x * 0.5f + 60f, mid.y + size.y * 0.5f + 9f);
                if (tagAt.y > a.yMax - 30f)
                {
                    tagAt.y = mid.y - size.y * 0.5f - 9f;
                }
                PlaceAt(_targetLabel, tagAt);
                for (int i = 0; i < _tags.Length; i++)
                {
                    bool show;
                    Vector2 q;
                    float box;
                    if (i < 4)
                    {
                        show = step >= 2 && i < _parked.Count;
                        q = show ? ToPanel(_parked[i].Centre) : Vector2.zero;
                        box = show ? _parked[i].Length * scale + 4f : 0f;
                    }
                    else
                    {
                        show = step >= 1;
                        q = ToPanel(_moverAt[i - 4]);
                        box = Mathf.Max(10f, 7f * scale + 4f);
                    }
                    show &= q.x > a.xMin + 10f && q.x < a.xMax - 10f && q.y > a.yMin + 10f && q.y < a.yMax - 30f;
                    if (_tags[i].gameObject.activeSelf != show)
                    {
                        _tags[i].gameObject.SetActive(show);
                    }
                    if (!show)
                    {
                        continue;
                    }
                    Brackets(q, new Vector2(box, box), Mathf.Max(3f, box * 0.25f), 1.1f, A(i < 4 ? T.Warn : T.Accent, 220));
                    PlaceAt(_tags[i], new Vector2(q.x + box * 0.5f + 42f, q.y + box * 0.5f));
                }

                // The notes on the objective at the closest look, each on a short leader.
                for (int i = 0; i < _notes.Count; i++)
                {
                    Vector2 q = ToPanel(_noteAt[i]);
                    Vector2 label = q + new Vector2(16f, 14f);
                    bool show = step >= Steps - 1 && within >= 1f && label.x < a.xMax - 60f && q.x > a.xMin + 8f && q.y > a.yMin + 20f && label.y < a.yMax - 24f;
                    if (_notes[i].gameObject.activeSelf != show)
                    {
                        _notes[i].gameObject.SetActive(show);
                    }
                    if (show)
                    {
                        _reticle.Disc(P(q.x, q.y), 1.6f * U, A(T.Text, 230));
                        _reticle.Line(P(q.x, q.y), P(label.x - 2f, label.y - 5f), U, A(T.Text, 200));
                        PlaceAt(_notes[i], label + new Vector2(45f, 0f));
                    }
                }

                // A scale bar, as long as fits.
                int metres = ScaleBars[0];
                foreach (int candidate in ScaleBars)
                {
                    if (candidate * scale <= 80f)
                    {
                        metres = candidate;
                    }
                }
                float barRight = a.xMax - 14f, barLeft = barRight - metres * scale, barY = a.yMin + 16f;
                _reticle.RoundedRect(P((barLeft + barRight) * 0.5f, barY), (barRight - barLeft + 8f) * U, 8f * U, 0f, A(Dark, 200));
                _reticle.Line(P(barLeft, barY), P(barRight, barY), 1.4f * U, A(T.Text, 220));
                _reticle.Line(P(barLeft, barY - 3f), P(barLeft, barY + 3f), U, A(T.Text, 220));
                _reticle.Line(P(barRight, barY - 3f), P(barRight, barY + 3f), U, A(T.Text, 220));
                _reticle.Line(P((barLeft + barRight) * 0.5f, barY), P((barLeft + barRight) * 0.5f, barY + 2f), U, A(T.Text, 220));
                if (within < 1f)
                {
                    // The sweep of a step.
                    float y = Mathf.Lerp(a.yMax, a.yMin, within);
                    _reticle.Line(P(a.xMin, y), P(a.xMax, y), 1.4f * U, A(T.Accent, 160));
                }
                if (_infraredAt >= 0f && t - _infraredAt < 0.4f)
                {
                    // The sensor changing over: a white flash, gone in a moment.
                    float flash = 1f - (t - _infraredAt) / 0.4f;
                    _reticle.RoundedRect(P(a.center.x, a.center.y), a.width * U, a.height * U, 0f, new Color32(255, 255, 255, (byte)(200f * flash * flash)));
                }
                _reticle.Commit();

                if (metres != _shownScale)
                {
                    _shownScale = metres;
                    _scaleLabel.text = metres + Ru(" М", " M");
                }
                int shown = step * 10 + Mathf.RoundToInt(ease * 9f) + (_infrared ? 1000 : 0);
                if (shown != _shownZoom)
                {
                    _shownZoom = shown;
                    _imageReading.text = (within < 1f ? Ru("УВЕЛИЧЕНИЕ", "ENHANCE") : "") + $"  x{zoom:0.0}";
                    float gsd = 1f / (scale * U);
                    string sensor = _infrared ? Ru("ТПВ", "IR WHT") : Ru("ТВ", "EO");
                    _imageData.text = Ru($"{sensor}  //  {_gridRef}  //  РАЗРЕШ. {gsd:0.00} М".Replace('.', ','), $"{sensor}  //  {_gridRef}  //  GSD {gsd:0.00} M");
                }
            }

            /// <summary>Where walker <paramref name="i"/> is at time <paramref name="t"/>.</summary>
            private Vector2 WalkerAt(int i, float t)
            {
                Walker w = _walkers[i];
                float along = w.Speed > 0f ? Mathf.PingPong((t + w.Phase) * w.Speed / Mathf.Max(1f, (w.B - w.A).magnitude), 1f) : 0f;
                return Vector2.Lerp(w.A, w.B, along);
            }

            /// <summary>A repeatable random number in 0..1 for a moment and a slot, so a redraw draws the same burst.</summary>
            private static float Chance(int moment, int slot)
            {
                float v = Mathf.Sin(moment * 12.9898f + slot * 78.233f) * 43758.547f;
                return v - Mathf.Floor(v);
            }

            /// <summary>
            /// The fight, in markers that keep their size at any zoom: the team (diamonds, the unit's
            /// colour) moving up, the defenders (the enemy's colour), tracers each way once they are in
            /// contact, a blast now and then, a cross where a defender fell.
            /// </summary>
            private void Combat(float t, float p)
            {
                Rect a = _panel;
                Color32 own = A(Own, 235), enemy = A(Enemy, 235);
                // The team: out from the far side, then onto the wire.
                float move = Mathf.Clamp01((p - 0.08f) / 0.5f);
                move = move * move * (3f - 2f * move);
                var team = new Vector2[_teamTo.Length];
                for (int i = 0; i < team.Length; i++)
                {
                    Vector2 start = _teamFrom + _siteX * ((i - 1.5f) * 6f);
                    team[i] = Vector2.Lerp(start, _teamTo[i], move);
                }
                bool contact = p > 0.3f;
                int alive = 0;
                for (int i = 0; i < _walkers.Count; i++)
                {
                    alive += p < _fallsAt[i] ? 1 : 0;
                }

                // Tracers: a few bursts at a time, eight moments a second, each a short streak flying shooter to target.
                if (contact && alive > 0)
                {
                    int moment = Mathf.FloorToInt(t * 8f);
                    float within = t * 8f - moment;
                    for (int b = 0; b < 3; b++)
                    {
                        if (Chance(moment, b) < 0.35f)
                        {
                            continue;
                        }
                        int ti = Mathf.FloorToInt(Chance(moment, b + 10) * team.Length) % team.Length;
                        int wi = Mathf.FloorToInt(Chance(moment, b + 20) * _walkers.Count) % _walkers.Count;
                        if (p >= _fallsAt[wi])
                        {
                            continue;
                        }
                        bool outgoing = Chance(moment, b + 30) < 0.6f;
                        Vector2 from = ToPanel(outgoing ? team[ti] : WalkerAt(wi, t));
                        Vector2 to = ToPanel(outgoing ? WalkerAt(wi, t) : team[ti]);
                        float k = Mathf.Clamp01(within * 1.6f);
                        Vector2 head = Vector2.Lerp(from, to, Mathf.Min(1f, k + 0.25f));
                        Vector2 tail = Vector2.Lerp(from, to, k);
                        _traffic.Line(P(tail.x, tail.y), P(head.x, head.y), 1.2f * U, Tracer);
                        if (within < 0.2f)
                        {
                            _traffic.Disc(P(from.x, from.y), 2.2f * U, new Color32(255, 240, 200, 220));
                        }
                    }
                }

                // A blast now and then near the compound: a ring opening out and fading.
                if (contact)
                {
                    int blast = Mathf.FloorToInt(t / 3.5f);
                    float age = t - blast * 3.5f;
                    if (age < 0.8f && Chance(blast, 99) > 0.3f)
                    {
                        Vector2 at = _site + _siteX * ((Chance(blast, 98) - 0.5f) * 50f) + _siteY * ((Chance(blast, 97) - 0.5f) * 36f);
                        Vector2 q = ToPanel(at);
                        float r = 3f + age * 22f;
                        byte fade = (byte)(220f * (1f - age / 0.8f));
                        if (age < 0.15f)
                        {
                            _traffic.Disc(P(q.x, q.y), (4f + age * 30f) * U, new Color32(255, 230, 170, fade));
                        }
                        for (int k = 0; k < 16; k++)
                        {
                            float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f;
                            _traffic.Line(P(q.x + Mathf.Cos(a0) * r, q.y + Mathf.Sin(a0) * r), P(q.x + Mathf.Cos(a1) * r, q.y + Mathf.Sin(a1) * r), 1.4f * U, new Color32(255, 200, 120, fade));
                        }
                    }
                }

                // The markers: the defenders standing or fallen, then the team over everything.
                for (int i = 0; i < _walkers.Count; i++)
                {
                    Vector2 q = ToPanel(WalkerAt(i, t));
                    if (!a.Contains(q))
                    {
                        continue;
                    }
                    if (p >= _fallsAt[i])
                    {
                        _traffic.Line(P(q.x - 3.5f, q.y - 3.5f), P(q.x + 3.5f, q.y + 3.5f), 1.4f * U, A(enemy, 160));
                        _traffic.Line(P(q.x - 3.5f, q.y + 3.5f), P(q.x + 3.5f, q.y - 3.5f), 1.4f * U, A(enemy, 160));
                    }
                    else
                    {
                        _traffic.Disc(P(q.x, q.y), 2.6f * U, enemy);
                    }
                }
                for (int i = 0; i < team.Length; i++)
                {
                    Vector2 q = ToPanel(team[i]);
                    if (a.Contains(q))
                    {
                        _traffic.Polygon(new[] { P(q.x, q.y + 3.6f), P(q.x + 3.6f, q.y), P(q.x, q.y - 3.6f), P(q.x - 3.6f, q.y) }, own);
                    }
                }

                int fallen = _walkers.Count - alive;
                int key = (contact ? 100 : 0) + fallen;
                if (key != _shownCombat)
                {
                    _shownCombat = key;
                    _combatLabel.text = !contact ? Ru("ГРУППА ВЫДВИГАЕТСЯ", "TEAM MOVING UP")
                        : Ru($"ОГНЕВОЙ КОНТАКТ  //  УНИЧТОЖЕНО {fallen}", $"TROOPS IN CONTACT  //  EKIA {fallen}");
                }
            }

            private void Brackets(Vector2 centre, Vector2 size, float arm, float width, Color32 color)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        Vector2 corner = centre + new Vector2(sx * size.x, sy * size.y) * 0.5f;
                        _reticle.Line(P(corner.x, corner.y), P(corner.x - sx * arm, corner.y), width * U, color);
                        _reticle.Line(P(corner.x, corner.y), P(corner.x, corner.y - sy * arm), width * U, color);
                    }
                }
            }

            private void PlaceAt(TextMeshProUGUI label, Vector2 at)
            {
                Vector2 to = P(at.x, at.y);
                if ((label.rectTransform.anchoredPosition - to).sqrMagnitude > 0.25f)
                {
                    label.rectTransform.anchoredPosition = to;
                }
            }

            // ==== Readiness ======================================================================
            //
            // A fighting unit's PERSTAT (строевая записка): its teams by callsign, a pip a soldier,
            // reporting in one after another as the load runs. A supply or medical unit's LOGSTAT:
            // the classes of supply on hand, in the quantities each army counts them in.

            private struct Team
            {
                internal Image[] Pips;
                internal int[] States;      // 0 fit, 1 wounded, 2 not present
                internal TextMeshProUGUI Count;
                internal TextMeshProUGUI Status;
                internal string StatusText;
                internal int Shown;
            }

            private struct Gauge
            {
                internal Image Fill;
                internal TextMeshProUGUI Value;
                internal float From;
                internal float To;
                internal float Width;
                internal float Full;
                internal string Unit;
                internal bool Decimal;
                internal string Shown;
            }

            private readonly List<Team> _teams = new List<Team>();
            private readonly List<Gauge> _gauges = new List<Gauge>();
            private TextMeshProUGUI _readyReading;
            private bool _stores;

            private void BuildReadiness(Rect zone)
            {
                _stores = T.Id == ThemeChoice.BearSupply || T.Id == ThemeChoice.UsecLogistics || T.Id == ThemeChoice.BearMedical || T.Id == ThemeChoice.UsecMedical;
                if (_stores)
                {
                    BuildLogstat(zone);
                }
                else
                {
                    BuildPerstat(zone);
                }
            }

            private void BuildPerstat(Rect zone)
            {
                _panel = PanelFrame(zone, Ru($"СТРОЕВАЯ ЗАПИСКА  //  НА {Rng.Next(4, 8):00}:00", $"PERSTAT  //  PERSONNEL STATUS  //  {Rng.Next(4, 8):00}00"), out _readyReading);
                Rect a = _panel;
                bool bear = T.Faction == Faction.Bear;
                string[] calls = bear
                    ? new[] { "ГРАНИТ-1", "ГРАНИТ-2", "ГРАНИТ-3", "БЕРЕГ", "ВОЛНА", "КЕДР" }
                    : new[] { "ALPHA 1-1", "BRAVO 1-2", "CHARLIE 1-3", "DELTA / QRF", "OVERWATCH", "HQ 6" };
                string[] roles = bear
                    ? new[] { "ШТУРМ", "ШТУРМ", "ОГН. ГРУППА", "РЕЗЕРВ", "РАЗВЕДКА", "УПРАВЛЕНИЕ" }
                    : new[] { "ASSAULT", "ASSAULT", "SUPPORT", "QRF", "RECON", "COMMAND" };
                string[] statuses = bear
                    ? new[] { "НА РУБЕЖЕ", "ВЫДВИГАЕТСЯ", "ПОПОЛНЕНИЕ", "ЭВАКУАЦИЯ", "В РЕЗЕРВЕ", "НА ПОЗИЦИИ" }
                    : new[] { "IN POSITION", "MOVING", "RESUPPLY", "CASEVAC", "STANDBY", "OVERWATCH" };
                int rows = Mathf.Clamp(Mathf.FloorToInt((a.height - 22f) / 26f), 2, calls.Length);
                float rowH = (a.height - 22f) / rows;
                float symbolX = a.xMin + 22f;
                float nameX = a.xMin + 40f;
                float nameW = Mathf.Min(120f, a.width * 0.26f);
                float pipX = nameX + nameW + 6f;
                float statusW = Mathf.Min(110f, a.width * 0.22f);
                float pipRoom = a.xMax - 14f - statusW - 40f - pipX;
                Shape symbols = Draw();
                for (int i = 0; i < rows; i++)
                {
                    float y = a.yMax - 14f - rowH * (i + 0.5f);
                    // The team's symbol, in its army's convention: a framed box with its branch inside.
                    Color32 ink = A(T.Accent, 200);
                    Outline(symbols, symbolX, y, 18f, 12f, 1.2f, ink);
                    if (i == 5)
                    {
                        symbols.Line(P(symbolX - 9f, y + 6f), P(symbolX - 9f, y + 12f), 1.2f * U, ink);
                    }
                    else if (i == 4)
                    {
                        symbols.Line(P(symbolX - 9f, y - 6f), P(symbolX + 9f, y + 6f), 1.2f * U, ink);
                    }
                    else
                    {
                        symbols.Line(P(symbolX - 9f, y - 6f), P(symbolX + 9f, y + 6f), 1.2f * U, ink);
                        symbols.Line(P(symbolX - 9f, y + 6f), P(symbolX + 9f, y - 6f), 1.2f * U, ink);
                    }
                    Fit(Label(calls[i], nameX + nameW * 0.5f, y + 4f, nameW, 9f, A(T.Text, 225), TextAlignmentOptions.Left), 9f);
                    Fit(Label(roles[i], nameX + nameW * 0.5f, y - 7f, nameW, 7f, A(T.Dim, 200), TextAlignmentOptions.Left), 7f);

                    // A pip a soldier: most fit, now and then one wounded or not there.
                    int size = i == 5 ? 4 : i == 4 ? 4 : Rng.Next(7, 10);
                    float pitch = Mathf.Min(11f, pipRoom / 10f);
                    var team = new Team { Pips = new Image[size], States = new int[size], Shown = -1 };
                    int present = 0;
                    for (int k = 0; k < size; k++)
                    {
                        int roll = Rng.Next(100);
                        team.States[k] = roll < 8 ? 2 : roll < 18 ? 1 : 0;
                        present += team.States[k] == 2 ? 0 : 1;
                        team.Pips[k] = Box(pipX + k * pitch + pitch * 0.5f, y, pitch - 3f, 9f, A(T.Accent, 30));
                    }
                    team.Count = Label("", pipX + 10f * pitch + 20f, y, 40f, 8f, A(T.Text, 210), TextAlignmentOptions.Left);
                    team.StatusText = team.States[0] == 2 ? statuses[2] : statuses[Rng.Next(statuses.Length)];
                    if (System.Array.IndexOf(team.States, 1) >= 0 && Rng.Next(100) < 50)
                    {
                        team.StatusText = statuses[3];
                    }
                    team.Status = Label("", a.xMax - 10f - statusW * 0.5f, y, statusW, 8f, A(T.Accent, 220), TextAlignmentOptions.Right);
                    _teams.Add(team);
                    symbols.Line(P(a.xMin + 10f, y - rowH * 0.5f), P(a.xMax - 10f, y - rowH * 0.5f), U, A(T.Accent, 22));
                }
                symbols.Commit();
            }

            private void BuildLogstat(Rect zone)
            {
                bool bear = T.Faction == Faction.Bear;
                _panel = PanelFrame(zone, Ru("ОБЕСПЕЧЕННОСТЬ МАТЕРИАЛЬНЫМИ СРЕДСТВАМИ", "LOGSTAT  //  CLASSES OF SUPPLY ON HAND"), out _readyReading);
                Rect a = _panel;
                // Name, full holding, unit, decimal: each army counts in its own measures.
                var rows = bear
                    ? new[]
                    {
                        ("БОЕПРИПАСЫ", 2f, " Б/К", true), ("ГСМ", 1.5f, " ЗАПР.", true), ("ПРОДОВОЛЬСТВИЕ", 5f, " СУТ/Д", true),
                        ("МЕДИМУЩЕСТВО", 1f, " КОМПЛ.", true), ("ВОДА", 3000f, " Л", false), ("ЗИП", 40f, " ЯЩ.", false)
                    }
                    : new[]
                    {
                        ("CL V  AMMUNITION", 60f, " CASES", false), ("CL III  FUEL", 3000f, " L", false), ("CL I  RATIONS", 5f, " DOS", true),
                        ("CL VIII  MEDICAL", 24f, " KITS", false), ("WATER", 2400f, " L", false), ("CL IX  SPARES", 40f, " CRATES", false)
                    };
                int count = Mathf.Clamp(Mathf.FloorToInt((a.height - 20f) / 26f), 2, rows.Length);
                float labelW = Mathf.Min(150f, a.width * 0.32f);
                float barX = a.xMin + 14f + labelW;
                float valueW = 96f;
                float barW = a.xMax - 14f - valueW - barX;
                for (int i = 0; i < count; i++)
                {
                    float y = a.yMax - 22f - i * ((a.height - 30f) / count);
                    Fit(Label(rows[i].Item1, a.xMin + 14f + labelW * 0.5f, y, labelW, 9f, A(T.Text, 210), TextAlignmentOptions.Left), 9f);
                    // A segmented gauge, ten cells, like a fuel state board.
                    Shape cells = Draw();
                    for (int k = 0; k < 10; k++)
                    {
                        float x = barX + barW * (k + 0.5f) / 10f;
                        Outline(cells, x, y, barW / 10f - 3f, 9f, 1f, A(T.Accent, 50));
                    }
                    cells.Commit();
                    Image fill = Box(barX, y, 0f, 7f, A(T.Accent, 200));
                    fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                    fill.rectTransform.anchoredPosition = P(barX, y);
                    TextMeshProUGUI value = Label("", a.xMax - 12f - valueW * 0.5f, y, valueW, 9f, A(T.Text, 220), TextAlignmentOptions.Right);
                    _gauges.Add(new Gauge
                    {
                        Fill = fill, Value = value, From = Range(0.1f, 0.3f), To = Range(0.3f, 0.95f), Width = barW,
                        Full = rows[i].Item2, Unit = rows[i].Item3, Decimal = rows[i].Item4
                    });
                }
            }

            private void AnimateReadiness(float t)
            {
                float p = Progress;
                if (!_stores)
                {
                    // The teams report in one after another, a pip at a time, then give their status.
                    int reported = 0, fit = 0, total = 0;
                    for (int i = 0; i < _teams.Count; i++)
                    {
                        Team team = _teams[i];
                        float f = Mathf.Clamp01(p * 1.25f - i * 0.12f);
                        int shown = Mathf.FloorToInt(f * team.Pips.Length + 0.0001f);
                        total += team.Pips.Length;
                        if (shown != team.Shown)
                        {
                            team.Shown = shown;
                            int up = 0;
                            for (int k = 0; k < team.Pips.Length; k++)
                            {
                                int state = team.States[k];
                                bool known = k < shown;
                                team.Pips[k].color = !known ? A(T.Accent, 30) : state == 0 ? A(T.Accent, 210) : state == 1 ? A(T.Warn, 220) : A(T.Accent, 12);
                                up += known && state != 2 ? 1 : 0;
                            }
                            team.Count.text = shown > 0 ? $"<mspace=0.6em>{up}/{team.Pips.Length}</mspace>" : "";
                            team.Status.text = shown >= team.Pips.Length ? team.StatusText : shown > 0 ? Ru("ДОКЛАД...", "REPORTING") : "";
                            team.Status.color = team.StatusText == Ru("ЭВАКУАЦИЯ", "CASEVAC") && shown >= team.Pips.Length ? A(T.Warn, 230) : A(T.Accent, 220);
                            _teams[i] = team;
                        }
                        reported += shown >= team.Pips.Length ? 1 : 0;
                        for (int k = 0; k < shown; k++)
                        {
                            fit += team.States[k] == 2 ? 0 : 1;
                        }
                    }
                    string reading = Ru($"Л/С {fit}/{total}", $"PAX {fit}/{total}") + "  " + Ru($"ДОЛОЖИЛИ {reported}/{_teams.Count}", $"{reported}/{_teams.Count} UP");
                    if (_readyReading.text != reading)
                    {
                        _readyReading.text = reading;
                    }
                    return;
                }

                int low = 0;
                for (int i = 0; i < _gauges.Count; i++)
                {
                    Gauge g = _gauges[i];
                    float f = Mathf.Clamp01(p * 1.3f - i * 0.06f);
                    float v = Mathf.Lerp(g.From, g.To, f * f * (3f - 2f * f));
                    // The fill steps a cell at a time, as such boards do.
                    float cells = Mathf.Ceil(v * 10f) / 10f;
                    g.Fill.rectTransform.sizeDelta = new Vector2(Mathf.Round((g.Width * cells - 3f) * U), Mathf.Round(7f * U));
                    bool short_ = v < 0.35f;
                    g.Fill.color = A(short_ ? T.Warn : T.Accent, 200);
                    float amount = v * g.Full;
                    string text = (g.Decimal ? amount.ToString(T.Faction == Faction.Bear ? "0.0" : "0.0").Replace('.', T.Faction == Faction.Bear ? ',' : '.') : Mathf.RoundToInt(amount).ToString("N0")) + g.Unit;
                    if (text != g.Shown)
                    {
                        g.Shown = text;
                        g.Value.text = text;
                    }
                    _gauges[i] = g;
                    low += short_ ? 1 : 0;
                }
                string board = low == 0 ? Ru("ОБЕСПЕЧЕНЫ", "GREEN") : Ru($"НЕХВАТКА {low}", $"AMBER  {low} SHORT");
                if (_readyReading.text != board)
                {
                    _readyReading.text = board;
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
