using HarmonyLib;
using Reptile;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuickPickGraffiti
{
    internal sealed class GameButtonHints
    {
        private readonly GameObject root;
        private readonly UIManager ui;
        private readonly TextMeshProUGUI[] glyphs = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[4];
        private readonly Image[] panels = new Image[2];
        private readonly int[] actions = { TRInputId.menuConfirm, TRInputId.menuCancel, TRInputId.menuCycleLeft, TRInputId.menuCycleRight };
        private readonly string[] textLabels = { "Spray", "Never mind", "Previous page", "Next page" };
        private float nextRefresh;
        internal Font GuiFont { get; private set; }

        internal GameButtonHints(UIManager manager)
        {
            ui = manager;
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            DanceAbilityUI dance = Traverse.Create(manager).Field("danceAbilityUI").GetValue<DanceAbilityUI>();
            if (dance != null)
            {
                TextMeshProUGUI source = Traverse.Create(dance).Field("optionText").GetValue<TextMeshProUGUI>();
                if (source != null && source.font != null) font = source.font;
            }
            if (font == null)
                foreach (TextMeshProUGUI text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                    if (text.font != null) { font = text.font; break; }
            if (font != null) GuiFont = font.sourceFontFile;

            root = new GameObject("QuickPickGraffitiButtonHints", typeof(RectTransform), typeof(Canvas));
            root.hideFlags = HideFlags.HideAndDontSave;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            for (int i = 0; i < panels.Length; i++)
            {
                var panel = new GameObject("HelperBacking", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(root.transform, false);
                panels[i] = panel.GetComponent<Image>();
                panels[i].color = new Color(0.03f, 0.04f, 0.025f, 0.93f);
                panels[i].raycastTarget = false;
            }
            for (int i = 0; i < glyphs.Length; i++)
            {
                glyphs[i] = MakeText("BoundButton", font);
                labels[i] = MakeText("ActionLabel", font);
            }
            root.SetActive(false);
        }

        private TextMeshProUGUI MakeText(string name, TMP_FontAsset font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(root.transform, false);
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.color = Color.white;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            return text;
        }

        private static void Place(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, Screen.height - top);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal void Refresh(GameInput input, bool hasLast)
        {
            root.SetActive(true);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.10f;
            float radius = Mathf.Min(Screen.height * 0.40f, Screen.width * 0.30f);
            float top = Screen.height * 0.46f + radius + Screen.height * 0.012f;
            float row = Screen.height * 0.032f;
            float left = Screen.width * 0.5f - radius * 0.78f;
            float colWidth = radius * 0.83f;
            for (int i = 0; i < 2; i++)
                Place(panels[i].rectTransform, left + i * colWidth - row * 0.2f, top - row * 0.10f, colWidth - row * 0.15f, row * 2.15f);
            textLabels[1] = hasLast ? "Use last" : "Never mind";
            for (int i = 0; i < actions.Length; i++)
            {
                int column = i / 2, line = i % 2;
                float x = left + column * colWidth, y = top + line * row;
                glyphs[i].fontSize = row * 0.88f;
                labels[i].fontSize = row * 0.80f;
                glyphs[i].fontSizeMin = row * 0.42f;
                glyphs[i].fontSizeMax = row * 0.88f;
                labels[i].fontSizeMin = row * 0.52f;
                labels[i].fontSizeMax = row * 0.80f;
                Place(glyphs[i].rectTransform, x, y, row * 1.6f, row);
                Place(labels[i].rectTransform, x + row * 1.55f, y, colWidth - row * 1.75f, row);
                ui.ShowTextMeshProCurrentControllerActionButtonGlyph(glyphs[i], actions[i]);
                labels[i].text = textLabels[i];
                if (string.IsNullOrEmpty(glyphs[i].text))
                {
                    ActionElementMap mapping = input.RewiredMappingHandler.GetCurrentControllerActionElementMap(actions[i], input.GetCurrentControllerType(0), 0);
                    glyphs[i].text = mapping == null ? "—" : mapping.elementIdentifierName;
                    glyphs[i].fontSize = row * 0.50f;
                }
            }
        }

        internal void Warmup(GameInput input)
        {
            // Generate glyph meshes while hidden, before the first graffiti encounter.
            try
            {
                Refresh(input, false);
                foreach(var text in glyphs) text.ForceMeshUpdate(true, true);
                foreach(var text in labels) text.ForceMeshUpdate(true, true);
                if(GuiFont != null && GuiFont.dynamic)
                {
                    const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /-!?.,'";
                    float radius=Mathf.Min(Screen.height*.40f,Screen.width*.30f);
                    GuiFont.RequestCharactersInTexture(alphabet,Mathf.Max(13,Mathf.RoundToInt(radius*.040f)),FontStyle.Normal);
                    GuiFont.RequestCharactersInTexture(alphabet,Mathf.Max(17,Mathf.RoundToInt(radius*.054f)),FontStyle.Bold);
                }
            }
            finally { Hide(); }
        }

        internal void Hide() { if (root != null) root.SetActive(false); }
        internal void Dispose() { if (root != null) Object.Destroy(root); }
    }
}
