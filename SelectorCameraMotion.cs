using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using Reptile;

namespace QuickPickGraffiti
{
    internal sealed class SelectorCameraMotion : IDisposable
    {
        private static readonly FieldInfo cameraField = AccessTools.Field(typeof(GraffitiGame), "graffitiCameraTf");
        private static readonly FieldInfo puppetField = AccessTools.Field(typeof(GraffitiGame), "characterPuppet");
        private static readonly FieldInfo playerField = AccessTools.Field(typeof(GraffitiGame), "player");
        private readonly Action<string> log;
        private GraffitiGame owner;
        private Transform camera;
        private Vector3 originalPosition, pivot;
        private Quaternion originalRotation;
        private bool bound, failed;
        private float orbitDirection = 1f;
        private readonly CameraMotionState attachment = new CameraMotionState();
        private float noiseSeed;

        internal SelectorCameraMotion(Action<string> logger) { log = logger; }

        internal void BeginSession()
        {
            Restore();
            orbitDirection = UnityEngine.Random.value < .5f ? -1f : 1f;
            noiseSeed = UnityEngine.Random.Range(10f, 1000f);
        }

        private void AttachTo(float clockwiseDegrees)
        {
            if (failed) return;
            float angle = clockwiseDegrees * Mathf.Deg2Rad;
            attachment.SetTarget(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        internal void UpdateActive(bool active, GraffitiGame game, float age, float selectedAngle)
        {
            if (!active || game == null || failed) { Restore(); return; }
            try
            {
                if (owner != game || camera == null || !bound)
                {
                    Restore();
                    Transform view = cameraField == null ? null : cameraField.GetValue(game) as Transform;
                    Component subject = puppetField == null ? null : puppetField.GetValue(game) as Component;
                    if (subject == null && playerField != null) subject = playerField.GetValue(game) as Component;
                    if (view == null || subject == null) return;
                    owner = game;
                    camera = view;
                    originalPosition = view.position;
                    originalRotation = view.rotation;
                    pivot = subject.transform.position + Vector3.up;
                    bound = true;
                }
                AttachTo(selectedAngle);
                attachment.Step(Time.unscaledDeltaTime);
                // Both position and aim follow the same orbit, preserving the character as pivot.
                float ageSeconds = Mathf.Max(0f, age);
                float phase = orbitDirection * ageSeconds * (2f * Mathf.PI / 18f);
                float entrance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ageSeconds / .8f));
                // A slow loop in view space, rather than a complete orbit behind the wall.
                Quaternion yaw = Quaternion.AngleAxis(3.2f * Mathf.Sin(phase) * entrance + 9f * attachment.X, Vector3.up);
                Vector3 side = yaw * originalRotation * Vector3.right;
                Quaternion pitch = Quaternion.AngleAxis(1.7f * Mathf.Cos(phase) * entrance + 4f * attachment.Y, side);
                Quaternion orbit = pitch * yaw;
                float shakeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ageSeconds / .4f));
                Vector3 handheld = SampleHandheld(ageSeconds, noiseSeed) * shakeFade;
                Quaternion shake = Quaternion.Euler(handheld);
                Quaternion viewRotation = orbit * originalRotation;
                camera.position = pivot + orbit * (originalPosition - pivot);
                camera.rotation = viewRotation * shake;
            }
            catch (Exception ex)
            {
                Restore();
                failed = true;
                if (log != null) log("Selector camera motion disabled: " + ex.Message);
            }
        }

        private static Vector3 SampleHandheld(float time, float seed)
        {
            float pitch = .10f * Noise(time * .63f, seed) + .025f * Noise(time * 1.8f, seed + 17f) + .012f * Mathf.Sin(time * 1.1f);
            float yaw = .11f * Noise(time * .52f, seed + 37f) + .025f * Noise(time * 1.6f, seed + 53f);
            float roll = .06f * Noise(time * .74f, seed + 71f) + .015f * Noise(time * 1.9f, seed + 97f);
            return new Vector3(pitch, yaw, roll);
        }

        private static float Noise(float time, float seed)
        {
            return 2f * Mathf.Clamp01(Mathf.PerlinNoise(time, seed)) - 1f;
        }

        internal void Restore()
        {
            if (bound && camera != null)
            {
                camera.position = originalPosition;
                camera.rotation = originalRotation;
            }
            bound = false;
            owner = null;
            camera = null;
            attachment.Reset();
        }

        public void Dispose() { Restore(); }
    }
}
