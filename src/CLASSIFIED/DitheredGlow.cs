using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// A radial glow drawn one texel per screen pixel with the dither built in.
    ///
    /// A wide, dark gradient on an 8-bit screen shows rings: each draw is rounded to whole steps
    /// of brightness as it is blended, and a glow across the whole screen changes by less than a
    /// step over dozens of pixels. Noise added afterwards (a grain layer) cannot help, because the
    /// glow has already been rounded by then. The noise has to be in the glow itself, so the
    /// rounding happens on a value that varies pixel to pixel: then neighbouring pixels round
    /// different ways and the eye sees the smooth average. That needs a texture as large as the
    /// glow is on screen, made once per layout on a worker thread (a few million pixels) and
    /// swapped in when ready; until then the stretched Art.Radial stands in.
    ///
    /// The noise is triangular, in steps of the texture's alpha, a fixed number of steps chosen
    /// so the final colour moves by one to two steps of brightness for every unit's
    /// colours. It does not depend on the colour (the texture is white; the glow is tinted), so
    /// one texture serves every unit at a given size, and switching unit in the
    /// preview does not have to wait for a new one. The last few sizes are kept.
    /// </summary>
    internal sealed class DitheredGlow
    {
        private readonly object _lock = new object();
        private readonly int _keep;
        private readonly List<KeyValuePair<string, Texture2D>> _made = new List<KeyValuePair<string, Texture2D>>();
        private Color32[] _ready;
        private int _readyWidth;
        private int _readyHeight;
        private int _wanted;
        private int _readyFor;
        private string _readyKey;
        private string _key;
        private Texture2D _texture;

        /// <param name="keep">How many finished sizes to keep for reuse.</param>
        internal DitheredGlow(int keep)
        {
            _keep = Mathf.Max(1, keep);
        }

        /// <summary>The texture for the current request, or null while it is being made.</summary>
        internal Texture2D Texture => _texture;

        /// <summary>
        /// Asks for a glow width x height pixels whose ellipse is centred at (cx, cy) from the
        /// bottom-left with radii (rx, ry), with noise of up to <paramref name="steps"/> alpha
        /// steps either way. True if it is ready now (made before); otherwise it is made on a
        /// worker thread and Collect swaps it in.
        /// </summary>
        internal bool Request(int width, int height, float cx, float cy, float rx, float ry, int steps)
        {
            string key = $"{width}x{height} {cx:0.0},{cy:0.0} {rx:0.0},{ry:0.0} n{steps}";
            for (int i = 0; i < _made.Count; i++)
            {
                if (_made[i].Key == key && _made[i].Value != null)
                {
                    _texture = _made[i].Value;
                    _key = key;
                    // A bake still running for another size must not replace this one when it lands.
                    Interlocked.Increment(ref _wanted);
                    return true;
                }
            }
            _texture = null;
            if (key == _key)
            {
                // Already being made.
                return false;
            }
            _key = key;
            int job = Interlocked.Increment(ref _wanted);
            var worker = new Thread(() =>
            {
                Color32[] pixels;
                try
                {
                    pixels = Bake(width, height, cx, cy, rx, ry, steps, 7);
                }
                catch (Exception e)
                {
                    ClassifiedPlugin.Log.LogWarning("Dithered glow not made: " + e.Message);
                    return;
                }
                lock (_lock)
                {
                    if (job != _wanted)
                    {
                        return;
                    }
                    _ready = pixels;
                    _readyWidth = width;
                    _readyHeight = height;
                    _readyFor = job;
                    _readyKey = key;
                }
            })
            {
                // Its own thread, not the pool: the game keeps the pool busy while it loads.
                IsBackground = true,
                Name = "ACCESS GRANTED glow"
            };
            worker.Start();
            return false;
        }

        /// <summary>On the main thread: turns a finished bake into the texture. True when a new one was made.</summary>
        internal bool Collect()
        {
            Color32[] pixels;
            int width, height;
            string key;
            lock (_lock)
            {
                if (_ready == null || _readyFor != _wanted)
                {
                    return false;
                }
                pixels = _ready;
                width = _readyWidth;
                height = _readyHeight;
                key = _readyKey;
                _ready = null;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _made.Add(new KeyValuePair<string, Texture2D>(key, texture));
            while (_made.Count > _keep)
            {
                UnityEngine.Object.Destroy(_made[0].Value);
                _made.RemoveAt(0);
            }
            _texture = texture;
            return true;
        }

        private static Color32[] Bake(int width, int height, float cx, float cy, float rx, float ry, int steps, int seed)
        {
            var pixels = new Color32[width * height];
            uint state = (uint)seed * 2654435761u + 1u;
            float irx = 1f / rx;
            float iry = 1f / ry;
            for (int y = 0; y < height; y++)
            {
                float dy = (y + 0.5f - cy) * iry;
                float dy2 = dy * dy;
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - cx) * irx;
                    float d2 = dx * dx + dy2;
                    if (d2 >= 1f)
                    {
                        pixels[row + x] = new Color32(255, 255, 255, 0);
                        continue;
                    }
                    float a = 1f - (float)Math.Sqrt(d2);
                    // xorshift: two draws make a triangular noise of -steps..+steps.
                    state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                    int n1 = (int)(state % (uint)(steps + 1));
                    state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                    int n2 = (int)(state % (uint)(steps + 1));
                    float value = a * a * 255f + (n1 - n2);
                    int alpha = (int)(value + 0.5f);
                    pixels[row + x] = new Color32(255, 255, 255, (byte)(alpha < 0 ? 0 : alpha > 255 ? 255 : alpha));
                }
            }
            return pixels;
        }
    }
}
