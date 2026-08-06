using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>
    /// Keeps momentary UGUI buttons from remaining visually selected after pointer up.
    /// Tabs use their own visual state and can safely use this component as well.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonSelectionResetter : MonoBehaviour, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private bool clearSelectionOnPointerUp = true;

        public static void Attach(Button button)
        {
            if (button != null && button.GetComponent<UiButtonSelectionResetter>() == null)
                button.gameObject.AddComponent<UiButtonSelectionResetter>();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (clearSelectionOnPointerUp)
                ClearSelection();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (clearSelectionOnPointerUp)
                ClearSelection();
        }

        private void OnDisable()
        {
            ClearSelection();
        }

        private static void ClearSelection()
        {
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
