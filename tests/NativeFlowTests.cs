using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Reptile;

internal static class NativeFlowTests
{
    static string managed;
    static string core;
    static string plugin;
    static void Main(string[] args)
    {
        managed = args[0]; core = args[1]; plugin = args[2];
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => {
            string name = new AssemblyName(eventArgs.Name).Name + ".dll";
            foreach (string folder in new[] { managed, core, Path.GetDirectoryName(plugin) })
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        MethodInfo native = AccessTools.Method(typeof(GraffitiGame), "SetState");
        var before = PatchProcessor.GetOriginalInstructions(native).ToList();
        Assembly mod = Assembly.LoadFrom(plugin);
        Type patches = mod.GetType("QuickPickGraffiti.Patches", true);
        Type pose = mod.GetType("QuickPickGraffiti.NativeSprayPose", true);
        MethodInfo animationName = AccessTools.Method(pose, "AnimationName");
        var uniquePoses = new HashSet<string>();
        for (int direction = 0; direction < (int)Side.MAX; direction++)
        {
            string name = (string)animationName.Invoke(null, new object[] { direction });
            if (name != "grafSlash" + ((Side)direction))
                throw new Exception("Selector pose must use the native directional name");
            if (!uniquePoses.Add(name)) throw new Exception("Every native direction must remain available");
        }
        MethodInfo isOriginalPose = AccessTools.Method(pose, "IsOriginalPose");
        int originalChoice = (int)AccessTools.Field(pose, "OriginalPose").GetRawConstantValue();
        int originalOptions = 0;
        for (int choice = 0; choice <= originalChoice; choice++)
        {
            if ((bool)isOriginalPose.Invoke(null, new object[] { choice })) originalOptions++;
            else animationName.Invoke(null, new object[] { choice });
        }
        if (originalChoice != (int)Side.MAX || originalOptions != 1)
            throw new Exception("Original pose must be exactly one additional option, separate from native direction names");
        foreach (int sentinel in new[] { (int)Side.NONE, (int)Side.MAX })
        {
            try { animationName.Invoke(null, new object[] { sentinel }); throw new Exception("Invalid pose accepted"); }
            catch (TargetInvocationException error)
            {
                if (!(error.InnerException is ArgumentOutOfRangeException)) throw;
            }
        }
        Type visualPatch = mod.GetType("QuickPickGraffiti.Patches+VisualStatePatch", true);
        var visualHook = PatchProcessor.GetOriginalInstructions(AccessTools.Method(visualPatch, "Prefix")).ToList();
        if (visualHook.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Begin")
            || !visualHook.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "ResetFor"))
            throw new Exception("Completion must restore the pose before native finisher, never replace it");
        if (AccessTools.Method(visualPatch, "Postfix") != null)
            throw new Exception("Native finishing animation must not have a pose override");
        Type updatePatch = mod.GetType("QuickPickGraffiti.Patches+UpdatePatch", true);
        if (AccessTools.Method(updatePatch, "Postfix") != null)
            throw new Exception("Completion animation must not be driven by selector pose ticking");
        Type selectorPluginType = mod.GetType("QuickPickGraffiti.Plugin", true);
        var opening = PatchProcessor.GetOriginalInstructions(AccessTools.Method(selectorPluginType, "BeginSelection")).ToList();
        if (!opening.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == pose && ((MethodInfo)i.operand).Name == "Begin"))
            throw new Exception("Random pose must start with the selector");
        var poseBegin = PatchProcessor.GetOriginalInstructions(AccessTools.Method(pose, "Begin")).ToList();
        if (poseBegin.Any(i => i.operand is MethodInfo && (((MethodInfo)i.operand).Name == "DoGraffitiEffect" || ((MethodInfo)i.operand).Name == "SetBoostpackEffect")))
            throw new Exception("Selector pose must leave native boost particles alone and not spawn paint slashes");
        Type cloud = mod.GetType("QuickPickGraffiti.SelectorPaintCloud", true);
        if (!poseBegin.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == cloud && ((MethodInfo)i.operand).Name == "Begin"))
            throw new Exception("Opening pose must create its owned native paint cloud");
        var poseReset = PatchProcessor.GetOriginalInstructions(AccessTools.Method(pose, "Reset")).ToList();
        if (!poseReset.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == cloud && ((MethodInfo)i.operand).Name == "Reset"))
            throw new Exception("Exiting the selector must remove its owned paint cloud");
        var cloudBegin = PatchProcessor.GetOriginalInstructions(AccessTools.Method(cloud, "Begin")).ToList();
        if (cloudBegin.Any(i => i.operand is MethodInfo && (((MethodInfo)i.operand).Name == "RemoveGraffitiSlash" || ((MethodInfo)i.operand).Name == "CreateGraffitiSlashEffect")))
            throw new Exception("Opening cloud must not replace or destroy native player effect ownership");
        foreach (string forbidden in new[] { "Play", "Stop", "Pause", "Simulate", "set_loop", "set_wrapMode" })
            if (cloudBegin.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == forbidden))
                throw new Exception("Opening must preserve authored native effect settings: " + forbidden);
        if (!cloudBegin.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == typeof(GraffitiEffect) && ((MethodInfo)i.operand).Name == "SetSpeed"))
            throw new Exception("Opening cloud must use native effect speed control");
        MethodInfo cloudTickMethod = AccessTools.Method(cloud, "Tick");
        if (cloudTickMethod == null || cloudTickMethod.GetMethodBody().ExceptionHandlingClauses.Count == 0)
            throw new Exception("Cloud playback must guard runtime failures");
        var cloudTick = PatchProcessor.GetOriginalInstructions(cloudTickMethod).ToList();
        foreach (string forbidden in new[] { "Destroy", "Stop", "Simulate", "GetComponentsInChildren", "GetValue" })
            if (cloudTick.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == forbidden))
                throw new Exception("Native playback must have no forced lifetime or per-frame discovery: " + forbidden);
        foreach (string required in new[] { "NextSpeed", "SetSpeed", "SetStun", "NextPhase", "get_IsInGamePaused" })
            if (!cloudTick.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == required))
                throw new Exception("Native playback missing " + required);
        int stunIndex = cloudTick.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "SetStun");
        int phaseIndex = cloudTick.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "NextPhase");
        if (stunIndex > phaseIndex) throw new Exception("Hit-pause must use the current phase before advancing, as native does");
        MethodInfo bodyUpdateMethod = AccessTools.Method(cloud, "UpdateBodySystems");
        if (bodyUpdateMethod == null) throw new Exception("Separate paint body tail missing");
        var bodyUpdate = PatchProcessor.GetOriginalInstructions(bodyUpdateMethod).ToList();
        if (bodyUpdate.Any(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "splashes"))
            throw new Exception("Paint body release must leave the yellow dots untouched");
        foreach (string forbidden in new[] { "GetValue", "GetComponentsInChildren", "GetParticles", "SetParticles", "Simulate", "Destroy" })
            if (bodyUpdate.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == forbidden))
                throw new Exception("Paint body tail must not discover/copy particles or destroy their owner: " + forbidden);
        if (bodyUpdate.Any(i => i.opcode == System.Reflection.Emit.OpCodes.Newarr))
            throw new Exception("Paint body tail must not allocate per-frame arrays");
        int bodyStops = 0;
        for (int i = 0; i < bodyUpdate.Count; i++)
            if (bodyUpdate[i].operand is MethodInfo && ((MethodInfo)bodyUpdate[i].operand).Name == "Stop")
            {
                bodyStops++;
                if (i < 2 || bodyUpdate[i - 2].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_0
                    || bodyUpdate[i - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_1)
                    throw new Exception("Body emitters must stop without clearing living particles or affecting children");
            }
        if (bodyStops != 2) throw new Exception("Only the separate cloud and streak emitters should stop");
        int bodyIndex = cloudTick.FindIndex(i => Equals(i.operand, bodyUpdateMethod));
        int speedIndex = cloudTick.FindLastIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "SetSpeed");
        if (bodyIndex <= speedIndex) throw new Exception("Paint body override must follow native dot speed control");
        MethodInfo initializeDots = AccessTools.Method(cloud, "InitializeDots");
        MethodInfo updateDotSpeed = AccessTools.Method(cloud, "UpdateDotSpeed");
        if (initializeDots == null || updateDotSpeed == null)
            throw new Exception("Repeated openings need an independent owned dot clock");
        int initializeIndex = cloudBegin.FindIndex(i => Equals(i.operand, initializeDots));
        int initialSpeedIndex = cloudBegin.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "SetSpeed");
        if (initializeIndex <= initialSpeedIndex || cloudBegin.Count(i => Equals(i.operand, initializeDots)) != 1)
            throw new Exception("Dots must warm once per opening, after native speed initialization");
        var dotInit = PatchProcessor.GetOriginalInstructions(initializeDots).ToList();
        int simulateIndex = dotInit.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Simulate");
        int dotPlayIndex = dotInit.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Play");
        int gentleSpeedIndex = dotInit.FindIndex(i => Equals(i.operand, updateDotSpeed));
        int dotEmissionRateIndex = dotInit.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "set_rateOverTimeMultiplier");
        int dotEmissionOffIndex = dotInit.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "set_enabled");
        if (dotEmissionRateIndex < 3 || dotEmissionRateIndex >= simulateIndex
            || !dotInit.Take(dotEmissionRateIndex).Any(i => i.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && Equals(i.operand, 0.75f))
            || dotEmissionOffIndex <= simulateIndex || dotEmissionOffIndex >= dotPlayIndex
            || dotInit[dotEmissionOffIndex - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_0)
            throw new Exception("Prepare 25% fewer dots, then disable only their emitter before resuming");
        if (simulateIndex < 4 || dotInit[simulateIndex - 4].opcode != System.Reflection.Emit.OpCodes.Ldc_R4
            || !Equals(dotInit[simulateIndex - 4].operand, 2f)
            || dotInit[simulateIndex - 3].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_0
            || dotInit[simulateIndex - 2].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_1
            || dotInit[simulateIndex - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_0)
            throw new Exception("Warm only the owned dot system for two seconds, restarting without children or frame-sized stepping");
        int warmSpeedIndex = dotInit.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "set_simulationSpeed");
        if (warmSpeedIndex < 1 || warmSpeedIndex >= simulateIndex
            || !Equals(dotInit[warmSpeedIndex - 1].operand, 1f))
            throw new Exception("Warmup must use normal time, never the native 100x clock");
        if (gentleSpeedIndex <= simulateIndex || dotPlayIndex <= gentleSpeedIndex
            || dotInit[dotPlayIndex - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_0)
            throw new Exception("Restore gentle/paused dot speed before resuming only dots");
        if (!dotInit.Any(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "splashes")
            || dotInit.Any(i => i.operand is FieldInfo && new[] { "splat", "strokes" }.Contains(((FieldInfo)i.operand).Name)))
            throw new Exception("Dot initialization must not touch the body particles");
        int pausedDotWrites = 0, activeDotWrites = 0;
        foreach (int index in Enumerable.Range(0, cloudTick.Count).Where(i => Equals(cloudTick[i].operand, updateDotSpeed)))
        {
            if (index < 3) throw new Exception("Missing native writes before dot override");
            bool paused = cloudTick[index - 1].opcode == System.Reflection.Emit.OpCodes.Ldc_I4_1;
            if (cloudTick[index - 1].opcode != (paused
                ? System.Reflection.Emit.OpCodes.Ldc_I4_1 : System.Reflection.Emit.OpCodes.Ldc_I4_0)
                || !(cloudTick[index - 2].operand is MethodInfo)
                || ((MethodInfo)cloudTick[index - 2].operand).Name != "SetStun")
                throw new Exception("Both active and paused dot overrides must follow native speed/stun writes");
            if (paused) pausedDotWrites++; else activeDotWrites++;
        }
        if (pausedDotWrites != 1 || activeDotWrites != 1
            || cloudTick.Any(i => Equals(i.operand, initializeDots)))
            throw new Exception("Apply dot clock in both pause and active paths without per-frame warmup");
        var dotUpdate = PatchProcessor.GetOriginalInstructions(updateDotSpeed).ToList();
        foreach (string forbidden in new[] { "Simulate", "Play", "Emit", "GetParticles", "SetParticles", "GetValue", "GetComponentsInChildren" })
            if (dotUpdate.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == forbidden))
                throw new Exception("Dot clock must stay cheap and preserve native particles: " + forbidden);
        if (dotUpdate.Any(i => i.opcode == System.Reflection.Emit.OpCodes.Newarr))
            throw new Exception("Dot timing must not allocate arrays per frame");
        MethodInfo getDotSpeed = AccessTools.Method(cloud, "GetDotSpeed");
        float activeDotSpeed = (float)getDotSpeed.Invoke(null, new object[] { 0f, false });
        if (activeDotSpeed <= 0f || activeDotSpeed > 0.1f
            || (float)getDotSpeed.Invoke(null, new object[] { 0f, true }) != 0f)
            throw new Exception("Dots should drift gently when active and stop during pause");
        foreach (float age in new[] { 0f, 0.375f, 0.75f, 1.499f, 1.5f, 5f, 60f })
        {
            float expected = activeDotSpeed * Math.Max(0f, 1f - age / 1.5f);
            float actual = (float)getDotSpeed.Invoke(null, new object[] { age, false });
            if (Math.Abs(actual - expected) > 0.000001f
                || (age >= 1.5f && actual != 0f)
                || (float)getDotSpeed.Invoke(null, new object[] { age, true }) != 0f)
                throw new Exception("Dots must settle linearly to an exact, permanent stop and always freeze in pause");
        }
        foreach (int fps in new[] { 30, 60, 143 })
        {
            float previousDotSpeed = activeDotSpeed;
            for (int frame = 0; frame < fps * 10; frame++)
            {
                float age = frame / (float)fps;
                float actual = (float)getDotSpeed.Invoke(null, new object[] { age, false });
                if (actual < 0f || actual > previousDotSpeed || (age >= 1.5f && actual != 0f))
                    throw new Exception("Settling must never resume or go negative at different frame rates");
                previousDotSpeed = actual;
            }
        }
        int firstDotAgeWrite = cloudTick.FindIndex(i => i.opcode == System.Reflection.Emit.OpCodes.Stsfld
            && i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "dotElapsed");
        int pausedOverride = cloudTick.FindIndex(i => Equals(i.operand, updateDotSpeed));
        if (firstDotAgeWrite <= pausedOverride || firstDotAgeWrite >= speedIndex)
            throw new Exception("Dot settle time must advance only after the paused early exit, before the active native writes");
        // Validate the paused path leaves Tick before it can advance dotElapsed.
        if (!cloudTick.Skip(pausedOverride + 1).Take(firstDotAgeWrite - pausedOverride - 1)
            .Any(i => i.opcode == System.Reflection.Emit.OpCodes.Leave || i.opcode == System.Reflection.Emit.OpCodes.Leave_S
                || i.opcode == System.Reflection.Emit.OpCodes.Ret))
            throw new Exception("Pausing must preserve the remaining dot drift time");
        Type foreground = mod.GetType("QuickPickGraffiti.SelectorForegroundDots", true);
        MethodInfo foregroundDraw = AccessTools.Method(foreground, "Draw");
        if (foregroundDraw.GetMethodBody().ExceptionHandlingClauses.Count == 0)
            throw new Exception("Dot overlay must restore native rendering if projection fails");
        var dotDraw = PatchProcessor.GetOriginalInstructions(foregroundDraw).ToList();
        foreach (string forbidden in new[] { "Instantiate", "GetComponent", "GetComponentsInChildren", "GetValue", "Simulate", "SetParticles", "Stop", "Play" })
            if (dotDraw.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == forbidden))
                throw new Exception("Dot overlay must only project live native particles: " + forbidden);
        if (dotDraw.Any(i => i.opcode == System.Reflection.Emit.OpCodes.Newarr))
            throw new Exception("Dot projection must reuse its buffer");
        foreach (string required in new[] { "GetParticles", "WorldToScreenPoint", "DrawTexture", "GetCurrentSize", "GetCurrentColor" })
            if (!dotDraw.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == required))
                throw new Exception("Native dot appearance/projection missing " + required);
        var drawWheel = PatchProcessor.GetOriginalInstructions(AccessTools.Method(selectorPluginType, "OnGUI")).ToList();
        int foregroundIndex = drawWheel.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "DrawForegroundDots");
        int lastWheelTexture = drawWheel.FindLastIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "DrawTexture");
        if (foregroundIndex <= lastWheelTexture) throw new Exception("Sharp dots must render over the finished wheel");
        var cloudReset = PatchProcessor.GetOriginalInstructions(AccessTools.Method(cloud, "Reset")).ToList();
        int clearDotIndex = cloudReset.FindIndex(i => i.opcode == System.Reflection.Emit.OpCodes.Stsfld
            && i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "dotSystem");
        if (clearDotIndex < 1 || cloudReset[clearDotIndex - 1].opcode != System.Reflection.Emit.OpCodes.Ldnull)
            throw new Exception("A later opening must not inherit the prior clone's dot system");
        int resetDotAgeIndex = cloudReset.FindIndex(i => i.opcode == System.Reflection.Emit.OpCodes.Stsfld
            && i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "dotElapsed");
        if (resetDotAgeIndex < 1 || !Equals(cloudReset[resetDotAgeIndex - 1].operand, 0f))
            throw new Exception("Each later opening must start a fresh drift-to-stop interval");
        int restoreDotsIndex = cloudReset.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == foreground && ((MethodInfo)i.operand).Name == "Dispose");
        int destroyCloudIndex = cloudReset.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Destroy");
        if (restoreDotsIndex < 0 || restoreDotsIndex >= destroyCloudIndex)
            throw new Exception("Owned dot renderer must restore before native cloud cleanup");
        Type selectionAudio = mod.GetType("QuickPickGraffiti.SelectionAudio", true);
        if ((int)AccessTools.Field(selectionAudio, "MaxVoices").GetRawConstantValue() != 3)
            throw new Exception("Selection sounds must have a hard three-voice limit");
        MethodInfo audioPlayMethod = AccessTools.Method(selectionAudio, "Play");
        if (audioPlayMethod.GetMethodBody().ExceptionHandlingClauses.Count == 0)
            throw new Exception("Sound failures must not escape into selector controls");
        var audioPlay = PatchProcessor.GetOriginalInstructions(audioPlayMethod).ToList();
        if (audioPlay.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "PlayOneShot"))
            throw new Exception("One-shots would bypass the three-voice pool");
        int audioStop = audioPlay.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Stop");
        int audioStart = audioPlay.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Play");
        if (audioStop < 0 || audioStart <= audioStop) throw new Exception("A reused voice must stop before its new clip starts");
        var voiceChoice = PatchProcessor.GetOriginalInstructions(AccessTools.Method(selectionAudio, "FindVoice")).ToList();
        if (!voiceChoice.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "get_isPlaying"))
            throw new Exception("Voice pool must prefer idle voices");
        var pluginSound = PatchProcessor.GetOriginalInstructions(AccessTools.Method(selectorPluginType, "PlaySelectionSound")).ToList();
        if (!pluginSound.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == selectionAudio)
            || pluginSound.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Invoke"))
            throw new Exception("Selection clicks must use the bounded pool rather than unlimited native one-shots");
        Type motionType = mod.GetType("QuickPickGraffiti.SelectorCameraMotion", true);
        MethodInfo handheldMethod = AccessTools.Method(motionType, "SampleHandheld");
        var cameraUpdate = PatchProcessor.GetOriginalInstructions(AccessTools.Method(motionType, "UpdateActive")).ToList();
        if (handheldMethod == null || !cameraUpdate.Any(i => Equals(i.operand, handheldMethod)))
            throw new Exception("Handheld sway must remain connected to the active selector camera");
        var handheld = PatchProcessor.GetOriginalInstructions(handheldMethod).ToList();
        if (handheld.Count(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Noise") != 6
            || handheld.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType.FullName == "UnityEngine.Random"))
            throw new Exception("Handheld sway must sample continuous bands without frame-dependent randomness");
        var selectorUpdate = PatchProcessor.GetOriginalInstructions(AccessTools.Method(selectorPluginType, "Update")).ToList();
        int cloudTickIndex = selectorUpdate.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == cloud && ((MethodInfo)i.operand).Name == "Tick");
        int inputGateIndex = selectorUpdate.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "get_IsRewiredInitialized");
        if (cloudTickIndex < 0 || inputGateIndex < 0 || cloudTickIndex > inputGateIndex)
            throw new Exception("Transient effect lifetime must advance independently of input readiness");
        MethodInfo localDirection = AccessTools.Method(cloud, "LocalDirection");
        int[] horizontal = { 0, 2, 1, 2, 0, -2, -1, -2 };
        int[] vertical = { 1, 1, 0, -1, -1, -1, 0, 1 };
        for (int side = 0; side < originalChoice; side++)
        {
            var vector = (UnityEngine.Vector2)localDirection.Invoke(null, new object[] { side });
            if (vector.x != horizontal[side] || vector.y != vertical[side])
                throw new Exception("Opening cloud must match native directional pose orientation");
        }
        foreach (string name in new[] { "Update", "CaptureMouseCursor" })
        {
            var cursorCode = PatchProcessor.GetOriginalInstructions(AccessTools.Method(selectorPluginType, name)).ToList();
            bool found = false;
            for (int index = 1; index < cursorCode.Count; index++)
                if (cursorCode[index].operand is MethodInfo && ((MethodInfo)cursorCode[index].operand).Name == "set_visible")
                {
                    found = true;
                    if (cursorCode[index - 1].opcode != System.Reflection.Emit.OpCodes.Ldc_I4_0)
                        throw new Exception("Selector mouse cursor must stay hidden");
                }
            if (!found) throw new Exception("Missing selector cursor hide");
        }
        var initVisual = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(GraffitiGame), "InitVisual")).ToList();
        if (!initVisual.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "SetBoostpackEffect"))
            throw new Exception("Native selector boost setup missing");
        var nativeVisual = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(GraffitiGame), "SetStateVisual")).ToList();
        if (!nativeVisual.Any(i => i.operand as string == "grafSlashFinisher"))
            throw new Exception("Native finishing animation missing");
        Type endPatch = mod.GetType("QuickPickGraffiti.Patches+EndPatch", true);
        var poseEnd = PatchProcessor.GetOriginalInstructions(AccessTools.Method(endPatch, "Prefix")).ToList();
        if (!poseEnd.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "ResetFor"))
            throw new Exception("Graffiti exit must clear the active pose");
        MethodInfo filter = patches.GetMethod("AvailableArt", BindingFlags.Static | BindingFlags.NonPublic);
        var ownedMedium = new GraffitiArt { title = "Owned M", graffitiSize = GraffitiSize.M };
        var ownedLarge = new GraffitiArt { title = "Owned L", graffitiSize = GraffitiSize.L };
        var owned = new List<GraffitiArt> { ownedMedium, null, ownedLarge };
        var available = (List<GraffitiArt>)filter.Invoke(null, new object[] { owned, GraffitiSize.M });
        if (available.Count != 1 || !ReferenceEquals(available[0], ownedMedium)) throw new Exception("Available-art filter leaked a wrong-size or unavailable design");
        var none = (List<GraffitiArt>)filter.Invoke(null, new object[] { null, GraffitiSize.M });
        if (none.Count != 0) throw new Exception("Missing unlock data must not expose the full catalog");
        if (mod.GetManifestResourceNames().Any(name => name.Contains("liquid-") || name.Contains("yellow-")))
            throw new Exception("Obsolete animation atlases/static spray assets should not ship");
        if(mod.GetType("QuickPickGraffiti.SprayDots")!=null) throw new Exception("Spray renderer should no longer ship");
        if(!mod.GetManifestResourceNames().Contains("QuickPickGraffiti.dance-pointer.png")) throw new Exception("Native dance pointer asset missing");
        Type pluginType=mod.GetType("QuickPickGraffiti.Plugin",true);
        var pluginUpdate=PatchProcessor.GetOriginalInstructions(AccessTools.Method(pluginType,"Update")).ToList();
        if(!pluginUpdate.Any(i=>i.operand is MethodInfo && ((MethodInfo)i.operand).Name=="WarmResources")) throw new Exception("Warmup must run outside first-use selection");
        var warmup=PatchProcessor.GetOriginalInstructions(AccessTools.Method(pluginType,"WarmResources")).ToList();
        if(warmup.Any(i=>i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType.Name=="BackgroundBlur"))
            throw new Exception("Blur must not prepare game resources during startup");
        var restore=PatchProcessor.GetOriginalInstructions(AccessTools.Method(pluginType,"RestorePickerMaps")).ToList();
        if(!restore.Any(i=>i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType.Name=="SelectorCameraMotion" && ((MethodInfo)i.operand).Name=="Restore"))
            throw new Exception("Native graffiti continuation must restore the selector camera first");
        Type blurType=mod.GetType("QuickPickGraffiti.BackgroundBlur",true);
        MethodInfo releaseBlurVolume = AccessTools.Method(blurType, "ReleaseVolume");
        if (releaseBlurVolume == null) throw new Exception("Blur must share owned-volume cleanup between recovery and disposal");
        var blurPrepare = PatchProcessor.GetOriginalInstructions(AccessTools.Method(blurType, "Prepare")).ToList();
        int releaseBlurIndex = blurPrepare.FindIndex(i => Equals(i.operand, releaseBlurVolume));
        int detachBlurIndex = blurPrepare.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Detach");
        if (releaseBlurIndex < 0 || detachBlurIndex < 0 || detachBlurIndex >= releaseBlurIndex)
            throw new Exception("Partial blur state must restore camera bindings before destroying its owned volume");
        int blurResourceIndex = blurPrepare.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "FindResources");
        if (blurResourceIndex < 0 || !blurPrepare.Skip(blurResourceIndex + 1).Take(detachBlurIndex - blurResourceIndex - 1)
            .Any(i => i.opcode == System.Reflection.Emit.OpCodes.Ret
                || i.opcode == System.Reflection.Emit.OpCodes.Leave || i.opcode == System.Reflection.Emit.OpCodes.Leave_S))
            throw new Exception("Healthy blur state must return before cleanup, preventing per-frame object recreation");
        foreach (string required in new[] { "root", "volume", "profile", "effect" })
            if (!blurPrepare.Take(releaseBlurIndex).Any(i => i.opcode == System.Reflection.Emit.OpCodes.Ldfld
                && i.operand is FieldInfo && ((FieldInfo)i.operand).Name == required))
                throw new Exception("A live profile alone must not skip rebuilding destroyed blur state: " + required);
        int persistentBlurIndex = blurPrepare.FindIndex(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "DontDestroyOnLoad");
        if (persistentBlurIndex < 1 || !(blurPrepare[persistentBlurIndex - 1].operand is FieldInfo)
            || ((FieldInfo)blurPrepare[persistentBlurIndex - 1].operand).Name != "root")
            throw new Exception("The owned blur root must survive normal area unloads");
        var blurRelease = PatchProcessor.GetOriginalInstructions(releaseBlurVolume).ToList();
        foreach (string forbidden in new[] { "camera", "layer", "ownedLayers", "resources" })
            if (blurRelease.Any(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == forbidden))
                throw new Exception("Blur recovery must release only owned volume objects, never native state: " + forbidden);
        var blurDispose = PatchProcessor.GetOriginalInstructions(AccessTools.Method(blurType, "Dispose")).ToList();
        if (blurDispose.Count(i => Equals(i.operand, releaseBlurVolume)) != 1)
            throw new Exception("Disposal must release the same owned volume/profile/effect exactly once");
        if (!blurDispose.Any(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "ownedLayers")
            || !blurDispose.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Destroy")
            || !blurDispose.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Clear"))
            throw new Exception("Blur disposal must also release its owned camera layers and clear their references");
        foreach(var method in blurType.GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
            if(method.DeclaringType==blurType && method.GetMethodBody()!=null)
                if(PatchProcessor.GetOriginalInstructions(method).Any(i=>i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType.FullName=="UnityEngine.AssetBundle"))
                    throw new Exception("Native blur must not load game asset bundles");
        Type paintType=mod.GetType("QuickPickGraffiti.PaintVisuals",true);
        var prepare=PatchProcessor.GetOriginalInstructions(AccessTools.Method(paintType,"Prepare")).ToList();
        if(prepare.Any(i=>i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType.FullName=="UnityEngine.GUI")) throw new Exception("Offscreen warmup must not display the wheel");
        Type statePatch = mod.GetType("QuickPickGraffiti.Patches+StatePatch", true);
        var transpiler = statePatch.GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static);
        var after = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { before.Select(i => new CodeInstruction(i)).ToList() })).ToList();
        if (before.Count != after.Count) throw new Exception("Native state logic length changed");
        int changed = 0;
        for (int i = 0; i < before.Count; i++)
            if (before[i].opcode != after[i].opcode || !Equals(before[i].operand, after[i].operand))
            {
                changed++;
                MethodInfo oldCall = before[i].operand as MethodInfo;
                MethodInfo newCall = after[i].operand as MethodInfo;
                if (oldCall == null || oldCall.Name != "FindBySequence" || newCall == null || newCall.Name != "ResolveArt")
                    throw new Exception("Unexpected native instruction modified");
            }
        if (changed != 1) throw new Exception("Expected exactly one art lookup replacement");
        foreach (string name in new[] { "Paint", "GiveRep", "SpawnRep", "End", "SetStateVisual" })
            if (!after.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == name))
                throw new Exception("Missing native lifecycle call: " + name);
        var update = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(GraffitiGame), "Update")).ToList();
        if (!update.Any(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "stateTimer")) throw new Exception("Native timers missing");
        if (!update.Any(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "showPieceDuration_L_XL")) throw new Exception("Native size-dependent reveal duration missing");
        MethodInfo nextSpeed = AccessTools.Method(cloud, "NextSpeed");
        foreach (float speed in new[] { 100f, 0.5f, 0.499f, 0.1f, 0f })
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 143f, 0.2f })
            {
                float factor = Math.Max(0f, Math.Min(1f, dt * (speed >= 0.5f ? 10f : 1f)));
                float expected = speed + (0f - speed) * factor;
                float actual = (float)nextSpeed.Invoke(null, new object[] { speed, dt });
                if (Math.Abs(expected - actual) > 0.000001f)
                    throw new Exception("Effect speed differs from native threshold/clamped linear interpolation");
            }
        MethodInfo nextPhase = AccessTools.Method(cloud, "NextPhase");
        object[][] boundaries = {
            new object[] { 1, 0.1f, 0f, 0.1f, 0.18f },
            new object[] { 1, 0.1f, 0.01f, 0.1f, 0.18f },
            new object[] { 2, 0.18f, 0f, 0.1f, 0.18f },
            new object[] { 2, 0.18f, 0.01f, 0.1f, 0.18f },
            new object[] { 3, 0f, 10f, 0.1f, 0.18f },
            new object[] { 1, 0f, 1f, 0.1f, 0.18f }
        };
        int[] phaseResults = { 1, 2, 2, 3, 3, 2 };
        float[] timerResults = { 0.1f, 0f, 0.18f, 0f, 10f, 0f };
        for (int i = 0; i < boundaries.Length; i++)
        {
            int result = (int)nextPhase.Invoke(null, boundaries[i]);
            if (result != phaseResults[i] || Math.Abs((float)boundaries[i][1] - timerResults[i]) > 0.000001f)
                throw new Exception("Native phase boundaries/reset/one-transition-per-frame changed");
        }
        foreach (int fps in new[] { 30, 60, 143 })
        {
            int phase = 1, transitions = 0;
            float timer = 0f;
            for (int frame = 0; frame < fps * 10; frame++)
            {
                object[] state = { phase, timer, 1f / fps, 0.1f, 0.18f };
                int updated = (int)nextPhase.Invoke(null, state);
                if (updated != phase) transitions++;
                phase = updated;
                timer = (float)state[1];
            }
            if (phase != 3 || transitions != 2) throw new Exception("Native hit-pause must release once and never recur");
        }
        Type nativePhases = typeof(GraffitiGame).GetNestedType("GrafCamState", BindingFlags.NonPublic | BindingFlags.Public);
        if (Convert.ToInt32(Enum.Parse(nativePhases, "FAST")) != 1
            || Convert.ToInt32(Enum.Parse(nativePhases, "HITLAG")) != 2
            || Convert.ToInt32(Enum.Parse(nativePhases, "SLOW")) != 3)
            throw new Exception("Native effect phases differ from the verified game version");
        var nativeEffectUpdate = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(GraffitiGame), "UpdateMainStateVisual")).ToList();
        int start = nativeEffectUpdate.FindIndex(i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "effectSpeed");
        int end = nativeEffectUpdate.FindIndex(start, i => i.operand is FieldInfo && ((FieldInfo)i.operand).Name == "grafCamState");
        var decay = nativeEffectUpdate.Skip(start).Take(end - start).ToList();
        foreach (float nativeConstant in new[] { 0.5f, 10f, 1f })
            if (!decay.Any(i => i.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && Equals(i.operand, nativeConstant)))
                throw new Exception("Native speed rule changed; inspect game before updating the copy");
        if (decay.Count(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Lerp") != 2)
            throw new Exception("Native effect no longer uses the verified two-stage decay");
        Console.WriteLine("Native slowdown/hit-pause/exit cleanup, pose/cursor ownership, collected-art and finishing-flow checks passed.");
    }
}
