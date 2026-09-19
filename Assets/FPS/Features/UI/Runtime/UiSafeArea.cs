using UnityEngine;

namespace FPS
{
    /// <summary>Insets a full-screen content root without moving the aiming reticle.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeArea : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastSize;
        private RectTransform rectTransform;

        private void OnEnable() { rectTransform = (RectTransform)transform; Apply(); }
        private void Update()
        {
            if (Screen.safeArea != lastArea || lastSize.x != Screen.width || lastSize.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0 || rectTransform == null) return;
            lastArea = Screen.safeArea;
            lastSize = new Vector2Int(Screen.width, Screen.height);
            rectTransform.anchorMin = new Vector2(lastArea.xMin / Screen.width, lastArea.yMin / Screen.height);
            rectTransform.anchorMax = new Vector2(lastArea.xMax / Screen.width, lastArea.yMax / Screen.height);
            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
        }
    }
}
