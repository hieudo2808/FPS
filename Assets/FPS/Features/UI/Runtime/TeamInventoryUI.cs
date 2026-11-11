using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Shared campaign key items and files. Runtime-built to match the existing CampaignHUD composition.</summary>
    public sealed class TeamInventoryUI : MonoBehaviour
    {
        private static SelectedUiTheme Theme => SelectedUiTheme.Current;
        private enum Tab : byte { KeyItems, Files, Missions }

        [SerializeField] private Sprite campaignPanel;
        public Sprite CampaignPanel => campaignPanel;

        public static TeamInventoryUI Instance { get; private set; }
        public static bool IsOpen => UiStateStack.Contains(UiScreen.Inventory);

        private GameObject root;
        private RectTransform listContent;
        private TMP_Text title;
        private TMP_Text detailTitle;
        private TMP_Text detailStatus;
        private TMP_Text detailCategory;
        private TMP_Text detailBody;
        private Image detailIcon;
        private Image detailSlot;
        private Button selectedRow;
        private Button keyItemsTab;
        private Button filesTab;
        private SelectedUiTypography typography;
        private Button missionsTab;
        private Button closeButton, resumeButton;
        private GameObject footerDivider;
        private TMP_Text closeKey;
        private ScrollRect listScroll, bodyScroll;
        private TMP_Text listScrollHint, bodyScrollHint;
        private CampaignHUD trackingHud;
        private InputManager input;
        private CampaignMissionController campaign;
        private Tab tab;
        private ulong viewedFiles;
        private CampaignFileId selectedFile;
        private CampaignState displayedState;
        private bool displayedResume;
        private bool readingRecord;
        private CampaignObjectiveId selectedObjective;
        private CampaignKeyItemId selectedItem;
        private bool focusOnRefresh;
        private bool forceRefresh;
        private CampaignFileId pendingFile;
        private uint pendingRevision;
        private readonly List<GameObject> rows = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
            HudSignals.NotifySourcesChanged();
        }

        private void OnEnable()
        {
            HudSignals.SourcesChanged += BindSources;
            BindSources();
        }
        private void OnDisable()
        {
            HudSignals.SourcesChanged -= BindSources;
            UnbindSources(); Close(); CancelPendingFile();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void LateUpdate()
        {
            if (root == null || !root.activeInHierarchy) return;
            UpdateScrollHint(listScroll, listScrollHint);
            UpdateScrollHint(bodyScroll, bodyScrollHint);
        }
        private static void UpdateScrollHint(ScrollRect scroll, TMP_Text hint)
        {
            bool overflow = scroll.content.rect.height > scroll.viewport.rect.height + 1;
            hint.gameObject.SetActive(overflow);
            if (overflow) hint.text = scroll.verticalNormalizedPosition <= .001f ? "END · SCROLL UP" : "SCROLL FOR MORE";
        }
        private void UnbindSources()
        {
            if (campaign != null) campaign.Changed -= OnCampaignChanged;
            if (trackingHud != null) trackingHud.TrackingChanged -= ForceRefresh;
            if (input != null) input.PromptBindingsChanged -= UpdateClosePrompt;
        }
        private void BindSources()
        {
            UnbindSources();
            campaign = CampaignMissionController.Instance;
            trackingHud = CampaignHUD.Instance; input = InputManager.Instance;
            if (campaign != null) campaign.Changed += OnCampaignChanged;
            if (trackingHud != null) trackingHud.TrackingChanged += ForceRefresh;
            if (input != null) input.PromptBindingsChanged += UpdateClosePrompt;
            UpdateClosePrompt(); forceRefresh = true; OnCampaignChanged();
        }
        private void OnCampaignChanged()
        {
            if (IsOpen && (campaign == null || campaign.BlocksInput)) Close();
            Refresh();
            if (pendingFile == CampaignFileId.None || campaign == null || campaign.State.revision < pendingRevision) return;
            CampaignFileId id = pendingFile; CancelPendingFile();
            if (campaign.State.HasFile(id) && CampaignHUD.Instance != null && CampaignHUD.Instance.CanAutoOpenFile()) OpenFile(id);
        }

        public void AcknowledgeFile(CampaignFileId id, uint revision)
        {
            pendingFile = id; pendingRevision = revision;
            CancelInvoke(nameof(CancelPendingFile));
            Invoke(nameof(CancelPendingFile), 3f);
            OnCampaignChanged();
        }

        public void CancelPendingFile() { pendingFile = CampaignFileId.None; CancelInvoke(nameof(CancelPendingFile)); }

        private void ForceRefresh() { forceRefresh = true; Refresh(); }
        private void UpdateClosePrompt()
        {
            string journal = SurvivalHotbar.Key("Journal", "I");
            closeButton.GetComponentInChildren<TMP_Text>().text = "CLOSE";
            closeKey.text = journal;
        }

        public void Toggle()
        {
            if (root == null) return;
            if (root.activeSelf) Close(); else Open(tab);
        }

        public void OpenFile(CampaignFileId id)
        {
            if (root == null || campaign == null || !campaign.State.HasFile(id)) return;
            selectedFile = id;
            readingRecord = false;
            Open(Tab.Files);
            CampaignFileDefinition file = campaign?.ContentCatalog?.FindFile(id);
            if (file != null) SelectFile(file);
        }

        public void OpenCampaignRecord()
        {
            if (campaign == null || root == null) return;
            readingRecord = true;
            Open(Tab.Files);
        }

        public void Close()
        {
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            UiStateStack.Remove(UiScreen.Inventory);
        }

        private void Open(Tab nextTab)
        {
            if (campaign == null || campaign.BlocksInput || InGameMenuUI.IsMenuOpen || CampaignStatusUI.IsDebriefOpen) return;
            tab = nextTab; forceRefresh = focusOnRefresh = true;
            UiStateStack.Push(UiScreen.Inventory, root, Close);
            Refresh();
        }

        private void Refresh()
        {
            if (root == null || !root.activeSelf || campaign == null) return;
            viewedFiles &= campaign.State.discoveredFiles;
            var state = campaign.State;
            bool canResume = tab == Tab.Missions && campaign.CanResumeSavedCampaign;
            if (!forceRefresh && displayedState != null && displayedState.chapter == state.chapter
                && displayedState.phase == state.phase && displayedState.discoveredFiles == state.discoveredFiles
                && displayedState.completed == state.completed && displayedState.factoryTankDefeated == state.factoryTankDefeated
                && displayedState.evidenceTransmitted == state.evidenceTransmitted && displayedResume == canResume) return;
            var focused = EventSystem.current?.currentSelectedGameObject;
            string focusName = !focusOnRefresh && focused != null && focused.transform.IsChildOf(root.transform) ? focused.name : null;
            float listPosition = listContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition;
            float bodyPosition = detailBody.rectTransform.parent.GetComponent<ScrollRect>().verticalNormalizedPosition;
            forceRefresh = false;
            displayedState = state.Copy(); displayedResume = canResume;
            ClearRows();
            bool keyTab = tab == Tab.KeyItems;
            SetTabState(keyItemsTab, keyTab);
            SetTabState(filesTab, tab == Tab.Files);
            SetTabState(missionsTab, tab == Tab.Missions);
            typography.Refresh();
            title.text = "Squad Inventory";
            resumeButton.gameObject.SetActive(canResume);
            footerDivider.SetActive(canResume);

            if (keyTab) PopulateKeyItems(); else if (tab == Tab.Files) PopulateFiles(); else PopulateMissions();
            if (rows.Count == 0)
            {
                // Files always lists the campaign record, so only the other two tabs can be empty.
                AddEmptyRow(keyTab ? "NO KEY ITEMS ACQUIRED" : "NO MISSIONS AVAILABLE");
                ShowDetail(null, "", "", keyTab ? "Mission items acquired by the squad appear here." : "Mission objectives appear when the squad enters a chapter.");
            }
            Canvas.ForceUpdateCanvases();
            if (!focusOnRefresh)
            {
                listContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = listPosition;
                detailBody.rectTransform.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = bodyPosition;
            }
            var restoredFocus = focusName == null ? null : root.GetComponentsInChildren<Button>()
                .FirstOrDefault(button => button.interactable && button.name == focusName);
            EventSystem.current?.SetSelectedGameObject(restoredFocus != null ? restoredFocus.gameObject
                : selectedRow != null ? selectedRow.gameObject
                : rows.FirstOrDefault(row => row.GetComponent<Button>().interactable) ?? closeButton.gameObject);
            focusOnRefresh = false;
        }

        private void PopulateKeyItems()
        {
            CampaignKeyItemDefinition[] definitions = campaign.ContentCatalog?.keyItems;
            if (definitions == null) return;
            foreach (CampaignKeyItemDefinition item in definitions.Where(x => x != null).OrderBy(x => x.order))
            {
                bool consumed = item.consumedAfter != CampaignObjectiveId.None && campaign.State.Has(item.consumedAfter);
                if (!item.IsOwned(campaign.State)) continue;
                string status = consumed ? "USED" : item.persistAfterUse ? "SECURED" : "OWNED";
                Sprite icon = item.icon != null ? item.icon : HudSkin.Get("KeyItem");
                Button row = AddRow(item.title, status, icon, () =>
                { selectedItem = item.id; ShowDetail(icon, item.title, status, item.description); });
                if (selectedRow == null || item.id == selectedItem)
                {
                    if (selectedRow != null) StyleRow(selectedRow, false);
                    selectedRow = row; StyleRow(row, true);
                    ShowDetail(icon, item.title, status, item.description);
                }
            }
        }

        private void PopulateFiles()
        {
            CampaignFileDefinition[] definitions = campaign.ContentCatalog?.files;
            CampaignFileDefinition selection = null;
            Button selectionRow = null;
            foreach (CampaignFileDefinition file in (definitions ?? Array.Empty<CampaignFileDefinition>()).Where(x => x != null)
                         .Where(x => campaign.State.HasFile(x.id)).OrderBy(x => x.chapter).ThenBy(x => x.order))
            {
                bool unread = (viewedFiles & CampaignState.FileBit(file.id)) == 0;
                string status = unread ? "NEW" : file.required ? "EVIDENCE" : "OPTIONAL";
                Button row = AddRow(file.title, status, HudSkin.Get("File"), () => SelectFile(file));
                if (selection == null || file.id == selectedFile) { selection = file; selectionRow = row; }
            }
            var recordRow = AddRow("Campaign Record", "RECORDED", HudSkin.Get("File"), SelectCampaignRecord);
            if (readingRecord || selection == null)
            { selectedRow = recordRow; StyleRow(recordRow, true); SelectCampaignRecord(); }
            else { selectedRow = selectionRow; StyleRow(selectedRow, true); SelectFile(selection); }
        }

        private void SelectCampaignRecord()
        {
            readingRecord = true;
            ShowDetail(HudSkin.Get("File"), "Campaign Record", "RECORDED",
                trackingHud != null ? trackingHud.JournalRecordText() : CampaignDialogue.Journal(campaign.State));
        }

        private void SelectFile(CampaignFileDefinition file)
        {
            viewedFiles |= CampaignState.FileBit(file.id);
            selectedFile = file.id;
            readingRecord = false;
            ShowDetail(file.preview != null ? file.preview : file.icon != null ? file.icon : HudSkin.Get("File"), file.title,
                file.required ? "EVIDENCE" : "OPTIONAL FILE", string.IsNullOrWhiteSpace(file.body) ? file.summary : file.body);
        }

        private void PopulateMissions()
        {
            var definitions = campaign.ContentCatalog?.objectives;
            if (definitions == null) return;
            CampaignObjectiveDefinition selection = null;
            foreach (var objective in definitions)
            {
                if (objective == null || objective.id == CampaignObjectiveId.None
                    || CampaignRules.ChapterOf(objective.id) != campaign.State.chapter) continue;
                bool done = CampaignRules.IsObjectiveComplete(campaign.State, objective.id);
                bool available = CampaignObjectiveSelection.CanTrack(campaign.State, objective.id)
                    && campaign.Objective(objective.id) != null;
                bool tracked = trackingHud != null && trackingHud.TrackedObjectiveId == objective.id;
                string state = done ? "COMPLETE" : tracked ? "TRACKING" : available ? "AVAILABLE" : "LOCKED";
                var row = AddRow(System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(objective.title.ToLowerInvariant()), state, MissionIcon(done, available), () =>
                {
                    selectedObjective = objective.id;
                    if (available) trackingHud?.TryTrackObjective(objective.id);
                    ShowMission(objective);
                });
                StyleRow(row, false);
                if (tracked) row.transform.Find("Content/Status").GetComponent<TMP_Text>().color = Theme.Text;
                if (selection == null || objective.id == selectedObjective
                    || selectedObjective == CampaignObjectiveId.None && tracked) { selection = objective; selectedRow = row; }
            }
            if (selection != null) { StyleRow(selectedRow, true); ShowMission(selection); }
        }

        private void ShowMission(CampaignObjectiveDefinition objective)
        {
            bool done = CampaignRules.IsObjectiveComplete(campaign.State, objective.id);
            bool available = CampaignObjectiveSelection.CanTrack(campaign.State, objective.id);
            bool tracked = trackingHud != null && trackingHud.TrackedObjectiveId == objective.id;
            string body = objective.instructions + "\n\n" + (done ? "Recorded for the squad."
                : available ? "Select this objective to follow it on your HUD."
                : "Requires: " + CampaignRules.Availability(campaign.State, objective.id).Reason.Replace(" / ", " — "));
            var branch = CampaignRules.FactoryBranchOf(objective.id);
            var steps = campaign.ContentCatalog.objectives.Where(step => step != null && step.id != CampaignObjectiveId.None
                && CampaignRules.ChapterOf(step.id) == campaign.State.chapter
                && (branch == CampaignFactoryBranch.None || CampaignRules.FactoryBranchOf(step.id) == branch)).ToArray();
            int complete = steps.Count(step => CampaignRules.IsObjectiveComplete(campaign.State, step.id));
            // The list on the left already shows each step's state; the detail keeps only the count.
            body += "\n\n" + (branch == CampaignFactoryBranch.None ? "CHAPTER PROGRESS" : branch.ToString().ToUpperInvariant())
                + $"  {complete} OF {steps.Length} COMPLETE";
            ShowDetail(MissionIcon(done, available), System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(objective.title.ToLowerInvariant()), done ? "COMPLETE" : tracked ? "TRACKING" : available ? "AVAILABLE" : "LOCKED",
                body);
            detailCategory.text = (campaign.State.chapter == CampaignChapter.Asylum ? "NORTHBRIDGE" : campaign.State.chapter.ToString().ToUpperInvariant())
                + (branch == CampaignFactoryBranch.None ? "" : " — " + branch.ToString().ToUpperInvariant());
        }

        private static Sprite MissionIcon(bool done, bool available) => HudSkin.Get(done ? "Complete" : available ? "Mission" : "Locked");

        private void Build()
        {
            GameObject canvasObject = new("TeamInventoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TacticalUiTheme.CampaignOrder + 2;
            HudLayout.ConfigureCanvas(canvas);
            typography = canvasObject.AddComponent<SelectedUiTypography>();

            root = Panel(HudLayout.PeripheralRoot(canvas), "TeamInventory", Vector2.zero, Vector2.one, Theme.CampaignBackdrop).gameObject;
            RectTransform shell = Panel(root.transform, "Shell", new Vector2(.11f, .16f), new Vector2(.89f, .91f), Theme.Text);
            ApplyCampaignPanel(shell.GetComponent<Image>());
            var eyebrow = Text(shell, "Eyebrow", new Vector2(.035f, .91f), new Vector2(.50f, .96f), 18);
            eyebrow.text = "CAMPAIGN"; eyebrow.color = Theme.Muted;
            title = Text(shell, "Title", new Vector2(.035f, .84f), new Vector2(.60f, .91f), 36, FontStyles.Bold);

            keyItemsTab = TextButton(shell, "KeyItemsTab", "KEY ITEMS", new Vector2(.035f, .75f), new Vector2(.13f, .82f), () => { tab = Tab.KeyItems; focusOnRefresh = true; ForceRefresh(); });
            filesTab = TextButton(shell, "FilesTab", "FILES", new Vector2(.14f, .75f), new Vector2(.20f, .82f), () => { tab = Tab.Files; focusOnRefresh = true; ForceRefresh(); });
            missionsTab = TextButton(shell, "MissionsTab", "MISSIONS", new Vector2(.21f, .75f), new Vector2(.30f, .82f), () => { tab = Tab.Missions; focusOnRefresh = true; ForceRefresh(); });
            closeButton = TextButton(shell, "Close", "CLOSE", new Vector2(.78f, .90f), new Vector2(.965f, .96f), Close);
            closeButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
            SelectedUiTheme.Place(closeButton.GetComponentInChildren<TMP_Text>().rectTransform, 0, 0, 196, 32, 248, 32);
            var closeKeycap = Image(closeButton.transform, "CloseKeycap", Vector2.zero, Vector2.one);
            SelectedUiTheme.Surface(closeKeycap, Theme.Keycap);
            SelectedUiTheme.Place(closeKeycap.rectTransform, 221, 1, 27, 30, 248, 32);
            closeKey = Text(closeKeycap.transform, "CloseKey", Vector2.zero, Vector2.one, 18);
            closeKey.alignment = TextAlignmentOptions.Center;

            RectTransform listViewport = Panel(shell, "ListViewport", new Vector2(.035f, .15f), new Vector2(.32f, .72f), Color.clear);
            listViewport.gameObject.AddComponent<RectMask2D>();
            listContent = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            listContent.SetParent(listViewport, false);
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(.5f, 1);
            listContent.offsetMin = Vector2.zero; listContent.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = listContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            listContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            listScroll = listViewport.gameObject.AddComponent<ScrollRect>();
            listScroll.content = listContent; listScroll.viewport = listViewport; listScroll.horizontal = false;
            ConfigureScrolling(listScroll);
            listScrollHint = Text(shell, "ListScrollHint", new Vector2(.035f, .12f), new Vector2(.32f, .15f), Theme.Small);
            listScrollHint.color = Theme.Muted;

            RectTransform detail = Panel(shell, "Detail", new Vector2(.375f, .15f), new Vector2(.96f, .72f), Color.clear);
            detailSlot = Image(detail, "IconSlot", new Vector2(.045f, .67f), new Vector2(.25f, .93f));
            HudSkin.Apply(detailSlot, "Slot", Theme.Text);
            detailIcon = Image(detail, "Icon", new Vector2(.075f, .70f), new Vector2(.22f, .90f));
            detailIcon.preserveAspect = true;
            detailTitle = Text(detail, "DetailTitle", new Vector2(0, .72f), new Vector2(.96f, .90f), 36, FontStyles.Bold);
            detailTitle.textWrappingMode = TextWrappingModes.Normal;
            detailTitle.enableAutoSizing = true; detailTitle.fontSizeMin = Theme.Body; detailTitle.fontSizeMax = Theme.Title;
            detailStatus = Text(detail, "DetailStatus", new Vector2(0, .90f), new Vector2(.96f, 1f), 18);
            detailStatus.color = Theme.Muted;
            detailCategory = Text(detail, "DetailCategory", new Vector2(0, .90f), new Vector2(.96f, 1f), 18);
            detailCategory.color = Theme.Muted;
            RectTransform bodyViewport = Panel(detail, "BodyViewport", new Vector2(0, .07f), new Vector2(.96f, .70f), Color.clear);
            bodyViewport.gameObject.AddComponent<RectMask2D>();
            detailBody = Text(bodyViewport, "Body", new Vector2(0, 1), new Vector2(1, 1), 23);
            detailBody.rectTransform.pivot = new Vector2(.5f, 1); detailBody.rectTransform.sizeDelta = new Vector2(0, 1);
            detailBody.enableWordWrapping = true; detailBody.overflowMode = TextOverflowModes.Overflow;
            ContentSizeFitter bodyFit = detailBody.gameObject.AddComponent<ContentSizeFitter>(); bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bodyScroll = bodyViewport.gameObject.AddComponent<ScrollRect>();
            bodyScroll.content = detailBody.rectTransform; bodyScroll.viewport = bodyViewport; bodyScroll.horizontal = false;
            ConfigureScrolling(bodyScroll);
            bodyScrollHint = Text(detail, "BodyScrollHint", Vector2.zero, new Vector2(.96f, .06f), Theme.Small);
            bodyScrollHint.color = Theme.Muted;
            resumeButton = TextButton(shell, "ResumeCheckpoint", "RESUME CHECKPOINT", new Vector2(.65f, .025f), new Vector2(.965f, .09f), () => { campaign.RequestRetry(true); Close(); });
            resumeButton.transform.Find("ActiveLine").gameObject.SetActive(false);
            resumeButton.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineRight;
            resumeButton.gameObject.SetActive(false);
            UpdateClosePrompt();
            Divider(shell, "HeaderDivider", new Vector2(.035f, .83f), new Vector2(.965f, .832f));
            Divider(shell, "ContentDivider", new Vector2(.34f, .15f), new Vector2(.341f, .72f));
            footerDivider = Divider(shell, "FooterDivider", new Vector2(.035f, .11f), new Vector2(.965f, .112f)).gameObject;
            SelectedUiTheme.Place(shell, 210, 95, 1500, 810, 1920, 1080);
            void Put(string name, float x, float y, float w, float h) => SelectedUiTheme.Place((RectTransform)shell.Find(name), x, y, w, h, 1500, 810);
            Put("Eyebrow", 52, 44, 900, 24); Put("Title", 52, 78, 1000, 48);
            Put("Close", 1200, 44, 248, 32); Put("HeaderDivider", 52, 146, 1396, 1);
            Put("KeyItemsTab", 52, 174, 110, 36); Put("FilesTab", 208, 174, 54, 36); Put("MissionsTab", 308, 174, 110, 36);
            var tabStrip = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            tabStrip.SetParent(shell, false); SelectedUiTheme.Place(tabStrip, 52, 174, 1396, 36, 1500, 810);
            var tabLayout = tabStrip.GetComponent<HorizontalLayoutGroup>(); tabLayout.spacing = 46;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true; tabLayout.childForceExpandWidth = false; tabLayout.childForceExpandHeight = true;
            foreach (var button in new[] { keyItemsTab, filesTab, missionsTab })
            {
                button.transform.SetParent(tabStrip, false);
                button.gameObject.AddComponent<LayoutElement>().minWidth = button == filesTab ? 54 : 110;
                var itemLayout = button.gameObject.AddComponent<HorizontalLayoutGroup>();
                itemLayout.childControlWidth = itemLayout.childControlHeight = true; itemLayout.childForceExpandWidth = false; itemLayout.childForceExpandHeight = true;
                button.transform.Find("ActiveLine").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            Put("ListViewport", 52, 238, 460, 470); Put("Detail", 564, 238, 832, 470);
            Put("ListScrollHint", 52, 712, 460, 26);
            Put("ContentDivider", 538, 238, 1, 470); Put("FooterDivider", 52, 742, 1396, 1);
            Put("ResumeCheckpoint", 1050, 756, 398, 44);
            detailBody.lineSpacing = 8;
            root.SetActive(false);
        }

        private static void ConfigureScrolling(ScrollRect scroll)
        {
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = Theme.Body;
            scroll.content.offsetMax = new Vector2(-Theme.Body, scroll.content.offsetMax.y);
            var track = Panel(scroll.viewport, "Scrollbar", new Vector2(1, 0), Vector2.one, Theme.Selection);
            track.offsetMin = new Vector2(-Theme.Small, 0);
            SelectedUiTheme.Surface(track.GetComponent<Image>(), Theme.Panel);
            track.GetComponent<Image>().color = Theme.Selection;
            var handle = Image(track, "Handle", Vector2.zero, Vector2.one);
            SelectedUiTheme.Surface(handle, Theme.Panel); handle.color = Theme.Muted;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var colors = scrollbar.colors; colors.normalColor = Theme.Text;
            colors.highlightedColor = colors.selectedColor = Theme.Text; colors.pressedColor = Theme.Muted;
            scrollbar.colors = colors;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private Button AddRow(string label, string status, Sprite icon, Action clicked)
        {
            RectTransform row = Panel(listContent, label, Vector2.zero, Vector2.one, Color.clear);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 92;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 17, 17); layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Button button = row.gameObject.AddComponent<Button>(); StyleRow(button, false);
            button.onClick.AddListener(() =>
            {
                if (selectedRow != null) StyleRow(selectedRow, false);
                selectedRow = button; StyleRow(button, true); clicked();
            });
            Image image = Image(row, "Icon", Vector2.zero, Vector2.one); image.sprite = icon; image.preserveAspect = true;
            image.gameObject.SetActive(icon != null);
            var iconSize = image.gameObject.AddComponent<LayoutElement>(); iconSize.preferredWidth = iconSize.preferredHeight = 32;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            content.transform.SetParent(row, false);
            var contentSize = content.GetComponent<LayoutElement>(); contentSize.minWidth = contentSize.preferredWidth = 0; contentSize.flexibleWidth = 1;
            var labels = content.GetComponent<VerticalLayoutGroup>();
            labels.spacing = 4; labels.childControlWidth = labels.childControlHeight = true;
            labels.childForceExpandWidth = true; labels.childForceExpandHeight = false;
            TMP_Text name = Text(content.transform, "Name", Vector2.zero, Vector2.one, 23); name.text = label;
            name.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text state = Text(content.transform, "Status", Vector2.zero, Vector2.one, 16); state.text = status; state.color = Theme.Muted;
            var separator = Divider(row, "Separator", new Vector2(0, 0), new Vector2(1, .012f));
            separator.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var selectionLine = Divider(row, "SelectionLine", Vector2.zero, new Vector2(.006f, 1));
            selectionLine.color = Theme.Text; selectionLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            selectionLine.gameObject.SetActive(false);
            rows.Add(row.gameObject);
            return button;
        }

        private void AddEmptyRow(string label)
        {
            Button button = AddRow(label, "", null, () => { });
            button.interactable = false;
        }

        private void ClearRows()
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i] != null) { rows[i].SetActive(false); Destroy(rows[i]); }
            rows.Clear();
            selectedRow = null;
        }

        private void ShowDetail(Sprite icon, string heading, string status, string body)
        {
            detailIcon.sprite = icon; detailIcon.enabled = icon != null && tab == Tab.KeyItems;
            detailSlot.enabled = icon != null && tab == Tab.KeyItems;
            bool mission = tab == Tab.Missions;
            detailCategory.gameObject.SetActive(mission);
            detailTitle.rectTransform.anchorMin = new Vector2(0, mission ? .80f : .72f);
            detailStatus.rectTransform.anchorMin = new Vector2(0, mission ? .70f : .90f);
            detailStatus.rectTransform.anchorMax = new Vector2(.96f, mission ? .78f : 1f);
            detailBody.rectTransform.parent.GetComponent<RectTransform>().anchorMax = new Vector2(.96f, .70f);
            detailTitle.text = heading; detailStatus.text = status; detailBody.text = body ?? "";
            detailBody.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void SetTabState(Button button, bool selected)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>();
            label.color = selected ? Theme.Text : Theme.Muted;
            var line = (RectTransform)button.transform.Find("ActiveLine");
            line.anchorMin = new Vector2(0, .04f); line.anchorMax = new Vector2(0, .08f);
            line.pivot = new Vector2(0, .5f); line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(label.preferredWidth, 0);
            line.gameObject.SetActive(selected);
        }

        private static Image Divider(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var line = Image(parent, name, min, max);
            line.sprite = HudSkin.Get("Fill");
            line.color = Theme.Border;
            return line;
        }

        private void ApplyCampaignPanel(Image image)
        {
            image.sprite = Theme.Panel;
            image.type = image.sprite.border == Vector4.zero ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.color = SelectedUiTheme.SurfaceTint(Theme.PanelAlpha);
        }

        private void StyleRow(Button button, bool selected)
        {
            var image = (Image)button.targetGraphic;
            image.sprite = null;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.color = Theme.Text;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = selected ? Theme.Selection : Color.clear;
            colors.highlightedColor = colors.selectedColor = Theme.SelectionFocus;
            colors.pressedColor = Theme.SelectionPressed;
            colors.disabledColor = Color.clear;
            button.colors = colors;
            var selectionLine = button.transform.Find("SelectionLine");
            if (selectionLine != null) selectionLine.gameObject.SetActive(selected);
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.GetComponent<Image>().color = color; return rect;
        }

        private static TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max, float size, FontStyles style = FontStyles.Normal)
        {
            TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(parent, false); RectTransform rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            text.font = Theme.BodyFont; text.fontSize = Theme.Size(size); text.fontStyle = style; text.color = Theme.Text; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Image Image(Transform parent, string name, Vector2 min, Vector2 max)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); RectTransform rect = image.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.raycastTarget = false; return image;
        }

        private static Button TextButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Action action)
        {
            RectTransform rect = Panel(parent, name, min, max, Color.clear);
            Button button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action());
            TMP_Text text = Text(rect, "Label", Vector2.zero, Vector2.one, 18); text.text = label; text.alignment = TextAlignmentOptions.MidlineLeft;
            if (name != "Close") Divider(rect, "ActiveLine", new Vector2(0, .04f), new Vector2(1, .08f)).color = Theme.Text;
            return button;
        }
    }
}
