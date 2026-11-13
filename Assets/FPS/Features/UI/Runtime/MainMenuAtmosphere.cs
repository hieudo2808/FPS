using UnityEngine;

namespace FPS
{
    /// <summary>Moves only the scenery; menu controls keep their authored positions.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuAtmosphere : MonoBehaviour
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform layer;
        private Vector2 backgroundOrigin, mistOrigin;
        private float startedAt;

        private void Awake()
        {
            if (background == null) background = transform.Find("Background") as RectTransform;
            if (layer == null) layer = transform.Find("Atmosphere") as RectTransform;
            if (background != null) backgroundOrigin = background.anchoredPosition;
            if (layer != null) mistOrigin = layer.anchoredPosition;
            startedAt = Time.unscaledTime;
        }

        private void Update()
        {
            float t = Time.unscaledTime - startedAt;
            if (background != null)
                background.anchoredPosition = backgroundOrigin + new Vector2(Mathf.Sin(t * .09f) * 10, Mathf.Sin(t * .061f) * 3);
            if (layer != null)
                layer.anchoredPosition = mistOrigin + new Vector2(Mathf.Sin(t * .13f) * 80, Mathf.Sin(t * .093f) * 9);
        }
    }
}
