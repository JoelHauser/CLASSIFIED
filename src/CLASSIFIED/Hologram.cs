using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The debrief's kit: what each unit lays on the table (its weapon, then its equipment), and
    /// each item's names and figures in English and Russian (Kit.Data.cs, from SPT's database).
    /// </summary>
    internal static partial class Kit
    {
        internal sealed class Spec
        {
            internal readonly string Id;

            /// <summary>
            /// weapon, helmet, armor, rig, nvg, headset, backpack, med, device, food, intel, radio,
            /// tech, valuable; since 1.1.0 also key, keycard, ammo, grenade, container, face,
            /// eyewear, optic and suppressor.
            /// </summary>
            internal readonly string Kind;
            internal readonly string KindEn;
            internal readonly string KindRu;
            internal readonly string Short;
            internal readonly string Name;
            internal readonly string ShortRu;
            internal readonly string NameRu;

            /// <summary>The figures under the name: calibre and rate for a weapon, class and material for armour...</summary>
            internal readonly string LineEn;
            internal readonly string LineRu;

            internal bool Weapon => Kind == "weapon";

            internal Spec(string id, string kind, string kindEn, string kindRu, string shortName, string name, string shortRu, string nameRu, string lineEn, string lineRu)
            {
                Id = id;
                Kind = kind;
                KindEn = kindEn;
                KindRu = kindRu;
                Short = shortName;
                Name = name;
                ShortRu = shortRu;
                NameRu = nameRu;
                LineEn = lineEn;
                LineRu = lineRu;
            }
        }

        /// <summary>An item's figures. A weapon's builds (ak74m_1, ak74m_2) share its default's.</summary>
        internal static Spec Get(string id)
        {
            int build = id.LastIndexOf('_');
            if (build > 0 && int.TryParse(id.Substring(build + 1), out _))
            {
                id = id.Substring(0, build);
            }
            foreach (Spec spec in All)
            {
                if (spec.Id == id)
                {
                    return spec;
                }
            }
            return All[0];
        }

        /// <summary>One of the game's own named builds of a weapon (Kit.Pools.cs): drawn as weapon_number.</summary>
        internal sealed class NamedBuild
        {
            internal readonly string Weapon;
            internal readonly int Number;
            internal readonly string En;
            internal readonly string Ru;

            internal NamedBuild(string weapon, int number, string en, string ru)
            {
                Weapon = weapon;
                Number = number;
                En = en;
                Ru = ru;
            }
        }

        /// <summary>The name of the build drawn (p90_12 is "SBR"), or null for a random build or equipment.</summary>
        internal static NamedBuild NameOf(string id)
        {
            foreach (NamedBuild n in Named)
            {
                if (id.Length == n.Weapon.Length + 1 + n.Number.ToString().Length && id.StartsWith(n.Weapon + "_")
                    && id.EndsWith("_" + n.Number))
                {
                    return n;
                }
            }
            return null;
        }

        /// <summary>
        /// How many builds each weapon has: its default (no suffix) and _1 to _11, put together at
        /// random; the game's named builds follow from _12 (Kit.Pools.cs).
        /// </summary>
        private const int Builds = 12;

        /// <summary>How a side's whole table reads: the hand-picked items, then what 1.1.0 added.</summary>
        private static string[] Side(string[] picked, string[] more)
        {
            var all = new List<string>(picked);
            foreach (string id in more)
            {
                if (!all.Contains(id))
                {
                    all.Add(id);
                }
            }
            return all.ToArray();
        }

        private static string[] _bearWeapons, _usecWeapons, _bearGear, _usecGear;
        private static string[] AllBearWeapons => _bearWeapons ?? (_bearWeapons = Side(BearWeapons, MoreBearWeapons));
        private static string[] AllUsecWeapons => _usecWeapons ?? (_usecWeapons = Side(UsecWeapons, MoreUsecWeapons));
        private static string[] AllBearGear => _bearGear ?? (_bearGear = Side(BearGear, MoreBearGear));
        private static string[] AllUsecGear => _usecGear ?? (_usecGear = Side(UsecGear, MoreUsecGear));

        private static readonly string[] BearWeapons =
        {
            "ak12", "ak105", "ak74m", "ak74n", "aks74", "aks74u", "ak103", "ak104", "svds", "sv98", "vss", "val", "vityaz",
            "pkm", "pkp", "saiga12", "ash12", "sks", "mosin", "sr2m", "vepr", "pl15", "saiga9", "ks23", "veprhunter",
            "ak101", "ak102", "aks74n", "rpk16", "a91", "sr3m", "mp155", "svt40", "opsks", "mosininf", "ppsh"
        };

        private static readonly string[] UsecWeapons =
        {
            "m4a1", "hk416", "mk16", "mk17", "mcx", "spear", "mk47", "sr25", "g28", "rsass", "m1a", "rem700", "tx15", "mdr",
            "aug", "m60", "mp5", "mp5k", "mpx", "ump", "vector", "m1911", "m9a3", "fiveseven", "usp", "rfb",
            "ak545", "rd704", "g36", "uzi", "uzipro", "stm9", "m870", "m590", "m3", "aa12", "trg", "mk18", "deagle", "adar", "raptor"
        };

        private static readonly string[] BearGear =
        {
            "6b47", "altyn", "ssh68", "kiver", "lshz2dtm", "vulkan5", "6b43", "6b13", "gzhel", "korundvm", "defender2", "6b3",
            "kirasa", "pnv10t", "gssh01", "6sh118", "salewa", "grizzly", "tushonka", "iskra", "intel", "cofdm",
            "zsh12m", "ulach", "redutm", "zhuk6a", "tv110", "tarzan", "azimut", "attack2", "berkut", "ifak", "cms", "propital",
            "flash", "vpx", "virtex", "gpu", "battery", "gasan", "tetriz", "lion",
            "tor2", "ryst", "maska", "6b23", "korakulon", "zhuk3", "6b2", "umka", "bankrobber", "triton", "takedown", "pillbox",
            "pnv57", "hdd", "gyro", "mcb", "labrys", "bluefolders", "smt", "defib", "raven", "carkit"
        };

        private static readonly string[] UsecGear =
        {
            "fast", "airframe", "exfil", "mich2001", "bastion", "avs", "slick", "cpc", "hexgrid", "gpnvg", "pvs14", "comtac",
            "blackjack", "nicecomm", "medpack", "betav2", "surv12", "ledx", "grizzly", "intel", "cofdm",
            "iotv", "otv", "paca", "commando", "thunderbolt", "pilgrim", "paratus", "sordin", "razor", "earmor", "n15",
            "t7", "ifak", "cms", "propital", "flash", "vpx", "virtex", "gpu", "battery", "tetriz", "lion",
            "achhc", "tc800", "trooper", "a18", "thorcrv", "hpc", "drd", "d3crx", "idearig", "mmac", "strandhogg", "tv115",
            "mech", "lbt", "trooper35", "satl", "dragonegg", "drawbridge", "xcel", "comtac5", "liberator", "tsport",
            "aesa", "ultralink", "rfid", "catstatue", "defib", "carkit"
        };

        /// <summary>Each unit's own weapons and gear, which it shows more often than the rest of its side's. Null for none.</summary>
        private static string[][] Pools(ThemeChoice unit)
        {
            string[][] picked = HandPools(unit);
            string[][] more = MorePools(unit);
            if (picked == null || more == null)
            {
                return picked;
            }
            return new[] { Side(picked[0], more[0]), Side(picked[1], more[1]) };
        }

        /// <summary>The units' own weapons and gear as picked by hand, before 1.1.0.</summary>
        private static string[][] HandPools(ThemeChoice unit)
        {
            switch (unit)
            {
                case ThemeChoice.BearCommand: return new[] { new[] { "ak12", "ak105", "ash12", "pl15", "ak101", "rpk16" }, new[] { "6b47", "6b43", "gssh01", "korundvm", "vulkan5", "intel", "zhuk6a", "redutm", "flash", "tor2", "6b23" } };
                case ThemeChoice.BearReconnaissance: return new[] { new[] { "svds", "sv98", "vss", "mosin", "veprhunter", "svt40", "mosininf" }, new[] { "pnv10t", "6sh118", "6b47", "kirasa", "gssh01", "tarzan", "berkut", "takedown", "pnv57" } };
                case ThemeChoice.BearSignals: return new[] { new[] { "aks74u", "aks74", "sr2m", "saiga9", "a91" }, new[] { "gssh01", "cofdm", "iskra", "6b3", "lshz2dtm", "vpx", "virtex", "battery", "gasan", "mcb", "gyro" } };
                case ThemeChoice.BearCounterintelligence: return new[] { new[] { "val", "vss", "vityaz", "sr2m", "pl15", "sr3m" }, new[] { "altyn", "intel", "6b13", "kiver", "defender2", "flash", "tetriz", "ulach", "labrys", "smt", "bluefolders" } };
                case ThemeChoice.BearSupply: return new[] { new[] { "pkm", "pkp", "ak103", "sks", "ks23", "opsks", "ppsh" }, new[] { "tushonka", "iskra", "6sh118", "gzhel", "6b3", "attack2", "azimut", "gpu", "lion", "umka", "bankrobber" } };
                case ThemeChoice.BearMedical: return new[] { new[] { "ak74m", "aks74", "ak74n", "saiga12", "mp155", "aks74n" }, new[] { "salewa", "grizzly", "ssh68", "medpack", "kirasa", "cms", "ifak", "propital", "defib", "carkit" } };
                case ThemeChoice.UsecOperations: return new[] { new[] { "m4a1", "mk16", "mdr", "aug", "usp", "rfb", "adar", "m590" }, new[] { "fast", "avs", "pvs14", "cpc", "comtac", "iotv", "earmor" } };
                case ThemeChoice.UsecIntelligence: return new[] { new[] { "sr25", "m1a", "rem700", "g28", "rsass", "trg", "mk18" }, new[] { "gpnvg", "intel", "exfil", "hexgrid", "bastion", "t7", "flash", "thunderbolt" } };
                case ThemeChoice.UsecSignals: return new[] { new[] { "mcx", "mk47", "mpx", "fiveseven", "raptor", "stm9" }, new[] { "comtac", "cofdm", "nicecomm", "mich2001", "slick", "battery", "vpx", "razor", "gpu" } };
                case ThemeChoice.UsecLogistics: return new[] { new[] { "m60", "mk47", "mk17", "rfb", "aa12", "m870" }, new[] { "blackjack", "slick", "comtac", "betav2", "cpc", "pilgrim", "paratus", "commando", "tetriz" } };
                case ThemeChoice.UsecMedical: return new[] { new[] { "mp5", "mp5k", "ump", "vector", "m9a3", "uzi", "uzipro" }, new[] { "surv12", "ledx", "grizzly", "medpack", "mich2001", "cms", "ifak", "propital", "defib", "carkit" } };
                case ThemeChoice.UsecCommand: return new[] { new[] { "hk416", "mk16", "spear", "tx15", "m1911", "ak545", "rd704", "deagle" }, new[] { "airframe", "slick", "gpnvg", "bastion", "hexgrid", "otv", "sordin", "n15" } };
                default: return null;
            }
        }

        /// <summary>Whether the unit has a table at all.</summary>
        internal static bool Has(ThemeChoice unit) => Pools(unit) != null;

        /// <summary>
        /// This launch's table for the unit: a weapon in one of its builds, then three pieces of
        /// gear. The weapon is the unit's own about half the time, else any of its side's, and
        /// over launches its side's whole armoury comes round before any repeats (Archive). Each
        /// build is put together at random; the stock one never shows. The same
        /// <paramref name="random"/> draws the same table, so laying the screen out again keeps it.
        /// </summary>
        internal static string[] Draw(ThemeChoice unit, Faction faction, System.Random random)
        {
            string[][] pools = Pools(unit);
            if (pools == null)
            {
                return null;
            }
            string[] side = faction == Faction.Usec ? AllUsecWeapons : AllBearWeapons;
            string weapon = random.Next(100) < 45
                ? pools[0][random.Next(pools[0].Length)]
                : side[Archive.PickShuffled("kit.weapon." + faction, new List<string>(side), random)];
            // Always one of the random builds, or one of the game's named ones: the stock ones
            // carry no attachments to show. A weapon with named builds shows one about a third
            // of the time.
            int build = FewerBuilds.TryGetValue(weapon, out int[] few)
                ? few[random.Next(few.Length)]
                : 1 + random.Next(Builds - 1);
            var names = new List<int>();
            foreach (NamedBuild n in Named)
            {
                if (n.Weapon == weapon)
                {
                    names.Add(n.Number);
                }
            }
            if (names.Count > 0 && random.Next(100) < 33)
            {
                build = names[random.Next(names.Count)];
            }

            // Gear: the unit's own first, then its side's, three different pieces.
            var gear = new List<string>();
            var own = new List<string>(pools[1]);
            var all = new List<string>(faction == Faction.Usec ? AllUsecGear : AllBearGear);
            while (gear.Count < 3 && (own.Count > 0 || all.Count > 0))
            {
                List<string> from = own.Count > 0 && (random.Next(100) < 60 || all.Count == 0) ? own : all;
                string pick = from[random.Next(from.Count)];
                from.Remove(pick);
                if (!gear.Contains(pick))
                {
                    gear.Add(pick);
                }
            }
            var table = new List<string> { build == 0 ? weapon : weapon + "_" + build };
            table.AddRange(gear);
            return table.ToArray();
        }
    }

    /// <summary>
    /// An item's 3D model, from the game's own parts (scripts/make_weapons.py): a few thousand
    /// triangles, their normals, every edge with the faces either side, and for a weapon a point
    /// in the middle of each part for the callouts. Centred, within one unit of the centre; a
    /// weapon's barrel runs along +x, and everything's top is +y. Read from the DLL the first time
    /// it is asked for, then kept.
    /// </summary>
    internal sealed class WeaponModel
    {
        internal Vector3[] Vertices;
        internal int[] Faces;
        internal Vector3[] Normals;
        internal int[] EdgeA;
        internal int[] EdgeB;
        internal int[] EdgeF0;
        internal int[] EdgeF1;
        internal bool[] Crease;
        internal string[] AnchorSlots;

        /// <summary>The part at each anchor by its short name, English and Russian; empty where the model has none.</summary>
        internal string[] AnchorNames;
        internal string[] AnchorNamesRu;
        internal Vector3[] Anchors;

        /// <summary>The lowest and highest points, for the table under it and the labels over it.</summary>
        internal float Floor;
        internal float Top;

        /// <summary>How far it reaches from its upright axis: what it sweeps as it turns.</summary>
        internal float Radius;

        /// <summary>The model's x range, the way the build-in runs.</summary>
        internal float MinX;
        internal float MaxX;

        private static readonly Dictionary<string, WeaponModel> Cache = new Dictionary<string, WeaponModel>();

        internal static WeaponModel Load(string id)
        {
            if (id == null)
            {
                return null;
            }
            lock (Cache)
            {
                if (Cache.TryGetValue(id, out WeaponModel cached))
                {
                    return cached;
                }
            }
            WeaponModel model = Decode(id);
            lock (Cache)
            {
                Cache[id] = model;
            }
            return model;
        }

        /// <summary>Forgets every model read so far; the next table reads its own again.</summary>
        internal static void ClearCache()
        {
            lock (Cache)
            {
                Cache.Clear();
            }
        }

        /// <summary>
        /// Starts reading models on a worker thread, so a later Load finds them ready instead of
        /// unpacking one on the frame its item comes onto the table. Reading is plain C#: no Unity
        /// calls, so it is safe off the main thread (logging goes through BepInEx's thread-safe log).
        /// </summary>
        internal static void Prefetch(IEnumerable<string> ids)
        {
            var wanted = new List<string>();
            lock (Cache)
            {
                foreach (string id in ids)
                {
                    if (id != null && !Cache.ContainsKey(id) && !wanted.Contains(id))
                    {
                        wanted.Add(id);
                    }
                }
            }
            if (wanted.Count == 0)
            {
                return;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (string id in wanted)
                {
                    WeaponModel model = Decode(id);
                    lock (Cache)
                    {
                        if (!Cache.ContainsKey(id))
                        {
                            Cache[id] = model;
                        }
                    }
                }
            });
        }

        private static WeaponModel Decode(string id)
        {
            WeaponModel model = null;
            string resource = "CLASSIFIED.item_" + id + ".bin";
            try
            {
                using (Stream stream = typeof(WeaponModel).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream != null)
                    {
                        // Gzipped by the script.
                        using (var unzipped = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress))
                        {
                            model = Read(new BinaryReader(unzipped));
                        }
                    }
                    else
                    {
                        ClassifiedPlugin.Log.LogWarning("Hologram: " + resource + " is missing from the DLL.");
                    }
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Hologram: could not read " + resource + ": " + e.Message);
                model = null;
            }
            return model;
        }

        /// <summary>A surface turning by more than this across an edge makes the edge a crease, drawn whichever way it faces.</summary>
        private const float CreaseDegrees = 32f;

        private static WeaponModel Read(BinaryReader r)
        {
            if (new string(r.ReadChars(4)) != "CLW2")
            {
                throw new InvalidDataException("not a model in the current format");
            }
            int vertices = r.ReadInt32();
            int faces = r.ReadInt32();
            int anchors = r.ReadInt32();
            var m = new WeaponModel
            {
                Vertices = new Vector3[vertices],
                Faces = new int[faces * 3],
                Normals = new Vector3[faces],
                AnchorSlots = new string[anchors],
                AnchorNames = new string[anchors],
                AnchorNamesRu = new string[anchors],
                Anchors = new Vector3[anchors],
                Floor = float.MaxValue,
                Top = float.MinValue,
                MinX = float.MaxValue,
                MaxX = float.MinValue
            };
            const float Q = 1f / 32767f;
            for (int i = 0; i < vertices; i++)
            {
                Vector3 v = new Vector3(r.ReadInt16() * Q, r.ReadInt16() * Q, r.ReadInt16() * Q);
                m.Vertices[i] = v;
                m.Floor = Mathf.Min(m.Floor, v.y);
                m.Top = Mathf.Max(m.Top, v.y);
                m.MinX = Mathf.Min(m.MinX, v.x);
                m.MaxX = Mathf.Max(m.MaxX, v.x);
                m.Radius = Mathf.Max(m.Radius, Mathf.Sqrt(v.x * v.x + v.z * v.z));
            }
            for (int i = 0; i < faces * 3; i++)
            {
                m.Faces[i] = r.ReadUInt16();
            }
            for (int i = 0; i < anchors; i++)
            {
                m.Anchors[i] = new Vector3(r.ReadInt16() * Q, r.ReadInt16() * Q, r.ReadInt16() * Q);
                // UTF-8, its length in bytes: "slot", or "slot|English name|Russian name".
                int length = r.ReadByte();
                string[] fields = System.Text.Encoding.UTF8.GetString(r.ReadBytes(length)).Split('|');
                m.AnchorSlots[i] = fields[0];
                m.AnchorNames[i] = fields.Length > 1 ? fields[1] : string.Empty;
                m.AnchorNamesRu[i] = fields.Length > 2 ? fields[2] : m.AnchorNames[i];
            }

            // Face normals (the faces come wound outward), then every edge with the faces either
            // side, and whether it is a crease.
            for (int f = 0; f < faces; f++)
            {
                Vector3 a = m.Vertices[m.Faces[f * 3]];
                Vector3 b = m.Vertices[m.Faces[f * 3 + 1]];
                Vector3 c = m.Vertices[m.Faces[f * 3 + 2]];
                m.Normals[f] = Vector3.Cross(b - a, c - a).normalized;
            }
            var table = new Dictionary<long, int>(faces * 2);
            var edgeA = new List<int>(faces * 2);
            var edgeB = new List<int>(faces * 2);
            var face0 = new List<int>(faces * 2);
            var face1 = new List<int>(faces * 2);
            var shared = new List<int>(faces * 2);
            for (int f = 0; f < faces; f++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int u = m.Faces[f * 3 + k];
                    int v = m.Faces[f * 3 + (k + 1) % 3];
                    long key = Math.Min(u, v) * 65536L + Math.Max(u, v);
                    if (table.TryGetValue(key, out int e))
                    {
                        if (face1[e] < 0)
                        {
                            face1[e] = f;
                        }
                        shared[e]++;
                    }
                    else
                    {
                        table[key] = edgeA.Count;
                        edgeA.Add(Math.Min(u, v));
                        edgeB.Add(Math.Max(u, v));
                        face0.Add(f);
                        face1.Add(-1);
                        shared.Add(1);
                    }
                }
            }
            int edges = edgeA.Count;
            m.EdgeA = edgeA.ToArray();
            m.EdgeB = edgeB.ToArray();
            m.EdgeF0 = face0.ToArray();
            m.EdgeF1 = face1.ToArray();
            m.Crease = new bool[edges];
            float cos = Mathf.Cos(CreaseDegrees * Mathf.Deg2Rad);
            for (int e = 0; e < edges; e++)
            {
                m.Crease[e] = m.EdgeF1[e] < 0 || shared[e] > 2 || Vector3.Dot(m.Normals[m.EdgeF0[e]], m.Normals[m.EdgeF1[e]]) < cos;
            }
            return m;
        }
    }

    /// <summary>
    /// An item turning over the debrief table as a hologram, drawn on the UI like everything
    /// else: the faces turned to the viewer filled faintly and lit from above, the creases and the
    /// outline drawn over them, the hidden edges faint behind, a ring of ticks turning on the table
    /// under it. A scan band runs along it and brightens what it passes. It builds in along its
    /// length behind a soft, bright edge, and dissolves the same way when the next item comes.
    ///
    /// The view (turn and tilt) is set once a frame in <see cref="Tick"/>, and both the mesh and
    /// <see cref="Project"/> use it, so the callouts point where the drawing is. Rebuilt at most
    /// <see cref="Rate"/> times a second, on a canvas of its own. Lines are quads over a soft
    /// profile texture, so they are smooth at a single quad each.
    /// </summary>
    internal sealed class Hologram : MaskableGraphic
    {
        /// <summary>Rebuilds a second, at most, while it builds in; once whole, it turns slowly enough for fewer.</summary>
        private const float Rate = 60f;
        private const float SettledRate = 60f;
        private const float Perspective = 0.14f;

        /// <summary>How far behind the build line a part takes to come to full strength, in model units.</summary>
        private const float Soft = 0.22f;

        internal WeaponModel Model;

        /// <summary>Canvas units for one model unit.</summary>
        internal float Scale = 100f;
        internal float Yaw;
        internal float Pitch = 0.32f;
        internal Color32 Ink = new Color32(140, 200, 255, 255);

        /// <summary>How much of the item is there, 0 to 1, along its length: building in, or dissolving.</summary>
        internal float Reveal = 1f;

        /// <summary>Where the scan band is along the item, 0 to 1; outside that, no band.</summary>
        internal float ScanAt = -9f;

        /// <summary>Overall strength, for flicker and fades.</summary>
        internal float Strength = 1f;

        /// <summary>The turntable's ticks turn on their own.</summary>
        internal float TableTurn;

        /// <summary>The table's radius, in model units: just outside what the item sweeps.</summary>
        internal float TableRadius => Model != null ? Mathf.Max(0.35f, Model.Radius * 1.06f) : 1f;

        /// <summary>How high the table sits, in model units.</summary>
        internal float TableHeight => Model != null ? Model.Floor - 0.06f : -0.5f;

        private float _builtAt = -1f;
        private Matrix4x4 _rotation = Matrix4x4.identity;
        private Vector2[] _screen = new Vector2[0];
        private bool[] _front = new bool[0];

        private static Texture2D _profile;

        /// <summary>A line's cross-section: clear at both edges, solid in the middle.</summary>
        private static Texture2D Profile
        {
            get
            {
                if (_profile != null)
                {
                    return _profile;
                }
                byte[] alpha = { 0, 70, 190, 255, 255, 190, 70, 0 };
                _profile = new Texture2D(1, alpha.Length, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontUnloadUnusedAsset
                };
                var pixels = new Color32[alpha.Length];
                for (int i = 0; i < alpha.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, alpha[i]);
                }
                _profile.SetPixels32(pixels);
                _profile.Apply(false, true);
                return _profile;
            }
        }

        public override Texture mainTexture => Profile;

        internal static Hologram Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // A canvas of its own: it is rebuilt many times a second.
            go.AddComponent<Canvas>();
            var hologram = go.AddComponent<Hologram>();
            hologram.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return hologram;
        }

        /// <summary>Sets this frame's view, and rebuilds if it is time to. Called every frame, before anything projects.</summary>
        internal void Tick(float now)
        {
            _rotation = Matrix4x4.Rotate(Quaternion.Euler(Pitch * Mathf.Rad2Deg, 0f, 0f) * Quaternion.Euler(0f, Yaw * Mathf.Rad2Deg, 0f));
            if (Model == null || now - _builtAt < 1f / (Reveal < 1f ? Rate : SettledRate))
            {
                return;
            }
            _builtAt = now;
            rectTransform.sizeDelta = new Vector2(Scale * 2.4f, Scale * 2f);
            SetVerticesDirty();
        }

        /// <summary>Where a point of the model shows, in canvas units from the graphic's centre, in this frame's view.</summary>
        internal Vector2 Project(Vector3 p, out float depth)
        {
            Vector3 q = _rotation.MultiplyVector(p);
            float f = 1f / (1f - q.z * Perspective);
            depth = q.z;
            return new Vector2(q.x * f * Scale, q.y * f * Scale);
        }

        /// <summary>How high and low the drawing reaches on screen at a given tilt, in model units: the item's top, the table's front.</summary>
        internal void Extent(float pitch, out float top, out float bottom)
        {
            float c = Mathf.Cos(pitch);
            float s = Mathf.Sin(pitch);
            float radius = Model != null ? Model.Radius : 1f;
            top = (Model != null ? Model.Top : 0.5f) * c + radius * s;
            bottom = TableHeight * c - TableRadius * s;
        }

        // The mesh, built straight into these lists and handed to the canvas renderer: the UI's
        // own VertexHelper keeps eight lists per vertex and was most of the cost.
        private Mesh _mesh;
        private readonly List<Vector3> _positions = new List<Vector3>(16384);
        private readonly List<Color32> _colors = new List<Color32>(16384);
        private readonly List<Vector2> _uvs = new List<Vector2>(16384);
        private readonly List<int> _indices = new List<int>(24576);
        private Vector3[] _turned = new Vector3[0];
        private float[] _facing = new float[0];
        private static readonly Vector2 Solid = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 EdgeLow = new Vector2(0.5f, 0f);
        private static readonly Vector2 EdgeHigh = new Vector2(0.5f, 1f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        protected override void UpdateGeometry()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();
            }
            // Counted with the terminal's own work, so the log's ms/frame includes the hologram.
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            _positions.Clear();
            _colors.Clear();
            _uvs.Clear();
            _indices.Clear();
            Build();
            _mesh.Clear();
            _mesh.SetVertices(_positions);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_indices, 0, false);
            canvasRenderer.SetMesh(_mesh);
            StartupTracker.NoteTerminal((System.Diagnostics.Stopwatch.GetTimestamp() - start) / (float)System.Diagnostics.Stopwatch.Frequency, frame: false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        private void Build()
        {
            WeaponModel m = Model;
            if (m == null || Strength <= 0.01f || Reveal <= 0f)
            {
                return;
            }

            int n = m.Vertices.Length;
            if (_screen.Length != n)
            {
                _screen = new Vector2[n];
            }
            for (int i = 0; i < n; i++)
            {
                _screen[i] = Project(m.Vertices[i], out _);
            }
            int faces = m.Normals.Length;
            if (_front.Length != faces)
            {
                _front = new bool[faces];
                _turned = new Vector3[faces];
                _facing = new float[faces];
            }
            for (int f = 0; f < faces; f++)
            {
                _turned[f] = _rotation.MultiplyVector(m.Normals[f]);
                _front[f] = _turned[f].z > -Grazing;
                _facing[f] = Facing(_turned[f].z);
            }

            // The build line runs along x from one end to the other, with room for the soft edge.
            float cut = Mathf.Lerp(m.MinX - 0.02f, m.MaxX + Soft, Reveal);
            DrawTable();
            DrawFaces(m, cut);
            DrawEdges(m, cut);
        }

        /// <summary>
        /// Faces and edges turning past the viewer fade over this much of a turn instead of popping:
        /// on a small, finely faceted part (a light, an optic) dozens cross at once and flickered.
        /// </summary>
        private const float Grazing = 0.12f;

        /// <summary>How much a face turned <paramref name="z"/> toward the viewer shows: none edge-on or away, all once it is turned well toward.</summary>
        private static float Facing(float z)
        {
            float k = Mathf.Clamp01((z + Grazing) / (2f * Grazing));
            return k * k * (3f - 2f * k);
        }

        /// <summary>How built a point is: 0 past the build line, coming to 1 a little behind it.</summary>
        private static float Built(float x, float cut) => Mathf.Clamp01((cut - x) / Soft);

        /// <summary>The bright edge just behind the build line, while it moves.</summary>
        private float Leading(float x, float cut)
        {
            if (Reveal >= 1f)
            {
                return 0f;
            }
            float d = cut - x;
            return d < 0f || d > Soft ? 0f : Mathf.Sin(d / Soft * Mathf.PI);
        }

        /// <summary>The faces turned to the viewer, faint, brighter where the light from above and in front falls.</summary>
        private void DrawFaces(WeaponModel m, float cut)
        {
            var light = new Vector3(0.25f, 0.75f, 0.6f).normalized;
            for (int f = 0; f < m.Normals.Length; f++)
            {
                if (!_front[f])
                {
                    continue;
                }
                int a = m.Faces[f * 3], b = m.Faces[f * 3 + 1], c = m.Faces[f * 3 + 2];
                float x = (m.Vertices[a].x + m.Vertices[b].x + m.Vertices[c].x) / 3f;
                float built = Built(x, cut);
                if (built <= 0f)
                {
                    continue;
                }
                float lit = Mathf.Max(0f, Vector3.Dot(_turned[f], light));
                Color32 color = Tint(((10f + 34f * lit + 30f * ScanGlow(x, m)) * built + 40f * Leading(x, cut)) * _facing[f]);
                if (color.a == 0)
                {
                    continue;
                }
                int start = _positions.Count;
                _positions.Add(_screen[a]);
                _positions.Add(_screen[b]);
                _positions.Add(_screen[c]);
                _colors.Add(color);
                _colors.Add(color);
                _colors.Add(color);
                _uvs.Add(Solid);
                _uvs.Add(Solid);
                _uvs.Add(Solid);
                _indices.Add(start);
                _indices.Add(start + 1);
                _indices.Add(start + 2);
            }
        }

        /// <summary>
        /// The creases turned to the viewer and the outline, over the faces, cut exactly at the
        /// build line. The creases round the back are left out: they were barely visible, and
        /// were half the lines.
        /// </summary>
        private void DrawEdges(WeaponModel m, float cut)
        {
            float weight = Mathf.Clamp(Scale / 220f, 0.75f, 1.4f);
            // Drawn small, the creases crowd into a shimmer: fewer of them, fainter.
            float creaseAlpha = Mathf.Lerp(70f, 120f, Mathf.Clamp01((Scale - 120f) / 160f));
            for (int e = 0; e < m.EdgeA.Length; e++)
            {
                int f0 = m.EdgeF0[e];
                int f1 = m.EdgeF1[e];
                float k0 = _facing[f0];
                float k1 = f1 >= 0 ? _facing[f1] : 0f;
                // An outline where one side faces the viewer and the other away, as strongly as the
                // two disagree; a crease as strongly as its nearer side faces the viewer.
                float outlineK = f1 < 0 ? k0 : Mathf.Abs(k0 - k1);
                float creaseK = m.Crease[e] ? Mathf.Max(k0, k1) : 0f;
                if (outlineK < 0.02f && creaseK < 0.02f)
                {
                    continue;
                }
                bool outline = outlineK >= creaseK * 0.6f;
                int a = m.EdgeA[e];
                int b = m.EdgeB[e];
                float xa = m.Vertices[a].x;
                float xb = m.Vertices[b].x;
                if (xa > cut && xb > cut)
                {
                    continue;
                }
                Vector2 pa = _screen[a];
                Vector2 pb = _screen[b];
                // An edge across the build line is drawn up to it.
                if (xa > cut)
                {
                    pa = Vector2.Lerp(pb, pa, (cut - xb) / (xa - xb));
                    xa = cut;
                }
                else if (xb > cut)
                {
                    pb = Vector2.Lerp(pa, pb, (cut - xa) / (xb - xa));
                    xb = cut;
                }
                float x = (xa + xb) * 0.5f;
                float alpha = outline ? 235f * outlineK + creaseAlpha * creaseK * (1f - outlineK) : creaseAlpha * creaseK;
                alpha = Mathf.Min(255f, (alpha + 120f * ScanGlow(x, m) * Mathf.Max(outlineK, creaseK)) * Built(x, cut) + 220f * Leading(x, cut));
                Line(pa, pb, (outline ? 1.6f : 1.1f) * weight, Tint(alpha));
            }
        }

        /// <summary>The turntable: a ring on the floor under the item, its ticks turning.</summary>
        private void DrawTable()
        {
            const int Ticks = 72;
            float y = TableHeight;
            float r = TableRadius;
            float shown = Mathf.Clamp01(Reveal * 2f);
            Vector2 last = Vector2.zero;
            for (int i = 0; i <= Ticks; i++)
            {
                float angle = i * Mathf.PI * 2f / Ticks;
                Vector2 s = Project(new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r), out _);
                if (i > 0)
                {
                    Line(last, s, 1.4f, Tint(70f * shown));
                }
                last = s;
                float spun = angle + TableTurn;
                bool major = i % 6 == 0;
                float inner = r - (major ? 0.1f : 0.05f) * r;
                Line(Project(new Vector3(Mathf.Cos(spun) * inner, y, Mathf.Sin(spun) * inner), out _),
                    Project(new Vector3(Mathf.Cos(spun) * r, y, Mathf.Sin(spun) * r), out _), major ? 1.6f : 1.1f, Tint((major ? 90f : 45f) * shown));
            }
            for (int i = 1; i <= Ticks; i++)
            {
                if (i % 3 == 0)
                {
                    continue;
                }
                float a0 = (i - 1) * Mathf.PI * 2f / Ticks;
                float a1 = i * Mathf.PI * 2f / Ticks;
                Vector2 s0 = Project(new Vector3(Mathf.Cos(a0) * r * 0.82f, y, Mathf.Sin(a0) * r * 0.82f), out _);
                Vector2 s1 = Project(new Vector3(Mathf.Cos(a1) * r * 0.82f, y, Mathf.Sin(a1) * r * 0.82f), out _);
                Line(s0, s1, 1.1f, Tint(34f * shown));
            }
        }

        private float ScanGlow(float x, WeaponModel m)
        {
            if (ScanAt < -1f)
            {
                return 0f;
            }
            float at = Mathf.Lerp(m.MinX - 0.15f, m.MaxX + 0.15f, ScanAt);
            float d = Mathf.Abs(x - at);
            return d > 0.16f ? 0f : 1f - d / 0.16f;
        }

        private Color32 Tint(float alpha) => new Color32(Ink.r, Ink.g, Ink.b, (byte)Mathf.Clamp(alpha * Strength * (Ink.a / 255f), 0f, 255f));

        /// <summary>A line as one quad across the soft profile.</summary>
        private void Line(Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.01f || color.a == 0)
            {
                return;
            }
            // Wide enough for the soft edges to fall outside the line's own width.
            Vector2 side = new Vector2(-d.y, d.x) / length * (width * 0.5f + 1f);
            int start = _positions.Count;
            _positions.Add(a - side);
            _positions.Add(a + side);
            _positions.Add(b + side);
            _positions.Add(b - side);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _uvs.Add(EdgeLow);
            _uvs.Add(EdgeHigh);
            _uvs.Add(EdgeHigh);
            _uvs.Add(EdgeLow);
            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }
    }
}
