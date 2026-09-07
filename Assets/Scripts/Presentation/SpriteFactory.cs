using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast.Presentation
{
    /// <summary>
    /// Generates every sprite the game needs at runtime, so the project ships without any
    /// imported art. Sprites are cached per key and reused for the lifetime of the process.
    /// </summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const int Supersample = 4;

        /// <summary>White rounded rectangle set up for 9-slicing, tint it with Image.color.</summary>
        public static Sprite RoundedRect(int size = 96, int radius = 24)
        {
            string key = "round_" + size + "_" + radius;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = RoundedCoverage(x, y, size, size, radius);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();

            int b = Mathf.Min(radius + 2, size / 2 - 1);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A single game block: rounded square with a baked bevel (bright top, darker
        /// bottom) plus a glossy highlight, so a flat tint still reads as a 3D candy block.
        /// </summary>
        public static Sprite Block(int size = 128, int radius = 26)
        {
            string key = "block_" + size + "_" + radius;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                // texture row 0 is the bottom of the sprite
                float t = 1f - (float)y / (size - 1); // 0 at top, 1 at bottom
                for (int x = 0; x < size; x++)
                {
                    float a = RoundedCoverage(x, y, size, size, radius);
                    float shade = Mathf.Lerp(1f, 0.66f, t * t);

                    // inner top gloss band
                    float u = (float)x / (size - 1);
                    float gloss = Mathf.Clamp01(1f - t * 6f) * Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
                    shade = Mathf.Min(1.35f, shade + gloss * 0.28f);

                    // dark inset along the very bottom edge
                    float bottomEdge = Mathf.Clamp01((t - 0.90f) * 10f);
                    shade *= Mathf.Lerp(1f, 0.78f, bottomEdge);

                    byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f);
                    pixels[y * size + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>Soft radial dot used for particles and sparkles.</summary>
        public static Sprite Circle(int size = 64)
        {
            string key = "circle_" + size;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.72f, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>Vertical two-stop gradient, 4px wide, stretched across the backdrop.</summary>
        public static Sprite VerticalGradient(Color top, Color bottom, int height = 256)
        {
            string key = "grad_" + ColorUtility.ToHtmlStringRGBA(top) + "_" + ColorUtility.ToHtmlStringRGBA(bottom) + "_" + height;
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            const int width = 4;
            var tex = NewTexture(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1); // 0 bottom, 1 top
                var c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t));
                for (int x = 0; x < width; x++) pixels[y * width + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(0, 2, 0, 2));
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            return tex;
        }

        /// <summary>Supersampled coverage of a rounded rectangle, for smooth corners.</summary>
        static float RoundedCoverage(int px, int py, int w, int h, float radius)
        {
            float hits = 0f;
            for (int sy = 0; sy < Supersample; sy++)
                for (int sx = 0; sx < Supersample; sx++)
                {
                    float x = px + (sx + 0.5f) / Supersample;
                    float y = py + (sy + 0.5f) / Supersample;
                    if (InsideRounded(x, y, w, h, radius)) hits += 1f;
                }
            return hits / (Supersample * Supersample);
        }

        static bool InsideRounded(float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            float cx = Mathf.Clamp(x, r, w - r);
            float cy = Mathf.Clamp(y, r, h - r);
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }
    }
}
