using System;
using UnityEngine;

namespace QuickPickGraffiti
{
    // Continuous UV deformation on fine geometry, rendered at the actual on-screen size.
    // A tiny repeating stripe texture replaces the low-resolution animation atlases.
    internal sealed class PaintVisuals : IDisposable
    {
        private const int Sectors = 512;
        private const int Rows = 24;
        private readonly Mesh liquid;
        private readonly Mesh backing;
        private readonly Material liquidMaterial;
        private readonly Material backingMaterial;
        private readonly Texture2D stripe;
        private readonly Vector3[] positions = new Vector3[(Sectors+1)*Rows];
        private readonly Vector2[] uvs = new Vector2[(Sectors+1)*Rows];
        private readonly Color[] liquidColors = new Color[(Sectors+1)*Rows];
        private readonly WirkleMotion.FlowPoint[] flow = new WirkleMotion.FlowPoint[(Sectors+1)*Rows];
        private readonly float[] sx = new float[Sectors+1];
        private readonly float[] sy = new float[Sectors+1];
        private readonly float[] outer = new float[Sectors+1];
        private readonly bool straightAlphaShader;
        private RenderTexture target;
        private float lastReveal = -1f;
        private int renderedFrame = -1;
        private float renderedAge = -1f;

        internal PaintVisuals()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) { shader = Shader.Find("UI/Default"); straightAlphaShader = true; }
            if (shader == null) throw new InvalidOperationException("No unlit texture shader is available for the wirkle.");
            try
            {
                stripe = CreateStripe();
                liquidMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = stripe };
                backingMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = Texture2D.whiteTexture };
                liquid = new Mesh { name = "WirkleLiquid", hideFlags = HideFlags.HideAndDontSave };
                liquid.MarkDynamic();
                backing = new Mesh { name = "WirkleBacking", hideFlags = HideFlags.HideAndDontSave };
                for (int sector = 0; sector <= Sectors; sector++)
                {
                    float angle = sector * Mathf.PI * 2f / Sectors;
                    sx[sector] = Mathf.Sin(angle); sy[sector] = -Mathf.Cos(angle);
                    outer[sector] = WirkleMotion.Outer(angle);
                    for (int row = 0; row < Rows; row++)
                    {
                        int index = sector * Rows + row;
                        float fraction = Mathf.Clamp01((row-1f)/(Rows-3f));
                        float radius = Mathf.Lerp(outer[sector]-.16f, outer[sector]-.013f, fraction);
                        flow[index] = new WirkleMotion.FlowPoint(angle, radius);
                        liquidColors[index] = Color.clear;
                    }
                }
                liquid.vertices = positions;
                liquid.colors = liquidColors;
                liquid.uv = uvs;
                liquid.triangles = GridTriangles(Rows);
                liquid.bounds = new Bounds(Vector3.zero, new Vector3(2.2f,2.2f,1f));
            }
            catch { Dispose(); throw; }
        }

        private static int[] GridTriangles(int rows)
        {
            var triangles = new int[Sectors*(rows-1)*6];
            int n = 0;
            for (int sector=0; sector<Sectors; sector++)
                for (int row=0; row<rows-1; row++)
                {
                    int a=sector*rows+row, b=a+rows;
                    triangles[n++]=a; triangles[n++]=b; triangles[n++]=a+1;
                    triangles[n++]=a+1; triangles[n++]=b; triangles[n++]=b+1;
                }
            return triangles;
        }

        private static Texture2D CreateStripe()
        {
            var texture = new Texture2D(2048, 1, TextureFormat.RGBA32, true);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            var pixels = new Color[2048];
            Color green = new Color(.20f,.57f,.17f,1f), cream = new Color(.95f,.99f,.90f,1f);
            Color ink=green, mint=cream;
            // Uneven layers: broad unmixed pools beside compressed hairline ribbons.
            float[] edges = {0f,.32f,.338f,.35f,.377f,.386f,.42f,.432f,.46f,.69f,.705f,.727f,.736f,.766f,.778f,.80f,1f};
            Color[] bands = {green,ink,cream,green,cream,ink,mint,ink,cream,ink,green,cream,green,ink,mint,green};
            for (int x=0; x<pixels.Length; x++)
            {
                float phase=(x+.5f)/pixels.Length;
                int band=0;
                while(band<bands.Length-1 && phase>=edges[band+1]) band++;
                pixels[x]=bands[band];
            }
            texture.SetPixels(pixels);
            texture.Apply(true,true);
            return texture;
        }

        private Color AlphaColor(float r, float g, float b, float alpha)
        {
            // UI/Default uses SrcAlpha for alpha too; compensate when rendering to clear RT.
            return new Color(r,g,b,straightAlphaShader ? Mathf.Sqrt(alpha) : alpha);
        }

        private void Resize(int size)
        {
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            target = new RenderTexture(size,size,0,RenderTextureFormat.ARGB32);
            target.name = "WirkleScreenResolution";
            target.hideFlags = HideFlags.HideAndDontSave;
            target.antiAliasing = 2;
            target.filterMode = FilterMode.Bilinear;
            target.Create();
            lastReveal = -1f;
            renderedFrame = -1;
            const int rings = 7;
            var vertices = new Vector3[(Sectors+1)*rings];
            var colors = new Color[vertices.Length];
            var coords = new Vector2[vertices.Length];
            float feather = 2f / size;
            for (int sector=0; sector<=Sectors; sector++)
            {
                float angle = sector*Mathf.PI*2f/Sectors;
                float inner = .44f+.045f*Mathf.Cos(8f*angle);
                for (int row=0; row<rings; row++)
                {
                    float radius; Color color;
                    switch (row)
                    {
                        case 0: radius=0f; color=AlphaColor(.025f,.03f,.023f,.532f); break;
                        case 1: radius=inner-feather; color=AlphaColor(.025f,.03f,.023f,.532f); break;
                        case 2: radius=inner+feather; color=AlphaColor(.075f,.085f,.062f,.686f); break;
                        case 3: radius=outer[sector]-.013f-feather; color=AlphaColor(.075f,.085f,.062f,.686f); break;
                        case 4: radius=outer[sector]-.013f; color=AlphaColor(.025f,.029f,.02f,1f); break;
                        case 5: radius=outer[sector]-feather; color=AlphaColor(.025f,.029f,.02f,1f); break;
                        default: radius=outer[sector]+feather; color=AlphaColor(.025f,.029f,.02f,0f); break;
                    }
                    int index=sector*rings+row;
                    vertices[index] = new Vector3(sx[sector]*radius,sy[sector]*radius,0f);
                    colors[index] = color;
                }
            }
            backing.Clear();
            backing.vertices=vertices; backing.colors=colors; backing.uv=coords;
            backing.triangles=GridTriangles(rings);
            backing.bounds = new Bounds(Vector3.zero,new Vector3(2.2f,2.2f,1f));
        }

        private void UpdateGeometry(float reveal)
        {
            if (reveal == lastReveal) return;
            lastReveal = reveal;
            float feather = 2f/target.width;
            for (int sector=0; sector<=Sectors; sector++)
                for (int row=0; row<Rows; row++)
                {
                    float fraction = Mathf.Clamp01((row-1f)/(Rows-3f));
                    float radius = Mathf.Lerp(outer[sector]-.16f,outer[sector]-.013f,fraction);
                    if (row==0) radius-=feather;
                    if (row==Rows-1) radius+=feather;
                    positions[sector*Rows+row] = new Vector3(sx[sector]*radius,sy[sector]*radius,0f);
                    float alpha = row==0 || row==Rows-1 ? 0f : WirkleMotion.RevealBand(reveal,fraction);
                    liquidColors[sector*Rows+row] = new Color(1f,1f,1f,alpha);
                }
            liquid.vertices=positions;
            liquid.colors=liquidColors;
        }

        internal void Draw(Rect disc, float age)
        {
            Prepare(disc.width,age);
            GUI.DrawTexture(disc,target,ScaleMode.StretchToFill,true);
        }

        // Also called offscreen during startup to create the RT and warm the shader passes.
        internal void Prepare(float diameter, float age)
        {
            int size = Mathf.Clamp(Mathf.CeilToInt(diameter),256,Mathf.Min(2048,SystemInfo.maxTextureSize));
            if (target==null || target.width!=size || !target.IsCreated()) Resize(size);
            if (renderedFrame!=Time.frameCount || renderedAge!=age)
            {
                renderedFrame=Time.frameCount; renderedAge=age;
                float reveal=WirkleMotion.Reveal(age);
                UpdateGeometry(reveal);
                var time = new WirkleMotion.FlowTime(age);
                for (int i=0;i<uvs.Length;i++) uvs[i]=new Vector2(flow[i].Sample(time),.5f);
                liquid.uv=uvs;
                RenderTexture previous=RenderTexture.active;
                GL.PushMatrix();
                try
                {
                    RenderTexture.active=target;
                    GL.Clear(true,true,Color.clear);
                    GL.LoadPixelMatrix(-1f,1f,1f,-1f);
                    backingMaterial.SetColor("_Color",new Color(1f,1f,1f,Mathf.Clamp01(age/.06f)));
                    if (backingMaterial.SetPass(0)) Graphics.DrawMeshNow(backing,Matrix4x4.identity);
                    liquidMaterial.SetColor("_Color",new Color(1f,1f,1f,Mathf.Clamp01(age/.08f)));
                    if (liquidMaterial.SetPass(0)) Graphics.DrawMeshNow(liquid,Matrix4x4.identity);
                }
                finally { RenderTexture.active=previous; GL.PopMatrix(); }
            }
        }

        public void Dispose()
        {
            if (target!=null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (stripe!=null) UnityEngine.Object.Destroy(stripe);
            if (liquid!=null) UnityEngine.Object.Destroy(liquid);
            if (backing!=null) UnityEngine.Object.Destroy(backing);
            if (liquidMaterial!=null) UnityEngine.Object.Destroy(liquidMaterial);
            if (backingMaterial!=null) UnityEngine.Object.Destroy(backingMaterial);
        }
    }
}
