using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Event-driven compact quick-use HUD. Bind is safe to call repeatedly.</summary>
    public sealed class SurvivalHotbar : MonoBehaviour
    {
        private readonly Image[] icons = new Image[4], selection = new Image[4], rings = new Image[4], empty = new Image[4];
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
            text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center;
            text.color = TacticalUiTheme.Text; text.raycastTarget = false;
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
            HudLayout.Corner(root, new Vector2(1, 0), new Vector2(-HudLayout.Margin, 216), new Vector2(420, 104));
            string[] artwork = { "Frag", "Fire", "Medkit", "Antidote" };
            for (int i = 0; i < 4; i++)
            {
                var slot = Rect(Names[i], root, new Vector2(98, 100), new Vector2(-159 + i * 106, 0));
                HudSkin.Apply(Picture("Shade", slot, new Vector2(98, 100), Vector2.zero, Color.white), "Slot", Color.white);
                selection[i] = Picture("Selected", slot, new Vector2(98, 100), Vector2.zero, HudSkin.Accent);
                HudSkin.Apply(selection[i], "SlotSelected", HudSkin.Accent);
                selection[i].fillCenter = false;
                icons[i] = Picture("Icon", slot, new Vector2(56, 56), new Vector2(-5, -3), Color.white);
                icons[i].sprite = HudSkin.Get(artwork[i]);
                icons[i].preserveAspect = true;
                counts[i] = Label("Count", slot, new Vector2(32, 30), new Vector2(21, -23), 26);
                counts[i].fontStyle = FontStyles.Bold;
                counts[i].gameObject.AddComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
                keys[i] = Label("Key", slot, new Vector2(32, 28), new Vector2(21, 24), 24);
                keys[i].alignment = TextAlignmentOptions.TopRight;
                keys[i].gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
                empty[i] = Picture("Unavailable", slot, new Vector2(48, 6), new Vector2(-5, -3), TacticalUiTheme.Muted);
                HudSkin.Apply(empty[i], "Unavailable", TacticalUiTheme.Muted);
                empty[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                rings[i] = Picture("Progress", slot, new Vector2(64, 64), new Vector2(-5, -3), HudSkin.Accent);
                rings[i].sprite = i >= 2 ? HudSkin.Get("FPSSample/GoalProgress") : catalog?.progressRing;
                rings[i].type = Image.Type.Filled;
                rings[i].fillMethod = Image.FillMethod.Radial360; rings[i].fillOrigin = (int)Image.Origin360.Top;
                rings[i].fillClockwise = true;
                rings[i].fillAmount = 0;
                rings[i].transform.SetSiblingIndex(counts[i].transform.GetSiblingIndex());
            }
            cycleHint = Label("SelectionDetail", root, new Vector2(420, 34), new Vector2(0, 77), 24);
            prompt = Label("MedicalAction", root, new Vector2(420, 34), new Vector2(0, 113), 24);
            prompt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            cycleHint.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            prompt.text = cycleHint.text = "";
            graphics = GetComponentsInChildren<Graphic>(true);
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
            cycleHint.enabled = !HudLayout.SecondaryReduced && !usingItem && Time.unscaledTime < detailUntil;
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
                counts[i].SetText("{0}", count);
                icons[i].color = count > 0 ? Color.white : TacticalUiTheme.Muted;
                empty[i].gameObject.SetActive(count == 0);
                bool selected = i < 2 ? throwable == i : usingItem && active == i;
                selection[i].gameObject.SetActive(selected);
                keys[i].text = i < 2 ? (selected ? Key("Grenade", "G") : "") : Key(i == 2 ? "Medkit" : "Antidote", i == 2 ? "4" : "5");
            }
            cycleHint.text = Names[Mathf.Clamp(throwable, 0, 1)] + "  [" + Key("CycleGrenade", "3") + "] CYCLE";
            prompt.text = usingItem ? (inventory.IsSelfUse ? "SELF: " : "ASSIST: ") + Names[active] : "";
        }
        public static string Key(string action, string fallback) => InputManager.Instance != null ? InputManager.Instance.GetPromptDisplayName(action) : fallback;
    }
}
