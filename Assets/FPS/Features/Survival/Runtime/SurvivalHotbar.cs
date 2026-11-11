using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Event-driven compact quick-use HUD. Bind is safe to call repeatedly.</summary>
    public sealed class SurvivalHotbar : MonoBehaviour
    {
        private static SelectedUiTheme Theme => SelectedUiTheme.Current;
        private readonly Image[] icons = new Image[4], selection = new Image[4], rings = new Image[4], empty = new Image[4], shades = new Image[4];
        private readonly TMP_Text[] counts = new TMP_Text[4], keys = new TMP_Text[4];
        private TMP_Text prompt, cycleHint;
        private SurvivalInventory inventory;
        private InputManager input;
        private Graphic[] graphics;
        private bool dirty = true, wasUsing;
        private ConsumableKind lastConsumable;
        private int lastThrowable = -1;
        private float detailUntil;
        private static readonly string[] Names = { "FRAG", "FIRE", "MEDKIT", "ANTIDOTE" };

        public void Bind(SurvivalInventory value)
        {
            EnsureCreated();
            if (inventory == value) return;
            if (inventory != null) inventory.InventoryChanged -= MarkDirty;
            inventory = value;
            foreach (var ring in rings) ring.fillAmount = 0;
            if (inventory != null) inventory.InventoryChanged += MarkDirty;
            dirty = true;
            lastThrowable = -1;
            RefreshPresentation();
        }

        private void MarkDirty() => dirty = true;
        private void OnEnable() { HudSkin.PanelOpacityChanged += ApplyPanelOpacity; ApplyPanelOpacity(HudSkin.PanelOpacity); }
        private void OnDisable() => HudSkin.PanelOpacityChanged -= ApplyPanelOpacity;
        private void ApplyPanelOpacity(float opacity)
        {
            foreach (var shade in shades) HudSkin.SetPanelAlpha(shade, 1f, opacity);
        }
        private void OnDestroy()
        {
            if (inventory != null) inventory.InventoryChanged -= MarkDirty;
            if (input != null) input.PromptBindingsChanged -= MarkDirty;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private static TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 position, int fontSize)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Theme.BodyFont; text.fontSize = Theme.Size(fontSize); text.alignment = TextAlignmentOptions.Center;
            text.color = Theme.Text; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        private static Image Picture(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }
        private void Awake() => EnsureCreated();
        // Kept for existing authored preview callers. Geometry now follows the shared canvas scaler.
        public void AdaptToCanvasScale(float canvasScale) { }

        private void EnsureCreated()
        {
            if (icons[0] != null) return;
            var catalog = SurvivalCatalog.Load();
            var root = (RectTransform)transform;
            HudLayout.Corner(root, new Vector2(1, 0), new Vector2(-56, 86), new Vector2(420, 80));
            string[] artwork = { "Frag", "Fire", "Medkit", "Antidote" };
            for (int i = 0; i < 4; i++)
            {
                var slot = Rect(Names[i], root, new Vector2(80, 52), new Vector2(-158 + i * 100, 0));
                shades[i] = Picture("Shade", slot, new Vector2(80, 52), Vector2.zero, Color.white);
                HudSkin.Apply(shades[i], "Slot", Color.white);
                selection[i] = Picture("Selected", slot, new Vector2(80, 52), Vector2.zero, Theme.Accent);
                SelectedUiTheme.Surface(selection[i], Theme.Module); selection[i].color = Theme.Text;
                selection[i].fillCenter = false;
                icons[i] = Picture("Icon", slot, new Vector2(32, 32), new Vector2(-16, 0), Color.white);
                icons[i].sprite = HudSkin.Get("Selected/" + artwork[i]);
                icons[i].preserveAspect = true;
                counts[i] = Label("Count", slot, new Vector2(38, 28), new Vector2(18, 0), 18);
                counts[i].fontStyle = FontStyles.Bold;
                counts[i].gameObject.AddComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
                keys[i] = Label("Key", slot, new Vector2(80, 34), new Vector2(0, 40), 18);
                keys[i].alignment = i < 2 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
                keys[i].gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
                empty[i] = Picture("Unavailable", slot, new Vector2(48, 6), new Vector2(-5, -3), Theme.Muted);
                HudSkin.Apply(empty[i], "Unavailable", Theme.Muted);
                empty[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                rings[i] = Picture("Progress", slot, new Vector2(44, 44), new Vector2(-16, 0), Theme.Accent);
                rings[i].sprite = i >= 2 ? HudSkin.Get("FPSSample/GoalProgress") : catalog?.progressRing;
                rings[i].type = Image.Type.Filled;
                rings[i].fillMethod = Image.FillMethod.Radial360; rings[i].fillOrigin = (int)Image.Origin360.Top;
                rings[i].fillClockwise = true;
                rings[i].fillAmount = 0;
                rings[i].transform.SetSiblingIndex(counts[i].transform.GetSiblingIndex());
            }
            cycleHint = Label("SelectionDetail", root, new Vector2(420, 34), new Vector2(-114, 40), 18);
            prompt = Label("MedicalAction", root, new Vector2(420, 34), new Vector2(0, 80), 24);
            prompt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            cycleHint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            prompt.text = cycleHint.text = "";
            graphics = GetComponentsInChildren<Graphic>(true);
            ApplyPanelOpacity(HudSkin.PanelOpacity);
        }

        private void Update()
        {
            if (input != InputManager.Instance)
            {
                if (input != null) input.PromptBindingsChanged -= MarkDirty;
                input = InputManager.Instance;
                if (input != null) input.PromptBindingsChanged += MarkDirty;
                dirty = true;
            }
            foreach (var graphic in graphics)
                graphic.enabled = HudLayout.GameplayVisible && inventory != null;
            if (!HudLayout.GameplayVisible || inventory == null) return;
            bool usingItem = inventory.IsUsingItem;
            if (usingItem != wasUsing || inventory.ActiveConsumable != lastConsumable)
            { wasUsing = usingItem; lastConsumable = inventory.ActiveConsumable; dirty = true; }
            if (dirty) RefreshPresentation();
            int active = inventory.ActiveConsumable == ConsumableKind.Medkit ? 2 : 3;
            for (int i = 2; i < 4; i++) rings[i].fillAmount = usingItem && i == active ? inventory.UseProgress : 0;
            cycleHint.enabled = !HudLayout.SecondaryReduced && !usingItem;
            prompt.enabled = usingItem && !HudLayout.SecondaryReduced;
        }

        private void RefreshPresentation()
        {
            if (inventory == null) return;
            dirty = false;
            bool usingItem = inventory.IsUsingItem;
            int active = inventory.ActiveConsumable == ConsumableKind.Medkit ? 2 : 3;
            int throwable = (int)inventory.SelectedThrowable.Value;
            if (lastThrowable >= 0 && lastThrowable != throwable) detailUntil = Time.unscaledTime + 2f;
            lastThrowable = throwable;
            for (int i = 0; i < 4; i++)
            {
                int count = inventory.Count((PickupType)((int)PickupType.FragGrenade + i));
                counts[i].SetText("x{0}", count);
                icons[i].color = count > 0 ? Theme.Text : Theme.Muted;
                empty[i].gameObject.SetActive(count == 0);
                bool selected = i < 2 ? throwable == i : usingItem && active == i;
                selection[i].gameObject.SetActive(selected);
                keys[i].text = i < 2 ? (selected ? Key("Grenade", "G") : "") : Key(i == 2 ? "Medkit" : "Antidote", i == 2 ? "4" : "5");
            }
            cycleHint.text = "[" + Key("CycleGrenade", "3") + "] CYCLE";
            cycleHint.rectTransform.sizeDelta = new Vector2(126, 34);
            prompt.text = usingItem ? (inventory.IsSelfUse ? "SELF: " : "ASSIST: ") + Names[active] : "";
        }
        public static string Key(string action, string fallback) => InputManager.Instance != null ? InputManager.Instance.GetPromptDisplayName(action) : fallback;
    }
}
