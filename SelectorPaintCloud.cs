using System;
using System.Reflection;
using HarmonyLib;
using Reptile;
using UnityEngine;

namespace QuickPickGraffiti
{
    // Native opening timing and finite paint body, with a separate gentle dot clock.
    internal static class SelectorPaintCloud
    {
        private static readonly FieldInfo playerField = AccessTools.Field(typeof(GraffitiGame), "player");
        private static readonly FieldInfo spotField = AccessTools.Field(typeof(GraffitiGame), "gSpot");
        private static readonly FieldInfo fastCamDurationField = AccessTools.Field(typeof(GraffitiGame), "fastCamDuration");
        private static readonly FieldInfo hitlagCamDurationField = AccessTools.Field(typeof(GraffitiGame), "hitlagCamDuration");
        private static readonly FieldInfo baseModuleField = AccessTools.Field(typeof(GraffitiGame), "baseModule");

        private static GraffitiGame owner;
        private static GameObject cloud;
        private static GraffitiEffect effect;
        private static BaseModule baseModule;
        private static float speed;
        private static int phase;
        private const int Fast = 1;
        private const int Hitlag = 2;
        private const int Slow = 3;
        private static float timer;
        private static float fastCamDuration;
        private static float hitlagCamDuration;
        private static bool disabled;
        private static ParticleSystem bodyCloud;
        private static ParticleSystem bodyStrokes;
        private static bool bodyReleased;
        private static SelectorForegroundDots foregroundDots;
        private static ParticleSystem dotSystem;
        private const float DotWarmupSeconds = 2f;
        private const float DotPlaybackSpeed = 0.08f;
        private const float DotSettleSeconds = 1.5f;
        private static float dotElapsed;

        internal static Vector2 LocalDirection(int side)
        {
            switch (side)
            {
                case 0: return new Vector2(0, 1);
                case 1: return new Vector2(2, 1);
                case 2: return new Vector2(1, 0);
                case 3: return new Vector2(2, -1);
                case 4: return new Vector2(0, -1);
                case 5: return new Vector2(-2, -1);
                case 6: return new Vector2(-1, 0);
                case 7: return new Vector2(-2, 1);
                default: return new Vector2(0, 1); // Original entry pose.
            }
        }

        internal static void Begin(GraffitiGame game, CharacterVisual puppet, int side)
        {
            if (disabled || game == null || puppet == null || ReferenceEquals(owner, game)) return;
            Reset();
            try
            {
                if (playerField == null || spotField == null || fastCamDurationField == null || hitlagCamDurationField == null || baseModuleField == null)
                    throw new InvalidOperationException("Native graffiti effect fields unavailable.");
                Reptile.Player player = (Reptile.Player)playerField.GetValue(game);
                GraffitiSpot spot = (GraffitiSpot)spotField.GetValue(game);
                fastCamDuration = (float)fastCamDurationField.GetValue(game);
                hitlagCamDuration = (float)hitlagCamDurationField.GetValue(game);
                baseModule = (BaseModule)baseModuleField.GetValue(game);
                if (player == null || player.VFXPrefabs == null || player.VFXPrefabs.graffitiSlash == null) return;
                if (baseModule == null) throw new InvalidOperationException("Native graffiti pause state unavailable.");
                Transform body = puppet.transform;
                Transform wall = spot == null ? body : spot.transform;
                Vector2 local = LocalDirection(side);
                Vector3 cardinal = (wall.up * local.y - wall.right * local.x).normalized;
                // Instantiate the native effect without changing its initial burst or animation.
                cloud = UnityEngine.Object.Instantiate(player.VFXPrefabs.graffitiSlash,
                    body.position + body.forward + body.up,
                    Quaternion.LookRotation(-body.forward, cardinal));
                owner = game;
                cloud.name = "QuickPickSelectorPaintCloud";
                cloud.transform.SetParent(body, true);
                effect = cloud.GetComponent<GraffitiEffect>();
                if (effect == null) throw new InvalidOperationException("Native graffiti effect controller unavailable.");
                bodyCloud = effect.splat != null && effect.splat != effect.splashes ? effect.splat : null;
                bodyStrokes = effect.strokes != null && effect.strokes != effect.splashes ? effect.strokes : null;
                speed = 100f;
                phase = Fast;
                timer = 0f;
                effect.SetSpeed(speed);
                InitializeDots();
                foregroundDots = new SelectorForegroundDots(game, effect.splashes);
            }
            catch (Exception ex)
            {
                disabled = true;
                Reset();
                Debug.LogWarning("Selector paint cloud disabled: " + ex.Message);
            }
        }

        internal static void ResetFor(GraffitiGame game)
        {
            if (ReferenceEquals(owner, game)) Reset();
        }

        internal static void Reset()
        {
            if (foregroundDots != null) foregroundDots.Dispose();
            foregroundDots = null;
            if (cloud != null) UnityEngine.Object.Destroy(cloud);
            cloud = null;
            effect = null;
            baseModule = null;
            owner = null;
            speed = 0f;
            phase = 0;
            timer = 0f;
            fastCamDuration = 0f;
            hitlagCamDuration = 0f;
            bodyCloud = null;
            bodyStrokes = null;
            bodyReleased = false;
            dotSystem = null;
            dotElapsed = 0f;
        }

        internal static void DrawForegroundDots()
        {
            if (foregroundDots != null) foregroundDots.Draw();
        }

        internal static void Tick(float deltaTime)
        {
            if (cloud == null || effect == null || baseModule == null || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            try
            {
                if (baseModule.IsInGamePaused)
                {
                    effect.SetSpeed(0f);
                    effect.SetStun(0f);
                    UpdateDotSpeed(true);
                    return;
                }

                dotElapsed += deltaTime;
                dotElapsed = Mathf.Min(dotElapsed, DotSettleSeconds);

                speed = NextSpeed(speed, deltaTime);
                effect.SetSpeed(speed);
                // Match native order: apply the current phase, then advance its timer.
                effect.SetStun(phase == Hitlag ? 0f : 1f);
                UpdateDotSpeed(false);
                phase = NextPhase(phase, ref timer, deltaTime, fastCamDuration, hitlagCamDuration);
                UpdateBodySystems();
            }
            catch (Exception ex)
            {
                disabled = true;
                Reset();
                Debug.LogWarning("Selector paint cloud disabled: " + ex.Message);
            }
        }

        private static void UpdateBodySystems()
        {
            // Stop only the body emitters. Their live particles and the parent dots survive.
            if (phase == Slow && !bodyReleased)
            {
                if (bodyCloud != null) bodyCloud.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                if (bodyStrokes != null) bodyStrokes.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                bodyReleased = true;
            }

            if (phase == Slow)
            {
                // Keep the body moving
                // and shrinking at half normal speed so its authored lifetime can finish.
                if (bodyCloud != null)
                {
                    var main = bodyCloud.main;
                    main.simulationSpeed = Mathf.Max(speed, 0.5f);
                }
                if (bodyStrokes != null)
                {
                    var main = bodyStrokes.main;
                    main.simulationSpeed = Mathf.Max(speed, 0.5f);
                }
            }
        }

        private static void InitializeDots()
        {
            dotSystem = effect.splashes;
            if (dotSystem == null) return;
            // This prefab emits gradually rather than bursting. Prepare a small field
            // once, on the owned clone only, without advancing the cloud or streaks.
            var main = dotSystem.main;
            main.simulationSpeed = 1f;
            var emission = dotSystem.emission;
            emission.rateOverTimeMultiplier *= 0.75f;
            dotSystem.Simulate(DotWarmupSeconds, false, true, false);
            // Keep this smaller field: no extra dots once the moment settles.
            emission.enabled = false;
            UpdateDotSpeed(baseModule.IsInGamePaused);
            dotSystem.Play(false);
        }

        private static void UpdateDotSpeed(bool paused)
        {
            if (dotSystem == null) return;
            var main = dotSystem.main;
            // Native SetSpeed writes to all three particle systems. Reapply this
            // override afterward so first-use stalls and warm openings look alike.
            main.simulationSpeed = GetDotSpeed(dotElapsed, paused);
        }

        internal static float GetDotSpeed(float elapsed, bool paused)
        {
            return paused ? 0f : Mathf.Lerp(DotPlaybackSpeed, 0f, elapsed / DotSettleSeconds);
        }

        internal static float NextSpeed(float speed, float dt)
        {
            return Mathf.Lerp(speed, 0f, dt * (speed >= 0.5f ? 10f : 1f));
        }

        internal static int NextPhase(int phase, ref float timer, float dt, float fastDuration, float hitlagDuration)
        {
            timer += dt;
            if (phase == Fast && timer > fastDuration)
            {
                timer = 0f;
                return Hitlag;
            }
            if (phase == Hitlag && timer > hitlagDuration)
            {
                timer = 0f;
                return Slow;
            }
            return phase;
        }
    }
}
