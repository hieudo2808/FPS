using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    // Small labels use the body token when the canvas would otherwise render them below 16 px.
    public sealed class SelectedUiTypography : MonoBehaviour
    {
        private readonly Dictionary<Graphic, float> nominal = new();
        private readonly Dictionary<TMP_Text, (Vector2 Extra, Vector2 Size, Vector2 Position)> adjusted = new();
        private float lastScale;
        private bool dirty = true;
        public void Refresh() => dirty = true;

        private void LateUpdate()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.scaleFactor <= 0 || (!dirty && Mathf.Approximately(lastScale, canvas.scaleFactor))) return;
            dirty = false; lastScale = canvas.scaleFactor;
            foreach (var removed in nominal.Keys.Where(key => key == null).ToArray()) nominal.Remove(removed);
            foreach (var removed in adjusted.Keys.Where(key => key == null).ToArray()) adjusted.Remove(removed);
            var theme = SelectedUiTheme.Current;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                float size = graphic is TMP_Text tmp ? tmp.fontSize : graphic is Text legacy ? legacy.fontSize : 0;
                if (size == 0) continue;
                if (!nominal.TryGetValue(graphic, out float original)) nominal[graphic] = original = size;
                float target = original * lastScale >= TacticalUiTheme.MinimumReadableTextPixels ? original : theme.Body;
                if (graphic is TMP_Text label)
                {
                    if (adjusted.TryGetValue(label, out var previous) && label.rectTransform.sizeDelta == previous.Size
                        && label.rectTransform.anchoredPosition == previous.Position)
                    {
                        label.rectTransform.sizeDelta -= previous.Extra;
                        label.rectTransform.anchoredPosition += new Vector2(0, previous.Extra.y * (1 - label.rectTransform.pivot.y));
                    }
                    label.fontSize = target;
                    if (label.GetComponent<ContentSizeFitter>() == null)
                    {
                        var rect = label.rectTransform;
                        var preferred = label.GetPreferredValues(label.text, label.textWrappingMode == TextWrappingModes.NoWrap ? float.PositiveInfinity : rect.rect.width, float.PositiveInfinity);
                        var extra = new Vector2(label.textWrappingMode == TextWrappingModes.NoWrap ? Mathf.Max(0, preferred.x + 2 - rect.rect.width) : 0,
                            Mathf.Max(0, preferred.y + 2 - rect.rect.height));
                        rect.sizeDelta += extra;
                        rect.anchoredPosition -= new Vector2(0, extra.y * (1 - rect.pivot.y));
                        adjusted[label] = (extra, rect.sizeDelta, rect.anchoredPosition);
                    }
                }
                else ((Text)graphic).fontSize = Mathf.RoundToInt(target);
            }
        }
    }
}
