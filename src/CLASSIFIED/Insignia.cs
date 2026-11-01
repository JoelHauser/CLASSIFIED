using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The factions' insignia: the game's own BEAR and USEC logos. Each is cut into the
    /// pieces that fly together when the terminal comes on (BEAR: the shield's two halves, the
    /// paw, the mountains, the name, the pin and the fang; USEC: the eagle, the four letters, the
    /// chevron and its bars), only ever along the gaps in the logo, so nothing seams once they meet.
    ///
    /// The pieces are packed into one picture per faction (Resources/insignia_*.png, white with
    /// the shape in alpha, tinted where drawn), made from the logos in art/ by
    /// scripts/make_insignia.py, which also writes the piece table in Insignia.Pieces.cs. Sizes
    /// are in units of the insignia's radius: the logo's farthest corner touches it, so it sits
    /// inside every frame. Loaded when first drawn and let go when the terminal hides.
    /// </summary>
    internal static partial class Insignia
    {
        /// <summary>The most pieces any insignia has.</summary>
        internal const int MaxPieces = 7;

        internal readonly struct Piece
        {
            internal readonly string Name;

            /// <summary>Where the piece sits in its faction's picture, in UV.</summary>
            internal readonly Rect Uv;

            /// <summary>Its centre in the assembled logo, in units of the radius, y up.</summary>
            internal readonly Vector2 Centre;

            /// <summary>Its size, in units of the radius.</summary>
            internal readonly Vector2 Size;

            /// <summary>The way it flies in from.</summary>
            internal readonly Vector2 Direction;

            internal Piece(string name, Rect uv, Vector2 centre, Vector2 size, Vector2 direction)
            {
                Name = name;
                Uv = uv;
                Centre = centre;
                Size = size;
                Direction = direction;
            }
        }

        private static readonly Texture2D[] Atlases = new Texture2D[3];

        internal static Piece[] PiecesOf(Faction faction) =>
            faction == Faction.Usec ? UsecPieces : BearPieces;

        /// <summary>The faction's picture, loaded the first time it is asked for. Null if it could not be read.</summary>
        internal static Texture2D Atlas(Faction faction)
        {
            if (faction == Faction.Unknown)
            {
                faction = Faction.Bear;
            }
            int slot = (int)faction;
            if (Atlases[slot] != null)
            {
                return Atlases[slot];
            }
            string resource = "CLASSIFIED.insignia_" + faction.ToString().ToLowerInvariant() + ".png";
            try
            {
                using (Stream stream = typeof(Insignia).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        ClassifiedPlugin.Log.LogWarning("Insignia: " + resource + " is missing from the DLL.");
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
                        ClassifiedPlugin.Log.LogWarning("Insignia: could not decode " + resource + ".");
                        return null;
                    }
                    texture.wrapMode = TextureWrapMode.Clamp;
                    // Mipmapped and trilinear: the same picture is drawn from a small emblem to a
                    // watermark half the screen tall. The slight negative bias keeps the small
                    // one crisp.
                    texture.filterMode = FilterMode.Trilinear;
                    texture.mipMapBias = -0.4f;
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    Atlases[slot] = texture;
                }
            }
            catch (Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Insignia: could not load " + resource + ": " + e.Message);
                return null;
            }
            return Atlases[slot];
        }

        /// <summary>Lets the pictures go. Anything still drawing one loads it again.</summary>
        internal static void Release()
        {
            for (int i = 0; i < Atlases.Length; i++)
            {
                if (Atlases[i] != null)
                {
                    UnityEngine.Object.Destroy(Atlases[i]);
                    Atlases[i] = null;
                }
            }
        }
    }

    /// <summary>
    /// One insignia on screen: an image per piece under a shared rect, each at its place in the
    /// logo. The terminal's emblem moves the pieces to assemble it; the watermark and the seal
    /// just show it whole.
    /// </summary>
    internal sealed class InsigniaView
    {
        internal readonly RectTransform Root;
        private readonly RawImage[] _images = new RawImage[Insignia.MaxPieces];
        private readonly Vector2[] _home = new Vector2[Insignia.MaxPieces];
        private readonly Vector2[] _direction = new Vector2[Insignia.MaxPieces];

        /// <summary>How many pieces the shown insignia has.</summary>
        internal int Count { get; private set; }

        private InsigniaView(RectTransform root)
        {
            Root = root;
        }

        internal static InsigniaView Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            var view = new InsigniaView(root);
            for (int i = 0; i < Insignia.MaxPieces; i++)
            {
                var piece = new GameObject("Piece" + i, typeof(RectTransform));
                piece.transform.SetParent(root, false);
                RawImage image = piece.AddComponent<RawImage>();
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                image.gameObject.SetActive(false);
                view._images[i] = image;
            }
            return view;
        }

        /// <summary>Shows <paramref name="faction"/>'s insignia assembled, radius <paramref name="r"/>, in <paramref name="color"/>.</summary>
        internal void Show(Faction faction, float r, Color32 color)
        {
            Insignia.Piece[] pieces = Insignia.PiecesOf(faction);
            Texture2D atlas = Insignia.Atlas(faction);
            Count = atlas != null ? pieces.Length : 0;
            for (int i = 0; i < _images.Length; i++)
            {
                RawImage image = _images[i];
                bool used = i < Count;
                image.gameObject.SetActive(used);
                if (!used)
                {
                    image.texture = null;
                    continue;
                }
                Insignia.Piece piece = pieces[i];
                image.texture = atlas;
                image.uvRect = piece.Uv;
                image.color = color;
                RectTransform rect = image.rectTransform;
                rect.sizeDelta = piece.Size * r;
                _home[i] = piece.Centre * r;
                _direction[i] = piece.Direction;
                rect.anchoredPosition = _home[i];
                image.canvasRenderer.SetAlpha(1f);
            }
            // The rect covers the logo, so a mask finds it where it is drawn.
            Root.sizeDelta = new Vector2(2f * r, 2f * r);
        }

        /// <summary>Piece <paramref name="i"/>, <paramref name="away"/> canvas units out along its way in, at <paramref name="alpha"/>.</summary>
        internal void Place(int i, float away, float alpha)
        {
            _images[i].rectTransform.anchoredPosition = _home[i] + _direction[i] * away;
            _images[i].canvasRenderer.SetAlpha(alpha);
        }

        /// <summary>Drops the pictures, so the faction's texture can be let go.</summary>
        internal void Clear()
        {
            Count = 0;
            foreach (RawImage image in _images)
            {
                image.texture = null;
                image.gameObject.SetActive(false);
            }
        }
    }
}
