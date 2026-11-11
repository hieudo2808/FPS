using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    // Visual tokens for the nine approved campaign screens, independent of the menu skin.
    public sealed class SelectedUiTheme : ScriptableObject
    {
        private static SelectedUiTheme current;
        public static SelectedUiTheme Current => current != null ? current : current = Resources.Load<SelectedUiTheme>("HudSkin/SelectedTheme");
        public Color Text = new Color32(243, 243, 243, 255);
        public Color Muted = new Color32(174, 178, 178, 255);
        public Color Accent = new Color32(233, 166, 76, 255);
        public Color Danger = new Color32(216, 75, 75, 255);
        public Color Background => Color.Lerp(Color.black, Muted, .13f);
        public Color Border => Color.Lerp(Color.black, Muted, .58f);
        public Color Paper => Color.Lerp(Muted, Text, .6f);
        public Color DeviceSurface => Color.Lerp(Color.black, Muted, .21f);
        public Color Plate => WithAlpha(Background, .94f);
        // CSS mockups blend in sRGB; the project renders UI in linear space.
        public Color CampaignBackdrop => WithAlpha(Background, 1 - Mathf.GammaToLinearSpace(1 - .39f));
        public Color DeviceBackdrop => WithAlpha(Background, 1 - Mathf.GammaToLinearSpace(1 - .83f));
        public Color Selection => WithAlpha(Text, Mathf.GammaToLinearSpace(.10f));
        public Color SelectionFocus => WithAlpha(Text, Mathf.GammaToLinearSpace(.16f));
        public Color SelectionPressed => WithAlpha(Text, Mathf.GammaToLinearSpace(.24f));
        public float Small = 18, Body = 24, Title = 36;
        public float PanelAlpha = .99f;
        public Font LegacyBody;
        public TMP_FontAsset BodyFont, NumericFont;
        public Sprite Panel, Tracker, Vitals, Loadout, ControlBezel, ManifestBezel, Module, Screen, ModuleHeader, Sidebar, PaperArea, PaperSurface, NoticeSurface, HoldSurface, LockedSurface;
        public Sprite WardenIcon;
        public Sprite Divider, Keycap, ReturnKey, DeviceHeader, DeviceFooter;

        public float Size(float requested) => requested <= 18 ? Small : requested <= 28 ? Body : Title;

        public static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        public static Color SurfaceTint(float alpha = 1) => WithAlpha(Color.white, alpha);

        public static void Surface(Image image, Sprite sprite, float alpha = 1)
        {
            image.sprite = sprite; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1; image.color = SurfaceTint(alpha);
            image.raycastTarget = false;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height, float parentWidth, float parentHeight)
        {
            rect.anchorMin = new Vector2(x / parentWidth, 1 - (y + height) / parentHeight);
            rect.anchorMax = new Vector2((x + width) / parentWidth, 1 - y / parentHeight);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
