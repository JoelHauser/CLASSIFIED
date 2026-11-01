using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The few textures the overlay needs, drawn once in code so the mod ships as a single DLL.
    /// All are white with the shape in alpha, and tinted where they are used.
    /// </summary>
    internal static class Art
    {
        private static Texture2D _radial;
        private static Texture2D _grid;
        private static Texture2D _scanlines;
        private static Texture2D _band;

        /// <summary>
        /// Opaque in the middle, fading to nothing at the edge of the circle. Stored in half
        /// floats where the graphics card takes them: in 8 bits the faint outer part of a glow
        /// stretched across the screen has only a handful of alpha values, which show as rings.
        /// The bands the screen's own 8 bits leave in the big glows are dealt with by DitheredGlow.
        /// </summary>
        internal static Texture2D Radial
        {
            get
            {
                if (_radial != null)
                {
                    return _radial;
                }
                const int Size = 256;
                System.Func<int, int, float> alpha = (x, y) =>
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(128f, 128f)) / 128f;
                    float a = Mathf.Clamp01(1f - d);
                    return a * a;
                };
                if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))
                {
                    return _radial = Make(Size, Size, TextureWrapMode.Clamp, FilterMode.Bilinear, alpha);
                }
                var pixels = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha(x, y));
                    }
                }
                _radial = new Texture2D(Size, Size, TextureFormat.RGBAHalf, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontUnloadUnusedAsset
                };
                _radial.SetPixels(pixels);
                _radial.Apply(false, true);
                return _radial;
            }
        }

        /// <summary>
        /// One 64-pixel grid cell: a one-pixel line along the bottom and left, brighter where they
        /// meet. Point-filtered and drawn one texel per screen pixel, so the lines stay one pixel.
        /// </summary>
        internal static Texture2D Grid => _grid ?? (_grid = Make(64, 64, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
            x < 2 && y < 2 ? 1f : (x == 0 || y == 0 ? 0.45f : 0f)));

        /// <summary>One dark line in every three pixels.</summary>
        internal static Texture2D Scanlines => _scanlines ?? (_scanlines = Make(1, 3, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
            y == 0 ? 1f : 0f));

        /// <summary>A vertical streak, clear at both ends.</summary>
        internal static Texture2D Band => _band ?? (_band = Make(1, 128, TextureWrapMode.Clamp, FilterMode.Bilinear, (x, y) =>
        {
            float s = Mathf.Sin(Mathf.PI * (y + 0.5f) / 128f);
            return s * s;
        }));

        private static Texture2D _dots;
        private static Texture2D _hex;
        private static Texture2D _pinstripe;
        private static Texture2D _ruled;

        /// <summary>A two-pixel dot every 32 pixels: graph paper for the corporate screens.</summary>
        internal static Texture2D Dots => _dots ?? (_dots = Make(32, 32, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
            x < 2 && y < 2 ? 1f : 0f));

        /// <summary>
        /// Flat-topped hexagon outlines, one pixel wide and soft-edged. The tile is 45 x 26: three
        /// sides across, the height rounded to whole pixels, which squashes the cells by a hair.
        /// </summary>
        internal static Texture2D Hex => _hex ?? (_hex = Make(45, 26, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
        {
            float px = x + 0.5f;
            float py = y + 0.5f;
            float nearest = float.MaxValue;
            float[] cx = { 0f, 45f, 0f, 45f, 22.5f };
            float[] cy = { 0f, 0f, 26f, 26f, 13f };
            for (int i = 0; i < cx.Length; i++)
            {
                float dx = Mathf.Abs(px - cx[i]);
                float dy = Mathf.Abs(py - cy[i]);
                nearest = Mathf.Min(nearest, Mathf.Max(dy, dx * 0.866f + dy * 0.5f));
            }
            return Mathf.Clamp01(1.2f - Mathf.Abs(13f - nearest));
        }));

        /// <summary>A thin diagonal line every 16 pixels.</summary>
        internal static Texture2D Pinstripe => _pinstripe ?? (_pinstripe = Make(16, 16, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
        {
            int d = ((x - y) % 16 + 16) % 16;
            return d == 0 ? 1f : d == 1 || d == 15 ? 0.3f : 0f;
        }));

        private static Texture2D _blueprint;

        /// <summary>Drafting paper: a faint line every 32 pixels and a stronger one every 160.</summary>
        internal static Texture2D Blueprint => _blueprint ?? (_blueprint = Make(160, 160, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
            x == 0 || y == 0 ? 1f : x % 32 == 0 || y % 32 == 0 ? 0.35f : 0f));

        /// <summary>A ruled line every 36 pixels, like a ledger page.</summary>
        internal static Texture2D Ruled => _ruled ?? (_ruled = Make(1, 36, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
            y == 0 ? 1f : 0f));

        private static Texture2D Make(int width, int height, TextureWrapMode wrap, FilterMode filter, System.Func<int, int, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = wrap,
                filterMode = filter,
                // EFT unloads unused assets between scenes; these are only held by a static field.
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
