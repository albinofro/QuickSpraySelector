using UnityEngine;

namespace QuickPickGraffiti
{
    internal static class WheelStyle
    {
        internal static Texture2D CreatePointer()
        {
            // Exact, unmodified DanceSelect_Button_danceselecter sprite from the game.
            using(var stream=typeof(WheelStyle).Assembly.GetManifestResourceStream("QuickPickGraffiti.dance-pointer.png"))
            using(var buffer=new System.IO.MemoryStream())
            {
                if(stream==null) throw new System.InvalidOperationException("Dance selector pointer resource is missing.");
                stream.CopyTo(buffer);
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,true);
                texture.hideFlags=HideFlags.HideAndDontSave;
                texture.filterMode=FilterMode.Trilinear;
                texture.wrapMode=TextureWrapMode.Clamp;
                if(!ImageConversion.LoadImage(texture,buffer.ToArray(),true))
                {
                    Object.Destroy(texture);
                    throw new System.InvalidOperationException("Dance selector pointer could not be loaded.");
                }
                return texture;
            }
        }
        // Stable silhouette under the animated liquid band. Dark fills are 30% less opaque.
        internal static Texture2D CreateBacking(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[size * size];
            float middle = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - middle) / middle, dy = (y - middle) / middle;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dx, dy);
                    float outer = 0.945f + 0.035f * Mathf.Cos(8f * angle) + 0.013f * Mathf.Sin(13f * angle);
                    float inner = 0.44f + 0.045f * Mathf.Cos(8f * angle);
                    Color color = Color.clear;
                    if (r <= outer)
                    {
                        color = new Color(0.075f, 0.085f, 0.062f, 0.686f);
                        if (r < inner) color = new Color(0.025f, 0.03f, 0.023f, 0.532f);
                        if (r > outer - 0.013f) color = new Color(0.025f, 0.029f, 0.02f, 1f);
                    }
                    pixels[y * size + x] = color;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
        internal static Texture2D CreateSelectionGlow(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[size * size];
            float middle = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - middle) / middle, dy = (y - middle) / middle;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dx, dy);
                    if (Mathf.Abs(angle) < 0.24f && r > 0.50f && r < 0.82f)
                        pixels[y * size + x] = new Color(0.48f, 0.85f, 0.29f, 0.15f);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
