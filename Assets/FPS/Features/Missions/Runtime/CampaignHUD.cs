using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace FPS
{
    public sealed partial class CampaignHUD : MonoBehaviour
    {
        private static SelectedUiTheme Theme => SelectedUiTheme.Current;
        public static CampaignHUD Instance { get; private set; }
        public static event Action InteractionMessageChanged;
        public static bool IsDocumentOpen => UiStateStack.Contains(UiScreen.Document);
        public bool HasInteractionMessage => progress != null && progress.isActiveAndEnabled && !string.IsNullOrWhiteSpace(progress.text);
        private Canvas canvas;
        private RectTransform panel, controls;
        private GameObject confirmation;
        private Text body, feedback, progress, subtitle;
        private TMP_Text status;
        private Image holdRing;
        private RectTransform subtitleBand;
        private Image subtitlePlate;
        private Text closeLabel;
        private RectTransform documentViewport;
        private GameObject deviceSurface, deviceBackdrop;
        private GameObject deviceClose;
        private TMP_Text deviceCloseLabel, deviceCloseKey;
        private TMP_Text deviceTitle, deviceBody, deviceState, deviceChapter, deviceSubtitle, deviceSource, deviceAction, deviceFooter, deviceHint, deviceBreadcrumb;
        private TMP_Text deviceSystemCaption, deviceInstructionHeading, deviceControlHeading;
        private Image deviceLock, deviceProgress, deviceProgressTrack;
        private RectTransform deviceViewport;
        private GameObject deviceInstructionCard, deviceControlCard, manifestSidebar, manifestPage;
        private GameObject deviceNotice;
        private TMP_Text deviceNoticeHeading, deviceNoticeValue, deviceNoticeHint, deviceScrollHint;
        private TMP_Text manifestPageTitle;
        private ulong deviceFiles, deviceCompleted;
        private CampaignPhase devicePhase;
        private CampaignChapter deviceCampaignChapter;
        private float subtitleUntil;
        private readonly System.Collections.Generic.List<CampaignDialogueDefinition> pendingDialogue = new();
        private AudioSource dialogueAudio;
        private CampaignInteractable focused;
        private bool journal, aidHeld, holding, mouseHeld;
        private bool releaseBeforeHold;
        private int first, second;
        private float holdStart, nextHeartbeat, nextAidHeartbeat, messageUntil;
        private readonly CampaignHintProgress hintProgress = new();
        private CampaignInteractable nearbyHint;
        private readonly string[] hintText = new string[3];
        private bool hintPaused, interactionMessageVisible;
        private float nextThreatCheck, combatUntil;
        private bool nearbyThreat;
        private Font font;
        private string message = "";
        private PlayerHealth Owner => PlayerHealth.LocalInstance;
        private CampaignMissionController Campaign => CampaignMissionController.Instance;
        private CampaignMissionController boundCampaign;
        private PlayerHealth boundOwner;
        private InputManager boundInput;
        private Vector2 layoutSize;
        private SelectedUiTypography selectedTypography;
        public float InteractionBandBottom { get; private set; } = HudLayout.InteractionBottom;
        private void Awake() { Instance = this; Build(); HudSignals.NotifySourcesChanged(); }
        private void OnEnable()
        {
            HudSkin.PanelOpacityChanged += ApplyTrackerOpacity;
            ApplyTrackerOpacity(HudSkin.PanelOpacity);
            HudSignals.SourcesChanged += BindSources;
            UiStateStack.Changed += RefreshPresentation;
            InputManager.InputStateChanged += RefreshPresentation;
            BindSources();
        }
        private void OnDisable()
        {
            HudSkin.PanelOpacityChanged -= ApplyTrackerOpacity;
            HudSignals.SourcesChanged -= BindSources;
            UiStateStack.Changed -= RefreshPresentation;
            InputManager.InputStateChanged -= RefreshPresentation;
            UnbindSources(); CancelInvoke(); ClearMessage(); Close(); InputManager.CampaignInputBlocked = false;
            if (canvas != null) canvas.enabled = false;
            SetInteractionText("");
        }
        private void OnDestroy() { if (Instance == this) { Instance = null; InteractionMessageChanged?.Invoke(); } }
        private void ApplyTrackerOpacity(float opacity)
        {
            if (tracker != null) HudSkin.SetPanelAlpha(tracker.GetComponent<Image>(), 1f, opacity);
        }
        private void LateUpdate()
        {
            if (deviceScrollHint != null)
                deviceScrollHint.gameObject.SetActive(deviceInstructionCard.activeInHierarchy
                    && deviceBody.preferredHeight > deviceViewport.rect.height + 1);
        }
        private void UnbindSources()
        {
            if (boundCampaign != null) boundCampaign.Changed -= RefreshCampaign;
            if (boundInput != null) boundInput.PromptBindingsChanged -= RefreshPresentation;
            if (boundOwner != null) boundOwner.CombatAvailabilityChanged -= OnCombatChanged;
            if (boundOwner != null) boundOwner.DirectionalDamageReceived -= OnDamageReceived;
        }
        private void BindSources()
        {
            UnbindSources();
            boundCampaign = Campaign; boundOwner = Owner; boundInput = InputManager.Instance;
            if (boundCampaign != null) boundCampaign.Changed += RefreshCampaign;
            if (boundInput != null) boundInput.PromptBindingsChanged += RefreshPresentation;
            if (boundOwner != null) boundOwner.CombatAvailabilityChanged += OnCombatChanged;
            if (boundOwner != null) boundOwner.DirectionalDamageReceived += OnDamageReceived;
            RefreshCampaign();
        }
        private void OnCombatChanged(bool available)
        {
            hintPaused = !available;
            if (!available) Close();
            RefreshCampaign();
        }
        private void OnDamageReceived(Vector3 direction) => combatUntil = Time.unscaledTime + 10;
        private void RefreshHintTarget()
        {
            nearbyHint = focused != null ? focused : Campaign?.Objective(TrackedObjectiveId);
            if (Campaign == null) nearbyHint = null;
            var objective = nearbyHint != null ? nearbyHint.objectiveId : CampaignObjectiveId.None;
            hintProgress.Observe(Campaign?.State, objective);
            var definition = Campaign?.ContentCatalog?.FindObjective(objective);
            for (int i = 0; i < hintText.Length; i++)
                hintText[i] = CampaignRules.HintText(Campaign?.State, definition, 45 + i * 45);
        }
        private void RefreshCampaign()
        {
            CancelInvoke(nameof(RefreshTracker));
            InputManager.CampaignInputBlocked = Campaign != null && Campaign.BlocksInput;
            if (Campaign != null)
            {
                if (shippingErrors != CampaignShippingGroup.None && (shippingErrorFiles != Campaign.State.discoveredFiles
                    || shippingErrorCompleted != Campaign.State.completed || shippingErrorPhase != Campaign.State.phase))
                {
                    ClearShippingErrors();
                    if (focused?.objectiveId == CampaignObjectiveId.FactoryShipping && panel.gameObject.activeSelf) { CancelHold(); RenderDocument(); }
                }
                ResolveTracking(Owner);
                if (Owner != null) RenderPhaseControls();
                if (focused != null && panel.gameObject.activeSelf && !holding && DeviceStateChanged())
                    RenderDocument();
                if (Campaign.State.phase is CampaignPhase.Encounter or CampaignPhase.Preparing)
                    InvokeRepeating(nameof(RefreshTracker), 1f, 1f);
            }
            hintPaused = Owner == null || !Owner.CanUseCombat;
            RefreshHintTarget();
            RefreshPresentation();
        }
        private void RefreshTracker() { if (Campaign != null) UpdateTracker(Owner); }
        private void RefreshPresentation()
        {
            if (canvas == null) return;
            canvas.enabled = !InGameMenuUI.IsMenuOpen;
            closeLabel.text = "CLOSE [" + SurvivalHotbar.Key("Journal", "I") + "]";
            deviceCloseLabel.text = "CLOSE";
            deviceCloseKey.text = SurvivalHotbar.Key("Journal", "I");
            bool overlay = TeamInventoryUI.IsOpen || IsDocumentOpen || InputManager.CinematicInputBlocked || CampaignStatusUI.IsDebriefOpen;
            if (overlay)
            {
                subtitleBand.anchorMin = new Vector2(.06f, 0); subtitleBand.anchorMax = new Vector2(.94f, 0);
                subtitleBand.pivot = new Vector2(.5f, 0); subtitleBand.anchoredPosition = new Vector2(0, 16);
                subtitleBand.sizeDelta = new Vector2(0, 110);
            }
            else HudLayout.CentralBand(subtitleBand, canvas, HudLayout.SubtitleBottom, HudLayout.SubtitleHeight);
            subtitleBand.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(overlay ? 110 : HudLayout.SubtitleHeight, subtitle.preferredHeight + 32));
            InteractionBandBottom = Mathf.Max(HudLayout.InteractionBottom,
                HudLayout.SubtitleBottom + subtitleBand.rect.height + 16);
            float promptHeight = HUDManager.HasInstance ? HUDManager.Instance.RefreshInteractionLayout(InteractionBandBottom) : HudLayout.InteractionHeight;
            HudLayout.CentralBand(progress.rectTransform, canvas, InteractionBandBottom, Mathf.Max(HudLayout.InteractionHeight, promptHeight));
            feedback.transform.SetParent(IsDocumentOpen ? panel : canvas.transform, false);
            if (IsDocumentOpen)
            {
                feedback.rectTransform.anchorMin = new Vector2(.04f, .30f); feedback.rectTransform.anchorMax = new Vector2(.96f, .30f);
                feedback.rectTransform.pivot = new Vector2(.5f, 0); feedback.rectTransform.anchoredPosition = Vector2.zero;
                feedback.rectTransform.sizeDelta = new Vector2(0, HudLayout.FeedbackHeight);
            }
            else HudLayout.CentralBand(feedback.rectTransform, canvas,
                Mathf.Max(HudLayout.FeedbackBottom, InteractionBandBottom + progress.rectTransform.rect.height + 12), HudLayout.FeedbackHeight);
            feedback.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(HudLayout.FeedbackHeight, feedback.preferredHeight));
            documentViewport.anchorMin = new Vector2(.04f, IsDocumentOpen ? Mathf.Clamp(.30f
                + (feedback.rectTransform.rect.height + 12) / Mathf.Max(1, panel.rect.height), .36f, .8f) : .36f);
            progress.enabled = HudLayout.GameplayVisible;
            feedback.enabled = HudLayout.GameplayVisible || (IsDocumentOpen && !TeamInventoryUI.IsOpen
                && !InputManager.CinematicInputBlocked && !CampaignStatusUI.IsDebriefOpen);
            if (IsDocumentOpen && deviceSurface.activeSelf)
            {
                feedback.transform.SetParent(deviceSurface.transform, false);
                bool showFeedback = !string.IsNullOrWhiteSpace(feedback.text) && !feedback.text.StartsWith("Next:", StringComparison.OrdinalIgnoreCase);
                feedback.enabled = showFeedback;
                deviceFooter.enabled = !showFeedback;
                var surfaceRect = (RectTransform)deviceSurface.transform;
                SelectedUiTheme.Place(feedback.rectTransform, 28, surfaceRect.rect.height - 43, surfaceRect.rect.width * .65f, 26, surfaceRect.rect.width, surfaceRect.rect.height);
            }
            if (!HudLayout.GameplayVisible) marker.gameObject.SetActive(false);
            NotifyInteractionMessage();
            RefreshTracker();
        }
        private void SetInteractionText(string text)
        {
            if (progress == null) return;
            if (progress.text != text) progress.text = text;
            NotifyInteractionMessage();
        }
        private void NotifyInteractionMessage()
        {
            bool visible = HasInteractionMessage;
            if (interactionMessageVisible == visible) return;
            interactionMessageVisible = visible;
            InteractionMessageChanged?.Invoke();
        }
        private void ShowDocument() => UiStateStack.Push(UiScreen.Document, panel.gameObject, Close);
        private void Build()
        {
            font = Theme.LegacyBody;
            var c = new GameObject("CampaignCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AudioSource));
            c.transform.SetParent(transform, false); canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = TacticalUiTheme.CampaignOrder;
            dialogueAudio = c.GetComponent<AudioSource>(); dialogueAudio.playOnAwake = false; dialogueAudio.spatialBlend = 0;
            HudLayout.ConfigureCanvas(canvas);
            selectedTypography = c.AddComponent<SelectedUiTypography>();
            var safe = HudLayout.PeripheralRoot(canvas);
            BuildNavigation(safe);
            feedback = Label(safe, "Feedback", Vector2.zero, Vector2.one, 24); feedback.alignment = TextAnchor.MiddleCenter;
            progress = Label(safe, "Interaction", Vector2.zero, Vector2.one, 26); progress.alignment = TextAnchor.MiddleCenter;
            holdRing = Box(safe, "HoldProgress", Vector2.zero, Vector2.one).GetComponent<Image>();
            HudSkin.Apply(holdRing, "FPSSample/GoalProgress", HudSkin.Accent);
            holdRing.type = Image.Type.Filled; holdRing.fillMethod = Image.FillMethod.Radial360; holdRing.fillOrigin = 2; holdRing.fillClockwise = true;
            holdRing.rectTransform.anchorMin = holdRing.rectTransform.anchorMax = holdRing.rectTransform.pivot = new Vector2(.5f, .5f);
            holdRing.rectTransform.anchoredPosition = Vector2.zero; holdRing.rectTransform.sizeDelta = new Vector2(64, 64); holdRing.gameObject.SetActive(false);
            var subtitles = new GameObject("SubtitlesLayer", typeof(RectTransform), typeof(Canvas));
            subtitles.transform.SetParent(safe, false);
            var subtitleRect = subtitles.GetComponent<RectTransform>(); subtitleRect.anchorMin = Vector2.zero; subtitleRect.anchorMax = Vector2.one; subtitleRect.offsetMin = subtitleRect.offsetMax = Vector2.zero;
            var subtitleCanvas = subtitles.GetComponent<Canvas>(); subtitleCanvas.overrideSorting = true; subtitleCanvas.sortingOrder = TacticalUiTheme.CampaignOrder + 3;
            subtitleBand = Box(subtitles.transform, "SubtitleBand", Vector2.zero, Vector2.one);
            subtitlePlate = subtitleBand.GetComponent<Image>(); HudSkin.Apply(subtitlePlate, "Panel", HudLayout.Plate);
            subtitlePlate.raycastTarget = false; subtitlePlate.enabled = false;
            subtitle = Label(subtitleBand,"MandatorySubtitles",new Vector2(.02f,.02f),new Vector2(.98f,.98f),26);subtitle.alignment=TextAnchor.MiddleCenter;subtitle.fontStyle=FontStyle.Bold;
            var outline=subtitle.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);
            deviceBackdrop = Box(safe, "DeviceBackdrop", Vector2.zero, Vector2.one).gameObject;
            deviceBackdrop.GetComponent<Image>().color = Theme.CampaignBackdrop;
            deviceBackdrop.SetActive(false);
            panel = Box(safe, "JournalAndDocument", new Vector2(.18f,.12f), new Vector2(.82f,.87f));
            HudSkin.Apply(panel.GetComponent<Image>(), "Panel", HudSkin.PanelTint, 2f);
            BuildDeviceSurface();
            documentViewport=Box(panel,"DocumentViewport",new Vector2(.04f,.36f),new Vector2(.96f,.93f));
            documentViewport.GetComponent<Image>().color=Color.clear;documentViewport.GetComponent<Image>().raycastTarget=true;documentViewport.gameObject.AddComponent<RectMask2D>();
            body = Label(documentViewport, "Document", Vector2.zero, Vector2.one, 23);
            body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=Vector2.one;body.rectTransform.pivot=new Vector2(0,1);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll=documentViewport.gameObject.AddComponent<ScrollRect>();scroll.content=body.rectTransform;scroll.viewport=documentViewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            controls = Box(panel, "Controls", new Vector2(.04f,.07f), new Vector2(.96f,.29f)); controls.GetComponent<Image>().color = Color.clear;
            closeLabel = Button(panel, "CLOSE", new Vector2(.65f,.94f), new Vector2(.98f,.995f), Close).GetComponentInChildren<Text>();
            deviceClose = JournalButton(safe, "CLOSE DEVICE", new Vector2(.88f, .025f), new Vector2(.98f, .07f), Close).gameObject;
            deviceCloseLabel = deviceClose.GetComponentInChildren<TMP_Text>();
            deviceCloseLabel.fontSize = Theme.Small; deviceCloseLabel.alignment = TextAlignmentOptions.Right; deviceClose.SetActive(false);
            SelectedUiTheme.Place((RectTransform)deviceClose.transform, 1745, 1019, 135, 30, 1920, 1080);
            var closeKeycap = Box(deviceClose.transform, "CloseKeycap", Vector2.zero, Vector2.one);
            SelectedUiTheme.Place(closeKeycap, 0, 0, 27, 30, 135, 30);
            SelectedUiTheme.Surface(closeKeycap.GetComponent<Image>(), Theme.Keycap);
            deviceCloseKey = DeviceText(closeKeycap, "CloseKey", Vector2.zero, Vector2.one, 18, FontStyles.Normal);
            deviceCloseKey.alignment = TextAlignmentOptions.Center;
            panel.gameObject.SetActive(false);
            feedback.transform.SetParent(c.transform, false);
            holdRing.transform.SetParent(c.transform, false);
        }
        private RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero;
            go.GetComponent<Image>().color = TacticalUiTheme.Surface; go.GetComponent<Image>().raycastTarget = false; return r;
        }
        private void BuildDeviceSurface()
        {
            var surface = Box(panel, "DeviceSurface", new Vector2(.047f, .10f), new Vector2(.953f, .935f));
            deviceSurface = surface.gameObject;
            var background = surface.GetComponent<Image>();
            background.sprite = Theme.Panel;
            background.type = background.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            background.color = Theme.Text;
            deviceInstructionCard = DeviceCard(surface, "DeviceInstructions", new Vector2(.031f, .237f), new Vector2(.606f, .702f));
            deviceControlCard = DeviceCard(surface, "DeviceControls", new Vector2(.626f, .237f), new Vector2(.970f, .702f));
            deviceNotice = DeviceCard(surface, "DeviceNotice", new Vector2(.05f, .33f), new Vector2(.588f, .48f));
            deviceNoticeHeading = DeviceText(deviceNotice.transform, "DeviceNoticeHeading", new Vector2(.035f, .70f), new Vector2(.965f, .94f), 18, FontStyles.Normal);
            deviceNoticeHeading.color = Theme.Muted;
            deviceNoticeValue = DeviceText(deviceNotice.transform, "DeviceNoticeValue", new Vector2(.035f, .36f), new Vector2(.965f, .70f), 23, FontStyles.Bold);
            deviceNoticeHint = DeviceText(deviceNotice.transform, "DeviceNoticeHint", new Vector2(.035f, .08f), new Vector2(.965f, .35f), 18, FontStyles.Normal);
            manifestSidebar = DeviceCard(surface, "ManifestSidebar", new Vector2(.045f, .18f), new Vector2(.23f, .85f));
            var manifestLabel = DeviceText(manifestSidebar.transform, "RecordsLabel", new Vector2(.10f, .86f), new Vector2(.9f, .95f), 18, FontStyles.Normal);
            manifestLabel.text = "RECORDS"; manifestLabel.color = Theme.Muted;
            var manifestSelection = DeviceText(manifestSidebar.transform, "DeliveryRecords", new Vector2(.10f, .70f), new Vector2(.9f, .80f), 23, FontStyles.Bold);
            manifestSelection.text = "Delivery records";
            manifestPage = Box(surface, "ManifestPageGreybox", new Vector2(.26f, .22f), new Vector2(.88f, .69f)).gameObject;
            manifestPage.GetComponent<Image>().color = Theme.Text;
            manifestPageTitle = DeviceText(manifestPage.transform, "ManifestPageTitle", new Vector2(.04f, .85f), new Vector2(.96f, .95f), 23, FontStyles.Bold);
            manifestPageTitle.color = Theme.Background;
            deviceChapter = DeviceText(surface, "DeviceChapter", new Vector2(.031f, .94f), new Vector2(.45f, .98f), 24, FontStyles.Bold);
            deviceSubtitle = DeviceText(surface, "DeviceSubtitle", new Vector2(.031f, .91f), new Vector2(.45f, .94f), 18, FontStyles.Normal);
            deviceSubtitle.color = Theme.Muted;
            var access = DeviceText(surface, "DeviceAccess", new Vector2(.60f, .91f), new Vector2(.970f, .98f), 18, FontStyles.Normal);
            access.text = "ON-SITE SYSTEM\nLOCAL ACCESS"; access.color = Theme.Muted; access.alignment = TextAlignmentOptions.TopRight;
            deviceTitle = DeviceText(surface, "DeviceTitle", new Vector2(.075f, .725f), new Vector2(.70f, .79f), 36, FontStyles.Bold);
            deviceState = DeviceText(surface, "DeviceState", new Vector2(.626f, .72f), new Vector2(.970f, .78f), 18, FontStyles.Normal);
            deviceState.color = Theme.Muted; deviceState.alignment = TextAlignmentOptions.BottomRight;
            deviceBreadcrumb = DeviceText(surface, "DeviceBreadcrumb", new Vector2(.031f, .80f), new Vector2(.70f, .84f), 18, FontStyles.Normal);
            deviceBreadcrumb.color = Theme.Muted;
            deviceInstructionHeading = DeviceText(deviceInstructionCard.transform, "DeviceInstructionsHeading", new Vector2(.03f, .84f), new Vector2(.97f, .95f), 18, FontStyles.Normal);
            deviceScrollHint = DeviceText(deviceInstructionCard.transform, "DeviceScrollHint", new Vector2(.62f, .84f), new Vector2(.97f, .95f), 18, FontStyles.Normal);
            deviceScrollHint.text = "SCROLL FOR MORE"; deviceScrollHint.color = Theme.Muted; deviceScrollHint.alignment = TextAlignmentOptions.Right;
            deviceControlHeading = DeviceText(deviceControlCard.transform, "DeviceControlsHeading", new Vector2(.05f, .84f), new Vector2(.95f, .95f), 18, FontStyles.Normal);
            deviceSystemCaption = DeviceText(deviceControlCard.transform, "DeviceSystemCaption", new Vector2(.05f, .66f), new Vector2(.40f, .79f), 23, FontStyles.Normal);
            var systemValue = DeviceText(deviceControlCard.transform, "DeviceSystemValue", new Vector2(.40f, .66f), new Vector2(.95f, .79f), 18, FontStyles.Normal);
            systemValue.text = "-"; systemValue.color = Theme.Muted; systemValue.alignment = TextAlignmentOptions.Right;
            var sourceCaption = DeviceText(deviceControlCard.transform, "DeviceSourceCaption", new Vector2(.05f, .50f), new Vector2(.40f, .61f), 23, FontStyles.Normal);
            sourceCaption.text = "Source";
            deviceSource = DeviceText(deviceControlCard.transform, "DeviceSource", new Vector2(.40f, .50f), new Vector2(.95f, .61f), 18, FontStyles.Normal);
            deviceSource.color = Theme.Muted; deviceSource.alignment = TextAlignmentOptions.Right;
            var actionCaption = DeviceText(deviceControlCard.transform, "DeviceActionCaption", new Vector2(.05f, .34f), new Vector2(.40f, .45f), 23, FontStyles.Normal);
            actionCaption.text = "Action";
            deviceAction = DeviceText(deviceControlCard.transform, "DeviceAction", new Vector2(.40f, .34f), new Vector2(.95f, .45f), 18, FontStyles.Normal);
            deviceAction.color = Theme.Muted; deviceAction.alignment = TextAlignmentOptions.Right;
            foreach (float height in new[] { .64f, .48f })
            {
                var divider = Box(deviceControlCard.transform, "DeviceRowDivider", new Vector2(.05f, height), new Vector2(.95f, height + .002f)).GetComponent<Image>();
                divider.sprite = HudSkin.Get("Fill"); divider.color = Theme.Border;
            }
            deviceLock = Box(panel, "DeviceLock", new Vector2(.665f, .355f), new Vector2(.687f, .389f)).GetComponent<Image>();
            deviceLock.sprite = HudSkin.Get("Locked"); deviceLock.color = Theme.Text; deviceLock.preserveAspect = true;
            deviceProgressTrack = Box(panel, "DeviceHoldTrack", new Vector2(.647f, .355f), new Vector2(.669f, .389f)).GetComponent<Image>();
            HudSkin.Apply(deviceProgressTrack, "FPSSample/GoalProgress", Theme.Background);
            deviceProgressTrack.preserveAspect = true; deviceProgressTrack.gameObject.SetActive(false);
            deviceProgress = Box(panel, "DeviceHoldProgress", new Vector2(.647f, .355f), new Vector2(.669f, .389f)).GetComponent<Image>();
            HudSkin.Apply(deviceProgress, "FPSSample/GoalProgress", HudSkin.Accent);
            deviceProgress.type = Image.Type.Filled; deviceProgress.fillMethod = Image.FillMethod.Radial360; deviceProgress.fillOrigin = 2;
            deviceProgress.preserveAspect = true; deviceProgress.gameObject.SetActive(false);
            deviceViewport = Box(surface, "DeviceViewport", new Vector2(.09f, .34f), new Vector2(.58f, .61f));
            deviceViewport.GetComponent<Image>().color = Color.clear; deviceViewport.GetComponent<Image>().raycastTarget = true;
            deviceViewport.gameObject.AddComponent<RectMask2D>();
            deviceBody = DeviceText(deviceViewport, "DeviceBody", new Vector2(0, 1), Vector2.one, 23, FontStyles.Normal);
            deviceBody.rectTransform.pivot = new Vector2(0, 1); deviceBody.rectTransform.sizeDelta = new Vector2(0, 1);
            deviceBody.textWrappingMode = TextWrappingModes.Normal; deviceBody.overflowMode = TextOverflowModes.Overflow;
            deviceBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = deviceViewport.gameObject.AddComponent<ScrollRect>(); scroll.content = deviceBody.rectTransform; scroll.viewport = deviceViewport; scroll.horizontal = false;
            deviceFooter = DeviceText(surface, "DeviceFooter", new Vector2(.031f, .035f), new Vector2(.970f, .07f), 18, FontStyles.Normal);
            deviceFooter.color = Theme.Muted;
            deviceHint = DeviceText(surface, "DeviceHint", new Vector2(.58f, .035f), new Vector2(.970f, .07f), 18, FontStyles.Normal);
            deviceHint.alignment = TextAlignmentOptions.Right;
            foreach (float height in new[] { .885f, .085f })
            {
                var divider = Box(surface, "DeviceDivider", new Vector2(0, height), new Vector2(1, height + .002f)).GetComponent<Image>();
                divider.sprite = HudSkin.Get("Fill"); divider.color = Theme.Border;
            }
            BuildSelectedDeviceDetails(surface);
            deviceSurface.SetActive(false);
        }
        private GameObject DeviceCard(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var card = Box(parent, name, min, max);
            var image = card.GetComponent<Image>();
            image.sprite = parent.GetComponent<Image>().sprite;
            image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.color = Theme.Text;
            return card.gameObject;
        }
        private UnityEngine.UI.Button JournalButton(Transform parent, string label, Vector2 min, Vector2 max, Action clicked)
        {
            var button = Button(parent, label, min, max, clicked);
            var image = button.GetComponent<Image>(); image.sprite = null; image.color = Color.clear;
            var colors = button.colors; colors.normalColor = Color.clear; colors.highlightedColor = Theme.SelectionFocus;
            colors.selectedColor = Theme.SelectionFocus; colors.pressedColor = Theme.SelectionPressed; button.colors = colors;
            button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            var text = DeviceText(button.transform, "JournalButtonLabel", Vector2.zero, Vector2.one, 23, FontStyles.Normal);
            text.text = label; text.alignment = TextAlignmentOptions.Left;
            return button;
        }
        private static TMP_Text DeviceText(Transform parent, string name, Vector2 min, Vector2 max, float size, FontStyles style)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(parent, false); text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = Theme.BodyFont; text.fontSize = Theme.Size(size);
            text.fontStyle = style; text.color = Theme.Text; text.raycastTarget = false;
            return text;
        }
        private void SetDeviceLayout(bool device)
        {
            deviceSurface.SetActive(device);
            deviceBackdrop.GetComponent<Image>().color = device ? Theme.DeviceBackdrop : Theme.CampaignBackdrop;
            deviceBackdrop.SetActive(device);
            deviceClose.SetActive(device);
            panel.Find("DeviceBrand").gameObject.SetActive(device);
            panel.Find("DeviceIdentity").gameObject.SetActive(device);
            if (!device) { deviceLock.gameObject.SetActive(false); deviceProgressTrack.gameObject.SetActive(false); deviceProgress.gameObject.SetActive(false); }
            closeLabel.transform.parent.gameObject.SetActive(!device);
            HudSkin.Apply(panel.GetComponent<Image>(), device ? "Button" : "Panel", device ? Theme.Muted : HudSkin.PanelTint, device ? 1f : 2f);
            panel.anchorMin = device ? new Vector2(.14f, .08f) : new Vector2(.18f, .12f);
            panel.anchorMax = device ? new Vector2(.86f, .94f) : new Vector2(.82f, .87f);
            documentViewport.gameObject.SetActive(!device);
            controls.anchorMin = device ? new Vector2(.65f, .34f) : new Vector2(.04f, .07f);
            controls.anchorMax = device ? new Vector2(.94f, .57f) : new Vector2(.96f, .29f);
        }
        private Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var t=go.GetComponent<Text>();t.font=font;t.fontSize=(int)Theme.Size(size);t.color=Theme.Text;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
            var outline=go.AddComponent<Outline>();outline.effectColor=Color.black;outline.effectDistance=new Vector2(1,-1);return t;
        }
        private UnityEngine.UI.Button Button(Transform parent,string label,Vector2 min,Vector2 max,Action clicked)
        {
            var r=Box(parent,label,min,max);r.GetComponent<Image>().color=Theme.Text;
            r.GetComponent<Image>().raycastTarget = true;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.colors=TacticalUiTheme.ButtonColors(); b.onClick.AddListener(()=>clicked());
            HudSkin.StyleButton(b);
            var t=Label(r,"Label",new Vector2(.02f,0),new Vector2(.98f,1),19);t.font=HudSkin.Font(true);t.alignment=TextAnchor.MiddleCenter;t.text=label;return b;
        }
        private void ClearControls()
        {
            mouseHeld = false;
            body.rectTransform.anchoredPosition=Vector2.zero;
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); confirmation = null; }
            foreach (Transform c in controls) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        }
        public void Open(CampaignInteractable item)
        {
            if (item == null || Campaign == null || Campaign.BlocksInput) return;
            if (item.objectiveId == CampaignObjectiveId.FactoryShipping && !CampaignRules.CanOpenShipping(Campaign.State))
            { ShowMessage("Requires: " + CampaignRules.LockReason(Campaign.State, item.objectiveId)); return; }
            var source = CampaignRules.ComparisonSource(item.objectiveId);
            if (source != CampaignFileId.None && !Campaign.State.HasFile(source)
                && !CampaignRules.CanReadComparisonSource(Campaign.State, item.objectiveId))
            { ShowMessage("Requires: " + CampaignRules.LockReason(Campaign.State, item.objectiveId)); return; }
            ClearShippingErrors();
            if (item != null && item.documentOnly)
            {
                Campaign.RequestDiscoverFile(item.fileId);
                return;
            }
            if (item.objectiveId == CampaignObjectiveId.FactoryManifest && !Campaign.State.HasFile(CampaignFileId.FactoryManifest))
                Campaign.RequestDiscoverFile(CampaignFileId.FactoryManifest);
            CancelAid();
            focused=item;journal=false;
            SetDeviceLayout(item.objectiveId is CampaignObjectiveId.FactoryManifest or CampaignObjectiveId.FactoryIsolate
                or CampaignObjectiveId.FactoryBackup or CampaignObjectiveId.FactoryGenerator);
            deviceViewport.GetComponent<ScrollRect>().StopMovement();
            deviceBody.rectTransform.anchoredPosition = Vector2.zero;
            first = item.Choices?.Length > 0 ? -1 : 0;
            second = item.SecondaryChoices?.Length > 0 ? -1 : 0;
            if(item.objectiveId==CampaignObjectiveId.LabPower)first=Campaign.State.powerSelection;
            RefreshHintTarget();
            ShowDocument();RenderDocument();
        }
        private void RenderDocument()
        {
            if(focused==null)return;
            body.text=focused.Title.ToUpperInvariant()+"\n\n"+focused.Instructions;
            string expectedResult = Campaign.ContentCatalog?.FindObjective(focused.objectiveId)?.result;
            if (!string.IsNullOrWhiteSpace(expectedResult)) body.text += "\n\nON COMPLETION\n" + expectedResult;
            CampaignFileDefinition file = focused.objectiveId == CampaignObjectiveId.FactoryManifest
                ? Campaign.ContentCatalog?.FindFile(CampaignFileId.FactoryManifest) : focused.LinkedFile;
            bool readingSource = CampaignRules.CanReadComparisonSource(Campaign.State, focused.objectiveId);
            deviceFiles = Campaign.State.discoveredFiles; deviceCompleted = Campaign.State.completed;
            devicePhase = Campaign.State.phase; deviceCampaignChapter = Campaign.State.chapter;
            if (file != null && (Campaign.State.HasFile(file.id) || readingSource) && !string.IsNullOrWhiteSpace(file.body))
                body.text += "\n\nEVIDENCE REFERENCE\n" + file.body;
            else if (file != null)
                body.text += "\n\nSOURCE NOT RECORDED\nRead the linked document before comparing its fields.";
            var referenceId = focused.objectiveId switch
            {
                CampaignObjectiveId.AsylumPatient => CampaignFileId.AsylumDeskLog,
                CampaignObjectiveId.AsylumTransfer => CampaignFileId.AsylumPatientRecord,
                CampaignObjectiveId.LabArchive => CampaignFileId.LabIndex,
                _ => CampaignFileId.None
            };
            var reference = Campaign.ContentCatalog?.FindFile(referenceId);
            if (reference != null && Campaign.State.HasFile(referenceId))
                body.text += "\n\nCROSS-REFERENCE — " + reference.title + "\n" + reference.body;
            if (deviceSurface.activeSelf)
            {
                deviceTitle.text = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(focused.Title.ToLowerInvariant());
                deviceChapter.text = CampaignRules.ChapterOf(focused.objectiveId).ToString().ToUpperInvariant();
                bool manifest = focused.objectiveId == CampaignObjectiveId.FactoryManifest;
                deviceInstructionCard.SetActive(!manifest); deviceControlCard.SetActive(!manifest);
                deviceNotice.SetActive(!manifest);
                manifestSidebar.SetActive(manifest); manifestPage.SetActive(manifest);
                deviceSubtitle.text = manifest ? "LOGISTICS RECORDS" : "PRODUCTION CONTROL";
                deviceFooter.text = manifest ? "RECORD VIEWER" : "UTILITIES / CONTROL INTERFACE";
                deviceHint.text = manifest && file != null && Campaign.State.HasFile(file.id) ? "FILE ADDED TO INVENTORY" : "";
                string system = focused.objectiveId switch
                {
                    CampaignObjectiveId.FactoryIsolate => "Production",
                    CampaignObjectiveId.FactoryBackup => "Backup",
                    CampaignObjectiveId.FactoryGenerator => "Generator",
                    _ => "System"
                };
                deviceSystemCaption.text = system;
                deviceInstructionHeading.text = (focused.objectiveId == CampaignObjectiveId.FactoryIsolate ? "PRODUCTION BREAKER" : system.ToUpperInvariant()) + " / INSTRUCTIONS";
                deviceControlHeading.text = "CONTROL / " + (focused.objectiveId == CampaignObjectiveId.FactoryIsolate ? "ISOLATE" : system.ToUpperInvariant());
                bool expandedSource = !manifest && file != null && Campaign.State.HasFile(file.id) && !string.IsNullOrWhiteSpace(file.body);
                deviceInstructionCard.GetComponent<RectTransform>().anchorMin = new Vector2(.031f, expandedSource ? .10f : .237f);
                var noticeRect = deviceNotice.GetComponent<RectTransform>();
                noticeRect.anchorMin = new Vector2(.05f, expandedSource ? .13f : .33f);
                noticeRect.anchorMax = new Vector2(.588f, expandedSource ? .25f : .48f);
                deviceTitle.rectTransform.anchorMin = manifest ? new Vector2(.26f, .72f) : new Vector2(.031f, .725f);
                deviceTitle.rectTransform.anchorMax = manifest ? new Vector2(.75f, .85f) : new Vector2(.70f, .79f);
                deviceBreadcrumb.rectTransform.anchorMin = manifest ? new Vector2(.26f, .69f) : new Vector2(.031f, .80f);
                deviceBreadcrumb.rectTransform.anchorMax = manifest ? new Vector2(.75f, .72f) : new Vector2(.70f, .84f);
                deviceBreadcrumb.text = manifest ? "RECORDS / LOGISTICS / MANIFEST" : focused.objectiveId switch
                {
                    CampaignObjectiveId.FactoryIsolate => "POWER SYSTEM / PRODUCTION",
                    CampaignObjectiveId.FactoryBackup => "POWER SYSTEM / BACKUP",
                    CampaignObjectiveId.FactoryGenerator => "POWER SYSTEM / GENERATOR",
                    _ => "POWER SYSTEM"
                };
                deviceViewport.anchorMin = manifest ? new Vector2(.285f, .25f) : new Vector2(.05f, expandedSource ? .27f : .50f);
                deviceViewport.anchorMax = manifest ? new Vector2(.855f, .60f) : new Vector2(.59f, .59f);
                manifestPageTitle.text = file != null ? file.title : focused.Title;
                deviceBody.color = manifest ? Theme.Background : Theme.Text;
                deviceBody.text = manifest && file != null && Campaign.State.HasFile(file.id) && !string.IsNullOrWhiteSpace(file.body)
                    ? file.body : !manifest && file != null && !Campaign.State.HasFile(file.id)
                        ? focused.Instructions : body.text.Substring(body.text.IndexOf("\n\n", StringComparison.Ordinal) + 2);
                CampaignResult result = CampaignRules.CanUse(Campaign.State, focused.objectiveId);
                if (!manifest && result != CampaignResult.Accepted && result != CampaignResult.AlreadyDone)
                    deviceHint.text = "CHECK REQUIREMENTS";
                deviceState.text = DeviceStateLabel(Campaign.State, focused.objectiveId, result);
                deviceSource.text = file != null && Campaign.State.HasFile(file.id)
                    ? "RECORDED" : CampaignRules.ComparisonSource(focused.objectiveId) == CampaignFileId.None ? "NOT REQUIRED" : "READ FIRST";
                deviceAction.text = result == CampaignResult.Accepted ? "READY" : result == CampaignResult.AlreadyDone ? "RECORDED" : "LOCKED";
                deviceLock.gameObject.SetActive(!manifest && result != CampaignResult.Accepted && result != CampaignResult.AlreadyDone);
                if (!manifest) RenderDeviceNotice(result, false);
                deviceFiles = Campaign.State.discoveredFiles; deviceCompleted = Campaign.State.completed;
                devicePhase = Campaign.State.phase; deviceCampaignChapter = Campaign.State.chapter;
                LayoutSelectedDevice(manifest, expandedSource);
                selectedTypography.Refresh();
            }
            ClearControls();
            if (focused.objectiveId == CampaignObjectiveId.FactoryManifest) return;
            if (readingSource)
            {
                var sourceId = CampaignRules.ComparisonSource(focused.objectiveId);
                Button(controls, "RECORD SOURCE TO COMPARE", new Vector2(.10f, .25f), new Vector2(.90f, .80f),
                    () => Campaign.RequestDiscoverFile(sourceId));
                UiStateStack.RefreshFocus(UiScreen.Document);
                return;
            }
            if (CampaignRules.ComparisonSource(focused.objectiveId) != CampaignFileId.None
                && CampaignRules.CanUse(Campaign.State, focused.objectiveId) != CampaignResult.Accepted) return;
            if(focused.objectiveId==CampaignObjectiveId.LabPower)
            {
                string[] loads={"SAFETY · 2","TEST RIG · 4","DATA · 2","FREIGHT LIFT · 2"};
                var row = ChoiceRow("PowerChoices", .46f, .98f);
                for(int i=0;i<4;i++){int bit=i;Button(row,((first&(1<<i))!=0?"[ON] ":"[OFF] ")+loads[i],Vector2.zero,Vector2.one,()=>{int mask=first^(1<<bit);if(CampaignRules.CanSelectPower(mask)){first=mask;Campaign.RequestPowerSelection(mask);RenderDocument();}else ShowMessage("Keep SAFETY online; total load cannot exceed 6.");});}
            }
            else
            {
                AddChoices(focused.Choices,false,.48f,.99f);
                AddChoices(focused.SecondaryChoices,true,.03f,.46f);
            }
            bool device = deviceSurface.activeSelf;
            bool deviceLocked = CampaignRules.CanUse(Campaign.State, focused.objectiveId) != CampaignResult.Accepted;
            string confirmLabel = Campaign.State.Has(focused.objectiveId) ? "RECORDED BY SQUAD"
                : deviceLocked ? "LOCKED" : first < 0 || second < 0 ? "SELECT REQUIRED OPTIONS"
                : $"HOLD {SurvivalHotbar.Key("Interact", "F")} TO CONFIRM";
            var confirm=Button(panel,confirmLabel,device ? new Vector2(.63f,.33f) : new Vector2(.04f,.01f),device ? new Vector2(.91f,.42f) : new Vector2(.76f,.065f),()=>{});
            if (device)
            {
                var legacy = confirm.GetComponentInChildren<Text>();
                var text = DeviceText(confirm.transform, "DeviceConfirmLabel", Vector2.zero, Vector2.one, 23, FontStyles.Normal);
                text.text = legacy.text; text.alignment = TextAlignmentOptions.Center; legacy.gameObject.SetActive(false);
                if (!deviceLocked && !Campaign.State.Has(focused.objectiveId))
                    text.text = first < 0 || second < 0 ? "SELECT REQUIRED OPTIONS" : "[" + SurvivalHotbar.Key("Interact", "F") + "]  HOLD TO CONFIRM";
                StyleSelectedDeviceButton(confirm);
                deviceLock.transform.SetAsLastSibling(); deviceProgressTrack.transform.SetAsLastSibling(); deviceProgress.transform.SetAsLastSibling();
            }
            confirmation = confirm.gameObject;
            confirm.interactable = !Campaign.State.Has(focused.objectiveId) && first >= 0 && second >= 0
                && !deviceLocked;
            var trigger=confirm.gameObject.AddComponent<EventTrigger>();
            var down=new EventTrigger.Entry{eventID=EventTriggerType.PointerDown};down.callback.AddListener(_=>mouseHeld=true);trigger.triggers.Add(down);
            var up=new EventTrigger.Entry{eventID=EventTriggerType.PointerUp};up.callback.AddListener(_=>mouseHeld=false);trigger.triggers.Add(up);
            UiStateStack.RefreshFocus(UiScreen.Document);
        }
        private bool DeviceStateChanged() => Campaign.State.discoveredFiles != deviceFiles || Campaign.State.completed != deviceCompleted
            || Campaign.State.phase != devicePhase || Campaign.State.chapter != deviceCampaignChapter;

        private static string DeviceStateLabel(CampaignState state, CampaignObjectiveId objective, CampaignResult result) => result switch
        {
            CampaignResult.Accepted => "ACTION READY",
            CampaignResult.AlreadyDone => "RECORDED",
            CampaignResult.WrongChapter => "Requires: correct chapter",
            CampaignResult.WrongPhase => "Requires: exploration phase",
            _ => "Requires: " + CampaignRules.LockReason(state, objective)
        };
        private void RenderDeviceNotice(CampaignResult result, bool activeHold)
        {
            bool locked = result != CampaignResult.Accepted && result != CampaignResult.AlreadyDone;
            bool choicesReady = first >= 0 && second >= 0;
            var source = CampaignRules.ComparisonSource(focused.objectiveId);
            bool sourceUnrecorded = source != CampaignFileId.None && !Campaign.State.HasFile(source);
            deviceNoticeHeading.text = locked ? "LOCKED" : result == CampaignResult.AlreadyDone ? "RECORDED" : "COMMAND";
            deviceNoticeValue.text = locked ? DeviceStateLabel(Campaign.State, focused.objectiveId, result)
                : result == CampaignResult.AlreadyDone ? "Recorded by squad" : choicesReady ? "Hold to confirm" : "Select required options";
            deviceNoticeHint.text = activeHold ? "RELEASE EARLY TO CANCEL." : result == CampaignResult.AlreadyDone ? "" : sourceUnrecorded ? "READ LINKED DOCUMENT FOR DETAILS." : locked ? "CHECK THE REQUIREMENTS." : choicesReady ? "HOLD THE INTERACTION KEY." : "SELECT AN OPTION TO CONTINUE.";
        }
        private void AddChoices(string[] choices,bool secondary,float min,float max)
        {
            if(choices==null||choices.Length==0)return;
            string group = focused?.objectiveId == CampaignObjectiveId.FactoryShipping
                ? (secondary ? "DESTINATION / RECEIVING TIME" : "SHIPMENT / SEAL")
                    + ((shippingErrors & (secondary ? CampaignShippingGroup.DestinationReceivingTime : CampaignShippingGroup.ShipmentSeal)) != 0 ? " — CHECK THIS GROUP\n" : "\n") : string.Empty;
            var row = ChoiceRow(secondary ? "SecondaryChoices" : "Choices", min, max);
            for(int i=0;i<choices.Length;i++){int index=i;var button=Button(row,group+((secondary?second:first)==i?"● ":"○ ")+choices[i],Vector2.zero,Vector2.one,()=>{if(secondary)second=index;else first=index;ClearShippingErrors();CancelHold();RenderDocument();});if(focused?.objectiveId==CampaignObjectiveId.FactoryShipping){var label=button.GetComponentInChildren<Text>();label.fontSize=(int)Theme.Size(15);label.verticalOverflow=VerticalWrapMode.Overflow;}}
        }
        private RectTransform ChoiceRow(string name, float min, float max)
        {
            var row = Box(controls, name, new Vector2(0, min), new Vector2(1, max));
            row.GetComponent<Image>().color = Color.clear;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            return row;
        }
        public void OpenSupply(CampaignSupply supply)
        {
            Close();journal=true;ShowDocument();body.text=$"FIELD SUPPLY\n\nThe squad can claim one package from this cache.\n{supply.AmmoLabel} +{supply.ammo}.\n"+(supply.ammoWeapon!=null?"Carry the matching weapon and make room for the whole pack. Otherwise the cache stays available.\n":"")+"Or take one medkit if you have space.";ClearControls();
            Button(controls,"TAKE " + supply.AmmoLabel,new Vector2(0,.2f),new Vector2(.48f,.8f),()=>{Campaign.RequestSupply(supply.supplyId,false);Close();});
            Button(controls,"TAKE MEDKIT",new Vector2(.52f,.2f),new Vector2(1,.8f),()=>{Campaign.RequestSupply(supply.supplyId,true);Close();});
        }
        // Completion state lives in Missions; this record keeps only received transmissions.
        public string JournalRecordText() => CampaignDialogue.Journal(Campaign.State).TrimStart();
        private void Close()
        {ClearShippingErrors();CancelHold();CancelAid();focused=null;journal=false;SetInteractionText("");panel.gameObject.SetActive(false);UiStateStack.Remove(UiScreen.Document);ClearControls();SetDeviceLayout(false);RefreshHintTarget();}
        public void CloseDocument() => Close();
        private void SetDeviceHolding(bool active)
        {
            if (!deviceSurface.activeSelf || confirmation == null) return;
            var button = confirmation.GetComponent<UnityEngine.UI.Button>();
            var image = button.GetComponent<Image>();
            var label = button.GetComponentInChildren<TMP_Text>();
            if (active)
            {
                button.transition = Selectable.Transition.None;
                SelectedUiTheme.Surface(image, Theme.HoldSurface); image.raycastTarget = true;
                image.CrossFadeColor(Theme.Text, 0, true, true);
                label.color = Theme.Background; deviceProgress.color = Theme.Text;
                deviceState.text = "COMMAND IN PROGRESS"; deviceHint.text = "RELEASE TO CANCEL"; deviceAction.text = "IN PROGRESS";
            }
            else
            {
                StyleSelectedDeviceButton(button);
                label.color = Theme.Text; deviceProgress.color = Theme.Accent;
                deviceHint.text = "";
                if (Campaign != null && focused != null)
                    deviceState.text = DeviceStateLabel(Campaign.State, focused.objectiveId, CampaignRules.CanUse(Campaign.State, focused.objectiveId));
            }
            if (Campaign != null && focused != null)
                RenderDeviceNotice(CampaignRules.CanUse(Campaign.State, focused.objectiveId), active);
        }
        private void CancelHold()
        {
            if (holding && focused != null)
            {
                SetDeviceHolding(false);
                Campaign?.RequestHold(focused.objectiveId, first, second, false);
                if (deviceSurface.activeSelf && Campaign != null)
                {
                    var result = CampaignRules.CanUse(Campaign.State, focused.objectiveId);
                    deviceAction.text = result == CampaignResult.Accepted ? "READY" : result == CampaignResult.AlreadyDone ? "RECORDED" : "LOCKED";
                }
            }
            holding=false;mouseHeld=false;if(holdRing!=null){holdRing.fillAmount=0;holdRing.gameObject.SetActive(false);}
            if (deviceProgress != null) deviceProgress.gameObject.SetActive(false);
            if (deviceProgressTrack != null) deviceProgressTrack.gameObject.SetActive(false);
        }
        private void CancelAid()
        {
            if(aidHeld) Campaign?.RequestAid(0,false,false);
            aidHeld=false;
        }
        public void ShowObjectiveResult(CampaignObjectiveFeedback receipt)
        {
            if (receipt.Objective == CampaignObjectiveId.FactoryShipping && receipt.Result is CampaignResult.WrongAnswer or CampaignResult.MissingSelection)
            {
                shippingErrors = receipt.Result == CampaignResult.MissingSelection ? receipt.MissingGroups : receipt.MismatchedGroups;
                shippingErrorFiles = Campaign.State.discoveredFiles; shippingErrorCompleted = Campaign.State.completed; shippingErrorPhase = Campaign.State.phase;
                ShowMessage((receipt.Result == CampaignResult.MissingSelection ? "Select: " : "Mismatch: ") + CampaignRules.ShippingGroupNames(shippingErrors));
                if (focused?.objectiveId == CampaignObjectiveId.FactoryShipping) { CancelHold(); RenderDocument(); }
                CancelInvoke(nameof(ExpireShippingErrors)); Invoke(nameof(ExpireShippingErrors), 4f);
                return;
            }
            if (receipt.Result != CampaignResult.Accepted) { ShowResult(receipt.Result); return; }
            ClearShippingErrors();
            ResolveTracking(Owner);
            string title = Campaign?.ContentCatalog?.FindObjective(receipt.Objective)?.title ?? ObjectiveAction(receipt.Objective);
            ShowMessage($"Recorded: {title}.\nNext: {NavigationAction()}");
            nextCueUntil = Time.unscaledTime + 1.1f;
            SurvivalEffects.PlayOneShot(SurvivalCatalog.Load()?.completionClip,
                Owner != null ? Owner.transform.position : Vector3.zero, .25f);
            if (focused != null && focused.objectiveId == receipt.Objective) Close();
        }
        private CampaignShippingGroup shippingErrors;
        private ulong shippingErrorFiles, shippingErrorCompleted;
        private CampaignPhase shippingErrorPhase;
        private void ClearShippingErrors() { if (shippingErrors != CampaignShippingGroup.None) ClearMessage(); shippingErrors = CampaignShippingGroup.None; CancelInvoke(nameof(ExpireShippingErrors)); }
        private void ExpireShippingErrors() { shippingErrors = CampaignShippingGroup.None; if (focused?.objectiveId == CampaignObjectiveId.FactoryShipping) RenderDocument(); }
        public void ShowResult(CampaignResult result)
        {
            if(result==CampaignResult.AmmoUnavailable){ShowMessage("AMMO NOT TAKEN · Carry the matching weapon and make room for the whole pack.");return;}
            if(result==CampaignResult.Accepted){ResolveTracking(Owner);ShowMessage("Recorded for the squad.\nNext: "+NavigationAction());Close();return;}
            if(result==CampaignResult.Interrupted){SetDeviceHolding(false);releaseBeforeHold=true;holding=false;mouseHeld=false;}
            string wrong = "The evidence does not match. Check the fields again.";
            ShowMessage(result switch{CampaignResult.WrongAnswer=>wrong,CampaignResult.Prerequisite=>"Prerequisites are incomplete. Check the team objective.",CampaignResult.Busy=>"Another operator is using this.",CampaignResult.OutOfRange=>"Move closer and keep the interaction point visible.",CampaignResult.AlreadyDone=>"The squad already completed this step.",_=>"Interaction interrupted; completed steps are preserved."});
        }
        public void ShowMessage(string text){message=text;feedback.text=text;RefreshPresentation();messageUntil=Time.unscaledTime+4;CancelInvoke(nameof(ClearMessage));Invoke(nameof(ClearMessage),4f);}
        private void ClearMessage() { message=""; feedback.text=""; }

        public void EnqueueDialogue(CampaignDialogueId id)
        {
            CampaignDialogueDefinition definition = Campaign?.Dialogue(id);
            if (definition == null || string.IsNullOrWhiteSpace(definition.english)) return;
            pendingDialogue.Add(definition);
        }

        public void ResetDialogue()
        {
            pendingDialogue.Clear();
            subtitleUntil = 0;
            if (subtitle != null) subtitle.text = string.Empty;
            RefreshPresentation();
            if (dialogueAudio != null) dialogueAudio.Stop();
        }

        public bool CanAutoOpenFile()
        {
            var owner = Owner;
            if (owner == null || !owner.CanUseCombat || Campaign == null || Campaign.BlocksInput
                || InGameMenuUI.IsMenuOpen || panel.gameObject.activeSelf) return false;
            nextThreatCheck = 0;
            UpdateHints(owner, InputManager.Instance?.ActionsAsset?.FindAction("Fire")?.IsPressed() == true);
            return !nearbyThreat && Time.unscaledTime >= combatUntil;
        }
        private void Update()
        {
            if (Campaign != null && Campaign.IsSpawned) UpdateSubtitles();
            if (InGameMenuUI.IsMenuOpen) { CancelHold(); CancelAid(); return; }
            if (Campaign == null || !Campaign.IsSpawned) return;
            var owner = Owner;
            var size = ((RectTransform)canvas.transform).rect.size;
            if (layoutSize != size) { layoutSize = size; RefreshPresentation(); }
            if (owner != null) UpdateNavigation(owner);
            var input = InputManager.Instance; var actions = input?.ActionsAsset;
            bool open = panel.gameObject.activeSelf;
            if(!UiStateStack.BackConsumed && input?.GetPauseInputDown()!=true && actions?.FindAction("Journal")?.WasPressedThisFrame()==true)
            {
                if (TeamInventoryUI.IsOpen) TeamInventoryUI.Instance.Close();
                else if(open) Close();
                else if (TeamInventoryUI.Instance != null) TeamInventoryUI.Instance.Toggle();
            }
            if (TeamInventoryUI.IsOpen)
            {
                CancelHold(); CancelAid();
                return;
            }
            if(owner==null){SetInteractionText("");return;}
            string interactionText="";
            if (holdRing != null) { holdRing.fillAmount = 0; holdRing.gameObject.SetActive(false); }
            deviceProgress.gameObject.SetActive(false); deviceProgressTrack.gameObject.SetActive(false);
            bool fire=actions?.FindAction("Fire")?.WasPressedThisFrame()==true;
            bool anyHold=mouseHeld||actions?.FindAction("Interact")?.IsPressed()==true;
            if(releaseBeforeHold&&!anyHold){releaseBeforeHold=false;Campaign.RequestAid(0,false,false);}
            if(fire&&!mouseHeld){releaseBeforeHold=true;CancelHold();CancelAid();}
            UpdateHints(owner,fire&&!mouseHeld);
            if(focused!=null)
            {
                if(!owner.CanUseCombat || Campaign.BlocksInput) { Close(); return; }
                bool pressed=!releaseBeforeHold&&(mouseHeld||actions?.FindAction("Interact")?.IsPressed()==true);
                if(fire&&!mouseHeld)pressed=false;
                if(!focused.InReach(owner,Campaign.settings.interactionRange)){CancelHold();SetInteractionText("");return;}
                if(pressed&&first>=0&&second>=0&&!Campaign.State.Has(focused.objectiveId)
                    && CampaignRules.CanUse(Campaign.State, focused.objectiveId) == CampaignResult.Accepted)
                {
                    if(!holding){holding=true;holdStart=Time.unscaledTime;nextHeartbeat=0;SetDeviceHolding(true);}
                    if(Time.unscaledTime>=nextHeartbeat){Campaign.RequestHold(focused.objectiveId,first,second,true);nextHeartbeat=Time.unscaledTime+.2f;}
                    float held = Mathf.Clamp01((Time.unscaledTime-holdStart)/Mathf.Max(.1f,focused.holdSeconds));
                    interactionText=$"HOLD {input?.GetPromptDisplayName("Interact")} TO CONFIRM {held:P0}";
                    if (deviceSurface.activeSelf)
                    {
                        deviceProgress.fillAmount = held; deviceProgressTrack.gameObject.SetActive(true); deviceProgress.gameObject.SetActive(true);
                        deviceAction.text = "IN PROGRESS";
                    }
                    else if (holdRing != null) { holdRing.fillAmount = held; holdRing.gameObject.SetActive(true); }
                }
                else
                {
                    bool wasHolding = holding;
                    CancelHold();
                    if (wasHolding && deviceSurface.activeSelf) RenderDocument();
                    if (wasHolding && !pressed && !anyHold)
                        ShowMessage("HOLD CANCELLED · No change recorded. Hold again to confirm.");
                }
                PresentHint(interactionText);
                return;
            }
            if(panel.gameObject.activeSelf||Campaign.BlocksInput||!owner.CanUseCombat){CancelAid();SetInteractionText("");return;}
            bool heartbeat=Time.unscaledTime>=nextAidHeartbeat;
            var cam=owner.GetComponentInChildren<Camera>();
            PlayerHealth downed=null;
            if(cam!=null&&Physics.Raycast(cam.transform.position,cam.transform.forward,out var hit,Campaign.settings.interactionRange,~0,QueryTriggerInteraction.Ignore))downed=hit.collider.GetComponentInParent<PlayerHealth>();
            bool revive=downed!=null&&downed!=owner&&downed.LifeState==PlayerLifeState.Downed;
            bool aid=!releaseBeforeHold&&owner.GetComponent<SurvivalInventory>()?.IsUsingItem!=true&&revive&&actions?.FindAction("Interact")?.IsPressed()==true&&!fire;
            if(revive)interactionText=$"HOLD {input?.GetPromptDisplayName("Interact")} TO REVIVE ({Campaign.settings.reviveSeconds:0}s)";
            if((aid&&heartbeat)||aidHeld!=aid){Campaign.RequestAid(downed!=null?downed.NetworkObjectId:0,false,aid);aidHeld=aid;nextAidHeartbeat=Time.unscaledTime+.2f;}
            PresentHint(interactionText);
        }
        private void UpdateHints(PlayerHealth owner,bool fired)
        {
            if(fired)combatUntil=Time.unscaledTime+10;
            if(Time.unscaledTime>=nextThreatCheck)
            {
                nextThreatCheck=Time.unscaledTime+.5f;nearbyThreat=false;
                foreach(var collider in Physics.OverlapSphere(owner.transform.position,16,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var enemy=collider.GetComponentInParent<EnemyAI>();
                    if(enemy==null||!enemy.isActiveAndEnabled||Mathf.Abs(enemy.transform.position.y-owner.transform.position.y)>4)continue;
                    Vector3 eye=owner.transform.position+Vector3.up*1.5f;
                    if(!Physics.Linecast(eye,enemy.transform.position+Vector3.up,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                        ||hit.collider.GetComponentInParent<EnemyAI>()==enemy){nearbyThreat=true;break;}
                }
            }
            hintProgress.Advance(Time.unscaledDeltaTime, hintPaused || nearbyThreat || Time.unscaledTime < combatUntil);
        }
        private void PresentHint(string interactionText)
        {
            int tier = CampaignRules.HintTier(hintProgress.Seconds);
            bool blocked = focused == null && HUDManager.HasInstance && HUDManager.Instance.HasInteractionPrompt;
            string hint = nearbyHint == null || blocked || nearbyThreat || Time.unscaledTime < combatUntil || tier == 0
                ? "" : hintText[tier - 1];
            SetInteractionText(string.IsNullOrWhiteSpace(hint) ? interactionText
                : string.IsNullOrEmpty(interactionText) ? hint : interactionText + "\n" + hint);
        }
        private void UpdateSubtitles()
        {
            if(Time.unscaledTime<subtitleUntil)return;
            if (pendingDialogue.Count == 0)
            {
                if (subtitle.text.Length > 0) { subtitle.text = ""; RefreshPresentation(); }
                subtitleUntil = Time.unscaledTime + .25f;
                return;
            }
            CampaignDialogueDefinition definition = CampaignDialogue.TakeNext(pendingDialogue);
            subtitle.text = CampaignDialogue.Format(definition);
            RefreshPresentation();
            if (dialogueAudio != null && definition.audio != null) dialogueAudio.PlayOneShot(definition.audio);
            subtitleUntil = Time.unscaledTime + Mathf.Max(6, subtitle.text.Length * .06f,
                definition.audio != null ? definition.audio.length : 0);
        }
        private CampaignPhase renderedPhase=(CampaignPhase)255;
        private CampaignTankStage renderedTankStage;
        private void RenderPhaseControls()
        {
            var phase=Campaign.State.phase;
            if (CampaignStatusUI.Instance != null && (phase is CampaignPhase.Failed or CampaignPhase.Completed))
            { if (panel.gameObject.activeSelf) Close(); return; }
            if(phase==CampaignPhase.Completed&&Campaign.Now-Campaign.PhaseStarted<Campaign.settings.endingSeconds){if(panel.gameObject.activeSelf)Close();return;}
            if(renderedPhase==phase && renderedTankStage==Campaign.State.tankStage)return;
            if (journal && focused == null && panel.gameObject.activeSelf) Close();
            renderedPhase=phase;renderedTankStage=Campaign.State.tankStage;
            if(phase is CampaignPhase.Preparing or CampaignPhase.Failed or CampaignPhase.Completed) Close();
            if (phase == CampaignPhase.Preparing && Campaign.State.tankStage is CampaignTankStage.Pending or CampaignTankStage.Active) return;
            if(phase==CampaignPhase.Preparing)
            {journal=true;ShowDocument();ClearControls();body.text="PREPARATION\n\nTake supplies, revive teammates and enter the preparation area.\nSolo: remain in the area to start the countdown. In a group, confirm READY when requested.\nLeaving the area can cancel the countdown.\nThe checkpoint includes supplies claimed before countdown.";Button(controls,"READY",new Vector2(0,.2f),new Vector2(.48f,.8f),()=>{Campaign.RequestReady(true);Close();});Button(controls,"NOT READY",new Vector2(.52f,.2f),new Vector2(1,.8f),()=>{Campaign.RequestReady(false);Close();});}
            if(phase==CampaignPhase.Failed)
            {journal=true;ShowDocument();ClearControls();body.text="MISSION FAILED\n\nRestore the nearest checkpoint. HP, ammo, medicine and objectives return to the saved state.";if(Campaign.IsServer)Button(controls,"RETRY CHECKPOINT",new Vector2(.2f,.2f),new Vector2(.8f,.8f),()=>{Campaign.RequestRetry();Close();});}
            if(phase==CampaignPhase.Completed)
            {journal=true;ShowDocument();ClearControls();body.text="COLD LEDGER — COMPLETE\n\nThe freight route opened and the evidence copy was transmitted outside.\n\nThe original archive and custody records have been preserved.\nThe zone remains quarantined; the investigation starts now.\n\nThe squad extracted with both sealed cases.";}
        }
    }
}
