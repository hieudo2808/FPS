using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Selected licensed sprites shared by authored HUD and runtime inventory.</summary>
    public static class HudSkin
    {
        private static readonly Dictionary<string, Sprite> sprites = new();
        public static readonly Color Accent = new Color32(230, 190, 117, 255);
        public static readonly Color PanelTint = new Color(.66f, .70f, .68f, .98f);

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
            Apply((Image)button.targetGraphic, selected ? "ButtonSelected" : "Button",
                selected ? new Color(.46f, .39f, .27f) : new Color(.78f, .81f, .79f), 1.5f);
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1f, .94f, .79f);
            colors.pressedColor = new Color(.65f, .65f, .65f);
            colors.disabledColor = new Color(.50f, .50f, .50f);
            colors.fadeDuration = .12f;
            button.colors = colors;
        }
    }
}
