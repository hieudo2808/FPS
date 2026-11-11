using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Campaign-only downed, phase, extraction and debrief presentation.</summary>
    public sealed class CampaignStatusUI : MonoBehaviour
    {
        private static SelectedUiTheme Theme => SelectedUiTheme.Current;
        public static CampaignStatusUI Instance { get; private set; }
        public static bool IsDebriefOpen => UiStateStack.Contains(UiScreen.Debrief);
        private CampaignMissionController campaign;
        private PlayerHealth owner;
        private Canvas canvas;
        private TMP_Text phaseText;
        private TMP_Text lifeText;
        private TMP_Text debriefText;
        private TMP_Text[] debriefStats;
        private TMP_Text debriefDiscovery;
        private TMP_Text debriefDiscoveryValues, debriefDescription, debriefFooter, debriefEnd;
        private GameObject debriefPanel;
        private Button retryButton;
        private TMP_Text retryLabel;
        private RectTransform retryKey;
        private TMP_Text[] debriefTexts;
        private float[] debriefBaseSizes;
        private float debriefScale;
        private float debriefSafeWidth;
        private RectTransform debriefContent;

        private void Awake() { Instance = this; Build(); }
        private void OnEnable()
        {
            HudSignals.SourcesChanged += BindSources;
            InputManager.InputStateChanged += Render;
            UiStateStack.Changed += Render;
            BindSources();
        }
        private void OnDisable()
        {
            HudSignals.SourcesChanged -= BindSources;
            InputManager.InputStateChanged -= Render;
            UiStateStack.Changed -= Render;
            UnbindSources(); CancelInvoke(nameof(Render)); UiStateStack.Remove(UiScreen.Debrief);
            if (canvas != null) canvas.enabled = false;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void LateUpdate()
        {
            if (!debriefPanel.activeInHierarchy || canvas.scaleFactor <= 0 || Screen.safeArea.width <= 0) return;
            if (Mathf.Approximately(debriefScale, canvas.scaleFactor) && Mathf.Approximately(debriefSafeWidth, Screen.safeArea.width)) return;
            debriefScale = canvas.scaleFactor;
            debriefSafeWidth = Screen.safeArea.width;
            float minimum = Theme.Small * debriefScale >= TacticalUiTheme.MinimumReadableTextPixels ? Theme.Small : Theme.Body;
            for (int index = 0; index < debriefTexts.Length; index++)
                debriefTexts[index].fontSize = Mathf.Max(debriefBaseSizes[index], minimum);
            var retryLabel = retryButton.GetComponentInChildren<TMP_Text>();
            retryKey.anchoredPosition = new Vector2(-retryLabel.GetPreferredValues(retryLabel.text).x - 12, 0);
            float labelWidth = debriefTexts.Where(text => text.name.StartsWith("StatisticLabel"))
                .Max(text => text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x);
            float parentWidth = ((RectTransform)debriefContent.parent).rect.width;
            float width = Mathf.Clamp01(Mathf.Max(.448f, 600f / debriefSafeWidth, (labelWidth / .22f + 16) / parentWidth));
            debriefContent.anchorMin = new Vector2(.5f - width / 2, 226f/1080);
            debriefContent.anchorMax = new Vector2(.5f + width / 2, 910f/1080);
        }
        private void UnbindSources()
        {
            if (campaign != null) campaign.Changed -= Refresh;
            if (owner != null) owner.LifeStateChanged -= OnLifeChanged;
        }
        private void BindSources()
        {
            UnbindSources();
            campaign = CampaignMissionController.Instance; owner = PlayerHealth.LocalInstance;
            if (campaign != null) campaign.Changed += Refresh;
            if (owner != null) owner.LifeStateChanged += OnLifeChanged;
            Refresh();
        }
        private void OnLifeChanged(PlayerLifeState before, PlayerLifeState after) => Refresh();
        private void Refresh()
        {
            CancelInvoke(nameof(Render)); Render();
            if (campaign != null && owner != null && (owner.LifeState == PlayerLifeState.Downed
                || campaign.State.phase is CampaignPhase.Encounter or CampaignPhase.Completed))
                InvokeRepeating(nameof(Render), .25f, .25f);
        }

        private void Render()
        {
            if (campaign == null || owner == null) { canvas.enabled = false; UiStateStack.Remove(UiScreen.Debrief); return; }
            canvas.enabled = !InGameMenuUI.IsMenuOpen && !TeamInventoryUI.IsOpen && !InputManager.CinematicInputBlocked;
            if (!canvas.enabled) return;

            float remaining = campaign.Remaining;
            phaseText.gameObject.SetActive(CampaignHUD.Instance == null && (campaign.State.phase is CampaignPhase.Preparing or CampaignPhase.Encounter or CampaignPhase.AwaitingParty));
            phaseText.text = campaign.State.phase switch
            {
                CampaignPhase.Preparing => CampaignRules.PartyGateText(campaign.State.partyGate, true),
                CampaignPhase.Encounter => $"EXTRACTION  ·  {Mathf.CeilToInt(remaining)}s",
                CampaignPhase.AwaitingParty => CampaignRules.PartyGateText(campaign.State.partyGate, false),
                _ => ""
            };

            bool downed = owner.LifeState == PlayerLifeState.Downed;
            bool dead = owner.LifeState is PlayerLifeState.Dead or PlayerLifeState.Spectating;
            lifeText.gameObject.SetActive(downed || dead);
            if (downed)
                lifeText.text = $"DOWNED\nBLEED OUT IN {Mathf.Max(0, Mathf.CeilToInt((float)(owner.LifeStateDeadline - campaign.Now)))}\nWAIT FOR REVIVE";
            else if (dead)
                lifeText.text = campaign.State.phase == CampaignPhase.Failed ? "MISSION FAILED" : "SPECTATING\nFIRE / AIM  ·  CHANGE OPERATOR";

            bool finished = campaign.State.phase is CampaignPhase.Completed or CampaignPhase.Failed;
            finished &= campaign.State.phase != CampaignPhase.Completed || campaign.Now - campaign.PhaseStarted >= campaign.settings.endingSeconds;
            finished &= campaign.RunSummary != null;
            if (finished) UiStateStack.Push(UiScreen.Debrief, debriefPanel, () => {});
            else UiStateStack.Remove(UiScreen.Debrief);
            retryButton.gameObject.SetActive(finished && (campaign.State.phase == CampaignPhase.Completed || campaign.IsServer));
            if (finished) UiStateStack.RefreshFocus(UiScreen.Debrief);
            if (finished)
            {
                CampaignRunSummary summary = campaign.RunSummary;
                if (summary == null)
                {
                    debriefPanel.SetActive(false);
                    return;
                }
                int optionalTotal = summary.optionalFilesTotal > 0 ? summary.optionalFilesTotal
                    : campaign.ContentCatalog?.files?.Count(f => f != null && !f.required) ?? 0;
                RenderSummary(summary, optionalTotal);
            }
        }

        private void RenderSummary(CampaignRunSummary summary, int optionalTotal)
        {
            bool failed = summary.result == CampaignPhase.Failed;
            debriefText.text = failed ? "MISSION FAILED" : "MISSION COMPLETE";
            debriefText.color = failed ? Theme.Danger : Theme.Text;
            debriefDescription.text = failed ? "The squad did not complete the mission." : "Mission summary";
            debriefStats[0].text = FormatTime(summary.durationSeconds);
            debriefStats[1].text = $"{summary.survivors}/{summary.rosterSize}";
            debriefStats[2].text = summary.teamDowns.ToString();
            debriefStats[3].text = summary.zombieKills.ToString();
            debriefDiscovery.text = "Optional files\nFactory case\nLab case";
            debriefDiscoveryValues.text = $"{summary.optionalFilesFound} / {optionalTotal}\n"
                + $"{(summary.factoryCaseSecured ? "Secured" : "Not secured")}\n"
                + $"{(summary.labCaseSecured ? "Secured" : "Not secured")}";
            debriefFooter.text = failed ? "Return to the last checkpoint." : "Return to menu.";
            retryLabel.text = failed ? "RETRY CHECKPOINT" : campaign != null && campaign.IsServer ? "END SESSION" : "RETURN TO MENU";
            debriefEnd.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(!failed || campaign != null && campaign.IsServer);
        }

        private void OnDebriefAction()
        {
            if (campaign?.State.phase == CampaignPhase.Failed) campaign.RequestRetry();
            else if (campaign?.State.phase == CampaignPhase.Completed) NetworkGameManager.Instance?.Disconnect();
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static int CountFiles(ulong mask)
        {
            int count = 0;
            while (mask != 0) { count += (int)(mask & 1); mask >>= 1; }
            return count;
        }

        private void Build()
        {
            GameObject canvasObject = new("CampaignStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = TacticalUiTheme.CampaignOrder + 1;
            HudLayout.ConfigureCanvas(canvas);
            var safe = HudLayout.PeripheralRoot(canvas);
            phaseText = Text(canvasObject.transform, "Phase", new Vector2(.32f, .87f), new Vector2(.68f, .95f), 24, FontStyles.Bold); phaseText.alignment = TextAlignmentOptions.Center;
            lifeText = Text(safe, "LifeState", new Vector2(.22f, .38f), new Vector2(.78f, .63f), 32, FontStyles.Bold); lifeText.alignment = TextAlignmentOptions.Center; lifeText.color = Theme.Danger;
            phaseText.font = lifeText.font = HudSkin.FontAsset(true);
            debriefPanel = new GameObject("Debrief", typeof(RectTransform), typeof(Image));
            debriefPanel.transform.SetParent(safe, false);
            RectTransform backdrop = debriefPanel.GetComponent<RectTransform>(); backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = Theme.CampaignBackdrop;
            var panelObject = new GameObject("DebriefContent", typeof(RectTransform), typeof(Image)); panelObject.transform.SetParent(backdrop, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>(); panel.anchorMin = new Vector2(.276f, .21f); panel.anchorMax = new Vector2(.724f, .843f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            debriefContent = panel;
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = Theme.Panel;
            panelImage.type = panelImage.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            panelImage.color = SelectedUiTheme.SurfaceTint(Theme.PanelAlpha);
            var eyebrow = Text(panel, "Eyebrow", new Vector2(.06f, .89f), new Vector2(.94f, .95f), 18, FontStyles.Normal);
            eyebrow.text = "CAMPAIGN / DEBRIEF"; eyebrow.color = Theme.Muted;
            debriefText = Text(panel, "Summary", new Vector2(.06f, .80f), new Vector2(.94f, .89f), 36, FontStyles.Bold);
            debriefDescription = Text(panel, "Description", new Vector2(.06f, .75f), new Vector2(.94f, .80f), 18, FontStyles.Normal); debriefDescription.color = Theme.Muted;
            debriefStats = new TMP_Text[4];
            string[] statNames = { "TIME", "SURVIVORS", "TEAM DOWNS", "ELIMINATED" };
            for (int index = 0; index < debriefStats.Length; index++)
            {
                float left = .06f + index * .225f;
                var label = Text(panel, "StatisticLabel" + index, new Vector2(left, .64f), new Vector2(left + .22f, .69f), 18, FontStyles.Normal);
                label.text = statNames[index]; label.color = Theme.Muted;
                debriefStats[index] = Text(panel, "Statistic" + index, new Vector2(left, .54f), new Vector2(left + .22f, .64f), 36, FontStyles.Bold);
                debriefStats[index].font = HudSkin.FontAsset(true);
            }
            var discoveryLabel = Text(panel, "DiscoveryLabel", new Vector2(.06f, .42f), new Vector2(.94f, .47f), 18, FontStyles.Normal); discoveryLabel.text = "DISCOVERY"; discoveryLabel.color = Theme.Muted;
            debriefDiscovery = Text(panel, "Discovery", new Vector2(.06f, .20f), new Vector2(.58f, .40f), 23, FontStyles.Normal);
            debriefDiscoveryValues = Text(panel, "DiscoveryValues", new Vector2(.58f, .20f), new Vector2(.94f, .40f), 23, FontStyles.Normal);
            debriefDiscovery.lineSpacing = debriefDiscoveryValues.lineSpacing = 16;
            debriefDiscoveryValues.alignment = TextAlignmentOptions.TopRight; debriefDiscoveryValues.color = Theme.Muted;
            debriefFooter = Text(panel, "Footer", new Vector2(.06f, .06f), new Vector2(.53f, .11f), 18, FontStyles.Normal); debriefFooter.color = Theme.Muted;
            debriefEnd = Text(panel, "EndOfSummary", new Vector2(.57f, .06f), new Vector2(.94f, .11f), 18, FontStyles.Normal); debriefEnd.text = "END OF SUMMARY"; debriefEnd.color = Theme.Muted; debriefEnd.alignment = TextAlignmentOptions.TopRight;
            Divider(panel, "HeaderDivider", .73f); Divider(panel, "StatisticsDivider", .53f); Divider(panel, "FooterDivider", .17f);
            var retry = new GameObject("RetryCheckpoint", typeof(RectTransform), typeof(Image), typeof(Button));
            retry.transform.SetParent(panel, false);
            var retryRect = retry.GetComponent<RectTransform>(); retryRect.anchorMin = new Vector2(.55f, .045f); retryRect.anchorMax = new Vector2(.94f, .13f); retryRect.offsetMin = retryRect.offsetMax = Vector2.zero;
            var retryImage = retry.GetComponent<Image>(); retryImage.color = Color.clear;
            retryButton = retry.GetComponent<Button>();
            retryButton.onClick.AddListener(OnDebriefAction);
            retryLabel = Text(retry.transform, "Label", Vector2.zero, Vector2.one, 18, FontStyles.Normal); retryLabel.text = "RETRY CHECKPOINT"; retryLabel.alignment = TextAlignmentOptions.MidlineRight; retryLabel.enableWordWrapping = false;
            var key = new GameObject("ReturnKey", typeof(RectTransform), typeof(Image)); key.transform.SetParent(retry.transform, false);
            retryKey = key.GetComponent<RectTransform>(); retryKey.anchorMin = retryKey.anchorMax = new Vector2(1, .5f); retryKey.pivot = new Vector2(1, .5f); retryKey.sizeDelta = new Vector2(34, 31);
            var keyImage = key.GetComponent<Image>(); keyImage.sprite = Theme.ReturnKey; keyImage.preserveAspect = true; keyImage.raycastTarget = false;
            retryButton.targetGraphic = retryLabel;
            var retryColors = retryButton.colors; retryColors.normalColor = Theme.Text; retryColors.highlightedColor = Theme.Accent; retryColors.selectedColor = Theme.Text; retryColors.pressedColor = Theme.Muted; retryButton.colors = retryColors;
            SelectedUiTheme.Place(panel, 530, 170, 860, 684, 1920, 1080);
            debriefTexts = debriefPanel.GetComponentsInChildren<TMP_Text>(true);
            debriefBaseSizes = debriefTexts.Select(text => text.fontSize).ToArray();
            phaseText.gameObject.SetActive(false); lifeText.gameObject.SetActive(false); debriefPanel.SetActive(false);
        }

        private static void Divider(Transform parent, string name, float y)
        {
            var line = new GameObject(name, typeof(RectTransform), typeof(Image)); line.transform.SetParent(parent, false);
            var rect = line.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.06f, y); rect.anchorMax = new Vector2(.94f, y + .002f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = line.GetComponent<Image>(); image.sprite = HudSkin.Get("Fill"); image.color = Theme.Border; image.raycastTarget = false;
        }

        private static TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max, float size, FontStyles style)
        {
            TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>(); text.transform.SetParent(parent, false); RectTransform rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; text.font = Theme.BodyFont; text.fontSize = Theme.Size(size); text.fontStyle = style; text.color = Theme.Text; text.raycastTarget = false; text.enableWordWrapping = true; return text;
        }
    }
}
