using System;
using System.Reflection;
using HarmonyLib;
using Reptile;
using UnityEngine;

namespace QuickPickGraffiti
{
    // Project the native dots after the wheel, outside the scene's depth-of-field pass.
    // Only this owned clone's renderer is hidden; particle simulation stays native.
    internal sealed class SelectorForegroundDots : IDisposable
    {
        private static readonly FieldInfo cameraField = AccessTools.Field(typeof(GraffitiGame), "graffitiCameraTf");
        private readonly ParticleSystem source;
        private readonly ParticleSystemRenderer renderer;
        private readonly Camera view;
        private readonly Texture texture;
        private readonly Color tint = Color.white;
        private readonly ParticleSystem.Particle[] particles;
        private readonly bool rendererWasEnabled;
        private bool hidden, disabled;

        internal SelectorForegroundDots(GraffitiGame game, ParticleSystem dots)
        {
            try
            {
                source = dots;
                if (source == null || cameraField == null) return;
                var cameraTransform = cameraField.GetValue(game) as Transform;
                if (cameraTransform == null) return;
                view = cameraTransform.GetComponent<Camera>();
                renderer = source.GetComponent<ParticleSystemRenderer>();
                if (view == null || renderer == null) return;
                var material = renderer.sharedMaterial;
                if (material == null || material.mainTexture == null) return;
                texture = material.mainTexture;
                if (material.HasProperty("_Color")) tint = material.GetColor("_Color");
                var main = source.main;
                // The native prefab has 1000 capacity and normally only a few dozen live dots.
                // Fall back to native rendering if an incompatible prefab is substituted.
                if (main.maxParticles <= 0 || main.maxParticles > 2048) return;
                particles = new ParticleSystem.Particle[main.maxParticles];
                rendererWasEnabled = renderer.enabled;
            }
            catch (Exception ex)
            {
                disabled = true;
                Debug.LogWarning("Selector foreground dots unavailable: " + ex.Message);
            }
        }

        internal void Draw()
        {
            if (disabled || source == null || renderer == null || view == null || texture == null || particles == null || !rendererWasEnabled) return;
            if (!view.isActiveAndEnabled) { RestoreRenderer(); return; }
            Color savedColor = GUI.color;
            try
            {
                int count = source.GetParticles(particles);
                var main = source.main;
                Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Custom
                    ? main.customSimulationSpace : source.transform;
                bool worldSpace = main.simulationSpace == ParticleSystemSimulationSpace.World;
                if (!worldSpace && space == null) { RestoreRenderer(); return; }
                Vector3 scale = main.scalingMode == ParticleSystemScalingMode.Hierarchy
                    ? source.transform.lossyScale : source.transform.localScale;
                float sizeScale = main.scalingMode == ParticleSystemScalingMode.Shape
                    ? 1f : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                Vector3 cameraUp = view.transform.up;
                float near = view.nearClipPlane;
                if (!hidden)
                {
                    renderer.enabled = false;
                    hidden = true;
                }
                for (int i = 0; i < count; i++)
                {
                    if (particles[i].remainingLifetime <= 0f) continue;
                    Vector3 world = worldSpace ? particles[i].position : space.TransformPoint(particles[i].position);
                    Vector3 screen = view.WorldToScreenPoint(world);
                    if (screen.z <= near) continue;
                    float diameter = particles[i].GetCurrentSize(source) * sizeScale;
                    if (diameter <= 0f) continue;
                    Vector3 edge = view.WorldToScreenPoint(world + cameraUp * (diameter * 0.5f));
                    float radius = Mathf.Abs(edge.y - screen.y);
                    if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) continue;
                    if (screen.x + radius < 0f || screen.x - radius > Screen.width
                        || screen.y + radius < 0f || screen.y - radius > Screen.height) continue;
                    GUI.color = tint * (Color)particles[i].GetCurrentColor(source);
                    GUI.DrawTexture(new Rect(screen.x - radius, Screen.height - screen.y - radius,
                        radius * 2f, radius * 2f), texture, ScaleMode.StretchToFill, true);
                }
            }
            catch (Exception ex)
            {
                disabled = true;
                RestoreRenderer();
                Debug.LogWarning("Selector foreground dots reverted to native rendering: " + ex.Message);
            }
            finally { GUI.color = savedColor; }
        }

        private void RestoreRenderer()
        {
            if (hidden && renderer != null) renderer.enabled = rendererWasEnabled;
            hidden = false;
        }

        public void Dispose() { RestoreRenderer(); disabled = true; }
    }
}
