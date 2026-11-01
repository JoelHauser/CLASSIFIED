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

        /// <summary>One dark line in every three pixels.</summary>
        internal static Texture2D Scanlines => _scanlines ?? (_scanlines = Make(1, 3, TextureWrapMode.Repeat, FilterMode.Point, (x, y) =>
            y == 0 ? 1f : 0f));

        /// <summary>A vertical streak, clear at both ends.</summary>
        internal static Texture2D Band => _band ?? (_band = Make(1, 128, TextureWrapMode.Clamp, FilterMode.Bilinear, (x, y) =>
        {
            float s = Mathf.Sin(Mathf.PI * (y + 0.5f) / 128f);
            return s * s;
        }));

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
