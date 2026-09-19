using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Shared visual tokens for authored menus and runtime campaign UI.</summary>
    public static class TacticalUiTheme
    {
        public static readonly Color Background = new Color32(14, 15, 16, 255);
        public static readonly Color Surface = new Color32(26, 27, 28, 255);
        public static readonly Color Control = new Color32(55, 54, 52, 255);
        public static readonly Color Border = new Color32(83, 79, 74, 255);
        public static readonly Color Text = new Color32(235, 232, 218, 255);
        public static readonly Color Muted = new Color32(174, 172, 163, 255);
        public static readonly Color Accent = new Color32(220, 119, 105, 255);
        public static readonly Color Action = new Color32(135, 49, 43, 255);
        public static readonly Color Success = new Color32(157, 184, 150, 255);
        public static readonly Color Danger = new Color32(255, 111, 111, 255);
        public const int HudOrder = 0;
        public const int CampaignOrder = 20;
        public const int MenuOrder = 100;
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        public static ColorBlock ButtonColors(bool primary = false)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = primary ? Action : Control;
            colors.highlightedColor = primary ? new Color32(172, 68, 51, 255) : new Color32(86, 81, 74, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? new Color32(110, 40, 32, 255) : new Color32(65, 60, 55, 255);
            colors.disabledColor = new Color32(38, 38, 37, 255);
            colors.fadeDuration = 0.12f;
            colors.colorMultiplier = 1;
            return colors;
        }
    }
}
