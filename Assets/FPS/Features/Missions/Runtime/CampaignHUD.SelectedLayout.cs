using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public sealed partial class CampaignHUD
    {
        private GameObject deviceHeader, deviceFooterPlate, manifestArea;

        private void BuildSelectedDeviceDetails(RectTransform surface)
        {
            deviceHeader = Box(surface, "DeviceHeaderPlate", Vector2.zero, Vector2.one).gameObject;
            SelectedUiTheme.Surface(deviceHeader.GetComponent<Image>(), Theme.DeviceHeader);
            deviceHeader.transform.SetAsFirstSibling();
            deviceFooterPlate = Box(surface, "DeviceFooterPlate", Vector2.zero, Vector2.one).gameObject;
            SelectedUiTheme.Surface(deviceFooterPlate.GetComponent<Image>(), Theme.DeviceFooter);
            deviceFooterPlate.transform.SetSiblingIndex(1);
            manifestArea = Box(surface, "ManifestPaperArea", Vector2.zero, Vector2.one).gameObject;
            SelectedUiTheme.Surface(manifestArea.GetComponent<Image>(), Theme.PaperArea);
            manifestArea.transform.SetSiblingIndex(2);
            var overview = DeviceText(manifestSidebar.transform, "RecordsOverview", Vector2.zero, Vector2.one, 24, FontStyles.Normal);
            overview.text = "Overview"; overview.color = Theme.Muted;
            var archived = DeviceText(manifestSidebar.transform, "RecordsArchive", Vector2.zero, Vector2.one, 24, FontStyles.Normal);
            archived.text = "Archived files"; archived.color = Theme.Muted;
            var selected = Box(manifestSidebar.transform, "RecordsSelection", Vector2.zero, Vector2.one);
            SelectedUiTheme.Surface(selected.GetComponent<Image>(), Theme.PaperSurface);
            selected.SetAsFirstSibling();
            var copy = DeviceText(manifestPage.transform, "ManifestCopy", Vector2.zero, Vector2.one, 18, FontStyles.Normal);
            copy.text = "DISPATCH COPY / FACTORY"; copy.color = Theme.Background;
            var record = DeviceText(manifestPage.transform, "ManifestRecord", Vector2.zero, Vector2.one, 18, FontStyles.Normal);
            record.text = "RECORD"; record.color = Theme.Background; record.alignment = TextAlignmentOptions.TopRight;
            var paperDivider = Box(manifestPage.transform, "PaperDivider", Vector2.zero, Vector2.one).GetComponent<Image>();
            paperDivider.sprite = Theme.Divider; paperDivider.color = Theme.Background;
            var brand = DeviceText(panel, "DeviceBrand", Vector2.zero, Vector2.one, 18, FontStyles.Normal);
            brand.color = Theme.Muted;
            var id = DeviceText(panel, "DeviceIdentity", Vector2.zero, Vector2.one, 18, FontStyles.Normal);
            id.color = Theme.Muted; id.alignment = TextAlignmentOptions.TopRight;
            foreach (var card in new[] { deviceInstructionCard, deviceControlCard })
            {
                SelectedUiTheme.Surface(card.GetComponent<Image>(), Theme.Module);
                var header = Box(card.transform, "ModuleHeaderPlate", Vector2.zero, Vector2.one);
                SelectedUiTheme.Surface(header.GetComponent<Image>(), Theme.ModuleHeader); header.SetAsFirstSibling();
            }
            SelectedUiTheme.Surface(deviceNotice.GetComponent<Image>(), Theme.NoticeSurface);
            var noticeLine = Box(deviceNotice.transform, "NoticeLine", Vector2.zero, new Vector2(.006f, 1)).GetComponent<Image>();
            noticeLine.sprite = HudSkin.Get("Fill"); noticeLine.color = Theme.Muted;
        }

        private void LayoutSelectedDevice(bool manifest, bool expandedSource)
        {
            float pw = manifest ? 1448 : 1386, ph = manifest ? 880 : 930;
            SelectedUiTheme.Place(panel, manifest ? 236 : 267, manifest ? 76 : 63, pw, ph, 1920, 1080);
            float parentWidth = ((RectTransform)panel.parent).rect.width;
            float fittedWidth = Mathf.Min(pw, parentWidth - 48);
            panel.anchorMin = new Vector2(.5f - fittedWidth / parentWidth / 2, panel.anchorMin.y);
            panel.anchorMax = new Vector2(.5f + fittedWidth / parentWidth / 2, panel.anchorMax.y);
            SelectedUiTheme.Surface(panel.GetComponent<Image>(), manifest ? Theme.ManifestBezel : Theme.ControlBezel);
            float sw = manifest ? 1320 : 1250, sh = manifest ? 748 : 778;
            var surface = (RectTransform)deviceSurface.transform;
            SelectedUiTheme.Place(surface, manifest ? 64 : 68, manifest ? 54 : 62, sw, sh, pw, ph);
            SelectedUiTheme.Surface(surface.GetComponent<Image>(), Theme.Screen);
            void Put(Component component, float x, float y, float w, float h) => SelectedUiTheme.Place((RectTransform)component.transform, x, y, w, h, sw, sh);
            void Child(GameObject parent, string name, float x, float y, float w, float h, float width, float height) => SelectedUiTheme.Place((RectTransform)parent.transform.Find(name), x, y, w, h, width, height);
            Put(deviceHeader.transform, 0, 0, sw, manifest ? 94 : 92);
            Put(deviceFooterPlate.transform, 0, sh - (manifest ? 48 : 69), sw, manifest ? 48 : 69);
            Put(deviceChapter, 38, 18, 500, 30); Put(deviceSubtitle, 38, 54, 600, 25);
            Put(surface.Find("DeviceAccess"), sw - 400, 20, 362, 60);
            deviceSubtitle.characterSpacing = surface.Find("DeviceAccess").GetComponent<TMP_Text>().characterSpacing = 5;
            Put(deviceFooter, 28, sh - 43, sw * .5f, 26); Put(deviceHint, sw * .48f, sh - 43, sw * .50f - 28, 26);
            int dividerIndex = 0, rowIndex = 0;
            foreach (var line in surface.GetComponentsInChildren<Image>(true))
            {
                if (line.name == "DeviceDivider")
                {
                    line.sprite = Theme.Divider; line.color = Color.white;
                    Put(line, 0, dividerIndex++ == 0 ? (manifest ? 94 : 92) : (manifest ? 700 : 709), sw, 1);
                }
                else if (line.name == "DeviceRowDivider")
                {
                    line.sprite = Theme.Divider; line.color = Color.white;
                    SelectedUiTheme.Place(line.rectTransform, 22, 138 + rowIndex++ * 59, 384, 1, 430, 362);
                }
            }
            Put(manifestArea.transform, 260, 94, sw - 260, sh - 142); manifestArea.SetActive(manifest);
            var brand = panel.Find("DeviceBrand").GetComponent<TMP_Text>();
            var id = panel.Find("DeviceIdentity").GetComponent<TMP_Text>();
            brand.text = manifest ? "INDUSTRIAL DISPLAY" : "INDUSTRIAL CONTROL PANEL";
            id.text = manifest ? "LOCAL TERMINAL" : "POWER SYSTEM";
            SelectedUiTheme.Place(brand.rectTransform, 50, ph - 42, pw * .6f, 24, pw, ph);
            SelectedUiTheme.Place(id.rectTransform, pw * .62f, ph - 42, pw * .34f, 24, pw, ph);
            deviceBody.lineSpacing = 8;
            if (manifest)
            {
                Put(manifestSidebar.transform, 0, 94, 260, 607);
                SelectedUiTheme.Surface(manifestSidebar.GetComponent<Image>(), Theme.Sidebar);
                Child(manifestSidebar, "RecordsLabel", 26, 30, 220, 24, 260, 607);
                Child(manifestSidebar, "RecordsOverview", 26, 86, 220, 34, 260, 607);
                Child(manifestSidebar, "RecordsSelection", 0, 127, 260, 58, 260, 607);
                Child(manifestSidebar, "DeliveryRecords", 26, 142, 230, 32, 260, 607);
                var selected = manifestSidebar.transform.Find("DeliveryRecords").GetComponent<TMP_Text>();
                selected.color = Theme.Background; selected.fontStyle = FontStyles.Normal;
                Child(manifestSidebar, "RecordsArchive", 26, 205, 220, 32, 260, 607);
                Put(deviceTitle, 303, 125, 790, 48); Put(deviceBreadcrumb, 303, 178, 850, 28);
                Put(deviceState, 1110, 125, 168, 30); deviceState.text = "EVIDENCE";
                Put(manifestPage.transform, 303, 220, 900, 435);
                SelectedUiTheme.Surface(manifestPage.GetComponent<Image>(), Theme.PaperSurface);
                Child(manifestPage, "ManifestPageTitle", 38, 28, 700, 34, 900, 435);
                Child(manifestPage, "ManifestCopy", 38, 64, 700, 24, 900, 435);
                Child(manifestPage, "ManifestRecord", 720, 28, 142, 30, 900, 435);
                Child(manifestPage, "PaperDivider", 38, 101, 824, 1, 900, 435);
                Put(deviceViewport, 341, 347, 824, 280);
            }
            else
            {
                Put(deviceBreadcrumb, 38, 124, 800, 28); Put(deviceTitle, 38, 162, 780, 50);
                Put(deviceState, 765, 182, 447, 30); deviceState.alignment = TextAlignmentOptions.TopRight;
                float leftHeight = expandedSource ? 442 : 362;
                Put(deviceInstructionCard.transform, 38, 231, 718, leftHeight);
                Put(deviceControlCard.transform, 782, 231, 430, 362);
                Child(deviceInstructionCard, "ModuleHeaderPlate", 0, 0, 718, 57, 718, leftHeight);
                Child(deviceInstructionCard, "DeviceInstructionsHeading", 20, 17, 678, 28, 718, leftHeight);
                Child(deviceInstructionCard, "DeviceScrollHint", 390, 17, 308, 26, 718, leftHeight);
                Child(deviceControlCard, "ModuleHeaderPlate", 0, 0, 430, 57, 430, 362);
                Child(deviceControlCard, "DeviceControlsHeading", 20, 17, 390, 28, 430, 362);
                string[] names = { "DeviceSystemCaption", "DeviceSourceCaption", "DeviceActionCaption" };
                string[] values = { "DeviceSystemValue", "DeviceSource", "DeviceAction" };
                for (int i = 0; i < 3; i++)
                {
                    Child(deviceControlCard, names[i], 22, 96 + i * 59, 180, 32, 430, 362);
                    Child(deviceControlCard, values[i], 202, 96 + i * 59, 206, 32, 430, 362);
                    deviceControlCard.transform.Find(values[i]).GetComponent<TMP_Text>().fontSize = Theme.Body;
                }
                Put(deviceViewport, 60, 311, 672, expandedSource ? 190 : 76);
                Put(deviceNotice.transform, 60, expandedSource ? 515 : 405, 672, 114);
                Child(deviceNotice, "DeviceNoticeHeading", 24, 12, 624, 24, 672, 114);
                Child(deviceNotice, "DeviceNoticeValue", 24, 45, 624, 32, 672, 114);
                Child(deviceNotice, "DeviceNoticeHint", 24, 83, 624, 27, 672, 114);
            }
        }

        private void StyleSelectedDeviceButton(UnityEngine.UI.Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            SelectedUiTheme.Surface(button.GetComponent<Image>(), Theme.LockedSurface);
            button.GetComponent<Image>().raycastTarget = true;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) SelectedUiTheme.Place(label.rectTransform, 74, 19, 298, 43, 384, 81);
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = colors.selectedColor = Theme.Paper;
            colors.disabledColor = Color.white; button.colors = colors;
            SelectedUiTheme.Place(button.GetComponent<RectTransform>(), 873, 550, 384, 81, 1386, 930);
            SelectedUiTheme.Place(deviceLock.rectTransform, 902, 576, 26, 26, 1386, 930);
            SelectedUiTheme.Place(deviceProgressTrack.rectTransform, 902, 570, 42, 42, 1386, 930);
            SelectedUiTheme.Place(deviceProgress.rectTransform, 902, 570, 42, 42, 1386, 930);
        }
    }
}
