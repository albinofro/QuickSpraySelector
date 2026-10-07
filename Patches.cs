using HarmonyLib;
using Reptile;
using System.Collections.Generic;
using System.Reflection.Emit;
using static Reptile.GraffitiGame;

namespace QuickPickGraffiti
{
    internal static class Patches
    {
        internal static List<GraffitiArt> AvailableArt(List<GraffitiArt> unlocked, GraffitiSize size)
        {
            return unlocked == null ? new List<GraffitiArt>() : unlocked.FindAll(art => art != null && art.graffitiSize == size);
        }
        // Preserve native paint, rewards, state timers, camera and animation code.
        internal static GraffitiArt ResolveArt(GraffitiArtInfo info, List<int> sequence)
        {
            return Plugin.TakeActiveSelection() ?? info.FindBySequence(sequence);
        }

        [HarmonyPatch(typeof(GraffitiGame), "SetState")]
        private static class StatePatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var find = AccessTools.Method(typeof(GraffitiArtInfo), "FindBySequence");
                var resolve = AccessTools.Method(typeof(Patches), nameof(ResolveArt));
                bool replaced = false;
                foreach (var instruction in instructions)
                {
                    if (instruction.Calls(find))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = resolve;
                        replaced = true;
                    }
                    yield return instruction;
                }
                if (!replaced) throw new System.InvalidOperationException("Native graffiti pattern lookup was not found.");
            }

            private static void Postfix(GraffitiGame __instance, GraffitiGameState setState)
            {
                if (setState == GraffitiGameState.SHOW_PIECE && Plugin.IsSelectionResolved(__instance))
                    Plugin.RememberPainted(Traverse.Create(__instance).Field("grafArt").GetValue<GraffitiArt>());
                if (setState == GraffitiGameState.FINISHED) Plugin.FinishSelection(__instance);
            }
        }

        [HarmonyPatch(typeof(GraffitiGame), "SetStateVisual")]
        private static class VisualStatePatch
        {
            private static void Prefix(GraffitiGame __instance, GraffitiGameState __0)
            {
                if (__0 != GraffitiGameState.MAIN_STATE)
                    NativeSprayPose.ResetFor(__instance);
            }
        }

        [HarmonyPatch(typeof(GraffitiGame), "Update")]
        private static class UpdatePatch
        {
            private static bool Prefix(GraffitiGame __instance)
            {
                var fields = Traverse.Create(__instance);
                if (fields.Field("baseModule").GetValue<BaseModule>().IsInGamePaused) return true;
                if (fields.Field("state").GetValue<GraffitiGameState>() != GraffitiGameState.MAIN_STATE) return true;
                GraffitiSpot spot = fields.Field("gSpot").GetValue<GraffitiSpot>();
                if (spot.size == GraffitiSize.S) return true;
                if (!Plugin.HasSelectionFor(__instance))
                {
                    List<GraffitiArt> unlockedGraffiti = fields.Field("unlockedGraffiti").GetValue<List<GraffitiArt>>();
                    List<GraffitiArt> available = AvailableArt(unlockedGraffiti, spot.size);
                    Plugin.BeginSelection(__instance, available, spot.size);
                }
                if (!Plugin.IsSelectionResolved(__instance)) return false;
                Plugin.RestorePickerMaps();
                if (Plugin.SelectionWasAborted(__instance))
                {
                    spot.SetState(GraffitiState.CANCEL);
                    __instance.End();
                    return false;
                }
                if (Plugin.TakeSelection(__instance) == null) return true;
                // Native SHOW_PIECE removes its terminal center target before pattern lookup.
                var sequence = fields.Field("targetsHitSequence").GetValue<List<int>>();
                if (sequence.Count == 0) sequence.Add(0);
                AccessTools.Method(typeof(GraffitiGame), "SetState").Invoke(__instance,
                    new object[] { GraffitiGameState.COMPLETE_TARGETS });
                return false;
            }

        }

        [HarmonyPatch(typeof(GraffitiGame), "End")]
        private static class EndPatch
        {
            // Restore before End reinstates normal gameplay maps.
            private static void Prefix(GraffitiGame __instance)
            {
                NativeSprayPose.ResetFor(__instance);
                Plugin.RestorePickerMaps();
            }
            private static void Postfix(GraffitiGame __instance) { Plugin.FinishSelection(__instance); }
        }
    }
}
