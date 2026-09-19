using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Lightweight text menu feedback shared by pointer and keyboard navigation.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class SurvivalMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image marker;
        [SerializeField] private Image background;
        [SerializeField] private bool primary;
        private Button button;
        private bool hovered, selected;
        private float focus;
        private float restingX;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (label != null) restingX = label.rectTransform.anchoredPosition.x;
        }

        private void OnEnable() { hovered = selected = false; focus = 0; Apply(); }
        private void OnDisable() { hovered = selected = false; focus = 0; Apply(); }
        private void Update()
        {
            float target = (hovered || selected) && button != null && button.IsInteractable() ? 1 : 0;
            focus = Mathf.MoveTowards(focus, target, Time.unscaledDeltaTime / .12f);
            Apply();
        }

        private void Apply()
        {
            if (label == null || marker == null || background == null) return;
            bool enabled = button == null || button.IsInteractable();
            label.color = enabled ? Color.Lerp(primary ? TacticalUiTheme.Accent : TacticalUiTheme.Text,
                Color.white, focus) : TacticalUiTheme.Muted;
            var position = label.rectTransform.anchoredPosition;
            position.x = restingX + 8 * focus;
            label.rectTransform.anchoredPosition = position;
            var color = TacticalUiTheme.Accent;
            color.a = enabled ? Mathf.Lerp(primary ? .65f : 0, 1, focus) : 0;
            marker.color = color;
            background.color = new Color(.55f, .32f, .28f,
                enabled ? Mathf.Lerp(primary ? .2f : 0, .72f, focus) : 0);
        }

        public void OnPointerEnter(PointerEventData _) => hovered = true;
        public void OnPointerExit(PointerEventData _) => hovered = false;
        public void OnSelect(BaseEventData _) => selected = true;
        public void OnDeselect(BaseEventData _) => selected = false;
    }
}
