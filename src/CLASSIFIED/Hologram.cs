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

            /// <summary>weapon, helmet, armor, nvg, headset, backpack, med, device, food, intel or radio.</summary>
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

        internal static Spec Get(string id)
        {
            foreach (Spec spec in All)
            {
                if (spec.Id == id)
                {
                    return spec;
                }
            }
            return All[0];
        }

        /// <summary>Each unit's table, in the order it is shown: the weapon first. Null for none.</summary>
        internal static string[] For(ThemeChoice unit)
        {
            switch (unit)
            {
                case ThemeChoice.BearCommand: return new[] { "ak12", "6b47", "6b43", "gssh01" };
                case ThemeChoice.BearReconnaissance: return new[] { "svds", "pnv10t", "6sh118", "6b47" };
                case ThemeChoice.BearSignals: return new[] { "aks74u", "gssh01", "cofdm", "iskra" };
                case ThemeChoice.BearCounterintelligence: return new[] { "val", "altyn", "intel", "6b13" };
                case ThemeChoice.BearSupply: return new[] { "pkm", "tushonka", "iskra", "6sh118" };
                case ThemeChoice.BearMedical: return new[] { "ak74m", "salewa", "grizzly", "ssh68" };
                case ThemeChoice.UsecOperations: return new[] { "m4a1", "fast", "avs", "pvs14" };
                case ThemeChoice.UsecIntelligence: return new[] { "sr25", "gpnvg", "intel", "exfil" };
                case ThemeChoice.UsecSignals: return new[] { "mcx", "comtac", "cofdm", "nicecomm" };
                case ThemeChoice.UsecLogistics: return new[] { "m60", "blackjack", "slick", "comtac" };
                case ThemeChoice.UsecMedical: return new[] { "mp5", "surv12", "ledx", "grizzly" };
                case ThemeChoice.UsecCommand: return new[] { "hk416", "airframe", "slick", "gpnvg" };
                default: return null;
            }
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
            if (Cache.TryGetValue(id, out WeaponModel cached))
            {
                return cached;
            }
            WeaponModel model = null;
            string resource = "CLASSIFIED.item_" + id + ".bin";
            try
            {
                using (Stream stream = typeof(WeaponModel).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream != null)
                    {
                        model = Read(new BinaryReader(stream));
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
            Cache[id] = model;
            return model;
        }

        private static WeaponModel Read(BinaryReader r)
        {
            if (new string(r.ReadChars(4)) != "CLW1")
            {
                throw new InvalidDataException("not a weapon model");
            }
            int vertices = r.ReadInt32();
            int faces = r.ReadInt32();
            int edges = r.ReadInt32();
            int anchors = r.ReadInt32();
            var m = new WeaponModel
            {
                Vertices = new Vector3[vertices],
                Faces = new int[faces * 3],
                Normals = new Vector3[faces],
                EdgeA = new int[edges],
                EdgeB = new int[edges],
                EdgeF0 = new int[edges],
                EdgeF1 = new int[edges],
                Crease = new bool[edges],
                AnchorSlots = new string[anchors],
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
            for (int i = 0; i < faces; i++)
            {
                m.Normals[i] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()) / 127f;
            }
            for (int i = 0; i < edges; i++)
            {
                m.EdgeA[i] = r.ReadUInt16();
                m.EdgeB[i] = r.ReadUInt16();
                m.EdgeF0[i] = r.ReadInt16();
                m.EdgeF1[i] = r.ReadInt16();
                m.Crease[i] = r.ReadByte() != 0;
            }
            for (int i = 0; i < anchors; i++)
            {
                m.Anchors[i] = new Vector3(r.ReadInt16() * Q, r.ReadInt16() * Q, r.ReadInt16() * Q);
                int length = r.ReadByte();
                m.AnchorSlots[i] = new string(r.ReadChars(length));
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
        /// <summary>Rebuilds a second, at most.</summary>
        private const float Rate = 60f;
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
            if (Model == null || now - _builtAt < 1f / Rate)
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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
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
            }
            for (int f = 0; f < faces; f++)
            {
                _front[f] = _rotation.MultiplyVector(m.Normals[f]).z > 0f;
            }

            // The build line runs along x from one end to the other, with room for the soft edge.
            float cut = Mathf.Lerp(m.MinX - 0.02f, m.MaxX + Soft, Reveal);
            DrawTable(vh);
            DrawFaces(vh, m, cut);
            DrawEdges(vh, m, cut);
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
        private void DrawFaces(VertexHelper vh, WeaponModel m, float cut)
        {
            var light = new Vector3(0.25f, 0.75f, 0.6f).normalized;
            var uv = new Vector2(0.5f, 0.5f);
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
                float lit = Mathf.Max(0f, Vector3.Dot(_rotation.MultiplyVector(m.Normals[f]), light));
                Color32 color = Tint((10f + 34f * lit + 30f * ScanGlow(x, m)) * built + 40f * Leading(x, cut));
                int start = vh.currentVertCount;
                vh.AddVert(_screen[a], color, uv);
                vh.AddVert(_screen[b], color, uv);
                vh.AddVert(_screen[c], color, uv);
                vh.AddTriangle(start, start + 1, start + 2);
            }
        }

        /// <summary>The creases and the outline over the faces; the creases round the back faint. Cut exactly at the build line.</summary>
        private void DrawEdges(VertexHelper vh, WeaponModel m, float cut)
        {
            float weight = Mathf.Clamp(Scale / 220f, 0.75f, 1.4f);
            for (int e = 0; e < m.EdgeA.Length; e++)
            {
                int f0 = m.EdgeF0[e];
                int f1 = m.EdgeF1[e];
                bool seen = _front[f0] || (f1 >= 0 && _front[f1]);
                bool outline = f1 < 0 ? _front[f0] : _front[f0] != _front[f1];
                if (!outline && !m.Crease[e])
                {
                    continue;
                }
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
                float alpha = outline ? 235f : seen ? 120f : 30f;
                alpha = Mathf.Min(255f, (alpha + 120f * ScanGlow(x, m)) * Built(x, cut) + 220f * Leading(x, cut));
                float width = (outline ? 1.6f : seen ? 1.1f : 0.9f) * weight;
                Line(vh, pa, pb, width, Tint(alpha));
            }
        }

        /// <summary>The turntable: a ring on the floor under the item, its ticks turning.</summary>
        private void DrawTable(VertexHelper vh)
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
                    Line(vh, last, s, 1.4f, Tint(70f * shown));
                }
                last = s;
                float spun = angle + TableTurn;
                bool major = i % 6 == 0;
                float inner = r - (major ? 0.1f : 0.05f) * r;
                Line(vh, Project(new Vector3(Mathf.Cos(spun) * inner, y, Mathf.Sin(spun) * inner), out _),
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
                Line(vh, s0, s1, 1.1f, Tint(34f * shown));
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
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.01f || color.a == 0)
            {
                return;
            }
            // Wide enough for the soft edges to fall outside the line's own width.
            Vector2 side = new Vector2(-d.y, d.x) / length * (width * 0.5f + 1f);
            int start = vh.currentVertCount;
            vh.AddVert(a - side, color, new Vector2(0.5f, 0f));
            vh.AddVert(a + side, color, new Vector2(0.5f, 1f));
            vh.AddVert(b + side, color, new Vector2(0.5f, 1f));
            vh.AddVert(b - side, color, new Vector2(0.5f, 0f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
