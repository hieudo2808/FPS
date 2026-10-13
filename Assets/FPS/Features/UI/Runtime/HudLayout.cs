using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Shared geometry for authored and runtime HUDs. Reticles remain outside this root.</summary>
    public static class HudLayout
    {
        public const float Margin = 28f;
        public const float VitalsWidth = 350f;
        public const float CombatWidth = 420f;
        public const float SubtitleBottom = 16f;
        public const float SubtitleHeight = 128f;
        public const float InteractionBottom = 160f;
        public const float InteractionHeight = 64f;
        public const float FeedbackBottom = 236f;
        public const float FeedbackHeight = 40f;
        public static readonly Color Plate = new Color(.025f, .03f, .028f, .94f);

        public static void ConfigureCanvas(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = TacticalUiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }

        public static RectTransform PeripheralRoot(Canvas canvas)
        {
            var root = canvas.transform.Find("HudSafeArea") as RectTransform;
            if (root != null) return root;
            root = new GameObject("HudSafeArea", typeof(RectTransform), typeof(UiSafeArea)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            return root;
        }

        public static void Corner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        public static void CentralBand(RectTransform rect, Canvas canvas, float bottom, float height)
        {
            var root = PeripheralRoot(canvas);
            float width = root.rect.width;
            float left = Margin + VitalsWidth + 24f;
            float right = width - Margin - CombatWidth - 24f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2((left + right - width) * .5f, bottom);
            rect.sizeDelta = new Vector2(Mathf.Max(240f, Mathf.Min(820f, right - left)), height);
        }

        public static bool SecondaryReduced => InputManager.Instance != null
            && !InputManager.GameplayInputBlocked
            && (InputManager.Instance.GetAimInput() || InputManager.Instance.GetFireInput());

        public static bool GameplayVisible => !InGameMenuUI.IsMenuOpen && !TeamInventoryUI.IsOpen
            && !CampaignHUD.IsDocumentOpen && !CampaignStatusUI.IsDebriefOpen && !InputManager.CinematicInputBlocked;
    }
}
