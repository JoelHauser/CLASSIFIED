using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The debrief: what fills the space beside the terminal for every unit, like the briefing
    /// room in a film. In the wider of the layout's two free zones, the unit's kit turns on a
    /// table as a hologram, one item after another (its weapon, then its equipment), built from
    /// the game's own models (Hologram.cs): each builds in along its length, turns while its name
    /// and figures show, and dissolves as the next comes. A weapon's parts are called out by name
    /// when it is side on. In the other zone, the operations map: terrain, a grid and the unit's
    /// operation drawn in the faction's own conventions, playing out as the load goes on.
    ///
    /// BEAR draws like a Soviet staff map: own forces red, the enemy blue, broad arrows, labels
    /// in Russian (or English, with the BEAR Language setting), a kilometre grid. USEC draws NATO
    /// operational graphics: friendly units blue boxes, hostile ones red diamonds, phase lines and
    /// objectives by code name, an MGRS grid.
    /// </summary>
    internal partial class Overlay
    {
        /// <summary>When this terminal's debrief started: kept outside the drawing, so laying the screen out again carries on where it was.</summary>
        private float _debriefAt;

        /// <summary>Whether BEAR's terminal speaks Russian (the default) or English (the BEAR Language setting).</summary>
        internal static bool BearRussian => ClassifiedPlugin.BearLanguage == null || ClassifiedPlugin.BearLanguage.Value == BearText.Russian;

        /// <summary>What the unit's map shows.</summary>
        private enum Operation
        {
            Assault,
            Recon,
            Signals,
            Convoy,
            Casevac,
            Surveillance
        }

        private static Operation OperationOf(ThemeChoice unit)
        {
            switch (unit)
            {
                case ThemeChoice.BearReconnaissance:
                case ThemeChoice.UsecIntelligence: return Operation.Recon;
                case ThemeChoice.BearSignals:
                case ThemeChoice.UsecSignals: return Operation.Signals;
                case ThemeChoice.BearSupply:
                case ThemeChoice.UsecLogistics: return Operation.Convoy;
                case ThemeChoice.BearMedical:
                case ThemeChoice.UsecMedical: return Operation.Casevac;
                case ThemeChoice.BearCounterintelligence: return Operation.Surveillance;
                default: return Operation.Assault;
            }
        }

        private sealed partial class Debrief : Ambient
        {
            private bool Bear => T.Faction == Faction.Bear;

            /// <summary>BEAR's words in Russian, unless the setting asks for English.</summary>
            private bool Russian => Bear && BearRussian;

            // Map conventions: Soviet maps draw own forces red and the enemy blue, NATO the
            // other way round.
            private Vector2 _danger = new Vector2(-9999f, -9999f);
            private float _bearingsAt = -1f;

            private Color32 Own => Bear ? new Color32(226, 58, 44, 255) : new Color32(82, 160, 255, 255);
            private Color32 Enemy => Bear ? new Color32(64, 140, 255, 255) : new Color32(240, 70, 64, 255);

            private string Ru(string russian, string english) => Russian ? O.Cyr(russian) : english;

            protected override bool Seeded => true;

            protected override void Build()
            {
                Rect a = Lz;
                Rect b = Rz;
                // The kit is wide: it takes the wider zone.
                bool leftWider = a.width * Mathf.Min(a.height, a.width * 0.6f) >= b.width * Mathf.Min(b.height, b.width * 0.6f);
                Rect kitZone = leftWider ? a : b;
                Rect mapZone = leftWider ? b : a;
                BuildArmory(kitZone);
                if (mapZone.width >= 220f && mapZone.height >= 150f)
                {
                    BuildModule(mapZone);
                }
                else
                {
                    _module = Module.Map;
                }
            }

            internal override void Animate(float t)
            {
                AnimateArmory(t);
                AnimateModule(t);
            }

            // ==== the armory =====================================================================

            /// <summary>How long an item stays once built, before the next.</summary>
            private const float ItemTime = 9.5f;
            private const float BuildTime = 1.9f;
            private const float DissolveTime = 0.75f;
            private const float RowGap = 22f;
            private const float PitchMax = 0.36f;

            private Hologram _holo;
            private string[] _kit;
            private int _item = -1;
            private Kit.Spec _spec;
            private float _itemAt;
            private bool _dissolving;
            private Rect _zone;
            private float _titleHeight;
            private float _yaw;
            private float _rowTop;
            private float _rowBottom;
            private float _wordsShown = -1f;
            private readonly List<Callout> _above = new List<Callout>();
            private readonly List<Callout> _below = new List<Callout>();

            private static void SetAlpha(TextMeshProUGUI label, float alpha)
            {
                if (label != null)
                {
                    label.alpha = alpha;
                }
            }

            private TextMeshProUGUI _kindLabel;
            private TextMeshProUGUI _counter;
            private TextMeshProUGUI _nameLabel;
            private TextMeshProUGUI _fullLabel;
            private TextMeshProUGUI _lineLabel;
            private int _shownAzimuth = -1;
            private Shape _leaders;
            private readonly List<Callout> _callouts = new List<Callout>();

            private sealed class Callout
            {
                internal Vector3 At;
                internal Vector2 Screen;
                internal bool Below;
                internal TextMeshProUGUI Label;
                internal float Width;
                internal float X;
                internal bool Placed;
                internal float Shown;
            }

            private void BuildArmory(Rect zone)
            {
                // This launch's table, the same however many times the screen is laid out.
                _kit = Kit.Draw(T.Id, T.Faction, Rng);
                if (_kit == null)
                {
                    return;
                }
                _zone = zone;
                bool roomy = zone.height >= 300f;
                _titleHeight = roomy ? 98f : 34f;

                float x = zone.xMin + 4f;
                float w = zone.width - 8f;
                float top = zone.yMax - 4f;
                if (roomy)
                {
                    // The kind of item on the left of the top line, the turntable's reading and the
                    // item's number on the right.
                    _kindLabel = Fit(Label("", x + (w - 200f) * 0.5f, top - 8f, w - 204f, 10f, A(T.Dim, 210), TextAlignmentOptions.Left), 10f);
                    _counter = Label("", x + w - 100f, top - 8f, 200f, 9f, A(T.Dim, 190), TextAlignmentOptions.Right);
                    _nameLabel = Fit(Label("", x + w * 0.5f, top - 34f, w, 26f, A(T.Text, 235), TextAlignmentOptions.Left, spacing: 8f), 26f);
                    _fullLabel = Fit(Label("", x + w * 0.5f, top - 59f, w, 10f, A(T.Accent, 190), TextAlignmentOptions.Left), 10f);
                    _lineLabel = Fit(Label("", x + w * 0.5f, top - 76f, w, 10f, A(T.Dim, 220), TextAlignmentOptions.Left), 10f);
                    Shape rule = Draw();
                    rule.Line(P(x, top - 90f), P(x + w, top - 90f), U, A(T.Accent, 40));
                    rule.Line(P(x, top - 90f), P(x + 60f, top - 90f), 2f * U, A(T.Accent, 140));
                    rule.Commit();
                }
                else
                {
                    // A short zone: the name and the figures on one line.
                    _nameLabel = Fit(Label("", x + w * 0.5f, top - 10f, w, 13f, A(T.Text, 230), TextAlignmentOptions.Left, spacing: 4f), 13f);
                    _lineLabel = Fit(Label("", x + w * 0.5f, top - 26f, w, 9f, A(T.Dim, 210), TextAlignmentOptions.Left), 9f);
                }

                // The table's models read on a worker thread now, not on the frame each comes on.
                WeaponModel.Prefetch(_kit);
                _holo = Hologram.Create("Item", R);
                _holo.Ink = A(T.Accent, 255);
                _leaders = Draw();
                // Where the table had got to: a layout partway through must not start it again.
                float since = Mathf.Max(0f, O._animClock - O._debriefAt);
                int shown = Mathf.FloorToInt(since / Cycle);
                ShowItem(shown % _kit.Length, O._debriefAt + shown * Cycle);
            }

            private const float Turn = 0.34f;

            /// <summary>How long an item holds side on before it starts to turn: through its build-in and a beat more.</summary>
            private const float SideOn = BuildTime + 0.4f;

            /// <summary>How long the turn takes to come up to speed: slowly, so the callouts are read before it has turned far.</summary>
            private const float SpinUp = 3f;

            /// <summary>
            /// The item's turn, from its time on the table: side on while it builds, with the barest
            /// drift, then easing straight into a steady turn. Each item starts side on, so its
            /// callouts are always seen; they fade as it turns away.
            /// </summary>
            private static float YawAt(float age)
            {
                float drift = 0.06f * Mathf.Sin(age * 0.7f);
                float s = age - SideOn;
                if (s <= 0f)
                {
                    return drift;
                }
                float turned = s < SpinUp ? Turn * s * s / (2f * SpinUp) : Turn * (s - SpinUp * 0.5f);
                return drift * Mathf.Exp(-s) + turned;
            }

            /// <summary>One item's whole time on the table: building in, turning, dissolving.</summary>
            private const float Cycle = BuildTime + ItemTime + DissolveTime;

            /// <summary>Puts kit item <paramref name="index"/> on the table, sized and centred for the zone.</summary>
            private void ShowItem(int index, float now)
            {
                _item = index;
                _spec = Kit.Get(_kit[index]);
                _itemAt = now;
                // The drawn build (ak74m_3), not the stock weapon its figures come from.
                WeaponModel model = WeaponModel.Load(_kit[index]);
                _holo.Model = model;
                _holo.Reveal = 0f;

                // The words.
                string name = Russian ? O.Cyr(_spec.ShortRu) : _spec.Short;
                // One of the game's named builds goes by its name too: AKS-74UN "Zenit".
                Kit.NamedBuild buildName = Kit.NameOf(_kit[index]);
                if (buildName != null)
                {
                    name += " \"" + (Russian ? O.Cyr(buildName.Ru) : buildName.En) + "\"";
                }
                string full = (Russian ? O.Cyr(_spec.NameRu) : _spec.Name).ToUpperInvariant();
                string line = Russian ? O.Cyr(_spec.LineRu) : _spec.LineEn;
                if (_kindLabel != null)
                {
                    _kindLabel.text = (Russian ? O.Cyr(_spec.KindRu) : _spec.KindEn) + Ru("  //  СЕКРЕТНО", "  //  RESTRICTED");
                    _shownAzimuth = -1;
                    _fullLabel.text = full;
                    _nameLabel.text = name;
                    _lineLabel.text = line;
                }
                else
                {
                    _nameLabel.text = name + "  <alpha=#88><size=70%>" + full + "</size>";
                    _lineLabel.text = line;
                }

                // The callouts, for a weapon: up to four parts, those under the barrel labelled
                // under it, the rest over it.
                foreach (Callout c in _callouts)
                {
                    Object.Destroy(c.Label.gameObject);
                }
                _callouts.Clear();
                if (model != null)
                {
                    // A weapon's parts by slot; equipment's by the points the generator marked on it.
                    string[] order = { "mod_scope", "mod_muzzle", "mod_magazine", "mod_stock", "mod_tactical", "mod_foregrip", "mod_sight_rear", "mod_reciever", "mod_gas_block", "mod_handguard", "mod_pistol_grip", "mod_pistolgrip", "mod_barrel", "mod_bipod",
                        "gear_plate", "gear_soft", "gear_pouch", "gear_strap", "gear_shell", "gear_rim", "gear_optics", "gear_mount", "gear_headband", "gear_cup", "gear_lid", "gear_body", "gear_name" };
                    // Each row (above the item, below it) takes labels only while they fit across the
                    // zone, so they never have to be pushed over each other.
                    float above = 0f, below = 0f, budget = _zone.width * 0.9f;
                    foreach (string slot in order)
                    {
                        // The first part in a slot of that kind (an optic may sit in mod_scope_000).
                        // A stock is named by its outermost piece (the stock on its buffer tube), anything else by its first.
                        int i = slot == "mod_stock"
                            ? System.Array.FindLastIndex(model.AnchorSlots, s => s == slot || s.StartsWith(slot + "_"))
                            : System.Array.FindIndex(model.AnchorSlots, s => s == slot || s.StartsWith(slot + "_"));
                        if (i < 0 || _callouts.Count >= 5)
                        {
                            continue;
                        }
                        // The part by name where the model knows it ("EXPS3", "PSO-1"), else what it is.
                        string named = Russian ? model.AnchorNamesRu[i] : model.AnchorNames[i];
                        string text = !string.IsNullOrEmpty(named) ? Clip((Russian ? O.Cyr(named) : named).ToUpperInvariant(), 18) : PartName(slot);
                        if (text == null)
                        {
                            continue;
                        }
                        var callout = new Callout { At = model.Anchors[i], Below = model.Anchors[i].y < -0.08f };
                        callout.Label = Label(text, 0f, 0f, 200f, 9f, A(T.Text, 220), TextAlignmentOptions.Center, _leaders.transform);
                        callout.Width = callout.Label.GetPreferredValues(text).x / U + 6f;
                        ref float used = ref callout.Below ? ref below : ref above;
                        if (used + callout.Width + 12f > budget)
                        {
                            Object.Destroy(callout.Label.gameObject);
                            continue;
                        }
                        used += callout.Width + 12f;
                        SetText(callout.Label, 0f, 0f, callout.Width * U, 14f * U, 9f * U, 6f);
                        callout.Label.gameObject.SetActive(false);
                        _callouts.Add(callout);
                    }
                }
                Fit();
            }

            /// <summary>Scales and places the item so the item, its table and its callout rows fill the zone under the title, centred.</summary>
            private void Fit()
            {
                if (_holo.Model == null)
                {
                    return;
                }
                _holo.Extent(PitchMax, out float top, out float bottom);
                float rows = _callouts.Count > 0 ? (RowGap + 16f) : 6f;
                float height = _zone.height - _titleHeight - 8f;
                float width = _zone.width * 0.47f;
                float scale = Mathf.Min(width / _holo.TableRadius, (height - rows * 2f) / Mathf.Max(0.1f, top - bottom));
                if (!_spec.Weapon)
                {
                    // Equipment is smaller than a rifle laid out: keep it so.
                    scale = Mathf.Min(scale, Mathf.Min(_zone.width * 0.40f, (height - 12f) / 2.05f) * 0.62f);
                }
                float middle = _zone.yMin + 4f + height * 0.5f;
                float centreY = middle - (top + bottom) * 0.5f * scale;
                _holo.Scale = scale * U;
                _holo.rectTransform.anchoredPosition = P(_zone.center.x, centreY);
                _leaders.rectTransform.anchoredPosition = P(_zone.center.x, centreY);
                _rowTop = (top * scale + RowGap) * U;
                _rowBottom = (bottom * scale - RowGap) * U;
            }

            private static string Clip(string text, int max) => text.Length <= max ? text : text.Substring(0, max).TrimEnd();

            private string PartName(string slot)
            {
                switch (slot)
                {
                    case "mod_scope": return Ru("ПРИЦЕЛ", "OPTIC");
                    case "mod_tactical": return Ru("ТАКТИЧЕСКИЙ БЛОК", "TACTICAL DEVICE");
                    case "mod_foregrip": return Ru("ПЕРЕДНЯЯ РУКОЯТКА", "FOREGRIP");
                    case "mod_muzzle": return Ru("ДУЛЬНОЕ УСТРОЙСТВО", "MUZZLE DEVICE");
                    case "mod_magazine": return Ru("МАГАЗИН", "MAGAZINE");
                    case "mod_stock": return Ru("ПРИКЛАД", "STOCK");
                    case "mod_sight_rear": return Ru("ЦЕЛИК", "REAR SIGHT");
                    case "mod_reciever": return Ru("КРЫШКА СТВОЛЬНОЙ КОРОБКИ", "UPPER RECEIVER");
                    case "mod_gas_block": return Ru("ГАЗОВАЯ КАМЕРА", "GAS BLOCK");
                    case "mod_pistol_grip":
                    case "mod_pistolgrip": return Ru("ПИСТОЛЕТНАЯ РУКОЯТКА", "PISTOL GRIP");
                    case "mod_barrel": return Ru("СТВОЛ", "BARREL");
                    case "mod_handguard": return Ru("ЦЕВЬЁ", "HANDGUARD");
                    case "mod_bipod": return Ru("СОШКИ", "BIPOD");
                    case "gear_plate": return Ru("БРОНЕПЛАСТИНА", "BALLISTIC PLATE");
                    case "gear_soft": return Ru("БРОНЕПАНЕЛИ", "ARMOUR PANELS");
                    case "gear_pouch": return Ru("ПОДСУМКИ", "POUCHES");
                    case "gear_strap": return Ru("ПЛЕЧЕВОЙ РЕМЕНЬ", "SHOULDER STRAP");
                    case "gear_shell": return Ru("КОРПУС ШЛЕМА", "SHELL");
                    case "gear_rim": return Ru("КРОМКА", "RIM");
                    case "gear_optics": return Ru("ОПТИКА", "OPTICS");
                    case "gear_mount": return Ru("КРЕПЛЕНИЕ", "MOUNT");
                    case "gear_headband": return Ru("ОГОЛОВЬЕ", "HEADBAND");
                    case "gear_cup": return Ru("ЧАШКА НАУШНИКА", "EAR CUP");
                    case "gear_lid": return Ru("КЛАПАН", "LID");
                    case "gear_body": return Ru("ОСНОВНОЕ ОТДЕЛЕНИЕ", "MAIN COMPARTMENT");
                    case "gear_name": return Russian ? O.Cyr(_spec.ShortRu) : _spec.Short;
                    default: return null;
                }
            }

            private void AnimateArmory(float t)
            {
                if (_holo == null)
                {
                    return;
                }
                // Everything here follows from the time on the table, so nothing restarts when the
                // screen is laid out again.
                float age = t - _itemAt;
                if (age >= Cycle && _kit.Length > 1)
                {
                    ShowItem((_item + 1) % _kit.Length, _itemAt + Cycle);
                    age = t - _itemAt;
                }
                float dissolveAt = BuildTime + ItemTime;
                _dissolving = _kit.Length > 1 && age > dissolveAt;

                // Building in, eased; dissolving, faster and the same way along.
                float build = Mathf.Clamp01((age - 0.15f) / BuildTime);
                build = build * build * (3f - 2f * build);
                float reveal = _dissolving ? 1f - Mathf.Clamp01((age - dissolveAt) / DissolveTime) : build;
                _holo.Reveal = reveal;

                _yaw = YawAt(age);
                _holo.Yaw = _yaw;
                _holo.Pitch = 0.30f + 0.05f * Mathf.Sin(t * 0.4f);
                _holo.TableTurn = -t * 0.25f;
                // A scan along it every few seconds, once it is built.
                float cycle = ((age - BuildTime) % 4.5f) / 4.5f;
                _holo.ScanAt = !_dissolving && age > BuildTime && cycle < 0.55f ? cycle / 0.55f : -9f;
                // Steady: the stutters and the shimmer read as the attachments flashing.
                _holo.Strength = 0.95f;
                _holo.Tick(t);

                // The words come in with the item and go with it.
                float words = Mathf.Clamp01(age / 0.5f) * (_dissolving ? 1f - Mathf.Clamp01((age - dissolveAt) / (DissolveTime * 0.6f)) : 1f);
                if (!Mathf.Approximately(words, _wordsShown))
                {
                    _wordsShown = words;
                    SetAlpha(_kindLabel, words);
                    SetAlpha(_counter, words);
                    SetAlpha(_nameLabel, words);
                    SetAlpha(_fullLabel, words);
                    SetAlpha(_lineLabel, words);
                }
                if (_counter != null)
                {
                    int azimuth = Mathf.RoundToInt(Mathf.Repeat(_yaw * Mathf.Rad2Deg, 360f));
                    if (azimuth != _shownAzimuth)
                    {
                        _shownAzimuth = azimuth;
                        _counter.text = Ru("АЗИМУТ", "AZIMUTH") + $" <mspace=0.62em>{azimuth:000}</mspace>  //  {_item + 1:00} / {_kit.Length:00}";
                    }
                }
                AnimateCallouts(reveal >= 1f && !_dissolving);
            }

            /// <summary>
            /// The part names, shown while the weapon is side on, each joined to the middle of its
            /// part by a leader: a dot on the part, up (or down) to its row, along to its label.
            /// Labels keep their order along the weapon and are pushed apart so none overlap.
            /// </summary>
            private void AnimateCallouts(bool built)
            {
                _leaders.Clear();
                float side = Mathf.Abs(Mathf.Cos(_yaw));
                float show = built ? Mathf.Clamp01((side - 0.7f) / 0.2f) : 0f;
                if (_callouts.Count > 0)
                {
                    _above.Clear();
                    _below.Clear();
                    foreach (Callout c in _callouts)
                    {
                        c.Screen = _holo.Project(c.At, out _);
                        (c.Below ? _below : _above).Add(c);
                    }
                    float limit = _zone.width * 0.5f * U - 4f * U;
                    LayoutRow(_above, limit);
                    LayoutRow(_below, limit);
                    foreach (Callout c in _callouts)
                    {
                        c.Shown = Mathf.MoveTowards(c.Shown, show, Dt * 3f);
                        if (c.Shown <= 0.01f)
                        {
                            c.Label.gameObject.SetActive(false);
                            continue;
                        }
                        Vector2 at = c.Screen;
                        float y = c.Below ? _rowBottom : _rowTop;
                        float dir = c.Below ? -1f : 1f;
                        float knee = y - dir * 10f * U;
                        Color32 line = A(T.Accent, (byte)(c.Shown * 160f));
                        _leaders.Disc(at, 2.4f * U, A(T.Text, (byte)(c.Shown * 230f)));
                        _leaders.Line(at, new Vector2(at.x, knee), 1.1f * U, line);
                        _leaders.Line(new Vector2(at.x, knee), new Vector2(c.X, y), 1.1f * U, line);
                        float half = c.Width * 0.5f * U;
                        _leaders.Line(new Vector2(c.X - half, y), new Vector2(c.X + half, y), 1.1f * U, line);
                        c.Label.gameObject.SetActive(true);
                        c.Label.rectTransform.anchoredPosition = new Vector2(c.X, y + dir * 8f * U);
                        c.Label.alpha = c.Shown;
                    }
                }
                _leaders.Commit();
            }

            /// <summary>Labels in a row: each over its part where it can be, in order, apart, and inside the zone.</summary>
            private readonly float[] _rowX = new float[8];
            private static readonly System.Comparison<Callout> ByScreenX = (p, q) => p.Screen.x.CompareTo(q.Screen.x);

            private void LayoutRow(List<Callout> row, float limit)
            {
                if (row.Count == 0)
                {
                    return;
                }
                row.Sort(ByScreenX);
                float gap = 12f * U;
                float[] x = _rowX;
                for (int i = 0; i < row.Count; i++)
                {
                    x[i] = row[i].Screen.x;
                    if (i > 0)
                    {
                        x[i] = Mathf.Max(x[i], x[i - 1] + (row[i - 1].Width + row[i].Width) * 0.5f * U + gap);
                    }
                }
                for (int i = row.Count - 1; i >= 0; i--)
                {
                    float right = limit - row[i].Width * 0.5f * U;
                    if (i < row.Count - 1)
                    {
                        right = Mathf.Min(right, x[i + 1] - (row[i].Width + row[i + 1].Width) * 0.5f * U - gap);
                    }
                    x[i] = Mathf.Min(x[i], right);
                }
                for (int i = 0; i < row.Count; i++)
                {
                    x[i] = Mathf.Max(x[i], -limit + row[i].Width * 0.5f * U);
                    Callout c = row[i];
                    // Eased, so a label slides rather than jumps as the weapon turns.
                    c.X = c.Placed ? Mathf.Lerp(c.X, x[i], 1f - Mathf.Exp(-Dt * 8f)) : x[i];
                    c.Placed = true;
                }
            }

            /// <summary>Lets a label shrink to fit its width rather than run past it.</summary>
            private TextMeshProUGUI Fit(TextMeshProUGUI label, float size)
            {
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMax = Mathf.Round(size * U);
                label.fontSizeMin = Mathf.Round(size * U * 0.55f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                return label;
            }

            // ==== the operations map =============================================================

            private Rect _map;
            private Operation _op;
            private Shape _live;
            private Shape _mover;
            private RectTransform _clip;
            private TextMeshProUGUI _clock;
            private float _drawn = -1f;
            private Vector2[] _path;
            private Vector2[] _stations;
            private Vector2 _target;
            private float _orbit;
            private int _shownMinute = -1;

            /// <summary>What on the map a label must keep clear of: other labels and the symbols.</summary>
            private readonly List<Rect> _taken = new List<Rect>();

            private Vector2 M(float u, float v) => new Vector2(Mathf.Lerp(_map.xMin, _map.xMax, u), Mathf.Lerp(_map.yMin, _map.yMax, v));

            private void Reserve(Vector2 centre, float w, float h) => _taken.Add(new Rect(centre.x - w * 0.5f, centre.y - h * 0.5f, w, h));

            /// <summary>
            /// A label on the map near <paramref name="at"/> (its left edge there, or centred on it),
            /// in the first of a few spots round it that is clear of everything already there and
            /// inside the map; left off if there is none.
            /// </summary>
            private void MapLabel(string text, Vector2 at, float size, Color32 color, bool centred = false)
            {
                TextMeshProUGUI label = Label(text, 0f, 0f, 200f, size, color, centred ? TextAlignmentOptions.Center : TextAlignmentOptions.Left, _clip);
                float w = label.GetPreferredValues(text).x / U + 4f;
                float h = size + 4f;
                Vector2 shift = centred ? Vector2.zero : new Vector2(w * 0.5f + 8f, 0f);
                Vector2[] tries =
                {
                    at + shift, at + shift + new Vector2(0f, h), at + shift - new Vector2(0f, h),
                    centred ? at + new Vector2(0f, h * 2f) : at - shift, centred ? at - new Vector2(0f, h * 2f) : at - shift + new Vector2(0f, h),
                    at + shift + new Vector2(0f, h * 2f), at + shift - new Vector2(0f, h * 2f)
                };
                foreach (Vector2 c in tries)
                {
                    var rect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
                    if (rect.xMin < _map.xMin + 2f || rect.xMax > _map.xMax - 2f || rect.yMin < _map.yMin + 2f || rect.yMax > _map.yMax - 2f)
                    {
                        continue;
                    }
                    bool clear = true;
                    foreach (Rect taken in _taken)
                    {
                        if (taken.Overlaps(rect))
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (!clear)
                    {
                        continue;
                    }
                    _taken.Add(rect);
                    SetText(label, (c.x - _map.center.x) * U, (c.y - _map.center.y) * U, w * U, h * U, size * U, 4f);
                    return;
                }
                label.gameObject.SetActive(false);
            }

            private void BuildMap(Rect zone)
            {
                _op = OperationOf(T.Id);
                float header = 22f;
                _map = Rect.MinMaxRect(zone.xMin + 6f, zone.yMin + 6f, zone.xMax - 6f, zone.yMax - header - 6f);
                // A dark ground under the map: the backdrop's own map showing through read as two maps at once.
                Box(_map.center.x, _map.center.y, _map.width, _map.height, A(Dark, 235));
                _clip = Clip(_map);

                // Terrain under a light grid, in the unit's own colour.
                Shape ground = Draw(-_map.center.x, -_map.center.y, _clip);
                Terrain(ground);
                Grid(ground);
                ground.Commit();

                // The operation's fixed graphics, then the spot heights wherever there is room left.
                Shape plan = Draw(-_map.center.x, -_map.center.y, _clip);
                Plan(plan);
                plan.Commit();
                SpotHeights();

                _live = Draw(-_map.center.x, -_map.center.y, _clip);
                _mover = Draw(0f, 0f, _clip);
                MoverShape();

                // The frame and its header.
                Shape frame = Draw();
                Outline(frame, _map.center.x, _map.center.y, _map.width, _map.height, 1.5f, A(T.Accent, 70));
                float tick = 10f;
                foreach (var corner in new[] { new Vector2(_map.xMin, _map.yMin), new Vector2(_map.xMax, _map.yMin), new Vector2(_map.xMin, _map.yMax), new Vector2(_map.xMax, _map.yMax) })
                {
                    float sx = corner.x < _map.center.x ? 1f : -1f;
                    float sy = corner.y < _map.center.y ? 1f : -1f;
                    frame.Line(P(corner.x, corner.y), P(corner.x + sx * tick, corner.y), 3f * U, A(T.Accent, 200));
                    frame.Line(P(corner.x, corner.y), P(corner.x, corner.y + sy * tick), 3f * U, A(T.Accent, 200));
                }
                frame.Commit();
                string sheet = $"{(char)('K' + Rng.Next(6))}-{Rng.Next(30, 44)}-{Rng.Next(1, 145)}";
                float clockWidth = 96f;
                Fit(Label(MapTitle(sheet),
                    _map.xMin + (_map.width - clockWidth) * 0.5f, _map.yMax + 13f, _map.width - clockWidth - 8f, 10f, A(T.Dim, 220), TextAlignmentOptions.Left), 10f);
                _clock = Label("", _map.xMax - clockWidth * 0.5f, _map.yMax + 13f, clockWidth, 10f, A(T.Accent, 220), TextAlignmentOptions.Right);
                Fit(Label(OperationTitle(), _map.center.x, _map.yMin + 10f, _map.width - 12f, 9f, A(T.Text, 170), TextAlignmentOptions.Right), 9f);
            }

            /// <summary>The map's header: the document it is, by the operation, as each army would title it.</summary>
            private string MapTitle(string sheet)
            {
                string order = $"{Rng.Next(1, 30):00}-{Rng.Next(1, 99):00}";
                switch (_op)
                {
                    case Operation.Recon:
                        return Ru($"СХЕМА РАЗВЕДКИ  //  ЛИСТ {sheet}  //  1:50 000", $"R&S OVERLAY  //  OPORD {order}  //  1:50,000");
                    case Operation.Signals:
                        return Ru($"СХЕМА ОРГАНИЗАЦИИ СВЯЗИ  //  ЛИСТ {sheet}", $"SIGNAL SITE OVERLAY  //  ANNEX H  //  1:50,000");
                    case Operation.Convoy:
                        return Ru($"СХЕМА МАРША КОЛОННЫ  //  ЛИСТ {sheet}  //  1:50 000", $"ROUTE OVERLAY  //  MOVEMENT ORDER {order}  //  1:50,000");
                    case Operation.Casevac:
                        return Ru($"СХЕМА ЭВАКУАЦИИ РАНЕНЫХ  //  ЛИСТ {sheet}", $"CASEVAC OVERLAY  //  ANNEX F  //  1:50,000");
                    case Operation.Surveillance:
                        return Ru($"СХЕМА НАБЛЮДЕНИЯ  //  ЛИСТ {sheet}  //  1:50 000", $"SURVEILLANCE OVERLAY  //  OPORD {order}  //  1:50,000");
                    default:
                        return Ru($"РАБОЧАЯ КАРТА КОМАНДИРА  //  ЛИСТ {sheet}  //  1:50 000", $"OPORD {order}  //  ANNEX C  //  OPERATION OVERLAY  //  1:50,000");
                }
            }

            private string OperationTitle()
            {
                switch (_op)
                {
                    case Operation.Recon: return Ru("РАЙОН НАБЛЮДЕНИЯ", "NAMED AREA OF INTEREST");
                    case Operation.Signals: return Ru("РАДИОПЕЛЕНГАЦИЯ", "DIRECTION FINDING");
                    case Operation.Convoy: return Ru("МАРШРУТ КОЛОННЫ", "MAIN SUPPLY ROUTE");
                    case Operation.Casevac: return Ru("ЭВАКУАЦИЯ РАНЕНЫХ", "CASEVAC");
                    case Operation.Surveillance: return Ru("НАРУЖНОЕ НАБЛЮДЕНИЕ", "SURVEILLANCE TRACK");
                    default: return Ru("ЗАМЫСЕЛ ОПЕРАЦИИ", "CONCEPT OF OPERATIONS");
                }
            }

            private Vector2[] _hills;

            /// <summary>Hills as contours, a river, a road.</summary>
            private void Terrain(Shape s)
            {
                const int Cols = 40;
                int rows = Mathf.Max(8, Mathf.RoundToInt(Cols * _map.height / _map.width));
                int hills = 6;
                _hills = new Vector2[hills];
                var sizes = new float[hills];
                var heights = new float[hills];
                for (int i = 0; i < hills; i++)
                {
                    _hills[i] = new Vector2(Range(0.05f, 0.95f), Range(0.05f, 0.95f));
                    sizes[i] = Range(0.12f, 0.32f);
                    heights[i] = Range(0.5f, 1.1f);
                }
                var field = new float[Cols + 1, rows + 1];
                for (int gx = 0; gx <= Cols; gx++)
                {
                    for (int gy = 0; gy <= rows; gy++)
                    {
                        var p = new Vector2(gx / (float)Cols, gy / (float)rows);
                        float h = 0f;
                        for (int i = 0; i < hills; i++)
                        {
                            Vector2 d = p - _hills[i];
                            d.y *= _map.height / _map.width;
                            h += heights[i] * Mathf.Exp(-d.sqrMagnitude / (sizes[i] * sizes[i]));
                        }
                        field[gx, gy] = h;
                    }
                }
                float cw = _map.width / Cols;
                float ch = _map.height / rows;
                for (int level = 1; level <= 8; level++)
                {
                    float iso = level * 0.14f;
                    Color32 color = A(T.Accent, (byte)(level % 4 == 0 ? 46 : 22));
                    for (int gx = 0; gx < Cols; gx++)
                    {
                        for (int gy = 0; gy < rows; gy++)
                        {
                            float a = field[gx, gy], b = field[gx + 1, gy], c = field[gx + 1, gy + 1], d = field[gx, gy + 1];
                            int code = (a > iso ? 1 : 0) | (b > iso ? 2 : 0) | (c > iso ? 4 : 0) | (d > iso ? 8 : 0);
                            if (code == 0 || code == 15)
                            {
                                continue;
                            }
                            Vector2 o = new Vector2(_map.xMin + gx * cw, _map.yMin + gy * ch);
                            Vector2 bottom = o + new Vector2(cw * Cut(a, b, iso), 0f);
                            Vector2 right = o + new Vector2(cw, ch * Cut(b, c, iso));
                            Vector2 top = o + new Vector2(cw * Cut(d, c, iso), ch);
                            Vector2 left = o + new Vector2(0f, ch * Cut(a, d, iso));
                            switch (code)
                            {
                                case 1: case 14: Seg(s, left, bottom, color); break;
                                case 2: case 13: Seg(s, bottom, right, color); break;
                                case 3: case 12: Seg(s, left, right, color); break;
                                case 4: case 11: Seg(s, right, top, color); break;
                                case 6: case 9: Seg(s, bottom, top, color); break;
                                case 7: case 8: Seg(s, left, top, color); break;
                                case 5: Seg(s, left, top, color); Seg(s, bottom, right, color); break;
                                case 10: Seg(s, left, bottom, color); Seg(s, right, top, color); break;
                            }
                        }
                    }
                }

                // A river winding across, and a road.
                Vector2[] river = Wander(M(0f, Range(0.15f, 0.85f)), M(1f, Range(0.15f, 0.85f)), 0.12f, 18);
                Polyline(s, river, 3f, A(T.Accent, 60));
                Vector2[] road = Wander(M(Range(0.1f, 0.4f), 0f), M(Range(0.6f, 0.9f), 1f), 0.06f, 10);
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    Vector2 d = (road[i + 1] - road[i]).normalized;
                    Vector2 n = new Vector2(-d.y, d.x) * 2.5f;
                    s.Line(P(road[i].x + n.x, road[i].y + n.y), P(road[i + 1].x + n.x, road[i + 1].y + n.y), U, A(T.Accent, 70));
                    s.Line(P(road[i].x - n.x, road[i].y - n.y), P(road[i + 1].x - n.x, road[i + 1].y - n.y), U, A(T.Accent, 70));
                }
                _path = road;
            }

            /// <summary>Spot heights on the hilltops, where the operation's labels have left room.</summary>
            private void SpotHeights()
            {
                Shape dots = Draw(-_map.center.x, -_map.center.y, _clip);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 at = M(_hills[i].x, _hills[i].y);
                    if (!_map.Contains(at))
                    {
                        continue;
                    }
                    dots.Disc(P(at.x, at.y), 2f * U, A(T.Accent, 120));
                    MapLabel(Russian ? $"{Rng.Next(140, 260)},{Rng.Next(0, 10)}" : $"{Rng.Next(140, 260)}.{Rng.Next(0, 10)}", at, 8f, A(T.Accent, 120));
                }
                dots.Commit();
            }

            private static float Cut(float from, float to, float iso) => Mathf.Clamp01((iso - from) / (to - from));

            private void Seg(Shape s, Vector2 a, Vector2 b, Color32 color) => s.Line(P(a.x, a.y), P(b.x, b.y), U, color);

            /// <summary>The grid with its numbers: kilometre lines for BEAR, MGRS squares for USEC.</summary>
            private void Grid(Shape s)
            {
                float step = 70f;
                int first = Rng.Next(20, 80);
                Color32 line = A(T.Accent, 26);
                Color32 number = A(T.Accent, 120);
                string zone = Bear ? string.Empty : $"{(char)('C' + Rng.Next(8))}{(char)('P' + Rng.Next(8))} ";
                int i = 0;
                for (float x = _map.xMin + step * 0.5f; x < _map.xMax; x += step, i++)
                {
                    s.Line(P(x, _map.yMin), P(x, _map.yMax), U, line);
                    string text = (i == 0 ? zone : string.Empty) + (first + i).ToString("00");
                    TextMeshProUGUI label = Label(text, 0f, 0f, 60f, 8f, number, TextAlignmentOptions.Left, _clip);
                    float w = label.GetPreferredValues(text).x / U + 2f;
                    SetText(label, (x + 3f + w * 0.5f - _map.center.x) * U, (_map.yMin + 7f - _map.center.y) * U, w * U, 12f * U, 8f * U, 2f);
                    Reserve(new Vector2(x + 3f + w * 0.5f, _map.yMin + 7f), w + 4f, 12f);
                }
                i = 0;
                int firstY = Rng.Next(10, 70);
                for (float y = _map.yMin + step * 0.5f; y < _map.yMax; y += step, i++)
                {
                    s.Line(P(_map.xMin, y), P(_map.xMax, y), U, line);
                    string text = (firstY + i).ToString("00");
                    TextMeshProUGUI label = Label(text, 0f, 0f, 40f, 8f, number, TextAlignmentOptions.Left, _clip);
                    SetText(label, (_map.xMin + 14f - _map.center.x) * U, (y + 7f - _map.center.y) * U, 20f * U, 12f * U, 8f * U, 2f);
                    Reserve(new Vector2(_map.xMin + 14f, y + 7f), 24f, 12f);
                }
                // The operation's title along the bottom.
                Reserve(new Vector2(_map.center.x, _map.yMin + 10f), _map.width, 16f);
            }

            /// <summary>The operation's fixed graphics: where it starts, the enemy, the objective.</summary>
            private void Plan(Shape s)
            {
                switch (_op)
                {
                    case Operation.Assault:
                    {
                        // Two axes of advance onto the objective, through the enemy's line.
                        _target = M(Range(0.65f, 0.8f), Range(0.55f, 0.75f));
                        _path = Curve(M(0.08f, Range(0.15f, 0.35f)), M(0.45f, Range(0.2f, 0.5f)), _target, 20);
                        Vector2[] second = Curve(M(0.1f, Range(0.6f, 0.85f)), M(0.4f, Range(0.75f, 0.9f)), _target + new Vector2(-12f, 14f), 20);
                        Polyline(s, second, 1.5f, A(Own, 60), dash: true);
                        // Both axes stay clear: no unit or label on them.
                        foreach (Vector2 point in _path)
                        {
                            Reserve(point, 30f, 30f);
                        }
                        foreach (Vector2 point in second)
                        {
                            Reserve(point, 20f, 20f);
                        }
                        DefenceLine(s, M(0.55f, 0.15f), M(0.6f, 0.92f));
                        Objective(s, _target, Ru("ЦЕЛЬ 1", "OBJ RAVEN"));
                        Units(s, 3, 0.08f, 0.25f, true);
                        Units(s, 3, 0.66f, 0.92f, false);
                        PhaseLine(s, 0.32f, Ru("РУБЕЖ АТАКИ", "PL DAGGER (LD)"));
                        break;
                    }
                    case Operation.Recon:
                    {
                        _target = M(Range(0.6f, 0.75f), Range(0.45f, 0.7f));
                        Objective(s, _target, Ru("РАЙОН 3", "NAI 3"), dashed: true, radius: 46f);
                        _path = new[] { M(0.06f, 0.12f), _target + new Vector2(-46f, 0f) };
                        ObservationPost(s, M(0.18f, 0.7f), Ru("НП-1", "OP 1"));
                        ObservationPost(s, M(0.4f, 0.2f), Ru("НП-2", "OP 2"));
                        Units(s, 2, 0.62f, 0.88f, false);
                        break;
                    }
                    case Operation.Signals:
                    {
                        // Our receivers, and the emitter their bearings will cross on.
                        _target = M(Range(0.55f, 0.8f), Range(0.5f, 0.8f));
                        _stations = new[] { M(0.1f, 0.2f), M(0.15f, 0.8f), M(0.5f, 0.1f) };
                        for (int i = 0; i < _stations.Length; i++)
                        {
                            if (i > 0)
                            {
                                Polyline(s, new[] { _stations[i - 1], _stations[i] }, 1f, A(Own, 50), dash: true);
                            }
                            Antenna(s, _stations[i], Ru($"ПОСТ {i + 1}", $"DF {i + 1}"));
                        }
                        // The fix will be drawn here: keep it clear.
                        Reserve(_target, 70f, 44f);
                        break;
                    }
                    case Operation.Convoy:
                    {
                        // A movement overlay: the column's start and release points on the road, its
                        // checkpoints with the times it is due at them, the route's name, and an
                        // alternate round the stretch where an ambush is expected.
                        Flag(s, Along(_path, 0.06f), Ru("ИП", "SP"));
                        Flag(s, Along(_path, 0.94f), Ru("РС", "RP"));
                        int hour = Rng.Next(4, 8), minute = Rng.Next(0, 4) * 10;
                        for (int i = 1; i < 4; i++)
                        {
                            minute += Rng.Next(18, 32);
                            Checkpoint(s, Along(_path, 0.08f + i * 0.21f), Ru($"КПП {i}  {hour + minute / 60:00}:{minute % 60:00}", $"CP {i}  {hour + minute / 60:00}{minute % 60:00}"));
                        }
                        float at = Range(0.55f, 0.68f);
                        Vector2 road = Along(_path, at);
                        Vector2 dir = (Along(_path, at + 0.02f) - road).normalized;
                        Vector2 side = new Vector2(-dir.y, dir.x);
                        _danger = road + side * 14f;
                        DangerArea(s, _danger, 26f, Ru("ВЕРОЯТНАЯ ЗАСАДА", "DANGER AREA  //  AMBUSH"));
                        // The alternate: off the road before the danger, round it on the far side, back after.
                        Vector2 from = Along(_path, at - 0.18f), to = Along(_path, Mathf.Min(0.92f, at + 0.16f));
                        Vector2[] alt = Curve(from, road - side * 70f, to, 14);
                        Polyline(s, alt, 1.2f, A(Own, 120), dash: true);
                        MapLabel(Ru("ЗАПАСНОЙ", "ALT ROUTE"), alt[7] - side * 10f, 8f, A(Own, 190), centred: true);
                        MapLabel(Ru("МАРШРУТ «ВОЛГА»", "MSR TAMPA"), Along(_path, 0.2f) + new Vector2(16f, 0f), 9f, A(Own, 210));
                        break;
                    }
                    case Operation.Casevac:
                    {
                        Vector2 poi = M(Range(0.6f, 0.8f), Range(0.55f, 0.8f));
                        Vector2 aid = M(0.15f, Range(0.2f, 0.35f));
                        _target = poi;
                        _path = Curve(aid, M(0.45f, 0.75f), poi, 16);
                        LandingZone(s, poi, Ru("ПЛОЩАДКА 2", "HLZ FALCON"));
                        AidStation(s, aid, Ru("МЕДПУНКТ", "ROLE 1"));
                        Units(s, 2, 0.55f, 0.9f, false);
                        break;
                    }
                    default:
                    {
                        // A subject's movements: out of the house where the watch on them began, along
                        // the road, off it to a meeting; sighted on the way, the times in order.
                        Vector2[] road = _path;
                        float from = Range(0.1f, 0.22f), to = Range(0.78f, 0.9f);
                        Vector2 side = Side(road, from);
                        Vector2 house = Along(road, from) + side * 26f;
                        Vector2 meet = Along(road, to) + Side(road, to) * (Rng.Next(2) == 0 ? 26f : -26f);
                        const int Steps = 24;
                        var track = new Vector2[Steps + 3];
                        track[0] = house;
                        for (int i = 0; i <= Steps; i++)
                        {
                            track[i + 1] = Along(road, Mathf.Lerp(from, to, i / (float)Steps));
                        }
                        track[Steps + 2] = meet;
                        _path = track;
                        Building(s, house, Ru("АДРЕС", "RESIDENCE"));
                        Building(s, meet, Ru("МЕСТО ВСТРЕЧИ", "MEETING POINT"));
                        int minute = Rng.Next(6, 10) * 60 + Rng.Next(0, 60);
                        for (int i = 0; i < 4; i++)
                        {
                            Vector2 at = Along(_path, 0.18f + i * 0.2f);
                            s.Line(P(at.x - 5f, at.y - 5f), P(at.x + 5f, at.y + 5f), 1.6f * U, A(Enemy, 200));
                            s.Line(P(at.x - 5f, at.y + 5f), P(at.x + 5f, at.y - 5f), 1.6f * U, A(Enemy, 200));
                            Reserve(at, 14f, 14f);
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            minute += Rng.Next(8, 40);
                            MapLabel($"{minute / 60 % 24:00}:{minute % 60:00}", Along(_path, 0.18f + i * 0.2f), 8f, A(Enemy, 200));
                        }
                        // Our teams watch from off the track.
                        foreach (Vector2 point in _path)
                        {
                            Reserve(point, 16f, 16f);
                        }
                        Units(s, 2, 0.3f, 0.6f, true);
                        break;
                    }
                }
            }

            private void MoverShape()
            {
                _mover.Clear();
                switch (_op)
                {
                    case Operation.Recon:
                        // A drone, and the footprint of its camera.
                        _mover.Polygon(new[] { P(0f, 9f), P(7f, -6f), P(0f, -2f), P(-7f, -6f) }, A(Own, 220));
                        _mover.Polygon(new[] { P(-8f, 18f), P(8f, 18f), P(24f, 58f), P(-24f, 58f) }, A(Own, 26));
                        break;
                    case Operation.Casevac:
                        // A helicopter: body, tail and the rotor disc.
                        _mover.Disc(Vector2.zero, 4.5f * U, A(Own, 230));
                        _mover.Line(P(0f, 0f), P(0f, -15f), 2f * U, A(Own, 230));
                        _mover.Line(P(-12f, 0f), P(12f, 0f), 1.4f * U, A(Own, 160));
                        _mover.Line(P(0f, -12f), P(0f, 12f), 1.4f * U, A(Own, 160));
                        break;
                    case Operation.Convoy:
                        for (int i = 0; i < 3; i++)
                        {
                            _mover.Polygon(new[] { P(-4f, -18f * i - 5f), P(4f, -18f * i - 5f), P(4f, -18f * i + 5f), P(-4f, -18f * i + 5f) }, A(Own, (byte)(230 - i * 50)));
                        }
                        break;
                    case Operation.Signals:
                        break;
                    default:
                        _mover.Polygon(new[] { P(0f, 11f), P(9f, -8f), P(-9f, -8f) }, A(Own, 200));
                        break;
                }
                _mover.Commit();
            }

            private void AnimateMap(float t)
            {
                float p = Progress;
                // Operation time: H-hour, Ч on BEAR's maps.
                int minute = Mathf.FloorToInt(p * 140f);
                if (minute != _shownMinute)
                {
                    _shownMinute = minute;
                    _clock.text = (Russian ? O.Cyr("Ч") : "H") + $" + <mspace=0.62em>{minute / 60:00}:{minute % 60:00}</mspace>";
                }

                switch (_op)
                {
                    case Operation.Recon:
                    {
                        const float Outbound = 0.35f;
                        Vector2 at;
                        float heading;
                        float radius = 46f;
                        if (p < Outbound)
                        {
                            at = Vector2.Lerp(_path[0], _path[1], p / Outbound);
                            Vector2 d = _path[1] - _path[0];
                            heading = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                            _orbit = 180f;
                        }
                        else
                        {
                            _orbit -= Dt * 45f;
                            at = _target + new Vector2(Mathf.Cos(_orbit * Mathf.Deg2Rad), Mathf.Sin(_orbit * Mathf.Deg2Rad)) * radius;
                            heading = _orbit - 180f;
                        }
                        Place(at, heading);
                        if (Due(p))
                        {
                            _leg[0] = _path[0];
                            _leg[1] = Vector2.Lerp(_path[0], _path[1], Mathf.Min(p / Outbound, 1f));
                            Polyline(_live, _leg, 1.4f, A(Own, 140), dash: true);
                            _live.Commit();
                        }
                        break;
                    }
                    case Operation.Signals:
                        // The bearings swing all the time: redrawn 30 times a second, which is smooth for a slow swing.
                        if (t - _bearingsAt >= 1f / 30f)
                        {
                            _bearingsAt = t;
                            Bearings(t, p);
                        }
                        break;
                    case Operation.Convoy:
                    {
                        // Each vehicle is put on the road where it has got to, turned along it there,
                        // so the column bends with the road instead of cutting its corners. The
                        // danger area pulses while the head of the column is in it.
                        _mover.gameObject.SetActive(false);
                        float head = Mathf.Lerp(0.06f, 0.94f, p);
                        bool inDanger = (Along(_path, head) - _danger).magnitude < 34f;
                        // In the danger area the pulse redraws a dozen times a second; else only as the column moves.
                        if (Due(inDanger ? 10f + Mathf.Repeat(Mathf.Floor(t * 12f), 1000f) * 0.001f : p))
                        {
                            if (inDanger)
                            {
                                _live.Disc(P(_danger.x, _danger.y), 26f * U, A(Enemy, (byte)(30 + 30 * Mathf.Abs(Mathf.Sin(t * 5f)))));
                            }
                            Polyline(_live, Part(_path, head), 3f, A(Own, 70));
                            for (int k = 0; k < 4; k++)
                            {
                                float f = head - k * 0.03f;
                                if (f <= 0f)
                                {
                                    break;
                                }
                                Vector2 at = Along(_path, f);
                                Vector2 dir = (at - Along(_path, Mathf.Max(0f, f - 0.01f))).normalized;
                                Vector2 side = new Vector2(-dir.y, dir.x);
                                Vector2 front = at + dir * 6.5f;
                                Vector2 back = at - dir * 6.5f;
                                _quad[0] = P(front.x + side.x * 4.5f, front.y + side.y * 4.5f);
                                _quad[1] = P(front.x - side.x * 4.5f, front.y - side.y * 4.5f);
                                _quad[2] = P(back.x - side.x * 4.5f, back.y - side.y * 4.5f);
                                _quad[3] = P(back.x + side.x * 4.5f, back.y + side.y * 4.5f);
                                _live.Polygon(_quad, A(Own, (byte)(245 - k * 40)));
                            }
                            _live.Commit();
                        }
                        break;
                    }
                    case Operation.Casevac:
                    {
                        // Out to the casualties, then back.
                        float leg = p < 0.5f ? p * 2f : 2f - p * 2f;
                        Vector2 at = Along(_path, leg);
                        Vector2 ahead = Along(_path, Mathf.Clamp01(leg + (p < 0.5f ? 0.02f : -0.02f))) - at;
                        Place(at, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg - 90f);
                        if (Due(p))
                        {
                            Polyline(_live, Part(_path, p < 0.5f ? leg : 1f), 1.4f, A(Own, 150), dash: true);
                            _live.Commit();
                        }
                        break;
                    }
                    case Operation.Surveillance:
                    {
                        Vector2 at = Along(_path, p);
                        Vector2 ahead = Along(_path, Mathf.Min(1f, p + 0.02f)) - at;
                        Place(at, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg - 90f);
                        if (Due(p))
                        {
                            Polyline(_live, Part(_path, p), 1.4f, A(Enemy, 170), dash: true);
                            _live.Commit();
                        }
                        break;
                    }
                    default:
                    {
                        // The arrow carries its own head: no marker.
                        _mover.gameObject.SetActive(false);
                        float reach = Mathf.Max(0.04f, p);
                        if (Due(p))
                        {
                            BroadArrow(_live, Part(_path, reach), 10f, Own, Along(_path, reach) - Along(_path, Mathf.Max(0f, reach - 0.05f)));
                            _live.Commit();
                        }
                        break;
                    }
                }
            }

            private void Place(Vector2 at, float heading)
            {
                _mover.rectTransform.anchoredPosition = P(at.x - _map.center.x, at.y - _map.center.y);
                _mover.rectTransform.localEulerAngles = new Vector3(0f, 0f, heading);
            }

            /// <summary>Redraws the live layer when the load has moved on enough to show.</summary>
            /// <summary>Whether the moving part of the map needs drawing again for <paramref name="p"/>; if so, cleared for it (the caller draws and commits).</summary>
            private bool Due(float p)
            {
                if (Mathf.Abs(p - _drawn) < 0.0005f)
                {
                    return false;
                }
                _drawn = p;
                _live.Clear();
                return true;
            }

            private readonly Vector2[] _leg = new Vector2[2];
            private readonly Vector2[] _quad = new Vector2[4];

            /// <summary>Signals: each receiver's bearing swings onto the emitter as the load goes on; once they cross, the fix.</summary>
            private void Bearings(float t, float p)
            {
                _live.Clear();
                float lockOn = Mathf.Clamp01((p - 0.15f) / 0.55f);
                for (int i = 0; i < _stations.Length; i++)
                {
                    Vector2 from = _stations[i];
                    float exact = Mathf.Atan2(_target.y - from.y, _target.x - from.x);
                    float error = (1f - lockOn) * (0.6f + 0.25f * i) * Mathf.Sin(t * (0.7f + 0.3f * i) + i) + 0.01f * Mathf.Sin(t * 3f + i);
                    float angle = exact + error;
                    float reach = _map.width + _map.height;
                    Vector2 to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                    _live.Line(P(from.x, from.y), P(to.x, to.y), 1.3f * U, A(Own, 150));
                    float spread = 0.06f + (1f - lockOn) * 0.12f;
                    Vector2 left = from + new Vector2(Mathf.Cos(angle + spread), Mathf.Sin(angle + spread)) * reach;
                    Vector2 right = from + new Vector2(Mathf.Cos(angle - spread), Mathf.Sin(angle - spread)) * reach;
                    _live.Polygon(new[] { P(from.x, from.y), P(left.x, left.y), P(right.x, right.y) }, A(Own, 10));
                }
                if (lockOn >= 1f)
                {
                    // The fix: an ellipse of error round the emitter, and its mark.
                    for (int k = 0; k < 24; k++)
                    {
                        float a0 = k * Mathf.PI / 12f;
                        float a1 = (k + 1) * Mathf.PI / 12f;
                        Vector2 e0 = _target + new Vector2(Mathf.Cos(a0) * 26f, Mathf.Sin(a0) * 14f);
                        Vector2 e1 = _target + new Vector2(Mathf.Cos(a1) * 26f, Mathf.Sin(a1) * 14f);
                        _live.Line(P(e0.x, e0.y), P(e1.x, e1.y), 1.5f * U, A(Enemy, 200));
                    }
                    _live.Disc(P(_target.x, _target.y), 3f * U, A(Enemy, 230));
                }
                _live.Commit();
            }

            // ---- map symbols --------------------------------------------------------------------

            /// <summary>Units in a band of the map, each clear of the others, with its designation.</summary>
            private void Units(Shape s, int count, float from, float to, bool own)
            {
                for (int i = 0; i < count; i++)
                {
                    // A few tries for a spot clear of what is already drawn.
                    Vector2 at = M(Range(from, to), Range(0.15f, 0.85f));
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        var rect = new Rect(at.x - 16f, at.y - 12f, 32f, 24f);
                        if (!_taken.Exists(r => r.Overlaps(rect)))
                        {
                            break;
                        }
                        at = M(Range(from, to), Range(0.15f, 0.85f));
                    }
                    Reserve(at, 32f, 24f);
                    Color32 c = A(own ? Own : Enemy, 200);
                    string designation;
                    if (Bear)
                    {
                        // Soviet marks: own a flag on a staff, the enemy a semicircle; both numbered.
                        if (own)
                        {
                            s.Line(P(at.x, at.y - 9f), P(at.x, at.y + 9f), 1.4f * U, c);
                            s.Polygon(new[] { P(at.x, at.y + 9f), P(at.x + 14f, at.y + 5f), P(at.x, at.y + 1f) }, c);
                            designation = Ru($"{Rng.Next(1, 4)}/{Rng.Next(1, 4)} мсб", $"{Rng.Next(1, 4)}/{Rng.Next(1, 4)} MRB");
                        }
                        else
                        {
                            for (int k = 0; k < 8; k++)
                            {
                                float a0 = Mathf.PI * k / 8f;
                                float a1 = Mathf.PI * (k + 1) / 8f;
                                s.Line(P(at.x + Mathf.Cos(a0) * 10f, at.y + Mathf.Sin(a0) * 10f), P(at.x + Mathf.Cos(a1) * 10f, at.y + Mathf.Sin(a1) * 10f), 1.6f * U, c);
                            }
                            s.Line(P(at.x - 10f, at.y), P(at.x + 10f, at.y), 1.6f * U, c);
                            designation = Ru($"до {Rng.Next(2, 5)}0 чел", $"UP TO {Rng.Next(2, 5)}0 PAX");
                        }
                    }
                    else if (own)
                    {
                        // NATO: a friendly infantry unit, a box with a cross.
                        Outline(s, at.x, at.y, 24f, 16f, 1.6f, c);
                        s.Line(P(at.x - 12f, at.y - 8f), P(at.x + 12f, at.y + 8f), 1.2f * U, c);
                        s.Line(P(at.x - 12f, at.y + 8f), P(at.x + 12f, at.y - 8f), 1.2f * U, c);
                        designation = $"{"ABC"[Rng.Next(3)]}/{Rng.Next(1, 4)}";
                    }
                    else
                    {
                        float d = 11f;
                        s.Line(P(at.x - d, at.y), P(at.x, at.y + d), 1.6f * U, c);
                        s.Line(P(at.x, at.y + d), P(at.x + d, at.y), 1.6f * U, c);
                        s.Line(P(at.x + d, at.y), P(at.x, at.y - d), 1.6f * U, c);
                        s.Line(P(at.x, at.y - d), P(at.x - d, at.y), 1.6f * U, c);
                        s.Line(P(at.x - 5f, at.y - 5f), P(at.x + 5f, at.y + 5f), 1.2f * U, c);
                        s.Line(P(at.x - 5f, at.y + 5f), P(at.x + 5f, at.y - 5f), 1.2f * U, c);
                        designation = "HOS";
                    }
                    MapLabel(designation, at + new Vector2(12f, 0f), 8f, A(own ? Own : Enemy, 190));
                }
            }

            private void DefenceLine(Shape s, Vector2 from, Vector2 to)
            {
                // A line with teeth facing the attacker, in the enemy's colour.
                Vector2[] line = Wander(from, to, 0.05f, 12);
                Polyline(s, line, 2f, A(Enemy, 200));
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector2 mid = (line[i] + line[i + 1]) * 0.5f;
                    Vector2 d = (line[i + 1] - line[i]).normalized;
                    Vector2 n = new Vector2(d.y, -d.x) * 7f;
                    s.Polygon(new[] { P(mid.x - d.x * 4f, mid.y - d.y * 4f), P(mid.x + d.x * 4f, mid.y + d.y * 4f), P(mid.x + n.x, mid.y + n.y) }, A(Enemy, 170));
                    Reserve(mid, 14f, 14f);
                }
            }

            private void Objective(Shape s, Vector2 at, string name, bool dashed = false, float radius = 30f)
            {
                const int Steps = 36;
                for (int i = 0; i < Steps; i++)
                {
                    if (dashed && i % 2 == 1)
                    {
                        continue;
                    }
                    float a0 = i * Mathf.PI * 2f / Steps;
                    float a1 = (i + 1) * Mathf.PI * 2f / Steps;
                    float wobble0 = 1f + 0.12f * Mathf.Sin(a0 * 3f);
                    float wobble1 = 1f + 0.12f * Mathf.Sin(a1 * 3f);
                    s.Line(P(at.x + Mathf.Cos(a0) * radius * wobble0, at.y + Mathf.Sin(a0) * radius * 0.75f * wobble0),
                        P(at.x + Mathf.Cos(a1) * radius * wobble1, at.y + Mathf.Sin(a1) * radius * 0.75f * wobble1), 1.8f * U, A(Own, 210));
                }
                Reserve(at, radius * 2f, radius * 1.5f);
                MapLabel(name, at + new Vector2(0f, radius * 0.75f + 10f), 10f, A(Own, 230), centred: true);
            }

            private void PhaseLine(Shape s, float u, string name)
            {
                Vector2[] line = Wander(M(u, 0f), M(u + 0.04f, 1f), 0.04f, 10);
                Polyline(s, line, 1.4f, A(Own, 120), dash: true);
                MapLabel(name, line[line.Length - 3], 8f, A(Own, 190));
            }

            private void ObservationPost(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Own, 210);
                s.Line(P(at.x - 9f, at.y - 6f), P(at.x + 9f, at.y - 6f), 1.6f * U, c);
                s.Line(P(at.x + 9f, at.y - 6f), P(at.x, at.y + 9f), 1.6f * U, c);
                s.Line(P(at.x, at.y + 9f), P(at.x - 9f, at.y - 6f), 1.6f * U, c);
                s.Disc(P(at.x, at.y - 1f), 2f * U, c);
                Reserve(at, 22f, 20f);
                MapLabel(name, at + new Vector2(8f, 0f), 8f, c);
            }

            private void Antenna(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Own, 210);
                s.Line(P(at.x, at.y - 8f), P(at.x, at.y + 8f), 1.6f * U, c);
                s.Line(P(at.x, at.y + 8f), P(at.x - 6f, at.y + 14f), 1.4f * U, c);
                s.Line(P(at.x, at.y + 8f), P(at.x + 6f, at.y + 14f), 1.4f * U, c);
                s.Disc(P(at.x, at.y - 8f), 2.5f * U, c);
                Reserve(at + new Vector2(0f, 3f), 16f, 26f);
                MapLabel(name, at + new Vector2(6f, 0f), 8f, c);
            }

            private void Checkpoint(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Own, 210);
                Outline(s, at.x, at.y, 12f, 12f, 1.6f, c);
                Reserve(at, 16f, 16f);
                MapLabel(name, at + new Vector2(6f, 0f), 8f, c);
            }

            /// <summary>A start or release point: a flag on a staff, its letters beside it.</summary>
            private void Flag(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Own, 220);
                s.Disc(P(at.x, at.y), 2.5f * U, c);
                s.Line(P(at.x, at.y), P(at.x, at.y + 20f), 1.6f * U, c);
                s.Polygon(new[] { P(at.x, at.y + 20f), P(at.x + 13f, at.y + 16f), P(at.x, at.y + 12f) }, c);
                Reserve(at + new Vector2(8f, 10f), 26f, 26f);
                MapLabel(name, at + new Vector2(16f, 16f), 9f, c);
            }

            /// <summary>A danger area: a dashed ring, hatched inside, the ambush symbol at its middle.</summary>
            private void DangerArea(Shape s, Vector2 at, float radius, string name)
            {
                Color32 c = A(Enemy, 200);
                const int Steps = 32;
                for (int k = 0; k < Steps; k += 2)
                {
                    float a0 = k * Mathf.PI * 2f / Steps, a1 = (k + 1) * Mathf.PI * 2f / Steps;
                    s.Line(P(at.x + Mathf.Cos(a0) * radius, at.y + Mathf.Sin(a0) * radius), P(at.x + Mathf.Cos(a1) * radius, at.y + Mathf.Sin(a1) * radius), 1.4f * U, c);
                }
                for (float o = -radius + 6f; o < radius; o += 7f)
                {
                    // Diagonal hatching, each line cut at the ring.
                    float half = Mathf.Sqrt(radius * radius - o * o) - 2f;
                    Vector2 mid = at + new Vector2(o, o) * 0.7071f;
                    Vector2 along = new Vector2(1f, -1f) * 0.7071f * half;
                    s.Line(P(mid.x - along.x, mid.y - along.y), P(mid.x + along.x, mid.y + along.y), U, A(Enemy, 70));
                }
                Ambush(s, at + new Vector2(0f, 6f));
                Reserve(at, radius * 2f, radius * 2f);
                MapLabel(name, at + new Vector2(0f, -radius - 10f), 8f, c, centred: true);
            }

            private void Ambush(Shape s, Vector2 at)
            {
                Color32 c = A(Enemy, 200);
                for (int k = 0; k < 6; k++)
                {
                    float a0 = Mathf.PI * (0.2f + 0.6f * k / 6f);
                    float a1 = Mathf.PI * (0.2f + 0.6f * (k + 1) / 6f);
                    s.Line(P(at.x + Mathf.Cos(a0) * 16f, at.y - Mathf.Sin(a0) * 16f), P(at.x + Mathf.Cos(a1) * 16f, at.y - Mathf.Sin(a1) * 16f), 1.6f * U, c);
                }
                s.Disc(P(at.x, at.y), 2.5f * U, c);
                Reserve(at + new Vector2(0f, -8f), 34f, 20f);
            }

            private void LandingZone(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Own, 210);
                const int Steps = 28;
                for (int k = 0; k < Steps; k++)
                {
                    float a0 = k * Mathf.PI * 2f / Steps;
                    float a1 = (k + 1) * Mathf.PI * 2f / Steps;
                    s.Line(P(at.x + Mathf.Cos(a0) * 14f, at.y + Mathf.Sin(a0) * 14f), P(at.x + Mathf.Cos(a1) * 14f, at.y + Mathf.Sin(a1) * 14f), 1.6f * U, c);
                }
                s.Line(P(at.x - 5f, at.y - 7f), P(at.x - 5f, at.y + 7f), 1.6f * U, c);
                s.Line(P(at.x + 5f, at.y - 7f), P(at.x + 5f, at.y + 7f), 1.6f * U, c);
                s.Line(P(at.x - 5f, at.y), P(at.x + 5f, at.y), 1.6f * U, c);
                Reserve(at, 32f, 32f);
                MapLabel(name, at - new Vector2(0f, 26f), 8f, c, centred: true);
            }

            /// <summary>A building the subject uses: a block with its roof ridge, its name under it.</summary>
            private void Building(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Enemy, 210);
                Outline(s, at.x, at.y, 14f, 10f, 1.6f, c);
                s.Line(P(at.x - 7f, at.y), P(at.x + 7f, at.y), U, c);
                Reserve(at, 20f, 16f);
                MapLabel(name, at - new Vector2(0f, 15f), 8f, c, centred: true);
            }

            /// <summary>The way across the path at f, a unit long.</summary>
            private static Vector2 Side(Vector2[] path, float f)
            {
                Vector2 d = (Along(path, Mathf.Min(1f, f + 0.02f)) - Along(path, Mathf.Max(0f, f - 0.02f))).normalized;
                return new Vector2(-d.y, d.x);
            }

            private void AidStation(Shape s, Vector2 at, string name)
            {
                Color32 c = A(Own, 220);
                s.Line(P(at.x - 8f, at.y), P(at.x + 8f, at.y), 4f * U, c);
                s.Line(P(at.x, at.y - 8f), P(at.x, at.y + 8f), 4f * U, c);
                Reserve(at, 20f, 20f);
                MapLabel(name, at - new Vector2(0f, 18f), 8f, c, centred: true);
            }

            /// <summary>A broad arrow along a path: a band with a firm edge and a filled head, as staffs draw an axis of attack.</summary>
            private void BroadArrow(Shape s, Vector2[] path, float width, Color32 color, Vector2 heading)
            {
                if (path.Length < 2)
                {
                    return;
                }
                // The band runs right up to the base of the head, so the head never runs ahead of it.
                Vector2 tipAt = path[path.Length - 1];
                Vector2 heads = heading.sqrMagnitude > 0.0001f ? heading.normalized : (tipAt - path[path.Length - 2]).normalized;
                Vector2 headBase = tipAt - heads * width * 1.6f;
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    Vector2 a = path[i];
                    Vector2 b = i + 2 == path.Length ? headBase : path[i + 1];
                    if ((b - a).sqrMagnitude < 0.25f)
                    {
                        continue;
                    }
                    s.Line(P(a.x, a.y), P(b.x, b.y), width * U, A(color, 46));
                    Vector2 d = (b - a).normalized;
                    Vector2 n = new Vector2(-d.y, d.x) * width * 0.5f;
                    s.Line(P(a.x + n.x, a.y + n.y), P(b.x + n.x, b.y + n.y), 1.5f * U, A(color, 210));
                    s.Line(P(a.x - n.x, a.y - n.y), P(b.x - n.x, b.y - n.y), 1.5f * U, A(color, 210));
                }
                // The head points along the path over its last stretch, not its last tiny step,
                // which turns from frame to frame.
                Vector2 tip = path[path.Length - 1];
                Vector2 dir = heading.sqrMagnitude > 0.0001f ? heading.normalized : (tip - path[path.Length - 2]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 baseCentre = tip - dir * width * 1.6f;
                s.Polygon(new[] { P(tip.x, tip.y), P(baseCentre.x + side.x * width, baseCentre.y + side.y * width), P(baseCentre.x - side.x * width, baseCentre.y - side.y * width) }, A(color, 220));
            }

            private void Polyline(Shape s, Vector2[] points, float width, Color32 color, bool dash = false)
            {
                for (int i = 0; i + 1 < points.Length; i++)
                {
                    if (!dash)
                    {
                        s.Line(P(points[i].x, points[i].y), P(points[i + 1].x, points[i + 1].y), width * U, color);
                        continue;
                    }
                    float length = Vector2.Distance(points[i], points[i + 1]);
                    for (float d = 0f; d < length; d += 12f)
                    {
                        Vector2 a = Vector2.Lerp(points[i], points[i + 1], d / Mathf.Max(1f, length));
                        Vector2 b = Vector2.Lerp(points[i], points[i + 1], Mathf.Min(d + 7f, length) / Mathf.Max(1f, length));
                        s.Line(P(a.x, a.y), P(b.x, b.y), width * U, color);
                    }
                }
            }

            /// <summary>A gently winding line from a to b.</summary>
            private Vector2[] Wander(Vector2 a, Vector2 b, float amount, int count)
            {
                var points = new Vector2[count + 1];
                Vector2 d = b - a;
                Vector2 n = new Vector2(-d.y, d.x).normalized;
                float phase = Range(0f, 6.28f);
                float k = Range(1.2f, 2.4f);
                for (int i = 0; i <= count; i++)
                {
                    float f = i / (float)count;
                    float off = Mathf.Sin(f * Mathf.PI * k + phase) * Mathf.Sin(f * Mathf.PI) * amount * d.magnitude;
                    points[i] = a + d * f + n * off;
                }
                return points;
            }

            /// <summary>A quadratic curve through a bend.</summary>
            private static Vector2[] Curve(Vector2 a, Vector2 bend, Vector2 b, int count)
            {
                var points = new Vector2[count + 1];
                for (int i = 0; i <= count; i++)
                {
                    float f = i / (float)count;
                    points[i] = (1 - f) * (1 - f) * a + 2f * (1 - f) * f * bend + f * f * b;
                }
                return points;
            }

            private static Vector2 Along(Vector2[] path, float f)
            {
                f = Mathf.Clamp01(f) * (path.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), path.Length - 2);
                return Vector2.Lerp(path[i], path[i + 1], f - i);
            }

            /// <summary>The path as far as f of it.</summary>
            private static Vector2[] Part(Vector2[] path, float f)
            {
                f = Mathf.Clamp01(f);
                float at = f * (path.Length - 1);
                int whole = Mathf.Min(Mathf.FloorToInt(at), path.Length - 2);
                var points = new Vector2[whole + 2];
                for (int i = 0; i <= whole; i++)
                {
                    points[i] = path[i];
                }
                points[whole + 1] = Along(path, f);
                return points;
            }
        }
    }
}
