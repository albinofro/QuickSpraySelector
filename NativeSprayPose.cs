using System;
using Reptile;
using UnityEngine;

namespace QuickPickGraffiti
{
    // Only the selector's display puppet: never replace the native finishing sequence.
    internal static class NativeSprayPose
    {
        private static GraffitiGame owner;
        private static Animator animator;
        private static AnimatorStateInfo previousState;
        private static float previousSpeed;
        private static bool disabled;

        internal const int OriginalPose = (int)Side.MAX;

        internal static bool IsOriginalPose(int choice)
        {
            return choice == OriginalPose;
        }

        internal static string AnimationName(int side)
        {
            if (side < 0 || side >= (int)Side.MAX) throw new ArgumentOutOfRangeException(nameof(side));
            return "grafSlash" + ((Side)side);
        }

        internal static void Begin(GraffitiGame game, CharacterVisual puppet)
        {
            if (disabled || game == null || puppet == null || puppet.anim == null || ReferenceEquals(owner, game)) return;
            Reset();
            try
            {
                Animator candidate = puppet.anim;
                int choice = UnityEngine.Random.Range(0, OriginalPose + 1);
                if (IsOriginalPose(choice))
                {
                    owner = game;
                    SelectorPaintCloud.Begin(game, puppet, choice);
                    return;
                }
                int poseHash = Animator.StringToHash(AnimationName(choice));
                if (!candidate.HasState(0, poseHash))
                {
                    owner = game;
                    SelectorPaintCloud.Begin(game, puppet, OriginalPose);
                    return;
                }
                previousState = candidate.GetCurrentAnimatorStateInfo(0);
                previousSpeed = candidate.speed;
                owner = game;
                animator = candidate;
                // Sample the middle of the directional pose, then hold it while choosing.
                // Native GRAFFITI boost particles follow the puppet's bones independently.
                candidate.Play(poseHash, 0, 0.45f);
                candidate.Update(0f);
                candidate.speed = 0f;
                SelectorPaintCloud.Begin(game, puppet, choice);
            }
            catch (Exception ex)
            {
                disabled = true;
                Reset();
                Debug.LogWarning("Selector pose disabled; native pose retained: " + ex.Message);
            }
        }

        internal static void ResetFor(GraffitiGame game)
        {
            if (ReferenceEquals(owner, game)) Reset();
            else SelectorPaintCloud.ResetFor(game);
        }

        internal static void Reset()
        {
            SelectorPaintCloud.Reset();
            Animator saved = animator;
            animator = null;
            owner = null;
            if (saved == null) return;
            // Restore before native SetStateVisual can play grafSlashFinisher.
            try
            {
                saved.speed = previousSpeed;
                if (previousState.fullPathHash != 0)
                    saved.Play(previousState.fullPathHash, 0, previousState.normalizedTime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Selector pose restore failed: " + ex.Message);
            }
        }
    }
}
