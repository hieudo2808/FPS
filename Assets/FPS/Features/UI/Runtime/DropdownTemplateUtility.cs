using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public static class DropdownTemplateUtility
    {
        public static void Normalize(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.template == null) return;

            RectTransform template = dropdown.template;
            Toggle itemToggle = dropdown.itemText != null
                ? dropdown.itemText.GetComponentInParent<Toggle>(true)
                : template.GetComponentInChildren<Toggle>(true);

            if (itemToggle == null) return;

            RectTransform itemRect = itemToggle.transform as RectTransform;
            RectTransform contentRect = itemRect != null ? itemRect.parent as RectTransform : null;
            if (itemRect == null || contentRect == null) return;

            DisableGeneratedLayout(contentRect);
            DisableGeneratedLayout(itemRect);

            RectTransform viewportRect = contentRect.parent as RectTransform;
            if (viewportRect != null)
            {
                Image viewportImage = viewportRect.GetComponent<Image>();
                if (viewportImage == null)
                {
                    viewportImage = viewportRect.gameObject.AddComponent<Image>();
                }

                viewportImage.color = Color.white;
                viewportImage.raycastTarget = false;

                Mask mask = viewportRect.GetComponent<Mask>();
                if (mask == null)
                {
                    mask = viewportRect.gameObject.AddComponent<Mask>();
                }

                mask.showMaskGraphic = false;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.pivot = new Vector2(0, 1);
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
            }

            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 32);

            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0, 32);

            if (dropdown.itemText != null)
            {
                RectTransform itemLabelRect = dropdown.itemText.rectTransform;
                itemLabelRect.anchorMin = Vector2.zero;
                itemLabelRect.anchorMax = Vector2.one;
                itemLabelRect.offsetMin = new Vector2(30, 2);
                itemLabelRect.offsetMax = new Vector2(-12, -2);
                dropdown.itemText.color = Color.white;
                dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
                dropdown.itemText.raycastTarget = false;
            }

            if (itemToggle.graphic != null)
            {
                RectTransform checkmarkRect = itemToggle.graphic.rectTransform;
                checkmarkRect.anchorMin = new Vector2(0, 0.5f);
                checkmarkRect.anchorMax = new Vector2(0, 0.5f);
                checkmarkRect.pivot = new Vector2(0, 0.5f);
                checkmarkRect.anchoredPosition = new Vector2(12, 0);
                checkmarkRect.sizeDelta = new Vector2(6, 22);
            }

            dropdown.RefreshShownValue();
        }

        private static void DisableGeneratedLayout(RectTransform rectTransform)
        {
            LayoutGroup layoutGroup = rectTransform.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.enabled = false;
            }

            LayoutElement layoutElement = rectTransform.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = true;
            }
        }
    }
}
