using UnityEngine;

namespace BifrostRally.Core
{
    public class ColorFilterSettings
    {
        public float Saturation = 1.15f;
        public float Contrast = 0.9f;
        public float Warmth = 0.04f;
        public float ShadowLift = 0.06f;
    }

    /// <summary>
    /// The rally look as a colour lookup table (LUT): a 256x16 strip of 16 slices of a 16³ colour
    /// cube. Any renderer that supports strip LUTs can fade it in by blending its contribution.
    /// </summary>
    public static class ColorFilter
    {
        public const int Size = 16;

        public static Color Apply(Color c, ColorFilterSettings s)
        {
            float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            Vector3 v = new Vector3(c.r, c.g, c.b);
            v = Vector3.LerpUnclamped(new Vector3(luma, luma, luma), v, s.Saturation);
            v = (v - Vector3.one * 0.5f) * s.Contrast + Vector3.one * 0.5f;
            v += (Vector3.one - v) * s.ShadowLift;
            v.x += s.Warmth;
            v.z -= s.Warmth;
            return new Color(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z), 1f);
        }

        public static Texture2D BuildLut(ColorFilterSettings settings)
        {
            var lut = new Texture2D(Size * Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "BifrostRallyLut",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[Size * Size * Size];
            float max = Size - 1;
            for (int g = 0; g < Size; g++)
            {
                for (int b = 0; b < Size; b++)
                {
                    for (int r = 0; r < Size; r++)
                    {
                        var input = new Color(r / max, g / max, b / max, 1f);
                        pixels[g * Size * Size + b * Size + r] = Apply(input, settings);
                    }
                }
            }
            lut.SetPixels(pixels);
            lut.Apply(false);
            return lut;
        }
    }
}
