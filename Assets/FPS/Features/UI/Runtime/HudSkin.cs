using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FPS
{
    /// <summary>Selected licensed sprites shared by authored HUD and runtime inventory.</summary>
    public static class HudSkin
    {
        private static readonly Dictionary<string, Sprite> sprites = new();
        public static Color Accent => SelectedUiTheme.Current.Accent;
        public static Color PanelTint => SelectedUiTheme.SurfaceTint(SelectedUiTheme.Current.PanelAlpha);
        public const string PanelOpacityKey = "HudPanelOpacity";
        public static event Action<float> PanelOpacityChanged;
        public static float PanelOpacity => Mathf.Clamp01(PlayerPrefs.GetFloat(PanelOpacityKey, 1f));

        public static void SetPanelOpacity(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PanelOpacityKey, value);
            PlayerPrefs.Save();
            PanelOpacityChanged?.Invoke(value);
        }

        public static void SetPanelAlpha(Image image, float originalAlpha, float opacity)
        {
            if (image == null) return;
            var color = image.color;
            color.a = originalAlpha * opacity;
            image.color = color;
        }

        public static Font Font(bool bold = false) => Load<Font>(bold ? "Teko-Bold" : "Teko-Regular");
        public static TMP_FontAsset FontAsset(bool bold = false) => Load<TMP_FontAsset>(bold ? "Teko-Bold SDF" : "Teko-Regular SDF");

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>("HudSkin/Fonts/" + name);
            if (asset == null) throw new InvalidOperationException("Missing HUD font: " + name);
            return asset;
        }

        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("HudSkin/" + name);
            if (sprite == null) throw new InvalidOperationException("Missing HUD skin sprite: " + name);
            sprites[name] = sprite;
            return sprite;
        }

        public static void Apply(Image image, string name, Color? tint = null, float borderScale = 3f)
        {
            image.sprite = Get(name);
            image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = borderScale;
            image.color = tint ?? PanelTint;
        }

        public static void StyleButton(Button button, bool selected = false)
        {
            var theme = SelectedUiTheme.Current;
            Apply((Image)button.targetGraphic, selected ? "ButtonSelected" : "Button",
                selected ? theme.Text : theme.Muted, 1.5f);
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = theme.Text;
            colors.highlightedColor = colors.selectedColor = theme.Text;
            colors.pressedColor = colors.disabledColor = theme.Muted;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }
    }
}
