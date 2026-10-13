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
        private enum Tab : byte { KeyItems, Files, Missions }

        public static TeamInventoryUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

        private GameObject root;
        private RectTransform listContent;
        private TMP_Text title;
        private TMP_Text detailTitle;
        private TMP_Text detailStatus;
        private TMP_Text detailBody;
        private Image detailIcon;
        private Image detailSlot;
        private Button selectedRow;
        private Button keyItemsTab;
        private Button filesTab;
        private Button missionsTab;
        private Button closeButton;
        private CampaignHUD trackingHud;
        private InputManager input;
        private CampaignMissionController campaign;
        private Tab tab;
        private ulong viewedFiles;
        private CampaignFileId selectedFile;
        private ulong displayedFiles, displayedObjectives;
        private uint displayedRevision;
        private bool forceRefresh;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private GameObject previousSelection;
        private CampaignFileId pendingFile;
        private uint pendingRevision;
        private float pendingUntil;
        private readonly List<GameObject> rows = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (campaign != null) campaign.Changed -= Refresh;
            if (trackingHud != null) trackingHud.TrackingChanged -= ForceRefresh;
            if (input != null) input.PromptBindingsChanged -= UpdateClosePrompt;
            if (Instance == this) Instance = null;
            InputManager.CampaignInputBlocked = CampaignHUD.IsDocumentOpen || (campaign != null && campaign.BlocksInput);
        }

        private void Update()
        {
            if (trackingHud != CampaignHUD.Instance)
            {
                if (trackingHud != null) trackingHud.TrackingChanged -= ForceRefresh;
                trackingHud = CampaignHUD.Instance;
                if (trackingHud != null) trackingHud.TrackingChanged += ForceRefresh;
                forceRefresh = true;
            }
            if (input != InputManager.Instance)
            {
                if (input != null) input.PromptBindingsChanged -= UpdateClosePrompt;
                input = InputManager.Instance;
                if (input != null) input.PromptBindingsChanged += UpdateClosePrompt;
                UpdateClosePrompt();
            }
            if (campaign == null && CampaignMissionController.Instance != null)
            {
                campaign = CampaignMissionController.Instance;
                campaign.Changed += Refresh;
                Refresh();
            }
            if (IsOpen && (campaign == null || campaign.BlocksInput)) Close();
            if (pendingFile != CampaignFileId.None && campaign != null)
            {
                if (Time.unscaledTime > pendingUntil) pendingFile = CampaignFileId.None;
                else if (campaign.State.revision >= pendingRevision)
                {
                    CampaignFileId id = pendingFile; pendingFile = CampaignFileId.None;
                    if (campaign.State.HasFile(id) && CampaignHUD.Instance != null && CampaignHUD.Instance.CanAutoOpenFile()) OpenFile(id);
                }
            }
        }

        public void AcknowledgeFile(CampaignFileId id, uint revision)
        {
            pendingFile = id; pendingRevision = revision; pendingUntil = Time.unscaledTime + 3f;
        }

        public void CancelPendingFile() => pendingFile = CampaignFileId.None;

        private void ForceRefresh() { forceRefresh = true; Refresh(); }
        private void UpdateClosePrompt() => closeButton.GetComponentInChildren<TMP_Text>().text =
            "CLOSE [" + SurvivalHotbar.Key("Journal", "I") + " / " + SurvivalHotbar.Key("Pause", "ESC") + "]";

        public void Toggle()
        {
            if (root == null) return;
            if (root.activeSelf) Close(); else Open(tab);
        }

        public void OpenFile(CampaignFileId id)
        {
            if (root == null || campaign == null || !campaign.State.HasFile(id)) return;
            selectedFile = id;
            Open(Tab.Files);
            CampaignFileDefinition file = campaign?.ContentCatalog?.FindFile(id);
            if (file != null) SelectFile(file);
        }

        public void Close()
        {
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            InputManager.CampaignInputBlocked = CampaignHUD.IsDocumentOpen || (campaign != null && campaign.BlocksInput);
            bool overlay = InGameMenuUI.IsMenuOpen || CampaignHUD.IsDocumentOpen || CampaignStatusUI.IsDebriefOpen;
            Cursor.visible = overlay || previousCursorVisible;
            Cursor.lockState = overlay ? CursorLockMode.None : previousCursorLock;
            EventSystem.current?.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        private void Open(Tab nextTab)
        {
            if (campaign == null || campaign.BlocksInput || InGameMenuUI.IsMenuOpen) return;
            tab = nextTab;
            forceRefresh = true;
            if (!root.activeSelf)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLock = Cursor.lockState;
                previousSelection = EventSystem.current?.currentSelectedGameObject;
            }
            root.SetActive(true);
            InputManager.CampaignInputBlocked = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Refresh();
        }

        private void Refresh()
        {
            if (root == null || !root.activeSelf || campaign == null) return;
            viewedFiles &= campaign.State.discoveredFiles;
            if (!forceRefresh && displayedRevision == campaign.State.revision
                && displayedFiles == campaign.State.discoveredFiles && displayedObjectives == campaign.State.completed) return;
            forceRefresh = false;
            displayedRevision = campaign.State.revision;
            displayedFiles = campaign.State.discoveredFiles; displayedObjectives = campaign.State.completed;
            ClearRows();
            bool keyTab = tab == Tab.KeyItems;
            SetTabState(keyItemsTab, keyTab);
            SetTabState(filesTab, tab == Tab.Files);
            SetTabState(missionsTab, tab == Tab.Missions);
            title.text = "SQUAD INVENTORY";

            if (keyTab) PopulateKeyItems(); else if (tab == Tab.Files) PopulateFiles(); else PopulateMissions();
            if (rows.Count == 0)
            {
                AddEmptyRow(keyTab ? "NO KEY ITEMS ACQUIRED" : tab == Tab.Files ? "NO FILES RECORDED" : "NO MISSIONS AVAILABLE");
                ShowDetail(null, "", "", keyTab ? "Mission items acquired by the squad appear here." : tab == Tab.Files ? "Read notes and records in the world to add them here." : "Mission objectives appear when the squad enters a chapter.");
            }
            EventSystem.current?.SetSelectedGameObject(rows.FirstOrDefault(row => row.GetComponent<Button>().interactable) ?? closeButton.gameObject);
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
                    ShowDetail(icon, item.title, status, item.description));
                if (rows.Count == 1) row.onClick.Invoke();
            }
        }

        private void PopulateFiles()
        {
            CampaignFileDefinition[] definitions = campaign.ContentCatalog?.files;
            if (definitions == null) return;
            CampaignFileDefinition selection = null;
            foreach (CampaignFileDefinition file in definitions.Where(x => x != null)
                         .Where(x => campaign.State.HasFile(x.id)).OrderBy(x => x.chapter).ThenBy(x => x.order))
            {
                bool unread = (viewedFiles & CampaignState.FileBit(file.id)) == 0;
                string status = unread ? "NEW" : file.required ? "EVIDENCE" : "OPTIONAL";
                Button row = AddRow(file.title, status, file.icon != null ? file.icon : HudSkin.Get("File"), () => SelectFile(file));
                if (selection == null || file.id == selectedFile) selection = file;
            }
            if (selection != null) SelectFile(selection);
        }

        private void SelectFile(CampaignFileDefinition file)
        {
            viewedFiles |= CampaignState.FileBit(file.id);
            selectedFile = file.id;
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
                bool done = campaign.State.Has(objective.id);
                bool available = CampaignObjectiveSelection.CanTrack(campaign.State, objective.id)
                    && campaign.Objective(objective.id) != null;
                bool tracked = trackingHud != null && trackingHud.TrackedObjectiveId == objective.id;
                string state = done ? "COMPLETE" : tracked ? "TRACKING" : available ? "AVAILABLE" : "LOCKED";
                var row = AddRow(objective.title, state, MissionIcon(done, available), () =>
                {
                    if (available) trackingHud?.TryTrackObjective(objective.id);
                    ShowMission(objective);
                });
                HudSkin.StyleButton(row, tracked);
                if (tracked) row.transform.Find("Status").GetComponent<TMP_Text>().color = HudSkin.Accent;
                if (selection == null || tracked) selection = objective;
            }
            if (selection != null) ShowMission(selection);
        }

        private void ShowMission(CampaignObjectiveDefinition objective)
        {
            bool done = campaign.State.Has(objective.id);
            bool available = CampaignObjectiveSelection.CanTrack(campaign.State, objective.id);
            bool tracked = trackingHud != null && trackingHud.TrackedObjectiveId == objective.id;
            ShowDetail(MissionIcon(done, available), objective.title, done ? "COMPLETE" : tracked ? "TRACKING" : available ? "AVAILABLE" : "LOCKED",
                CampaignHUD.ObjectiveAction(objective.id) + "\n\n" + (done ? "Recorded for the squad."
                : available ? "Select this objective to follow it on your HUD. Tracking is local; progress is shared by the squad."
                : "Complete the preceding objectives or wait for the exploration phase."));
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

            root = Panel(HudLayout.PeripheralRoot(canvas), "TeamInventory", Vector2.zero, Vector2.one, new Color(0.015f, 0.02f, 0.018f, .94f)).gameObject;
            RectTransform shell = Panel(root.transform, "Shell", new Vector2(.10f, .09f), new Vector2(.90f, .91f), TacticalUiTheme.Surface);
            HudSkin.Apply(shell.GetComponent<Image>(), "InventoryBoard", new Color(.24f, .27f, .25f), 1f);
            var headerIcon = Image(shell, "HeaderIcon", new Vector2(.04f, .905f), new Vector2(.085f, .965f));
            headerIcon.sprite = HudSkin.Get("KeyItem"); headerIcon.preserveAspect = true;
            title = Text(shell, "Title", new Vector2(.10f, .895f), new Vector2(.72f, .97f), 36, FontStyles.Bold);
            title.color = HudSkin.Accent;

            keyItemsTab = TextButton(shell, "KeyItemsTab", "KEY ITEMS", new Vector2(.04f, .81f), new Vector2(.23f, .89f), () => { tab = Tab.KeyItems; forceRefresh = true; Refresh(); });
            filesTab = TextButton(shell, "FilesTab", "FILES", new Vector2(.24f, .81f), new Vector2(.43f, .89f), () => { tab = Tab.Files; selectedFile = CampaignFileId.None; forceRefresh = true; Refresh(); });
            missionsTab = TextButton(shell, "MissionsTab", "MISSIONS", new Vector2(.44f, .81f), new Vector2(.63f, .89f), () => { tab = Tab.Missions; ForceRefresh(); });
            closeButton = TextButton(shell, "Close", "CLOSE", new Vector2(.74f, .90f), new Vector2(.98f, .97f), Close);
            UpdateClosePrompt();

            RectTransform listViewport = Panel(shell, "ListViewport", new Vector2(.04f, .08f), new Vector2(.42f, .79f), new Color(0.025f, .035f, .032f, .96f));
            HudSkin.Apply(listViewport.GetComponent<Image>(), "Panel", HudSkin.PanelTint, 2f);
            listViewport.gameObject.AddComponent<RectMask2D>();
            listContent = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            listContent.SetParent(listViewport, false);
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(.5f, 1);
            listContent.offsetMin = new Vector2(12, 0); listContent.offsetMax = new Vector2(-12, 0);
            VerticalLayoutGroup layout = listContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 18, 18);
            layout.spacing = 8; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            listContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect listScroll = listViewport.gameObject.AddComponent<ScrollRect>();
            listScroll.content = listContent; listScroll.viewport = listViewport; listScroll.horizontal = false;

            RectTransform detail = Panel(shell, "Detail", new Vector2(.44f, .08f), new Vector2(.96f, .79f), new Color(.018f, .025f, .023f, .98f));
            HudSkin.Apply(detail.GetComponent<Image>(), "Panel", HudSkin.PanelTint, 2f);
            detailSlot = Image(detail, "IconSlot", new Vector2(.045f, .67f), new Vector2(.25f, .93f));
            HudSkin.Apply(detailSlot, "Slot", Color.white);
            detailIcon = Image(detail, "Icon", new Vector2(.075f, .70f), new Vector2(.22f, .90f));
            detailIcon.preserveAspect = true;
            detailTitle = Text(detail, "DetailTitle", new Vector2(.29f, .78f), new Vector2(.96f, .96f), 30, FontStyles.Bold);
            detailTitle.textWrappingMode = TextWrappingModes.Normal;
            detailStatus = Text(detail, "DetailStatus", new Vector2(.29f, .67f), new Vector2(.96f, .77f), 20, FontStyles.Bold);
            detailStatus.color = HudSkin.Accent;
            RectTransform bodyViewport = Panel(detail, "BodyViewport", new Vector2(.055f, .07f), new Vector2(.945f, .63f), Color.clear);
            bodyViewport.gameObject.AddComponent<RectMask2D>();
            detailBody = Text(bodyViewport, "Body", new Vector2(.03f, 1), new Vector2(.97f, 1), 23);
            detailBody.rectTransform.pivot = new Vector2(.5f, 1); detailBody.rectTransform.sizeDelta = new Vector2(0, 1);
            detailBody.enableWordWrapping = true; detailBody.overflowMode = TextOverflowModes.Overflow;
            ContentSizeFitter bodyFit = detailBody.gameObject.AddComponent<ContentSizeFitter>(); bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect bodyScroll = bodyViewport.gameObject.AddComponent<ScrollRect>();
            bodyScroll.content = detailBody.rectTransform; bodyScroll.viewport = bodyViewport; bodyScroll.horizontal = false;
            root.SetActive(false);
        }

        private Button AddRow(string label, string status, Sprite icon, Action clicked)
        {
            RectTransform row = Panel(listContent, label, Vector2.zero, Vector2.one, new Color(.10f, .12f, .11f, .96f));
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 112;
            Button button = row.gameObject.AddComponent<Button>(); HudSkin.StyleButton(button);
            button.onClick.AddListener(() =>
            {
                if (selectedRow != null) HudSkin.StyleButton(selectedRow);
                selectedRow = button; HudSkin.StyleButton(button, true); clicked();
            });
            Image image = Image(row, "Icon", new Vector2(.02f, .12f), new Vector2(.17f, .88f)); image.sprite = icon; image.preserveAspect = true; image.enabled = icon != null;
            float textLeft = icon != null ? .20f : .04f;
            TMP_Text name = Text(row, "Name", new Vector2(textLeft, .46f), new Vector2(.96f, .96f), 21, FontStyles.Bold); name.text = label;
            name.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text state = Text(row, "Status", new Vector2(textLeft, .08f), new Vector2(.96f, .39f), 16); state.text = status; state.color = status == "NEW" || status == "TRACKING" ? HudSkin.Accent : TacticalUiTheme.Muted;
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
            detailIcon.sprite = icon; detailIcon.enabled = icon != null;
            detailSlot.enabled = icon != null;
            detailTitle.rectTransform.anchorMin = new Vector2(icon != null ? .29f : .04f, .78f);
            detailStatus.rectTransform.anchorMin = new Vector2(icon != null ? .29f : .04f, .67f);
            detailTitle.text = heading; detailStatus.text = status; detailBody.text = body ?? "";
            detailBody.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void SetTabState(Button button, bool selected)
        {
            if (button?.targetGraphic != null) HudSkin.StyleButton(button, selected);
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
            text.fontSize = Mathf.Max(24, size); text.fontStyle = style; text.color = TacticalUiTheme.Text; text.raycastTarget = false; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
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
            RectTransform rect = Panel(parent, name, min, max, TacticalUiTheme.Control);
            Button button = rect.gameObject.AddComponent<Button>(); HudSkin.StyleButton(button); button.onClick.AddListener(() => action());
            TMP_Text text = Text(rect, "Label", new Vector2(.03f, .05f), new Vector2(.97f, .95f), 20, FontStyles.Bold); text.text = label; text.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
