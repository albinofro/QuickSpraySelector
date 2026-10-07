using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Reptile;
using Rewired;
using BepInEx.Configuration;
using System;
using System.Reflection;

namespace QuickPickGraffiti
{
    [BepInPlugin("codex.quickpickgraffiti", "QuickSpraySelector", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony harmony;
        private static GraffitiGame activeGame;
        private static List<GraffitiArt> choices;
        private static Texture[] previews;
        private static GUIContent[] artNames;
        private static WheelSelection wheel;
        private static readonly WheelAimInput aimInput = new WheelAimInput();
        private static Vector2 lastMousePosition;
        private static bool mouseCaptured;
        private static CursorLockMode savedCursorLock;
        private static bool savedCursorVisible;
        private static bool pickerOpen;
        private static bool selectionResolved;
        private static bool aborted;
        private static int rememberedIndex = -1;
        private static string sizeCategory;
        private static LastGraffitiStore memory;
        private static readonly Dictionary<ControllerMap, bool> pickerMaps = new Dictionary<ControllerMap, bool>();
        private static readonly int[] pickerActions = { TRInputId.menuConfirm, TRInputId.menuCancel, TRInputId.menuCycleLeft, TRInputId.menuCycleRight, TRInputId.menuX };
        private static float inputReadyTime;
        private static float openedAt;
        private static float visualPointerAngle;
        private static readonly float[] iconScales = new float[WheelSelection.PageSize];
        private bool hintsWarmed;
        private bool rendererWarmed;
        private float nextWarmAttempt;
        private GameButtonHints hints;
        private PaintVisuals paintVisuals;
        private static BackgroundBlur backgroundBlur;
        private static SelectorCameraMotion selectorCamera;
        private Texture2D ringTexture;
        private Texture2D pointerTexture;
        private static GameObject hiddenCircle;
        private static bool circleWasActive;
        private GUIStyle titleStyle;
        private GUIStyle detailStyle;
        private GUIStyle sizeStyle;
        private static Dictionary<GraffitiSize, ConfigEntry<string>> lastGraffitiTitles;
        private SelectionAudio selectionAudio;
        private ConfigEntry<bool> nativeBlurEnabled;
        private ConfigEntry<UnityEngine.Rendering.PostProcessing.KernelSize> nativeBlurQuality;
        private static readonly ControllerType[] pickerControllerTypes = { ControllerType.Keyboard, ControllerType.Mouse, ControllerType.Joystick };

        private void Awake()
        {
            harmony = new Harmony("codex.quickpickgraffiti.patch");
            ringTexture = WheelStyle.CreateSelectionGlow(512);
            pointerTexture = WheelStyle.CreatePointer();
            harmony.PatchAll();
            Logger.LogInfo("QuickSpraySelector 1.0.0 loaded: native game depth-of-field blur.");
            backgroundBlur = new BackgroundBlur(message => Logger.LogWarning(message));
            selectorCamera = new SelectorCameraMotion(message => Logger.LogWarning(message));
            nativeBlurEnabled = Config.Bind("Graphics", "NativeBlurEnabled", true, "Enable background blur while the wheel is open. Turn off for the lowest GPU cost.");
            nativeBlurQuality = Config.Bind("Graphics", "NativeBlurQuality", UnityEngine.Rendering.PostProcessing.KernelSize.Medium,
                "Small has the lowest blur cost. Medium is the recommended default; Large and VeryLarge use more GPU work.");

            lastGraffitiTitles = new Dictionary<GraffitiSize, ConfigEntry<string>>();
            foreach (GraffitiSize size in Enum.GetValues(typeof(GraffitiSize)))
            {
                lastGraffitiTitles[size] = Config.Bind<string>("LastGraffiti", size.ToString(), "", "Last successfully painted art of this size");
            }
            memory = new LastGraffitiStore(
                key => lastGraffitiTitles[(GraffitiSize)Enum.Parse(typeof(GraffitiSize), key)].Value,
                (key, title) => {
                    lastGraffitiTitles[(GraffitiSize)Enum.Parse(typeof(GraffitiSize), key)].Value = title;
                    Config.Save();
                });

            selectionAudio = new SelectionAudio(transform, message => Logger.LogWarning(message));
        }

        private static IGamepadTemplate GetGamepad()
        {
            Reptile.Core core = Reptile.Core.Instance;
            if (core == null || core.GameInput == null || !core.GameInput.IsRewiredInitialized)
                return null;
            Rewired.Player player = core.GameInput.FirstRewiredPlayer;
            if (player == null) return null;
            foreach (Joystick joystick in player.controllers.Joysticks)
            {
                IGamepadTemplate template = joystick.GetTemplate<IGamepadTemplate>();
                if (template != null) return template;
            }
            return null;
        }

        private void Update()
        {
            WarmResources();
            if (pickerOpen) SelectorPaintCloud.Tick(Time.deltaTime);
            if (!pickerOpen || wheel == null) { if (hints != null) hints.Hide(); return; }
            Reptile.Core core = Reptile.Core.Instance;
            if (core == null || core.GameInput == null || !core.GameInput.IsRewiredInitialized) return;
            GameInput input = core.GameInput;
            if (core.BaseModule.IsInGamePaused || !Application.isFocused)
            {
                RestoreMouseCursor();
                return;
            }
            CaptureMouseCursor();
            EnablePickerMaps(input);
            if (Time.unscaledTime < inputReadyTime) return;
            bool keyboard = input.GetCurrentControllerType(0) != ControllerType.Joystick;
            Rewired.Player player = input.FirstRewiredPlayer;
            IGamepadTemplate pad = GetGamepad();
            bool previous = (keyboard && player != null && player.GetNegativeButtonDown(TRInputId.menuX))
                || (pad != null && pad.dPad.left.justPressed);
            bool next = (keyboard && player != null && player.GetButtonDown(TRInputId.menuX))
                || (pad != null && pad.dPad.right.justPressed);
            bool previousPage = input.GetButtonNew(TRInputId.menuCycleLeft, 0) || UnityEngine.Input.mouseScrollDelta.y > 0f;
            bool nextPage = input.GetButtonNew(TRInputId.menuCycleRight, 0) || UnityEngine.Input.mouseScrollDelta.y < 0f;
            bool discrete = previous || next || previousPage || nextPage;
            int previousSelection = wheel.SelectedIndex; // Moved this line
            if (previousPage) wheel.ChangePage(-1);
            else if (nextPage) wheel.ChangePage(1);
            else if (previous) wheel.Step(-1);
            else if (next) wheel.Step(1);

            // Templates expose both physical sticks without enabling gameplay/camera maps.
            Vector2 leftStick = pad == null ? Vector2.zero : pad.leftStick.value;
            Vector2 rightStick = pad == null ? Vector2.zero : pad.rightStick.value;
            Vector2 mouse = UnityEngine.Input.mousePosition;
            bool mouseMoved = (mouse - lastMousePosition).sqrMagnitude > 0.5f;
            if (mouseMoved) lastMousePosition = mouse;
            Vector2 mouseAim = mouse - new Vector2(Screen.width * 0.5f, Screen.height * 0.54f);
            float radius = Mathf.Min(Screen.height * 0.40f, Screen.width * 0.30f);
            mouseAim = mouseAim.sqrMagnitude < radius * radius * 0.0324f ? Vector2.zero : mouseAim.normalized;
            float aimX, aimY;
            bool hasAim = aimInput.Update(leftStick.x, leftStick.y, rightStick.x, rightStick.y,
                mouseAim.x, mouseAim.y, mouseMoved, discrete, out aimX, out aimY);
            if (hasAim || discrete || !aimInput.MouseActive || mouseMoved)
                wheel.UpdateStick(aimX, aimY, discrete);
            Cursor.visible = false;

            if (wheel.SelectedIndex != previousSelection)
            {
                PlaySelectionSound(core.AudioManager);
            }

            if (input.GetButtonNew(TRInputId.menuConfirm, 0) || UnityEngine.Input.GetMouseButtonDown(0))
            {
                selectionResolved = true;
                pickerOpen = false;
            }
            else if (input.GetButtonNew(TRInputId.menuCancel, 0) || UnityEngine.Input.GetMouseButtonDown(1))
            {
                if (rememberedIndex >= 0) wheel.SelectIndex(rememberedIndex);
                else aborted = true;
                selectionResolved = true;
                pickerOpen = false;
            }
            if (pickerOpen && core.UIManager != null)
            {
                if (hints == null) hints = new GameButtonHints(core.UIManager);
                hints.Refresh(input, rememberedIndex >= 0);
            }
            else if (hints != null) hints.Hide();
            if (!pickerOpen && backgroundBlur != null) backgroundBlur.UpdateActive(false);
            if (!pickerOpen) RestoreMouseCursor();
        }

        private void PlaySelectionSound(AudioManager manager)
        {
            if (selectionAudio != null) selectionAudio.Play(manager);
        }

        private void WarmResources()
        {
            if(hintsWarmed && paintVisuals != null) return;
            if(Time.unscaledTime < nextWarmAttempt) return;
            Reptile.Core core=Reptile.Core.Instance;
            if(core==null || core.UIManager==null || core.GameInput==null || !core.GameInput.IsRewiredInitialized) return;
            try
            {
                // Separate mesh construction and text preparation across loading frames.
                if(paintVisuals==null) { paintVisuals=new PaintVisuals(); return; }
                if(hints==null) { hints=new GameButtonHints(core.UIManager); return; }
                if(!hintsWarmed && !pickerOpen) { hints.Warmup(core.GameInput); hintsWarmed=true; }
            }
            catch(Exception ex)
            {
                nextWarmAttempt=Time.unscaledTime+5f;
                Logger.LogWarning("Picker warmup will retry when UI is ready: "+ex.Message);
            }
        }

        private void LateUpdate()
        {
            Reptile.Core core = Reptile.Core.Instance;
            bool presenting = pickerOpen && wheel != null && activeGame != null
                && core != null && core.BaseModule != null && !core.BaseModule.IsInGamePaused;
            float age = Mathf.Max(0f, Time.unscaledTime - openedAt);
            float selectedAngle = presenting ? (wheel.SelectedIndex - wheel.PageStart) * (360f / wheel.VisibleCount) : 0f;
            if (selectorCamera != null) selectorCamera.UpdateActive(presenting, activeGame, age, selectedAngle);
            if (backgroundBlur != null)
            {
                backgroundBlur.SetQuality(nativeBlurQuality.Value);
                backgroundBlur.UpdateActive(presenting && nativeBlurEnabled.Value, PresentationTiming.BlurWeight(age));
            }
            if(!pickerOpen || wheel==null) return;
            float dt=Time.unscaledDeltaTime;
            visualPointerAngle=WheelMotion.SmoothAngle(visualPointerAngle,wheel.PointerAngle,dt);
            float blend=1f-Mathf.Exp(-dt/.045f);
            float sector=360f/wheel.VisibleCount;
            for(int slot=0;slot<wheel.VisibleCount;slot++)
            {
                bool selected=wheel.PageStart+slot==wheel.SelectedIndex;
                float targetScale=WheelMotion.TargetScale(slot*sector,visualPointerAngle,selected);
                iconScales[slot]=Mathf.Lerp(iconScales[slot],targetScale,blend);
            }
        }

        private void OnGUI()
        {
            if(Event.current.type==EventType.Repaint && !rendererWarmed && paintVisuals!=null)
            {
                // Render to the private texture only: nothing flashes onscreen.
                paintVisuals.Prepare(Mathf.Min(Screen.height*.40f,Screen.width*.30f)*2f,1f);
                rendererWarmed=true;
            }
            if (Event.current.type != EventType.Repaint) return;
            if (titleStyle == null && (paintVisuals != null || pickerOpen))
            {
                detailStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
                titleStyle = new GUIStyle(detailStyle) { fontStyle = FontStyle.Bold };
                sizeStyle = new GUIStyle(titleStyle);
            }
            if (!pickerOpen || wheel == null) return;
            if (backgroundBlur != null) backgroundBlur.Draw(Time.unscaledTime - openedAt);
            float radius = Mathf.Min(Screen.height * 0.40f, Screen.width * 0.30f);
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.46f);
            float sectorWidth = 360f / wheel.VisibleCount;
            detailStyle.fontSize = Mathf.Max(13, Mathf.RoundToInt(radius * 0.040f));
            titleStyle.fontSize = Mathf.Max(17, Mathf.RoundToInt(radius * 0.054f));
            sizeStyle.fontSize = Mathf.RoundToInt(titleStyle.fontSize * 1.7f);
            if (hints != null && hints.GuiFont != null) titleStyle.font = detailStyle.font = sizeStyle.font = hints.GuiFont;
            Color savedColor = GUI.color;
            GUI.color = Color.white;
            Rect disc = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            // The game's UI/sprite shaders are loaded by the time graffiti mode opens.
            if (paintVisuals == null) paintVisuals = new PaintVisuals();
            paintVisuals.Draw(disc, Time.unscaledTime - openedAt);
            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot((wheel.SelectedIndex-wheel.PageStart)*sectorWidth, center);
            GUI.DrawTexture(disc, ringTexture);
            GUI.matrix = savedMatrix;
            DrawTitleTab(new Rect(center.x - radius * 0.91f, center.y - radius * 0.93f, radius * 0.83f, radius * 0.10f), "GRAFFITI SELECT", -17f, detailStyle);
            DrawTitleTab(new Rect(center.x + radius * 0.56f, center.y - radius * 0.78f, radius * 0.29f, radius * 0.16f), sizeCategory, 17f, sizeStyle);

            for (int slot = 0; slot < wheel.VisibleCount; slot++)
            {
                int index = wheel.PageStart + slot;
                Vector2 position = center + Direction(slot * sectorWidth) * radius * 0.73f;
                float size = radius * 0.26f;
                Rect card = new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size);
                bool selected = index == wheel.SelectedIndex;
                Rect imageRect = new Rect(card.x + 7f, card.y + 7f, size - 14f, size * 0.73f);
                float magnification=iconScales[slot];
                Vector2 imageCenter=imageRect.center;
                imageRect.width*=magnification; imageRect.height*=magnification;
                imageRect.center=imageCenter;
                if (previews[index] != null)
                    GUI.DrawTexture(imageRect, previews[index], ScaleMode.ScaleToFit, true);
                else
                    GUI.Label(imageRect, "Preview unavailable", detailStyle);
                DrawName(new Rect(card.x - size * 0.12f, card.y + size * 0.76f, size * 1.24f, size * 0.42f), artNames[index], detailStyle, selected);
                if (selected) Fill(new Rect(card.x + size * 0.19f, card.y + size * 1.13f, size * 0.62f, Mathf.Max(3f, radius * 0.008f)), new Color(0.56f, 0.9f, 0.28f, 1f));
                GUI.color = Color.white;
            }

            Rect previewRect = new Rect(center.x - radius * 0.38f, center.y - radius * 0.30f, radius * 0.76f, radius * 0.53f);
            if (previews[wheel.SelectedIndex] != null)
                GUI.DrawTexture(previewRect, previews[wheel.SelectedIndex], ScaleMode.ScaleToFit, true);
            DrawName(new Rect(center.x - radius * 0.40f, center.y + radius * 0.24f, radius * 0.80f, radius * 0.13f), artNames[wheel.SelectedIndex], titleStyle, false);
            GUI.Label(new Rect(center.x - radius * 0.4f, center.y + radius * 0.37f, radius * 0.8f, radius * 0.06f),
                (wheel.SelectedIndex + 1) + " / " + choices.Count + "   •   " + (wheel.PageStart / WheelSelection.PageSize + 1) + " / " + ((choices.Count + WheelSelection.PageSize - 1) / WheelSelection.PageSize)
                + (wheel.SelectedIndex == rememberedIndex ? "   •   LAST USED" : ""), detailStyle);

            Vector2 direction = Direction(visualPointerAngle);
            Vector2 pointerCenter = center + direction * radius * 0.515f;
            float pointerSize = radius * .17f;
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(visualPointerAngle, pointerCenter);
            float pointerHeight=pointerSize*pointerTexture.height/pointerTexture.width;
            GUI.DrawTexture(new Rect(pointerCenter.x-pointerSize*.5f,pointerCenter.y-pointerHeight*.5f,pointerSize,pointerHeight),pointerTexture);
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
            SelectorPaintCloud.DrawForegroundDots();
        }

        private static void DrawTitleTab(Rect rect, string text, float angle, GUIStyle style)
        {
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            Fill(rect, new Color(0.025f, 0.03f, 0.023f, 1f));
            GUI.Label(rect, text, style);
            GUI.matrix = matrix;
        }

        private static void DrawName(Rect rect, GUIContent content, GUIStyle style, bool selected)
        {
            Color savedColor = GUI.color;
            int savedSize = style.fontSize;
            while (style.fontSize > Mathf.Max(11, savedSize * 2 / 3) && style.CalcHeight(content, rect.width) > rect.height)
                style.fontSize--;
            float outline = Mathf.Max(1f, Screen.height / 720f);
            GUI.color = Color.black;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (x != 0 || y != 0)
                        GUI.Label(new Rect(rect.x + x * outline, rect.y + y * outline, rect.width, rect.height), content, style);
            GUI.color = selected ? new Color(0.72f, 1f, 0.42f, 1f) : Color.white;
            GUI.Label(rect, content, style);
            style.fontSize = savedSize;
            GUI.color = savedColor;
        }

        private static void EnablePickerMaps(GameInput input)
        {
            // Init disables all maps except graffiti. Enable the actual mapped menu controls
            // only while choosing, retaining each map's previous state for animation/exit.
            foreach (ControllerType type in pickerControllerTypes)
                foreach (int action in pickerActions)
                {
                    ActionElementMap binding = input.RewiredMappingHandler.GetCurrentControllerActionElementMap(action, type, 0);
                    ControllerMap map = binding == null ? null : binding.controllerMap;
                    if (map == null) continue;
                    if (!pickerMaps.ContainsKey(map)) pickerMaps.Add(map, map.enabled);
                    map.enabled = true;
                }
        }

        internal static void RestorePickerMaps()
        {
            NativeSprayPose.ResetFor(activeGame);
            RestoreMouseCursor();
            if (selectorCamera != null) selectorCamera.Restore();
            if (backgroundBlur != null) backgroundBlur.UpdateActive(false);
            RestoreNativeCircle();
            foreach (var entry in pickerMaps) entry.Key.enabled = entry.Value;
            pickerMaps.Clear();
        }

        private static void CaptureMouseCursor()
        {
            if (mouseCaptured) return;
            savedCursorLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            mouseCaptured = true;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;
            lastMousePosition = UnityEngine.Input.mousePosition;
        }

        private static void RestoreMouseCursor()
        {
            if (!mouseCaptured) return;
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
            mouseCaptured = false;
        }

        private static void HideNativeCircle(GraffitiGame game)
        {
            RestoreNativeCircle();
            // Only the original pattern backdrop; leave the game cameras/effects intact.
            hiddenCircle = Traverse.Create(game).Field("backgroundCircle").GetValue<GameObject>();
            if (hiddenCircle == null) return;
            circleWasActive = hiddenCircle.activeSelf;
            hiddenCircle.SetActive(false);
        }

        private static void RestoreNativeCircle()
        {
            if (hiddenCircle != null) hiddenCircle.SetActive(circleWasActive);
            hiddenCircle = null;
        }

        private static Vector2 Direction(float clockwiseDegrees)
        {
            float angle = clockwiseDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawLine(Vector2 from, Vector2 to, float width, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color saved = GUI.color;
            Vector2 delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = saved;
        }

        internal static void BeginSelection(GraffitiGame game, List<GraffitiArt> available, GraffitiSize size)
        {
            if (ReferenceEquals(activeGame, game)) return;
            activeGame = game;
            openedAt = Time.unscaledTime;
            inputReadyTime = Time.unscaledTime + 0.15f;
            choices = available;
            pickerOpen = choices != null && choices.Count > 0;
            selectionResolved = !pickerOpen;
            aborted = false;
            rememberedIndex = -1;
            sizeCategory = size.ToString();
            wheel = pickerOpen ? new WheelSelection(choices.Count) : null;
            previews = pickerOpen ? new Texture[choices.Count] : null;
            artNames = pickerOpen ? new GUIContent[choices.Count] : null;
            if (pickerOpen && selectorCamera != null) selectorCamera.BeginSession();
            if (pickerOpen) HideNativeCircle(game);
            if (pickerOpen)
                for (int i = 0; i < choices.Count; i++)
                {
                    GraffitiArt art = choices[i];
                    artNames[i] = new GUIContent(art == null ? "" : art.title);
                    if (art == null) continue;
                    Texture texture = art.unlockable == null ? null : art.unlockable.GraffitiTexture;
                    previews[i] = texture != null ? texture : (art.graffitiMaterial == null ? null : art.graffitiMaterial.mainTexture);
                }

            if (pickerOpen && memory != null)
            {
                rememberedIndex = memory.FindAvailable(sizeCategory, choices.ConvertAll(art => art == null ? null : art.title));
                if (rememberedIndex >= 0) wheel.SelectIndex(rememberedIndex);
            }
            if(pickerOpen)
            {
                aimInput.Reset();
                CaptureMouseCursor();
                NativeSprayPose.Begin(game, Traverse.Create(game).Field("characterPuppet").GetValue<CharacterVisual>());
                visualPointerAngle=wheel.PointerAngle;
                for(int i=0;i<iconScales.Length;i++) iconScales[i]=i==wheel.SelectedIndex-wheel.PageStart ? 1.80f : 1f;
            }
        }

        internal static bool IsSelectionResolved(GraffitiGame game)
        {
            return ReferenceEquals(activeGame, game) && selectionResolved;
        }

        internal static bool HasSelectionFor(GraffitiGame game)
        {
            return ReferenceEquals(activeGame, game);
        }

        internal static GraffitiArt TakeSelection(GraffitiGame game)
        {
            if (!ReferenceEquals(activeGame, game) || !selectionResolved || wheel == null || aborted) return null;
            return choices[wheel.SelectedIndex];
        }

        internal static GraffitiArt TakeActiveSelection()
        {
            return activeGame == null ? null : TakeSelection(activeGame);
        }

        internal static void FinishSelection(GraffitiGame game)
        {
            if (!ReferenceEquals(activeGame, game)) return;
            RestorePickerMaps();
            activeGame = null;
            choices = null;
            previews = null;
            artNames = null;
            wheel = null;
            pickerOpen = false;
            selectionResolved = false;
            aborted = false;
            rememberedIndex = -1;
        }

        internal static bool SelectionWasAborted(GraffitiGame game)
        {
            return ReferenceEquals(activeGame, game) && aborted;
        }

        internal static void RememberPainted(GraffitiArt art)
        {
            if (art != null && memory != null) memory.RecordPainted(art.graffitiSize.ToString(), art.title);
        }

        private void OnDestroy()
        {
            RestorePickerMaps();
            NativeSprayPose.Reset();
            harmony.UnpatchSelf();
            if (hints != null) hints.Dispose();
            if (paintVisuals != null) paintVisuals.Dispose();
            if (backgroundBlur != null) { backgroundBlur.Dispose(); backgroundBlur = null; }
            if (selectorCamera != null) { selectorCamera.Dispose(); selectorCamera = null; }
            if (selectionAudio != null) { selectionAudio.Dispose(); selectionAudio = null; }
            if (ringTexture != null) Destroy(ringTexture);
            if (pointerTexture != null) Destroy(pointerTexture);
            activeGame = null;
            choices = null;
            previews = null;
            artNames = null;
            wheel = null;
            pickerOpen = false;
        }
    }
}
