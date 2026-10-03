using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public sealed class SurvivalHotbar : MonoBehaviour
    {
        private readonly Image[] icons = new Image[4];
        private readonly Image[] selection = new Image[4];
        private readonly TMP_Text[] counts = new TMP_Text[4];
        private readonly TMP_Text[] keys = new TMP_Text[4];
        private readonly Image[] rings = new Image[4];
        private TMP_Text prompt;
        private TMP_Text cycleHint;
        private SurvivalInventory inventory;
        private float nextRefresh;
        private Canvas canvas;
        private static readonly string[] Names = { "FRAG", "FIRE", "MEDKIT", "ANTIDOTE" };
        public void Bind(SurvivalInventory value)
        {
            EnsureCreated();
            inventory = value;
            nextRefresh = 0;
            RefreshPresentation();
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
        private static TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 position, int fontSize)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = TacticalUiTheme.Text;
            text.raycastTarget = false;
            return text;
        }
        private static Image Picture(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private void Awake() => EnsureCreated();
        public void AdaptToCanvasScale(float canvasScale)
        {
            // Preserve legible item icons and key labels at 720p and 4:3 resolutions.
            float multiplier = Mathf.Max(1f, 320f / (368f * Mathf.Max(.1f, canvasScale)));
            transform.localScale = Vector3.one * multiplier;
            ((RectTransform)transform).anchoredPosition = new Vector2(0, 38f * multiplier);
        }
        private void EnsureCreated()
        {
            if (icons[0] != null) return;
            var catalog = SurvivalCatalog.Load();
            canvas = GetComponentInParent<Canvas>();
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, 0f);
            root.pivot = new Vector2(.5f, 0f);
            root.sizeDelta = new Vector2(368, 72);
            root.anchoredPosition = new Vector2(0, 38);
            for (int i = 0; i < 4; i++)
            {
                var slot = Rect(Names[i], root, new Vector2(84, 72), new Vector2(-138 + i * 92, 0));
                Picture("Shade", slot, new Vector2(84, 72), Vector2.zero, new Color(.025f, .031f, .028f, .85f));
                selection[i] = Picture("Selected", slot, new Vector2(84, 3), new Vector2(0, -34), TacticalUiTheme.Accent);
                icons[i] = Picture("Icon", slot, new Vector2(42, 42), new Vector2(-9, 0), Color.white);
                icons[i].sprite = catalog != null && catalog.icons.Length > i ? catalog.icons[i] : null;
                icons[i].preserveAspect = true;
                counts[i] = Label("Count", slot, new Vector2(26, 30), new Vector2(26, 0), 21);
                counts[i].fontStyle = FontStyles.Bold;
                keys[i] = Label("Key", slot, new Vector2(58, 18), new Vector2(0, 28), 14);
                Label("Name", slot, new Vector2(82, 18), new Vector2(0, -25), 13).text = Names[i];
                rings[i] = Picture("Progress", slot, new Vector2(50, 50), new Vector2(-9, 0), TacticalUiTheme.Accent);
                rings[i].sprite = catalog?.progressRing;
                rings[i].type = Image.Type.Filled;
                rings[i].fillMethod = Image.FillMethod.Radial360;
                rings[i].fillOrigin = (int)Image.Origin360.Top;
                rings[i].fillAmount = 0;
            }
            prompt = Label("MedicalAction", root, new Vector2(460, 24), new Vector2(0, 64), 15);
            cycleHint = Label("GrenadeSelectionHint", root, new Vector2(200, 18), new Vector2(-90, -48), 13);
        }
        private void Update()
        {
            if (canvas != null) AdaptToCanvasScale(canvas.scaleFactor);
            if (inventory == null || !inventory.IsSpawned) return;
            RefreshPresentation();
        }
        private void RefreshPresentation()
        {
            if (inventory == null) return;
            bool usingItem = inventory.IsUsingItem;
            int active = inventory.ActiveConsumable == ConsumableKind.Medkit ? 2 : 3;
            for (int i = 2; i < 4; i++) rings[i].fillAmount = usingItem && i == active ? inventory.UseProgress : 0;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            for (int i = 0; i < 4; i++)
            {
                int count = inventory.Count((PickupType)((int)PickupType.FragGrenade + i));
                counts[i].SetText("{0}", count);
                icons[i].color = count > 0 ? Color.white : new Color(.5f, .5f, .5f, .35f);
                bool selected = i < 2 ? (int)inventory.SelectedThrowable.Value == i : usingItem && active == i;
                selection[i].enabled = selected;
                keys[i].text = i < 2 ? (selected ? Key("Grenade", "G") : "") : Key(i == 2 ? "Medkit" : "Antidote", i == 2 ? "4" : "5");
            }
            cycleHint.text = "[" + Key("CycleGrenade", "3") + "] SWITCH GRENADE";
            prompt.text = usingItem ? (inventory.IsSelfUse ? "SELF · " : "ASSIST · ") + (active == 2 ? "APPLYING MEDKIT" : "ADMINISTERING ANTIDOTE") : "";
        }
        public static string Key(string action, string fallback) => InputManager.Instance != null ? InputManager.Instance.GetBindingDisplayName(action) : fallback;
    }
}
