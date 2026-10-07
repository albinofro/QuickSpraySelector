using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace QuickPickGraffiti
{
    // Run the game's own depth-of-field renderer; selector overlays render afterward.
    internal sealed class BackgroundBlur : IDisposable
    {
        private static readonly FieldInfo resourceField = AccessTools.Field(typeof(PostProcessLayer), "m_Resources");
        private readonly Action<string> log;
        private readonly List<PostProcessLayer> ownedLayers = new List<PostProcessLayer>();
        private Camera[] cameras = new Camera[8];
        private PostProcessResources resources;
        private GameObject root;
        private PostProcessProfile profile;
        private PostProcessVolume volume;
        private DepthOfField effect;
        private Camera camera;
        private PostProcessLayer layer;
        private bool originalEnabled, bound, disposed, failed, warned, logged;
        private LayerMask originalMask;
        private DepthTextureMode originalDepth;
        private float nextResourceAttempt;
        private KernelSize requestedQuality = KernelSize.Medium;

        internal BackgroundBlur(Action<string> logger) { log = logger; }

        internal void SetQuality(KernelSize quality)
        {
            if (requestedQuality == quality) return;
            requestedQuality = quality;
            if (effect != null) effect.kernelSize.Override(quality);
        }

        private static bool Usable(PostProcessResources candidate)
        {
            return candidate != null && candidate.shaders != null && candidate.shaders.depthOfField != null
                && candidate.shaders.depthOfField.isSupported;
        }

        private void FindResources()
        {
            if (Usable(resources) || Time.unscaledTime < nextResourceAttempt) return;
            nextResourceAttempt = Time.unscaledTime + 2f;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<PostProcessResources>())
                if (Usable(candidate)) { resources = candidate; return; }
            // Serialized camera components can hold resources that Shader.Find does not expose.
            foreach (var existing in Resources.FindObjectsOfTypeAll<PostProcessLayer>())
            {
                var candidate = resourceField == null ? null : resourceField.GetValue(existing) as PostProcessResources;
                if (Usable(candidate)) { resources = candidate; return; }
            }
        }

        internal void Prepare()
        {
            if (disposed || failed) return;
            try
            {
                FindResources();
                if (root != null && volume != null && profile != null && effect != null) return;
                // Scene changes can destroy one member while leaving its profile alive.
                // Reuse complete state; restore bindings and clean up only partial state.
                Detach();
                ReleaseVolume();
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                effect = profile.AddSettings<DepthOfField>();
                effect.hideFlags = HideFlags.HideAndDontSave;
                effect.enabled.Override(true);
                // Keep the focus plane in front of the scene so its world geometry is defocused.
                effect.focusDistance.Override(.15f);
                effect.focalLength.Override(100f);
                effect.aperture.Override(1f);
                effect.kernelSize.Override(requestedQuality);
                root = new GameObject("SpraySelectorNativeBlur") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(root);
                root.SetActive(false);
                volume = root.AddComponent<PostProcessVolume>();
                volume.sharedProfile = profile;
                volume.isGlobal = true;
                volume.priority = 100000f;
                volume.weight = 0f;
            }
            catch (Exception ex) { Fail("Native blur preparation failed: " + ex.Message); }
        }

        internal void UpdateActive(bool active, float strength = 1f)
        {
            strength = Mathf.Clamp01(strength);
            if (!active || strength <= 0f || disposed || failed) { Detach(); return; }
            Prepare();
            if (failed || volume == null) return;
            volume.weight = strength;
            try
            {
                int count = Camera.allCamerasCount;
                if (cameras.Length < count) cameras = new Camera[Mathf.NextPowerOfTwo(count)];
                count = Camera.GetAllCameras(cameras);
                Camera best = null;
                for (int i = 0; i < count; i++)
                {
                    Camera candidate = cameras[i];
                    if (candidate == null || !candidate.isActiveAndEnabled || candidate.cameraType != CameraType.Game
                        || candidate.targetTexture != null || candidate.targetDisplay != 0) continue;
                    Rect rect = candidate.rect;
                    if (Mathf.Abs(rect.x) > .001f || Mathf.Abs(rect.y) > .001f
                        || Mathf.Abs(rect.width-1f) > .001f || Mathf.Abs(rect.height-1f) > .001f) continue;
                    if (best == null || candidate.depth >= best.depth) best = candidate;
                }
                if (bound && best == camera && layer != null) return;
                Detach();
                ownedLayers.RemoveAll(owned => owned == null);
                if (best == null) { Warn("Native blur: no active fullscreen camera found."); return; }
                PostProcessLayer existing = best.GetComponent<PostProcessLayer>();
                var native = existing == null || resourceField == null ? null : resourceField.GetValue(existing) as PostProcessResources;
                if (!Usable(native)) native = resources;
                if (!Usable(native)) { Warn("Native blur: waiting for the game's post-processing resources."); return; }
                if (existing != null && !Usable(resourceField.GetValue(existing) as PostProcessResources))
                {
                    // Preserve an existing layer rather than overwrite its resource configuration.
                    Warn("Native blur: existing camera effects do not have usable resources."); return;
                }
                camera = best;
                originalDepth = camera.depthTextureMode;
                if (existing == null)
                {
                    // Native OnEnable only initializes its buffers; Init supplies resources before rendering.
                    existing = camera.gameObject.AddComponent<PostProcessLayer>();
                    ownedLayers.Add(existing);
                    existing.enabled = false;
                    existing.Init(native);
                }
                layer = existing;
                originalEnabled = ownedLayers.Contains(layer) ? false : layer.enabled;
                originalMask = layer.volumeLayer;
                bound = true;
                int mask = originalMask.value;
                int volumeLayer = 0;
                if (mask == 0) layer.volumeLayer = 1;
                else while (volumeLayer < 31 && (mask & (1 << volumeLayer)) == 0) volumeLayer++;
                root.layer = volumeLayer;
                root.SetActive(true);
                layer.enabled = true;
                if (!logged && log != null)
                {
                    logged = true;
                    log("Native depth-of-field blur enabled on camera " + camera.name + ".");
                }
            }
            catch (Exception ex) { Fail("Native blur activation failed: " + ex.Message); }
        }

        internal void Draw(float age) { /* The native camera pipeline renders the effect. */ }

        private void Detach()
        {
            if (root != null) root.SetActive(false);
            if (bound)
            {
                if (layer != null) { layer.volumeLayer = originalMask; layer.enabled = originalEnabled; }
            }
            if (camera != null) camera.depthTextureMode = originalDepth;
            bound = false; layer = null; camera = null;
        }

        private void ReleaseVolume()
        {
            if (volume != null) volume.sharedProfile = null;
            if (profile != null) profile.settings.Clear();
            if (effect != null) UnityEngine.Object.Destroy(effect);
            if (profile != null) UnityEngine.Object.Destroy(profile);
            if (root != null) UnityEngine.Object.Destroy(root);
            effect = null; profile = null; volume = null; root = null;
        }

        private void Warn(string message)
        {
            if (warned) return;
            warned = true;
            if (log != null) log(message);
        }

        private void Fail(string message) { failed = true; Detach(); Warn(message); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Detach();
            foreach (var owned in ownedLayers)
            {
                if (owned != null)
                {
                    owned.enabled = false;
                    UnityEngine.Object.Destroy(owned);
                }
            }
            ownedLayers.Clear();
            ReleaseVolume();
            resources = null;
        }
    }
}
