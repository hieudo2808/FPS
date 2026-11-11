using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using FPS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class HudRegressionTests
    {
        [Test]
        public void HudOpacitySliderPersistsAndDoesNotDuplicateOnReopen()
        {
            bool hadValue = PlayerPrefs.HasKey(HudSkin.PanelOpacityKey);
            float previous = HudSkin.PanelOpacity;
            var root = new GameObject("Opacity settings fixture");
            var graphics = new GameObject("Graphics", typeof(RectTransform));
            var row = new GameObject("SensitivitySliderRow", typeof(RectTransform));
            graphics.transform.SetParent(root.transform); row.transform.SetParent(root.transform);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            var value = new GameObject("ValueText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            var sliderObject = new GameObject("SensitivitySlider", typeof(RectTransform), typeof(Slider));
            label.transform.SetParent(row.transform); value.transform.SetParent(row.transform); sliderObject.transform.SetParent(row.transform);
            try
            {
                HudSkin.SetPanelOpacity(1f);
                var settings = root.AddComponent<SettingsUI>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(SettingsUI).GetField("graphicsPanel", flags).SetValue(settings, graphics);
                typeof(SettingsUI).GetField("sensitivitySlider", flags).SetValue(settings, sliderObject.GetComponent<Slider>());
                var initialize = typeof(SettingsUI).GetMethod("InitializeUi", flags);
                initialize.Invoke(settings, null);
                var opacity = graphics.GetComponentInChildren<Slider>(true);
                Assert.That(opacity, Is.Not.Null);
                opacity.value = .35f;
                Assert.That(HudSkin.PanelOpacity, Is.EqualTo(.35f).Within(.001f));
                Assert.That(graphics.transform.Find("HudOpacitySliderRow/ValueText").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("35%"));
                initialize.Invoke(settings, null);
                Assert.That(graphics.GetComponentsInChildren<Slider>(true).Length, Is.EqualTo(1));
                Assert.That(opacity.value, Is.EqualTo(.35f).Within(.001f));
                HudSkin.SetPanelOpacity(-1f);
                Assert.That(HudSkin.PanelOpacity, Is.Zero);
                HudSkin.SetPanelOpacity(2f);
                Assert.That(HudSkin.PanelOpacity, Is.EqualTo(1f));
            }
            finally
            {
                HudSkin.SetPanelOpacity(previous);
                if (!hadValue) { PlayerPrefs.DeleteKey(HudSkin.PanelOpacityKey); PlayerPrefs.Save(); }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeviceStatusUsesCampaignEligibility()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var label = typeof(CampaignHUD).GetMethod("DeviceStateLabel", flags);
            var state = new CampaignState { chapter = CampaignChapter.Factory, phase = CampaignPhase.Exploring };
            string Status(CampaignObjectiveId objective) => (string)label.Invoke(null,
                new object[] { state, objective, CampaignRules.CanUse(state, objective) });
            Assert.That(Status(CampaignObjectiveId.FactoryIsolate), Is.EqualTo("ACTION READY"));
            Assert.That(Status(CampaignObjectiveId.FactoryBackup), Is.EqualTo("Requires: complete Power / Isolate"));
            state.completed = CampaignState.Bit(CampaignObjectiveId.FactoryIsolate);
            Assert.That(Status(CampaignObjectiveId.FactoryIsolate), Is.EqualTo("RECORDED"));
            Assert.That(Status(CampaignObjectiveId.FactoryBackup), Is.EqualTo("ACTION READY"));
            state.phase = CampaignPhase.Preparing;
            Assert.That(Status(CampaignObjectiveId.FactoryBackup), Is.EqualTo("Requires: exploration phase"));
            state.chapter = CampaignChapter.Asylum;
            Assert.That(Status(CampaignObjectiveId.FactoryBackup), Is.EqualTo("Requires: correct chapter"));
        }

        [Test]
        public void ManifestDeviceShowsFileOnlyAfterItIsRecorded()
        {
            var campaignRoot = new GameObject("Manifest campaign fixture");
            var hudRoot = new GameObject("Manifest HUD fixture");
            var settings = ScriptableObject.CreateInstance<CampaignSettings>();
            var catalog = ScriptableObject.CreateInstance<CampaignContentCatalog>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                var campaign = campaignRoot.AddComponent<CampaignMissionController>();
                typeof(CampaignMissionController).GetMethod("Awake", flags).Invoke(campaign, null);
                campaign.settings = settings; settings.contentCatalog = catalog;
                campaign.State.phase = CampaignPhase.Exploring;
                catalog.objectives = new[] { new CampaignObjectiveDefinition { id = CampaignObjectiveId.FactoryManifest,
                    title = "Manifest", instructions = "Read record", file = CampaignFileId.FactoryManifest } };
                catalog.files = new[] { new CampaignFileDefinition { id = CampaignFileId.FactoryManifest,
                    required = true, body = "Recorded manifest content" } };
                var hud = hudRoot.AddComponent<CampaignHUD>();
                typeof(CampaignHUD).GetMethod("Awake", flags).Invoke(hud, null);
                var item = hudRoot.AddComponent<CampaignInteractable>(); item.objectiveId = CampaignObjectiveId.FactoryManifest;
                typeof(CampaignHUD).GetField("focused", flags).SetValue(hud, item);
                typeof(CampaignHUD).GetMethod("SetDeviceLayout", flags).Invoke(hud, new object[] { true });
                var render = typeof(CampaignHUD).GetMethod("RenderDocument", flags);
                var body = (TMPro.TMP_Text)typeof(CampaignHUD).GetField("deviceBody", flags).GetValue(hud);
                render.Invoke(hud, null);
                Assert.That(body.text, Does.Not.Contain("Recorded manifest content"));
                Assert.That(body.text, Does.Contain("SOURCE NOT RECORDED"));
                var instructionCard = (GameObject)typeof(CampaignHUD).GetField("deviceInstructionCard", flags).GetValue(hud);
                var controlCard = (GameObject)typeof(CampaignHUD).GetField("deviceControlCard", flags).GetValue(hud);
                var manifestSidebar = (GameObject)typeof(CampaignHUD).GetField("manifestSidebar", flags).GetValue(hud);
                var manifestPage = (GameObject)typeof(CampaignHUD).GetField("manifestPage", flags).GetValue(hud);
                Assert.That(instructionCard.activeSelf, Is.False);
                Assert.That(controlCard.activeSelf, Is.False);
                Assert.That(manifestSidebar.activeSelf, Is.True);
                Assert.That(manifestPage.activeSelf, Is.True);
                var title = (TMPro.TMP_Text)typeof(CampaignHUD).GetField("deviceTitle", flags).GetValue(hud);
                Assert.That(title.rectTransform.anchorMin.y, Is.GreaterThan(manifestPage.GetComponent<RectTransform>().anchorMax.y));
                var breadcrumb = (TMPro.TMP_Text)typeof(CampaignHUD).GetField("deviceBreadcrumb", flags).GetValue(hud);
                Assert.That(breadcrumb.text, Is.EqualTo("RECORDS / LOGISTICS / MANIFEST"));
                campaign.State.discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest);
                Assert.That((bool)typeof(CampaignHUD).GetMethod("DeviceStateChanged", flags).Invoke(hud, null), Is.True);
                render.Invoke(hud, null);
                Assert.That(body.text, Does.Contain("Recorded manifest content"));
                Assert.That(body.text, Does.Not.Contain("SOURCE NOT RECORDED"));
                var status = (TMPro.TMP_Text)typeof(CampaignHUD).GetField("deviceState", flags).GetValue(hud);
                Assert.That(status.text, Is.EqualTo("EVIDENCE"));
                var hint = (TMPro.TMP_Text)typeof(CampaignHUD).GetField("deviceHint", flags).GetValue(hud);
                Assert.That(hint.text, Is.EqualTo("FILE ADDED TO INVENTORY"));
            }
            finally
            {
                Object.DestroyImmediate(hudRoot); Object.DestroyImmediate(campaignRoot);
                Object.DestroyImmediate(settings); Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void DeviceLayoutRestoresDocumentLayoutWhenClosed()
        {
            var root = new GameObject("Device layout fixture");
            try
            {
                var hud = root.AddComponent<CampaignHUD>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CampaignHUD).GetMethod("Awake", flags).Invoke(hud, null);
                var setLayout = typeof(CampaignHUD).GetMethod("SetDeviceLayout", flags);
                var surface = (GameObject)typeof(CampaignHUD).GetField("deviceSurface", flags).GetValue(hud);
                var backdrop = (GameObject)typeof(CampaignHUD).GetField("deviceBackdrop", flags).GetValue(hud);
                var viewport = (RectTransform)typeof(CampaignHUD).GetField("documentViewport", flags).GetValue(hud);
                var controls = (RectTransform)typeof(CampaignHUD).GetField("controls", flags).GetValue(hud);
                var instructions = surface.transform.Find("DeviceInstructions");
                var deviceControls = surface.transform.Find("DeviceControls");
                Assert.That(instructions, Is.Not.Null);
                Assert.That(deviceControls, Is.Not.Null);
                Assert.That(instructions.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(deviceControls.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(deviceControls.Find("DeviceSource"), Is.Not.Null);
                Assert.That(deviceControls.Find("DeviceAction"), Is.Not.Null);
                setLayout.Invoke(hud, new object[] { true });
                Assert.That(surface.activeSelf, Is.True);
                Assert.That(backdrop.activeSelf, Is.True);
                Assert.That(backdrop.GetComponent<Image>().color, Is.EqualTo(SelectedUiTheme.Current.DeviceBackdrop));
                Assert.That(((GameObject)typeof(CampaignHUD).GetField("deviceClose", flags).GetValue(hud)).activeSelf, Is.True);
                Assert.That(surface.GetComponent<RectTransform>().anchorMin, Is.EqualTo(new Vector2(.047f, .10f)));
                Assert.That(viewport.gameObject.activeSelf, Is.False);
                Assert.That(controls.anchorMin.x, Is.EqualTo(.65f));
                setLayout.Invoke(hud, new object[] { false });
                Assert.That(surface.activeSelf, Is.False);
                Assert.That(backdrop.activeSelf, Is.False);
                Assert.That(((GameObject)typeof(CampaignHUD).GetField("deviceClose", flags).GetValue(hud)).activeSelf, Is.False);
                Assert.That(((Image)typeof(CampaignHUD).GetField("deviceLock", flags).GetValue(hud)).gameObject.activeSelf, Is.False);
                Assert.That(viewport.gameObject.activeSelf, Is.True);
                Assert.That(controls.anchorMin, Is.EqualTo(new Vector2(.04f, .07f)));
                Assert.That(hud.GetComponentsInChildren<Transform>(true).Any(item => item.name == "JournalSurface"), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DebriefShowsSummaryValuesAndResultState()
        {
            var root = new GameObject("Debrief fixture");
            try
            {
                var status = root.AddComponent<CampaignStatusUI>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CampaignStatusUI).GetMethod("Awake", flags).Invoke(status, null);
                var render = typeof(CampaignStatusUI).GetMethod("RenderSummary", flags);
                var summary = new CampaignRunSummary { result = CampaignPhase.Failed, durationSeconds = 75, survivors = 2,
                    rosterSize = 4, teamDowns = 3, zombieKills = 17, optionalFilesFound = 5, factoryCaseSecured = true };
                render.Invoke(status, new object[] { summary, 12 });
                TMPro.TMP_Text Field(string name) => (TMPro.TMP_Text)typeof(CampaignStatusUI).GetField(name, flags).GetValue(status);
                var stats = (TMPro.TMP_Text[])typeof(CampaignStatusUI).GetField("debriefStats", flags).GetValue(status);
                Assert.That(Field("debriefText").text, Is.EqualTo("MISSION FAILED"));
                Assert.That(Field("debriefText").color, Is.EqualTo(SelectedUiTheme.Current.Danger));
                Assert.That(stats.Select(text => text.text), Is.EqualTo(new[] { "01:15", "2/4", "3", "17" }));
                Assert.That(Field("debriefDiscoveryValues").text, Is.EqualTo("5 / 12\nSecured\nNot secured"));
                Assert.That(Field("debriefEnd").gameObject.activeSelf, Is.False);
                summary.result = CampaignPhase.Completed;
                render.Invoke(status, new object[] { summary, 12 });
                Assert.That(Field("debriefText").text, Is.EqualTo("MISSION COMPLETE"));
                Assert.That(Field("debriefEnd").gameObject.activeSelf, Is.False);
                var action = (Button)typeof(CampaignStatusUI).GetField("retryButton", flags).GetValue(status);
                Assert.That(action.gameObject.activeSelf, Is.True, "A completed run must retain a reachable action.");
                Assert.That(action.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("RETURN TO MENU"));
                Assert.That(Field("debriefFooter").text, Is.EqualTo("Return to menu."));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1024, 768, 1f)]
        [TestCase(1920, 1080, 1.25f)]
        public void InventoryOverflowKeepsScrollControlsAndEndCueAtStressSizes(int width, int height, float scale)
        {
            var root = new GameObject("Inventory overflow fixture");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                var inventory = root.AddComponent<TeamInventoryUI>();
                typeof(TeamInventoryUI).GetMethod("Awake", flags).Invoke(inventory, null);
                var canvas = root.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
                ((GameObject)typeof(TeamInventoryUI).GetField("root", flags).GetValue(inventory)).SetActive(true);
                var body = (TMPro.TMP_Text)typeof(TeamInventoryUI).GetField("detailBody", flags).GetValue(inventory);
                body.text = string.Join("\n", Enumerable.Range(1, 40).Select(index => "Synthetic long document line " + index));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
                var scroll = (ScrollRect)typeof(TeamInventoryUI).GetField("bodyScroll", flags).GetValue(inventory);
                var hint = (TMPro.TMP_Text)typeof(TeamInventoryUI).GetField("bodyScrollHint", flags).GetValue(inventory);
                var update = typeof(TeamInventoryUI).GetMethod("LateUpdate", flags);
                scroll.verticalNormalizedPosition = 1;
                update.Invoke(inventory, null);
                Assert.That(body.rectTransform.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(scroll.verticalScrollbar.handleRect, Is.Not.Null);
                Assert.That(hint.gameObject.activeSelf, Is.True);
                Assert.That(hint.text, Is.EqualTo("SCROLL FOR MORE"));
                scroll.verticalScrollbar.value = 0;
                update.Invoke(inventory, null);
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.001f));
                Assert.That(hint.text, Is.EqualTo("END · SCROLL UP"));
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, body.rectTransform);
                Assert.That(bounds.min.y, Is.EqualTo(scroll.viewport.rect.yMin).Within(1), "The final line must be reachable above the viewport bottom.");
                var list = (ScrollRect)typeof(TeamInventoryUI).GetField("listScroll", flags).GetValue(inventory);
                Assert.That(list.verticalScrollbar.handleRect, Is.Not.Null, "The entry list also needs a visible scroll control.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ThreeTabInventoryKeepsRecordAndMissionDataWithoutChangingProgress()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var campaignRoot = new GameObject("Three tab campaign fixture");
            var inventoryRoot = new GameObject("Three tab UI fixture");
            var hudRoot = new GameObject("Three tab HUD fixture");
            var settings = ScriptableObject.CreateInstance<CampaignSettings>();
            var catalog = ScriptableObject.CreateInstance<CampaignContentCatalog>();
            try
            {
                var campaign = campaignRoot.AddComponent<CampaignMissionController>();
                typeof(CampaignMissionController).GetMethod("Awake", flags).Invoke(campaign, null);
                campaign.settings = settings; settings.contentCatalog = catalog;
                campaign.State.phase = CampaignPhase.Exploring;
                catalog.objectives = new[]
                {
                    new CampaignObjectiveDefinition { id = CampaignObjectiveId.FactoryIsolate, title = "Isolate Production", instructions = "Isolate the production line." },
                    new CampaignObjectiveDefinition { id = CampaignObjectiveId.FactoryBackup, title = "Backup Cold Store", instructions = "Inspect the backup panel." }
                };
                catalog.files = System.Array.Empty<CampaignFileDefinition>();
                var hud = hudRoot.AddComponent<CampaignHUD>();
                typeof(CampaignHUD).GetMethod("Awake", flags).Invoke(hud, null);
                var inventory = inventoryRoot.AddComponent<TeamInventoryUI>();
                typeof(TeamInventoryUI).GetMethod("Awake", flags).Invoke(inventory, null);
                typeof(TeamInventoryUI).GetMethod("BindSources", flags).Invoke(inventory, null);
                inventory.OpenCampaignRecord();
                var body = (TMPro.TMP_Text)typeof(TeamInventoryUI).GetField("detailBody", flags).GetValue(inventory);
                Assert.That(body.text, Is.EqualTo(hud.JournalRecordText()));
                Assert.That(inventory.GetComponentsInChildren<Button>().Any(button => button.name == "Campaign Record"), Is.True, "The record remains reachable with no files.");
                // Runtime rows use deferred destruction; this fixture owns their EditMode cleanup.
                var rows = (List<GameObject>)typeof(TeamInventoryUI).GetField("rows", flags).GetValue(inventory);
                foreach (var row in rows) Object.DestroyImmediate(row);
                rows.Clear();
                inventory.GetComponentsInChildren<Button>(true).First(button => button.name == "MissionsTab").onClick.Invoke();
                typeof(TeamInventoryUI).GetMethod("ShowMission", flags).Invoke(inventory, new object[] { catalog.objectives[1] });
                Assert.That(body.text, Does.Contain("Requires: complete Power — Isolate"));
                Assert.That(body.text, Does.Contain("0 OF 2 COMPLETE"));
                Assert.That(body.text, Does.Contain("Inspect the backup panel."));
                Assert.That(campaign.State.completed, Is.Zero);
                Assert.That(campaign.State.discoveredFiles, Is.Zero);
                Assert.That(inventory.GetComponentsInChildren<Button>(true).First(button => button.name == "ResumeCheckpoint").gameObject.activeSelf, Is.False, "Unspawned client fixture cannot resume.");
                inventory.Close();
            }
            finally
            {
                Object.DestroyImmediate(inventoryRoot); Object.DestroyImmediate(hudRoot); Object.DestroyImmediate(campaignRoot);
                Object.DestroyImmediate(catalog); Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void InventoryRowKeepsSelectionAndKeyboardFocusVisible()
        {
            var root = new GameObject("Inventory row fixture", typeof(RectTransform));
            try
            {
                var inventory = root.AddComponent<TeamInventoryUI>();
                var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
                row.transform.SetParent(root.transform, false);
                var button = row.GetComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                var selectionLine = new GameObject("SelectionLine", typeof(RectTransform));
                selectionLine.transform.SetParent(row.transform, false);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var style = typeof(TeamInventoryUI).GetMethod("StyleRow", flags);
                style.Invoke(inventory, new object[] { button, false });
                Assert.That(button.colors.normalColor.a, Is.Zero);
                Assert.That(button.colors.selectedColor.a, Is.GreaterThan(0));
                Assert.That(selectionLine.activeSelf, Is.False);
                style.Invoke(inventory, new object[] { button, true });
                Assert.That(button.colors.normalColor.a, Is.GreaterThan(0));
                Assert.That(row.GetComponent<Image>().sprite, Is.Null);
                Assert.That(selectionLine.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void InfectionOverlayUsesHealthBarWithoutHidingSepsisWarning()
        {
            var root = new GameObject("Vitals fixture", typeof(RectTransform));
            try
            {
                var hud = root.AddComponent<HUDManager>();
                var health = new GameObject("Health", typeof(RectTransform)); health.transform.SetParent(root.transform, false);
                var healthBar = new GameObject("HealthBar", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); healthBar.transform.SetParent(health.transform, false);
                var infection = new GameObject("Infection", typeof(RectTransform)); infection.transform.SetParent(root.transform, false);
                var infectionBar = new GameObject("InfectionFill", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); infectionBar.transform.SetParent(infection.transform, false);
                var infectionLabel = new GameObject("InfectionLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>(); infectionLabel.transform.SetParent(infection.transform, false);
                var warning = new GameObject("SepsisWarning", typeof(RectTransform)); warning.transform.SetParent(infection.transform, false);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(HUDManager).GetField("healthFill", flags).SetValue(hud, healthBar);
                typeof(HUDManager).GetField("infectionFill", flags).SetValue(hud, infectionBar);
                typeof(HUDManager).GetField("infectionStageText", flags).SetValue(hud, infectionLabel);
                typeof(HUDManager).GetField("sepsisWarning", flags).SetValue(hud, warning);
                typeof(HUDManager).GetMethod("ConfigureVitalsPresentation", flags).Invoke(hud, null);
                Assert.That(infection.activeSelf, Is.False);
                Assert.That(infectionBar.transform.parent, Is.EqualTo(health.transform));
                Assert.That(infectionLabel.transform.parent, Is.EqualTo(health.transform));
                Assert.That(warning.transform.parent, Is.EqualTo(health.transform));
                Assert.That(infectionBar.rectTransform.sizeDelta, Is.EqualTo(healthBar.rectTransform.sizeDelta));
                Assert.That(infectionBar.fillOrigin, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FactoryTrackerMarksOnlyTheSelectedBranch()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var format = typeof(CampaignHUD).GetMethod("FormatFactoryBranches", flags);
            Assert.That(format, Is.Not.Null);
            string power = (string)format.Invoke(null, new object[] { CampaignObjectiveId.FactoryBackup });
            string logistics = (string)format.Invoke(null, new object[] { CampaignObjectiveId.FactoryManifest });
            string neither = (string)format.Invoke(null, new object[] { CampaignObjectiveId.FactoryCase });
            Assert.That(power, Is.EqualTo("▶  Power\n    Logistics"));
            Assert.That(logistics, Is.EqualTo("    Power\n▶  Logistics"));
            Assert.That(neither, Is.EqualTo("    Power\n    Logistics"));
        }

        [Test]
        public void IdleHintsYieldToInteractionPromptsAndResumeAwayFromTargets()
        {
            var root = new GameObject("Prompt priority regression");
            var catalog = ScriptableObject.CreateInstance<CampaignContentCatalog>();
            var settings = ScriptableObject.CreateInstance<CampaignSettings>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                var controller = root.AddComponent<CampaignMissionController>();
                typeof(CampaignMissionController).GetMethod("Awake", flags).Invoke(controller, null);
                controller.State.phase = CampaignPhase.Exploring;
                settings.contentCatalog = catalog; controller.settings = settings;
                catalog.objectives = new[] { new CampaignObjectiveDefinition { id = CampaignObjectiveId.FactoryIsolate, hint = "Read the source." } };
                var hud = root.AddComponent<HUDManager>();
                typeof(SceneSingleton<HUDManager>).GetMethod("Awake", flags).Invoke(hud, null);
                var prompt = new GameObject("Prompt", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                prompt.transform.SetParent(root.transform);
                typeof(HUDManager).GetField("interactionPromptText", flags).SetValue(hud, prompt.GetComponent<TMPro.TextMeshProUGUI>());
                var campaign = root.AddComponent<CampaignHUD>();
                typeof(CampaignHUD).GetMethod("Awake", flags).Invoke(campaign, null);
                ((Text)typeof(CampaignHUD).GetField("progress", flags).GetValue(campaign)).enabled = true;
                var source = root.AddComponent<CampaignInteractable>();
                source.objectiveId = CampaignObjectiveId.FactoryIsolate;
                typeof(CampaignHUD).GetField("nearbyHint", flags).SetValue(campaign, source);
                ((CampaignHintProgress)typeof(CampaignHUD).GetField("hintProgress", flags).GetValue(campaign))
                    .Tick(new CampaignState { phase = CampaignPhase.Exploring }, CampaignObjectiveId.FactoryIsolate, 100, false);
                var cachedHints = (string[])typeof(CampaignHUD).GetField("hintText", flags).GetValue(campaign);
                cachedHints[0] = "Read the source.";
                cachedHints[1] = "Reopen the source in Files.";
                cachedHints[2] = "Use the source to compare the fields.";
                var presentHint = typeof(CampaignHUD).GetMethod("PresentHint", flags);
                hud.SetInteractionPrompt("[F] READ MANIFEST", true);
                presentHint.Invoke(campaign, new object[] { "" });
                Assert.That(campaign.HasInteractionMessage, Is.False, "A source hint must not hide the interaction key or locked reason.");
                hud.SetInteractionPrompt(null, false);
                presentHint.Invoke(campaign, new object[] { "" });
                var progressLabel = (Text)typeof(CampaignHUD).GetField("progress", flags).GetValue(campaign);
                var seconds = ((CampaignHintProgress)typeof(CampaignHUD).GetField("hintProgress", flags).GetValue(campaign)).Seconds;
                Assert.That(campaign.HasInteractionMessage, Is.True,
                    $"The source hint remains available away from interactable objects. text='{progressLabel.text}' active={progressLabel.isActiveAndEnabled} seconds={seconds} nearby={typeof(CampaignHUD).GetField("nearbyHint", flags).GetValue(campaign) != null}");
                hud.SetInteractionPrompt("\U0001F512 Cần: read the Manifest\n[T] TRACK", true);
                var label = prompt.GetComponent<TMPro.TextMeshProUGUI>();
                Assert.That(label.text, Does.StartWith("Cần:"));
                Assert.That(label.text, Does.Contain("[T] TRACK"));
                Assert.That(prompt.GetComponentInChildren<Image>().sprite, Is.EqualTo(HudSkin.Get("Locked")));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(settings); Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void SaveGateLeavesUnsavedScenesUntouched()
        {
            SaveAuthoredScenes();
            // EditMode Test Runner already supplies an unsaved bootstrap scene.
            var temporary = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .First(scene => string.IsNullOrEmpty(scene.path));
            var fixture = new GameObject("Unsaved save-gate fixture");
            SceneManager.MoveGameObjectToScene(fixture, temporary);
            try
            {
                EditorSceneManager.MarkSceneDirty(temporary);
                Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/Save Authored Scenes"), Is.True);
                Assert.That(temporary.path, Is.Empty);
                Assert.That(temporary.isDirty, Is.True, "An unsaved scene must be skipped, with no Save Scene dialog.");
            }
            finally
            {
                Object.DestroyImmediate(fixture);
                SaveAuthoredScenes();
            }
        }

        [Test]
        public void FpsSampleAuthoringIsRepeatableAndKeepsOnlyImageFeedback()
        {
            SaveAuthoredScenes();
            const string scenePath = "Assets/FPS/Scenes/GameScene.unity";
            byte[] before = File.ReadAllBytes(scenePath);
            var source = SceneManager.GetSceneByPath(scenePath);
            bool opened = !source.isLoaded;
            if (opened) source = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var scene = EditorSceneManager.NewPreviewScene();
            var sourceCanvas = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
            var copy = Object.Instantiate(sourceCanvas.gameObject);
            SceneManager.MoveGameObjectToScene(copy, scene);
            if (copy.GetComponentInChildren<HUDManager>(true) == null)
                new GameObject("HUDManager", typeof(HUDManager)).transform.SetParent(copy.transform, false);
            try
            {
                FPS.UI.Editor.TacticalUiWorkshop.ApplyGameplayLayout(copy.GetComponent<Canvas>());
                var canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
                int count = canvas.GetComponentsInChildren<Transform>(true).Length;
                FPS.UI.Editor.TacticalUiWorkshop.ApplyGameplayLayout(copy.GetComponent<Canvas>());
                Assert.That(canvas.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HUDManager>(true)).Single();
                var data = new SerializedObject(manager);
                var marker = (Image)data.FindProperty("hitMarker").objectReferenceValue;
                Assert.That(marker.sprite, Is.EqualTo(HudSkin.Get("FPSSample/HitMarker")));
                Assert.That(marker.rectTransform.sizeDelta, Is.EqualTo(new Vector2(48, 48)));
                Assert.That(marker.GetComponent<TMPro.TMP_Text>(), Is.Null);
                Assert.That(marker.transform.IsChildOf(canvas.transform.Find("MinimalHUDRoot/Crosshair")), Is.False);
                Assert.That(data.FindProperty("directionalDamageIndicators").arraySize, Is.EqualTo(4));
                var blood = (Image)data.FindProperty("healthDangerBackground").objectReferenceValue;
                Assert.That(blood.sprite, Is.EqualTo(HudSkin.Get("FPSSample/DamageVignette")));
                Assert.That(blood.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(blood.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(blood.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "TreatmentProgressFill"), Is.False);
                Assert.That(canvas.GetComponentsInChildren<Image>(true).Any(i => i.sprite != null && i.sprite.name.Contains("Inifity")), Is.False);
                Assert.That(canvas.transform.Find("HudSafeArea/WeaponPanel").GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(176));
                foreach (var image in marker.transform.parent.GetComponentsInChildren<Image>(true)) Assert.That(image.raycastTarget, Is.False);
                foreach (var name in new[] { "HitMarker", "DamageIndicator", "DamageVignette", "GoalMarker", "GoalMarkerCentre", "GoalProgress", "UI_HUD_LMB", "UI_HUD_RMB", "UI_Hud_Bullets" })
                    Assert.That(HudSkin.Get("FPSSample/" + name), Is.Not.Null, name);
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(before), "Preview authoring must not write the real scene.");
            }
            finally
            {
                SaveAuthoredScenes();
                EditorSceneManager.ClosePreviewScene(scene);
                if (opened) EditorSceneManager.CloseScene(source, true);
            }
        }

        [Test]
        public void DamageCopiesKeepSourceSeparateFromImpactPoint()
        {
            var info = new DamageInfo(30, hitPoint: Vector3.up, sourcePosition: Vector3.right * 20);
            var copy = (DamageInfo)typeof(EnemyHealth).GetMethod("CopyWithAmount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { info, 10f });
            Assert.That(copy.amount, Is.EqualTo(10));
            Assert.That(copy.sourcePosition, Is.EqualTo(info.sourcePosition));
            Assert.That(copy.hitPoint, Is.EqualTo(Vector3.up));
            Assert.That(new DamageInfo(1).sourcePosition, Is.Null);
        }

        static void SaveAuthoredScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path) && !scene.name.StartsWith("Untitled")
                    && !scene.path.StartsWith("Assets/InitTestScene", System.StringComparison.Ordinal)
                    && !EditorSceneManager.IsPreviewScene(scene) && scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void TrackingUsesNearestCompleteRouteThenKeepsManualSelection()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
            state.discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest);
            var lengths = new Dictionary<CampaignObjectiveId, float?>
            { [ids[0]] = 30, [ids[1]] = null, [ids[2]] = 12 };
            var chosen = CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, id => lengths[id]);
            Assert.That(chosen, Is.EqualTo(ids[2]));
            lengths[ids[0]] = 1;
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, chosen, id => lengths[id]), Is.EqualTo(chosen));
            state.completed |= CampaignState.Bit(chosen); // Includes a teammate's authoritative completion.
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, chosen, id => lengths[id]), Is.EqualTo(ids[0]));
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => null), Is.EqualTo(ids[0]));
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => float.NaN), Is.EqualTo(ids[0]));
        }

        [Test]
        public void TrackingInvalidatesOnChapterPhaseAndCheckpointRollback()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, completed = CampaignState.Bit(CampaignObjectiveId.FactoryIsolate) };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup };
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[1], _ => 1), Is.EqualTo(ids[1]));
            state.completed = 0; // Restore a checkpoint before the prerequisite.
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[1], _ => 1), Is.EqualTo(ids[0]));
            state.chapter = CampaignChapter.Asylum;
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[0], _ => 1), Is.EqualTo(CampaignObjectiveId.None));
            state.phase = CampaignPhase.Preparing;
            Assert.That(CampaignObjectiveSelection.Resolve(state, new[] { CampaignObjectiveId.AsylumAccess }, CampaignObjectiveId.AsylumAccess, _ => 1), Is.EqualTo(CampaignObjectiveId.None));
        }

        [Test]
        public void ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner()
        {
            var bounds = new Rect(100, 80, 1080, 560);
            var center = new Vector2(640, 360);
            var root = new GameObject("Marker projection camera", typeof(Camera));
            try
            {
                var camera = root.GetComponent<Camera>(); camera.pixelRect = new Rect(0, 0, 1280, 720);
                var front = new Vector3(0, 0, 20);
                Assert.That(HudMarkerProjection.Project(camera, front, bounds, out var edge), Is.EqualTo(center));
                Assert.That(edge, Is.False);
                foreach (float depth in new[] { -20f, -.001f, 0f, .001f, 20f })
                {
                    var right = HudMarkerProjection.Project(camera, new Vector3(20, 0, depth), bounds, out edge);
                    Assert.That(edge, Is.True);
                    Assert.That(right.x, Is.EqualTo(bounds.xMax).Within(.01f), "Right bearing stays right across the camera plane.");
                    Assert.That(right.y, Is.EqualTo(center.y).Within(.01f));
                    var left = HudMarkerProjection.Project(camera, new Vector3(-20, 0, depth), bounds, out edge);
                    Assert.That(left.x, Is.EqualTo(bounds.xMin).Within(.01f));
                }
                var behind = HudMarkerProjection.Project(camera, new Vector3(0, 0, -20), bounds, out edge);
                Assert.That(edge, Is.True);
                Assert.That(float.IsNaN(behind.x) || float.IsInfinity(behind.y), Is.False);
                Assert.That(behind.y, Is.EqualTo(bounds.yMin));
            }
            finally { Object.DestroyImmediate(root); }
            var adjusted = HudMarkerProjection.AvoidCornerHud(new Vector2(100, 90), bounds, new Rect(0, 0, 400, 300), 40);
            Assert.That(adjusted.y, Is.GreaterThanOrEqualTo(340));
            Assert.That(adjusted.x, Is.EqualTo(bounds.xMin));
            var crowded = new[] { new Rect(0, 0, 400, 570), new Rect(0, 644, 500, 220) };
            var safe = HudMarkerProjection.AvoidHud(new Vector2(130, 580), new Rect(130, 100, 1276, 684), crowded, 60);
            foreach (var area in crowded)
            {
                var expanded = new Rect(area.xMin - 60, area.yMin - 60, area.width + 120, area.height + 120);
                Assert.That(expanded.Contains(safe), Is.False, "Avoid all corner groups together at the 125% fixture.");
            }
        }

        [Test]
        public void PromptsFollowOverridesAndReset()
        {
            var root = new GameObject("HUD binding regression");
            var saved = new Dictionary<string, string>();
            InputManager input = null;
            try
            {
                input = root.AddComponent<InputManager>();
                foreach (var action in input.ActionsAsset)
                    Save("Input_" + action.name);
                Save("InputBindings");
                int updates = 0;
                input.PromptBindingsChanged += () => updates++;
                input.RebindKey("Grenade", KeyCode.H);
                Assert.That(input.GetPromptDisplayName("Grenade"), Is.EqualTo("H").IgnoreCase);
                Assert.That(updates, Is.GreaterThan(0));
                input.RebindKey("Fire", KeyCode.F);
                input.RebindKey("Aim", KeyCode.Mouse0);
                Assert.That(input.GetPromptBindingPath("Fire"), Is.EqualTo("<Keyboard>/f").IgnoreCase);
                Assert.That(input.GetPromptBindingPath("Aim"), Is.EqualTo("<Mouse>/leftButton").IgnoreCase);
                input.ResetBindingsToDefault();
                Assert.That(input.GetPromptBindingPath("Fire"), Is.EqualTo("<Mouse>/leftButton").IgnoreCase);
                Assert.That(input.GetPromptBindingPath("Aim"), Is.EqualTo("<Mouse>/rightButton").IgnoreCase);
                Assert.That(input.GetPromptDisplayName("Grenade"), Is.EqualTo("G").IgnoreCase);
                Assert.That(input.GetPromptDisplayName("CycleGrenade"), Does.Contain("3"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var pair in saved)
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetString(pair.Key, pair.Value);
                PlayerPrefs.Save();
            }
            void Save(string key) => saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }
    }
}
