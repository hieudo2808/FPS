using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class HUDManager : SceneSingleton<HUDManager>
    {
        private static SelectedUiTheme Theme => SelectedUiTheme.Current;
        private TMP_Text selectedHealthCaption;
        private float selectedCanvasScale;
        [Header("Legacy Disconnect")]
        [SerializeField] private Button disconnectButton;

        [Header("Health")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI healthStateText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image healthDangerBackground;

        [Header("Infection")]
        [SerializeField] private Image infectionFill;
        [SerializeField] private Image infectionIcon;
        [SerializeField] private TextMeshProUGUI infectionStageText;
        [SerializeField] private GameObject sepsisWarning;

        [Header("Ammo")]
        [SerializeField] private TextMeshProUGUI currentAmmo;
        [SerializeField] private TextMeshProUGUI reservedAmmo;
        [SerializeField] private TextMeshProUGUI ammoStatusText;
        [SerializeField] private Image ammoTypeUI;

        [Header("Weapon")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image unusedWeaponIcon;
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private TextMeshProUGUI unusedWeaponNameText;

        [Header("Throwables")]
        [SerializeField] private TextMeshProUGUI grenadeKeyText;
        [SerializeField] private TextMeshProUGUI grenadeCount;
        [SerializeField] private TextMeshProUGUI incendiaryCount;
        [SerializeField] private TextMeshProUGUI medkitCount;
        [SerializeField] private TextMeshProUGUI antidoteCount;

        [Header("Combat Info")]
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI zombieCountText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI playerCountText;

        [Header("Match Flow")]
        [SerializeField] private TextMeshProUGUI matchStateText;
        [SerializeField] private TextMeshProUGUI respawnCountdownText;

        [Header("Hit Feedback")]
        [SerializeField] private Image hitMarker;
        [SerializeField] private float hitMarkerDuration = 0.18f;
        [SerializeField] private Image[] directionalDamageIndicators = new Image[4];

        [Header("Weapon Prompts")]
        [SerializeField] private Image firePromptIcon;
        [SerializeField] private Image aimPromptIcon;
        [SerializeField] private TextMeshProUGUI firePromptText;
        [SerializeField] private TextMeshProUGUI aimPromptText;

        [Header("Aiming")]
        [SerializeField] private GameObject crosshairRoot;
        [SerializeField] private GameObject scopeOverlayRoot;
        [SerializeField] private Image scopeOverlayImage;

        [Header("Prompts")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;
        private bool interactionPromptRequested;
        private Image interactionLock;
        public bool HasInteractionPrompt => interactionPromptRequested && interactionPromptText != null;
        [SerializeField] private GameObject waveAnnouncementPanel;
        [SerializeField] private TextMeshProUGUI waveAnnouncementText;

        [Header("Special Threat")]
        [SerializeField] private TextMeshProUGUI specialThreatWarningText;

        private static Color TextColor => Theme.Text;
        private static Color MutedColor => Theme.Muted;
        private static Color AccentColor => Theme.Accent;
        private static Color WarningColor => Theme.Text;
        private static Color CriticalColor => Theme.Danger;

        private WeaponManager weaponManager;
        private Weapon currentWeapon;
        private Weapon unusedWeapon;
        private PlayerHealth playerHealth;
        private PlayerInfectionController playerInfection;
        private SurvivalInventory survivalInventory;
        private float hitMarkerUntil;
        private float vignetteTargetAlpha, vignetteAlpha, vignetteVelocity;
        private struct DamageCue { public Vector3 source; public float until; }
        private readonly DamageCue[] damageCues = new DamageCue[4];
        private float specialThreatWarningUntil;
        private Vector3 specialThreatWorldPosition;
        private int validatedCrosshairScreenWidth = -1;
        private int validatedCrosshairScreenHeight = -1;
        private Vector2 validatedHudSize;
        private bool crosshairAlignmentWarningIssued;
        private RectTransform adsReticleRect;
        private Canvas adsReticleCanvas;
        private Sprite adsReticleSprite;
        private SurvivalHotbar survivalHotbar;
        private RectTransform peripheralRoot;
        private GameObject centerHudRoot;
        private InputManager promptInput;
        private TMP_Text primaryKeyLabel, secondaryKeyLabel;
        private Image[] opacityPanels;
        private float[] opacityPanelAlphas;

        private AIDirector boundDirector;
        private DifficultyManager boundDifficulty;
        private NetworkGameManager boundNetwork;
        private NetworkMatchStateManager boundMatch;

        private void OnEnable()
        {
            HudSkin.PanelOpacityChanged += ApplyPanelOpacity;
            ApplyPanelOpacity(HudSkin.PanelOpacity);
            HudSignals.SourcesChanged += BindSources;
            InputManager.InputStateChanged += UpdateHudVisibility;
            UiStateStack.Changed += UpdateHudVisibility;
            CampaignHUD.InteractionMessageChanged += UpdateInteractionPromptVisibility;
            SpecialThreatSignal.Raised += OnSpecialThreatRaised;
            BindSources();
        }

        private void OnDisable()
        {
            HudSkin.PanelOpacityChanged -= ApplyPanelOpacity;
            HudSignals.SourcesChanged -= BindSources;
            InputManager.InputStateChanged -= UpdateHudVisibility;
            UiStateStack.Changed -= UpdateHudVisibility;
            CampaignHUD.InteractionMessageChanged -= UpdateInteractionPromptVisibility;
            SpecialThreatSignal.Raised -= OnSpecialThreatRaised;
            UnbindSources();
            CancelInvoke(nameof(UpdateMatchFlowInfo));
            UpdateHudVisibility();
            ClearCombatFeedback();
            if (specialThreatWarningText != null) specialThreatWarningText.enabled = false;
            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        }

        private void BindSources()
        {
            UnbindSources();
            promptInput = InputManager.Instance;
            if (promptInput != null) promptInput.PromptBindingsChanged += UpdateWeaponPromptBindings;
            TryAcquireLocalPlayerHealth();
            TryAcquireWeaponManager();
            TryAcquireSurvivalInventory();
            if (weaponManager != null) weaponManager.WeaponIndexChanged += OnWeaponIndexChanged;
            UpdateWeaponUI();
            boundDirector = AIDirector.Instance;
            if (boundDirector != null) boundDirector.CountersChanged += UpdateCombatInfo;
            boundDifficulty = DifficultyManager.Instance;
            if (boundDifficulty != null) boundDifficulty.CurrentDifficulty.OnValueChanged += OnDifficultyChanged;
            boundNetwork = NetworkGameManager.HasInstance ? NetworkGameManager.Instance : null;
            if (boundNetwork != null)
            {
                boundNetwork.OnClientConnected += UpdateCombatInfo;
                boundNetwork.OnClientDisconnected += UpdateCombatInfo;
            }
            boundMatch = NetworkMatchStateManager.Instance;
            if (boundMatch != null)
            {
                boundMatch.OnStateChanged += OnMatchChanged;
                boundMatch.RespawnDeadlineChanged += RefreshMatch;
            }
            UpdateWeaponPromptBindings(); UpdateCombatInfo(); RefreshMatch(); UpdateHudVisibility();
        }

        private void UnbindSources()
        {
            if (promptInput != null) promptInput.PromptBindingsChanged -= UpdateWeaponPromptBindings;
            if (weaponManager != null) weaponManager.WeaponIndexChanged -= OnWeaponIndexChanged;
            if (currentWeapon != null) currentWeapon.AmmoChanged -= UpdateAmmoInfo;
            if (survivalInventory != null) survivalInventory.InventoryChanged -= UpdateSurvivalInventoryUI;
            if (boundDirector != null) boundDirector.CountersChanged -= UpdateCombatInfo;
            if (boundDifficulty != null) boundDifficulty.CurrentDifficulty.OnValueChanged -= OnDifficultyChanged;
            if (boundNetwork != null)
            {
                boundNetwork.OnClientConnected -= UpdateCombatInfo;
                boundNetwork.OnClientDisconnected -= UpdateCombatInfo;
            }
            if (boundMatch != null)
            {
                boundMatch.OnStateChanged -= OnMatchChanged;
                boundMatch.RespawnDeadlineChanged -= RefreshMatch;
            }
            UnsubscribeInfection(); UnsubscribeHealth();
            weaponManager = null; currentWeapon = unusedWeapon = null; survivalInventory = null;
        }

        private void OnWeaponIndexChanged(int index) => UpdateWeaponUI();
        private void OnDifficultyChanged(DifficultyLevel before, DifficultyLevel after) => UpdateCombatInfo();
        private void OnMatchChanged(NetworkMatchState before, NetworkMatchState after) => RefreshMatch();
        private void RefreshMatch()
        {
            CancelInvoke(nameof(UpdateMatchFlowInfo));
            UpdateMatchFlowInfo();
            if (playerHealth != null && playerHealth.IsDead)
                InvokeRepeating(nameof(UpdateMatchFlowInfo), .25f, .25f);
        }

        private void UpdateHudVisibility()
        {
            bool visible = isActiveAndEnabled && HudLayout.GameplayVisible;
            UpdateInteractionPromptVisibility();
            if (adsReticleRect != null) adsReticleRect.GetComponent<Image>().enabled = visible;
            if (scopeOverlayImage != null) scopeOverlayImage.enabled = visible;
            if (healthText == null) return;
            peripheralRoot ??= HudLayout.PeripheralRoot(healthText.canvas.rootCanvas);
            peripheralRoot.gameObject.SetActive(visible);
            if (centerHudRoot == null && healthText.canvas.rootCanvas.transform.Find("MinimalHUDRoot") is Transform center)
                centerHudRoot = center.gameObject;
            if (centerHudRoot != null) centerHudRoot.SetActive(visible);
        }

        private void Start()
        {
            ConfigureVitalsPresentation();
            ConfigureSelectedPresentation();
            HudLayout.PeripheralRoot(healthText.canvas.rootCanvas).gameObject.AddComponent<SelectedUiTypography>();
            CacheOpacityPanels();
            if (firePromptText != null) firePromptText.transform.parent.gameObject.SetActive(false);
            if (aimPromptText != null) aimPromptText.transform.parent.gameObject.SetActive(false);
            // Director phases are internal pacing state, not player-facing HUD information.
            if (phaseText != null)
            {
                phaseText.text = string.Empty;
                phaseText.gameObject.SetActive(false);
            }
            EnsureSpecialThreatWarning();
            SetAimHudVisible(false, false);
            weaponManager = WeaponManager.LocalInstance;
            if (weaponManager != null)
            {
                UpdateWeaponUI();
            }

            if (disconnectButton != null)
            {
                disconnectButton.onClick.AddListener(OnDisconnectClicked);
            }

            if (interactionPromptText != null)
            {
                interactionPromptText.gameObject.SetActive(false);
            }

            if (waveAnnouncementPanel != null)
            {
                waveAnnouncementPanel.SetActive(false);
            }

            if (waveAnnouncementText != null && string.IsNullOrEmpty(waveAnnouncementText.text))
            {
                waveAnnouncementText.text = "INCOMING HORDE";
            }

            ClearCombatFeedback();

            if (respawnCountdownText != null)
            {
                respawnCountdownText.gameObject.SetActive(false);
            }

            UpdateHealthUI(100f, 100f);
            UpdateInfectionUI(0f, 0f, InfectionStage.None);
            UpdateAmmoInfo();
            UpdateCombatInfo();
            BindSources();
        }

        private void CacheOpacityPanels()
        {
            if (healthText == null || healthText.canvas == null) return;
            var images = healthText.canvas.rootCanvas.GetComponentsInChildren<Image>(true);
            opacityPanels = System.Array.FindAll(images, image => image.name is "HealthReadabilityPlate" or "WeaponFrame");
            opacityPanelAlphas = System.Array.ConvertAll(opacityPanels, image => image.color.a);
            ApplyPanelOpacity(HudSkin.PanelOpacity);
        }

        private void ApplyPanelOpacity(float opacity)
        {
            if (opacityPanels == null) return;
            for (int index = 0; index < opacityPanels.Length; index++)
                HudSkin.SetPanelAlpha(opacityPanels[index], opacityPanelAlphas[index], opacity);
        }

        private void ConfigureVitalsPresentation()
        {
            if (healthFill == null || infectionFill == null || infectionStageText == null) return;
            var healthPanel = healthFill.rectTransform.parent as RectTransform;
            var infectionPanel = infectionFill.rectTransform.parent;
            if (healthPanel == null || infectionPanel == healthPanel) return;
            healthPanel.sizeDelta = new Vector2(healthPanel.sizeDelta.x, 150);
            var infectionRect = infectionFill.rectTransform;
            infectionRect.SetParent(healthPanel, false);
            var healthRect = healthFill.rectTransform;
            infectionRect.anchorMin = healthRect.anchorMin;
            infectionRect.anchorMax = healthRect.anchorMax;
            infectionRect.pivot = healthRect.pivot;
            infectionRect.anchoredPosition = healthRect.anchoredPosition;
            infectionRect.sizeDelta = healthRect.sizeDelta;
            infectionFill.fillOrigin = 1;
            infectionRect.SetSiblingIndex(healthRect.GetSiblingIndex() + 1);
            var label = infectionStageText.rectTransform;
            label.SetParent(healthPanel, false);
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0, 1);
            label.anchoredPosition = new Vector2(64, -75);
            label.sizeDelta = new Vector2(262, 30);
            if (sepsisWarning != null) sepsisWarning.transform.SetParent(healthPanel, false);
            infectionPanel.gameObject.SetActive(false);
        }

        private void ConfigureSelectedPresentation()
        {
            if (healthFill == null || weaponIcon == null) return;
            var health = (RectTransform)healthFill.transform.parent;
            var weapon = (RectTransform)weaponIcon.transform.parent;
            HudLayout.Corner(health, Vector2.zero, Vector2.zero, new Vector2(340, 138));
            HudLayout.Corner(weapon, new Vector2(1, 0), Vector2.zero, new Vector2(526, 218));
            foreach (var text in health.GetComponentsInChildren<TMP_Text>(true))
            { text.font = Theme.BodyFont; text.fontSharedMaterial = text.font.material; text.fontSize = Theme.Body; text.color = Theme.Text; }
            foreach (var text in weapon.GetComponentsInChildren<TMP_Text>(true))
            { text.font = Theme.BodyFont; text.fontSharedMaterial = text.font.material; text.fontSize = Theme.Body; text.color = Theme.Text; }
            void Put(Transform parent, string name, float x, float y, float w, float h, float pw, float ph)
            {
                var rect = parent.Find(name) as RectTransform;
                if (rect != null) SelectedUiTheme.Place(rect, x, y, w, h, pw, ph);
            }
            SelectedUiTheme.Surface(health.Find("HealthReadabilityPlate").GetComponent<Image>(), Theme.Vitals);
            SelectedUiTheme.Surface(weapon.Find("WeaponFrame").GetComponent<Image>(), Theme.Loadout);
            foreach (string name in new[] { "HealthIcon", "HealthFrame" }) health.Find(name)?.gameObject.SetActive(false);
            Put(health, "HealthText", 108, 26, 200, 56, 340, 138);
            healthText.font = Theme.NumericFont; healthText.fontSharedMaterial = healthText.font.material; healthText.fontSize = Theme.Title; healthText.overflowMode = TextOverflowModes.Overflow;
            var healthCaption = new GameObject("HealthCaption", typeof(RectTransform), typeof(TextMeshProUGUI));
            healthCaption.transform.SetParent(health, false);
            SelectedUiTheme.Place(healthCaption.GetComponent<RectTransform>(), 24, 40, 84, 32, 340, 138);
            var caption = healthCaption.GetComponent<TMP_Text>(); caption.font = Theme.BodyFont;
            selectedHealthCaption = caption;
            caption.text = "HEALTH"; caption.fontSize = Theme.Small; caption.color = Theme.Text; caption.raycastTarget = false;
            Put(health, "InfectionStageText", 24, 70, 284, 30, 340, 138);
            infectionStageText.fontSize = Theme.Small;
            Put(health, "HealthTrack", 24, 100, 260, 6, 340, 138);
            Put(health, "HealthBar", 24, 100, 260, 6, 340, 138);
            SelectedUiTheme.Place(infectionFill.rectTransform, 24, 100, 260, 6, 340, 138);
            healthFill.color = Theme.Text; infectionFill.color = Theme.Muted;
            Put(weapon, "PrimaryWeaponKeyText", 60, 136, 28, 32, 526, 218);
            Put(weapon, "WeaponNameText", 88, 136, 136, 32, 526, 218);
            Put(weapon, "WeaponIcon", 214, 132, 120, 38, 526, 218);
            Put(weapon, "CurrentAmmoText", 398, 132, 52, 40, 526, 218);
            Put(weapon, "ReserveAmmoText", 448, 132, 56, 40, 526, 218);
            currentAmmo.font = Theme.NumericFont; currentAmmo.fontSharedMaterial = currentAmmo.font.material; currentAmmo.fontSize = Theme.Body; currentAmmo.overflowMode = TextOverflowModes.Overflow;
            reservedAmmo.font = Theme.NumericFont; reservedAmmo.fontSharedMaterial = reservedAmmo.font.material; reservedAmmo.fontSize = Theme.Body; reservedAmmo.overflowMode = TextOverflowModes.Overflow;
            reservedAmmo.textWrappingMode = currentAmmo.textWrappingMode = TextWrappingModes.NoWrap;
            var secondary = weapon.Find("SecondaryWeaponSlot");
            Put(weapon, "SecondaryWeaponSlot", 60, 174, 444, 32, 526, 218);
            if (secondary != null)
            {
                Put(secondary, "SlotKey", 0, 0, 28, 32, 444, 32);
                Put(secondary, "UnusedWeaponNameText", 28, 0, 136, 32, 444, 32);
                Put(secondary, "UnusedWeaponIcon", 164, 0, 64, 28, 444, 32);
            }
            if (ammoTypeUI != null) ammoTypeUI.gameObject.SetActive(false);
        }

        private void Update()
        {
            ValidateCrosshairAlignmentIfNeeded();
            UpdateCombatFeedback();
            UpdateSpecialThreatWarning();
        }

        private void LateUpdate()
        {
            if (selectedHealthCaption == null || healthText == null || healthText.canvas == null) return;
            float scale = healthText.canvas.scaleFactor;
            if (Mathf.Approximately(scale, selectedCanvasScale)) return;
            selectedCanvasScale = scale;
            selectedHealthCaption.fontSize = Theme.Small * scale >= TacticalUiTheme.MinimumReadableTextPixels ? Theme.Small : Theme.Body;
            float captionWidth = Mathf.Max(84, selectedHealthCaption.GetPreferredValues(selectedHealthCaption.text, float.PositiveInfinity, float.PositiveInfinity).x + 2);
            SelectedUiTheme.Place(selectedHealthCaption.rectTransform, 24, 40, captionWidth, 32, 340, 138);
            float numberLeft = Mathf.Max(108, 24 + captionWidth + (captionWidth > 84 ? 8 : 0));
            SelectedUiTheme.Place(healthText.rectTransform, numberLeft, 26, 316 - numberLeft, 56, 340, 138);
        }

        private void UpdateInteractionPromptVisibility()
        {
            if (interactionPromptText != null)
                interactionPromptText.gameObject.SetActive(interactionPromptRequested && HudLayout.GameplayVisible
                    && CampaignHUD.Instance?.HasInteractionMessage != true);
        }

        protected override void OnDestroy()
        {
            UnbindSources();
            base.OnDestroy();
            if (promptInput != null) promptInput.PromptBindingsChanged -= UpdateWeaponPromptBindings;

            if (disconnectButton != null)
            {
                disconnectButton.onClick.RemoveListener(OnDisconnectClicked);
            }

            UnsubscribeHealth();
            UnsubscribeInfection();
            if (survivalInventory != null)
                survivalInventory.InventoryChanged -= UpdateSurvivalInventoryUI;
            SpecialThreatSignal.Raised -= OnSpecialThreatRaised;
            if (adsReticleRect != null && !adsReticleRect.IsChildOf(transform))
            {
                if (Application.isPlaying)
                    Destroy(adsReticleRect.gameObject);
                else
                    DestroyImmediate(adsReticleRect.gameObject);
            }

        }

        private void OnSpecialThreatRaised(Vector3 worldPosition, float durationSeconds)
        {
            specialThreatWorldPosition = worldPosition;
            specialThreatWarningUntil = Time.unscaledTime + Mathf.Max(0.1f, durationSeconds);
            EnsureSpecialThreatWarning();
            UpdateSpecialThreatWarning();
        }

        private void EnsureSpecialThreatWarning()
        {
            if (specialThreatWarningText != null)
                return;

            GameObject warning = new GameObject(
                "SpecialThreatWarning",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            RectTransform rect = warning.GetComponent<RectTransform>();
            rect.SetParent(healthText != null && healthText.canvas != null ? HudLayout.PeripheralRoot(healthText.canvas) : transform, false);
            HudLayout.Corner(rect, Vector2.zero, new Vector2(28, 272), new Vector2(HudLayout.VitalsWidth, 48));

            specialThreatWarningText = warning.GetComponent<TextMeshProUGUI>();
            specialThreatWarningText.alignment = TextAlignmentOptions.Center;
            specialThreatWarningText.fontSize = 28f;
            specialThreatWarningText.fontStyle = FontStyles.Bold;
            specialThreatWarningText.raycastTarget = false;
            specialThreatWarningText.color = WarningColor;
            var outline = warning.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, -1);
            warning.SetActive(false);
        }

        private void UpdateSpecialThreatWarning()
        {
            if (specialThreatWarningText == null)
                return;

            bool visible = Time.unscaledTime < specialThreatWarningUntil;
            specialThreatWarningText.gameObject.SetActive(visible);
            if (!visible)
                return;

            Transform view = Camera.main != null ? Camera.main.transform : playerHealth?.transform;
            if (view == null)
            {
                specialThreatWarningText.text = "SCREAMER";
                return;
            }

            Vector3 direction = Vector3.ProjectOnPlane(
                specialThreatWorldPosition - view.position,
                Vector3.up).normalized;
            float forward = Vector3.Dot(view.forward, direction);
            float right = Vector3.Dot(view.right, direction);
            string marker = Mathf.Abs(right) > Mathf.Abs(forward)
                ? right >= 0f ? "▶" : "◀"
                : forward >= 0f ? "▲" : "▼";
            specialThreatWarningText.text = $"{marker}  SCREAMER  {marker}";
        }

        private void TryAcquireWeaponManager()
        {
            var local = playerHealth != null ? playerHealth.GetComponent<WeaponManager>() : null;
            if (weaponManager == local) return;
            weaponManager = local;
            if (weaponManager != null) UpdateWeaponUI();
            else { currentWeapon = unusedWeapon = null; SetAmmoUnavailable(); }
        }

        private void TryAcquireLocalPlayerHealth()
        {
            var localHealth = PlayerHealth.LocalInstance;
            var player = localHealth != null ? localHealth.gameObject : null;
            if (localHealth != null && !localHealth.IsOwner) localHealth = null;
            if (playerHealth == localHealth) return;
            UnsubscribeHealth();
            UnsubscribeInfection();
            if (survivalInventory != null) survivalInventory.InventoryChanged -= UpdateSurvivalInventoryUI;
            survivalInventory = null;
            if (survivalHotbar != null) survivalHotbar.Bind(null);
            ClearCombatFeedback();
            playerHealth = localHealth;
            if (playerHealth == null) { UpdateHealthUI(0, 100); return; }

            playerHealth.HealthChangedEvent += UpdateHealthUI;
            playerHealth.DirectionalDamageReceived += OnDirectionalDamageReceived;
            playerHealth.LifeStateChanged += OnLocalLifeStateChanged;
            playerHealth.PlayerDeathEvent += ClearCombatFeedback;
            playerHealth.CombatFeedbackReset += ClearCombatFeedback;
            UpdateHealthUI(playerHealth.CurrentHealth, playerHealth.MaxHealth);

            playerInfection = player.GetComponent<PlayerInfectionController>();
            if (playerInfection != null)
            {
                playerInfection.OnInfectionChanged += UpdateInfectionUI;
                UpdateInfectionUI(
                    playerInfection.CurrentInfection,
                    playerInfection.CurrentInfection,
                    playerInfection.CurrentStage);
            }
        }

        private void TryAcquireSurvivalInventory()
        {
            if (survivalInventory != null || playerHealth == null) return;
            survivalInventory = playerHealth.GetComponent<SurvivalInventory>();
            if (survivalInventory == null) return;
            survivalInventory.InventoryChanged += UpdateSurvivalInventoryUI;
            EnsureSurvivalInventoryLabels();
            UpdateSurvivalInventoryUI();
        }

        private void EnsureSurvivalInventoryLabels()
        {
            if (survivalHotbar != null) return;
            Canvas canvas = healthText != null ? healthText.canvas : GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = new GameObject("SurvivalHotbar", typeof(RectTransform));
            root.transform.SetParent(weaponIcon != null ? weaponIcon.transform.parent : HudLayout.PeripheralRoot(canvas), false);
            survivalHotbar = root.AddComponent<SurvivalHotbar>();
            survivalHotbar.Bind(survivalInventory);
            if (grenadeCount != null) grenadeCount.transform.parent.gameObject.SetActive(false);
            if (incendiaryCount != null) incendiaryCount.gameObject.SetActive(false);
            if (medkitCount != null) medkitCount.gameObject.SetActive(false);
            if (antidoteCount != null) antidoteCount.gameObject.SetActive(false);
        }

        private void UpdateSurvivalInventoryUI()
        {
            EnsureSurvivalInventoryLabels();
            if (survivalHotbar != null) survivalHotbar.Bind(survivalInventory);
        }

        private void UnsubscribeInfection()
        {
            if (playerInfection == null) return;
            playerInfection.OnInfectionChanged -= UpdateInfectionUI;
            playerInfection = null;
        }

        private void UnsubscribeHealth()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChangedEvent -= UpdateHealthUI;
                playerHealth.DirectionalDamageReceived -= OnDirectionalDamageReceived;
                playerHealth.LifeStateChanged -= OnLocalLifeStateChanged;
                playerHealth.PlayerDeathEvent -= ClearCombatFeedback;
                playerHealth.CombatFeedbackReset -= ClearCombatFeedback;
                playerHealth = null;
            }
        }

        private void OnDisconnectClicked()
        {
            NetworkGameManager.Instance?.Disconnect();
        }

        public void UpdateWeaponUI()
        {
            var manager = weaponManager != null ? weaponManager : WeaponManager.LocalInstance;
            if (currentWeapon != null) currentWeapon.AmmoChanged -= UpdateAmmoInfo;
            GameObject currentObj = manager != null ? manager.CurrentWeapon : null;
            GameObject unusedObj = manager != null ? manager.UnusedWeapon : null;

            currentWeapon = currentObj != null ? currentObj.GetComponent<Weapon>() : null;
            unusedWeapon = unusedObj != null && unusedObj != currentObj ? unusedObj.GetComponent<Weapon>() : null;

            if (isActiveAndEnabled && currentWeapon != null) currentWeapon.AmmoChanged += UpdateAmmoInfo;
            UpdateWeaponVisuals();
            UpdateAmmoInfo();
        }

        public void UpdateAmmoInfo()
        {
            if (ammoTypeUI != null) ammoTypeUI.enabled = currentWeapon != null;
            if (currentWeapon == null)
            {
                SetAmmoUnavailable();
                return;
            }

            int magazine = currentWeapon.Data != null ? currentWeapon.Data.magazineSize : Mathf.Max(currentWeapon.CurrentAmmo, 1);
            float ammoPercent = magazine > 0 ? currentWeapon.CurrentAmmo / (float)magazine : 0f;
            Color ammoColor = ammoPercent <= 0f ? CriticalColor : ammoPercent <= 0.25f ? WarningColor : TextColor;

            if (currentAmmo != null)
            {
                currentAmmo.text = currentWeapon.CurrentAmmo.ToString();
                currentAmmo.color = ammoColor;
            }

            if (reservedAmmo != null)
            {
                reservedAmmo.text = $"/ {currentWeapon.ReservedAmmo}";
                reservedAmmo.color = currentWeapon.ReservedAmmo <= 0 ? WarningColor : MutedColor;
                float reserveWidth = Mathf.Max(56, reservedAmmo.GetPreferredValues(reservedAmmo.text, float.PositiveInfinity, float.PositiveInfinity).x + 4);
                SelectedUiTheme.Place(reservedAmmo.rectTransform, 504 - reserveWidth, 132, reserveWidth, 40, 526, 218);
                if (currentAmmo != null) SelectedUiTheme.Place(currentAmmo.rectTransform, 452 - reserveWidth, 132, 52, 40, 526, 218);
            }

            if (ammoStatusText != null)
            {
                if (currentWeapon.IsReloading) { ammoStatusText.text = "RELOADING"; ammoStatusText.color = WarningColor; }
                else if (currentWeapon.CurrentAmmo <= 0 && currentWeapon.ReservedAmmo <= 0)
                {
                    ammoStatusText.text = "DRY";
                    ammoStatusText.color = CriticalColor;
                }
                else if (ammoPercent <= 0.25f)
                {
                    ammoStatusText.text = "LOW AMMO";
                    ammoStatusText.color = WarningColor;
                }
                else
                {
                    ammoStatusText.text = "READY";
                    ammoStatusText.color = AccentColor;
                }
            }

            UpdateWeaponVisuals();
        }

        private void SetAmmoUnavailable()
        {
            if (ammoTypeUI != null) ammoTypeUI.enabled = false;
            if (currentAmmo != null)
            {
                currentAmmo.text = "--";
                currentAmmo.color = MutedColor;
            }

            if (reservedAmmo != null)
            {
                reservedAmmo.text = "/ --";
                reservedAmmo.color = MutedColor;
            }

            if (ammoStatusText != null)
            {
                ammoStatusText.text = "NO WEAPON";
                ammoStatusText.color = MutedColor;
            }

            if (weaponNameText != null)
            {
                weaponNameText.text = "UNARMED";
                weaponNameText.color = MutedColor;
            }

            if (weaponIcon != null)
            {
                weaponIcon.enabled = false;
            }
        }

        private void UpdateWeaponVisuals()
        {
            UpdateWeaponPromptBindings();
            SetWeaponIcon(weaponIcon, currentWeapon);
            SetWeaponIcon(unusedWeaponIcon, unusedWeapon);

            if (weaponNameText != null)
            {
                weaponNameText.text = GetWeaponName(currentWeapon, "WEAPON");
                weaponNameText.color = currentWeapon != null ? TextColor : MutedColor;
            }

            if (unusedWeaponNameText != null)
            {
                unusedWeaponNameText.text = GetWeaponName(unusedWeapon, "EMPTY");
                unusedWeaponNameText.color = MutedColor;
            }
        }

        private void UpdateWeaponPromptBindings()
        {
            if (weaponNameText == null) return;
            if (primaryKeyLabel == null)
                foreach (var text in weaponNameText.transform.parent.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name == "PrimaryWeaponKeyText") primaryKeyLabel = text;
                    else if (text.name == "SlotKey") secondaryKeyLabel = text;
                }
            bool second = weaponManager != null && weaponManager.CurrentWeaponIndex == 1;
            if (primaryKeyLabel != null) primaryKeyLabel.text = SurvivalHotbar.Key(second ? "Weapon2" : "Weapon1", second ? "2" : "1");
            if (secondaryKeyLabel != null) secondaryKeyLabel.text = SurvivalHotbar.Key(second ? "Weapon1" : "Weapon2", second ? "1" : "2");
            UpdateActionPrompt("Fire", "FIRE", firePromptIcon, firePromptText);
            UpdateActionPrompt("Aim", "AIM", aimPromptIcon, aimPromptText);
        }

        private void UpdateActionPrompt(string action, string caption, Image icon, TMP_Text label)
        {
            string path = promptInput != null ? promptInput.GetPromptBindingPath(action) : string.Empty;
            string sprite = path.ToLowerInvariant() switch
            {
                "<mouse>/leftbutton" => "FPSSample/UI_HUD_LMB",
                "<mouse>/rightbutton" => "FPSSample/UI_HUD_RMB",
                _ => null
            };
            if (icon != null)
            {
                icon.enabled = sprite != null;
                icon.sprite = sprite != null ? HudSkin.Get(sprite) : null;
            }
            if (label == null) return;
            label.text = sprite != null ? caption : (promptInput != null ? promptInput.GetPromptDisplayName(action) : "--") + " " + caption;
            label.rectTransform.anchoredPosition = new Vector2(sprite != null ? 28 : 0, 0);
            label.rectTransform.sizeDelta = new Vector2(sprite != null ? 148 : 176, 24);
        }

        private void SetWeaponIcon(Image target, Weapon weapon)
        {
            if (target == null) return;

            Sprite icon = weapon != null && weapon.Data != null ? weapon.Data.weaponIcon : null;
            if (icon == null && weapon != null && weapon.Data != null && weapon.Data.weaponName == "Warden") icon = Theme.WardenIcon;
            if (weapon != null && weapon.Data != null && weapon.Data.weaponName == "Classic") icon = HudSkin.Get("Selected/Classic");
            target.sprite = icon;
            target.enabled = icon != null;
            target.color = Theme.Muted;
        }

        private string GetWeaponName(Weapon weapon, string fallback)
        {
            if (weapon == null || weapon.Data == null || string.IsNullOrWhiteSpace(weapon.Data.weaponName))
            {
                return fallback;
            }

            return weapon.Data.weaponName.ToUpperInvariant();
        }

        public void SetInteractionPrompt(string text, bool visible)
        {
            if (interactionPromptText == null)
                return;

            if (!string.IsNullOrEmpty(text))
            {
                bool locked = text.StartsWith("\U0001F512 ", System.StringComparison.Ordinal);
                interactionPromptText.text = locked ? text.Substring(3) : text;
                interactionPromptText.textWrappingMode = TextWrappingModes.Normal;
                interactionPromptText.overflowMode = TextOverflowModes.Overflow;
                if (locked && interactionLock == null)
                {
                    interactionLock = new GameObject("InteractionLock", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    interactionLock.transform.SetParent(interactionPromptText.transform, false);
                    HudSkin.Apply(interactionLock, "Locked", Theme.Text);
                    interactionLock.raycastTarget = false;
                    interactionLock.rectTransform.anchorMin = interactionLock.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    interactionLock.rectTransform.sizeDelta = new Vector2(24, 24);
                }
                if (interactionLock != null)
                {
                    interactionLock.gameObject.SetActive(locked);
                    interactionLock.rectTransform.anchoredPosition = new Vector2(-Mathf.Min(interactionPromptText.rectTransform.rect.width,
                        interactionPromptText.preferredWidth) * .5f - 20, 0);
                }
            }
            interactionPromptRequested = visible;
            UpdateInteractionPromptVisibility();
        }

        private void UpdateHealthUI(float current, float max)
        {
            max = Mathf.Max(1f, max);
            float percent = Mathf.Clamp01(current / max);
            int currentRounded = Mathf.CeilToInt(current);
            Color healthColor = percent <= 0.25f ? CriticalColor : percent <= 0.5f ? WarningColor : TextColor;

            if (healthText != null)
            {
                int maxRounded = Mathf.CeilToInt(max);
                healthText.text = $"{currentRounded} / {maxRounded}";
                healthText.color = healthColor;
            }

            if (healthStateText != null)
            {
                healthStateText.gameObject.SetActive(false);
            }

            if (healthFill != null)
            {
                healthFill.enabled = true;
                healthFill.fillAmount = percent;
                healthFill.color = healthColor;
            }

            vignetteTargetAlpha = Mathf.Clamp01((.45f - percent) / .45f) * .35f;
        }

        private void UpdateInfectionUI(float previous, float current, InfectionStage stage)
        {
            float normalized = Mathf.Clamp01(current / PlayerInfectionController.MaxInfection);
            if (infectionFill != null)
                infectionFill.fillAmount = normalized;
            if (infectionIcon != null)
                infectionIcon.enabled = current > 0.01f;
            if (infectionStageText != null)
            {
                infectionStageText.text = stage == InfectionStage.None
                    ? "INFECTION 0%"
                    : $"{stage.ToString().ToUpperInvariant()} {Mathf.CeilToInt(current)}%";
            }
            if (sepsisWarning != null)
                sepsisWarning.SetActive(stage == InfectionStage.Sepsis);
        }

        private void UpdateCombatInfo()
        {
            if (AIDirector.Instance != null)
            {
                if (killCountText != null)
                    killCountText.text = $"Kills: {AIDirector.Instance.TotalKills}";

                if (zombieCountText != null)
                    zombieCountText.text = $"Zombies Left: {AIDirector.Instance.ZombiesAlive}";
            }
            else
            {
                if (killCountText != null) killCountText.text = "Kills: --";
                if (zombieCountText != null) zombieCountText.text = "Zombies Left: --";
            }

            if (playerCountText != null)
            {
                playerCountText.text = NetworkGameManager.HasInstance
                    ? $"Squad: {NetworkGameManager.Instance.ConnectedPlayerCount}/4"
                    : "Squad: --";
            }

            if (difficultyText != null)
            {
                difficultyText.text = DifficultyManager.Instance != null
                    ? $"Difficulty: {DifficultyManager.Instance.CurrentDifficulty.Value}"
                    : "Difficulty: --";
            }

            if (grenadeCount != null)
            {
                grenadeCount.text = survivalInventory != null ? survivalInventory.FragGrenadeCount.Value.ToString() : "--";
            }

            if (grenadeKeyText != null)
            {
                grenadeKeyText.text = InputManager.Instance != null
                    ? FormatKeyName(InputManager.Instance.GetKeyForAction("Grenade"))
                    : "G";
            }
        }

        private void UpdateMatchFlowInfo()
        {
            NetworkMatchStateManager matchManager = NetworkMatchStateManager.Instance;
            if (matchStateText != null)
            {
                // Never leave an authored warmup/loading label visible while the manager is absent.
                bool showState = matchManager != null && matchManager.State == NetworkMatchState.GameOver;
                matchStateText.text = showState ? "GAME OVER" : string.Empty;
                matchStateText.gameObject.SetActive(showState);
            }

            bool showRespawn = playerHealth != null && playerHealth.IsDead;
            float remaining = matchManager != null ? matchManager.LocalRespawnRemainingSeconds : 0f;
            string respawnText = remaining > 0f
                ? $"RESPAWN IN {Mathf.CeilToInt(remaining)}"
                : "DOWN";

            if (respawnCountdownText != null)
            {
                respawnCountdownText.gameObject.SetActive(showRespawn);
                if (showRespawn)
                    respawnCountdownText.text = respawnText;
            }

            if (healthStateText != null)
            {
                healthStateText.gameObject.SetActive(showRespawn);
                if (showRespawn)
                {
                    healthStateText.text = respawnText;
                    healthStateText.color = WarningColor;
                }
            }
        }

        public void ShowHitConfirmed(HitboxZone zone, float finalDamage)
        {
            if (hitMarker == null || !LocalPlayerAlive) return;
            hitMarker.color = zone == HitboxZone.Head ? WarningColor : TextColor;
            hitMarkerUntil = Time.unscaledTime + hitMarkerDuration;
            hitMarker.enabled = HudLayout.GameplayVisible;
        }

        public void SetAimHudVisible(
            bool aiming,
            bool showScopeOverlay,
            Sprite scopeSprite = null,
            bool showAdsReticle = false)
        {
            EnsureCrosshairReference();
            if (showScopeOverlay)
                EnsureScopeOverlay();
            if (scopeOverlayImage != null && scopeSprite != null && scopeOverlayImage.sprite != scopeSprite)
            {
                scopeOverlayImage.sprite = scopeSprite;
                // Each sprite's authored pivot marks its optical center.
                Vector2 center = scopeSprite.pivot / scopeSprite.rect.size;
                RectTransform artworkRect = scopeOverlayImage.rectTransform;
                artworkRect.anchorMin = Vector2.one * .5f - center;
                artworkRect.anchorMax = Vector2.one * 1.5f - center;
                artworkRect.pivot = center;
                artworkRect.offsetMin = artworkRect.offsetMax = Vector2.zero;
                var fitter = artworkRect.parent.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                    fitter.aspectRatio = scopeSprite.rect.width / scopeSprite.rect.height;
            }
            if (scopeOverlayRoot != null)
                scopeOverlayRoot.SetActive(showScopeOverlay);
            if (crosshairRoot != null)
                crosshairRoot.SetActive(!aiming && !showScopeOverlay);

            // A physical-ADS weapon can opt into a small reflex-style marker.
            // Scope weapons never opt in, including their raise/lower transition.
            bool reticleVisible = aiming && showAdsReticle && !showScopeOverlay;
            if (reticleVisible)
                EnsureAdsReticle();
            if (adsReticleRect != null)
            {
                if (reticleVisible && adsReticleCanvas != null)
                {
                    // Keep the 3 px core readable without growing with CanvasScaler.
                    Vector2 size = Vector2.one * (10f / Mathf.Max(.01f, adsReticleCanvas.scaleFactor));
                    if (adsReticleRect.sizeDelta != size)
                        adsReticleRect.sizeDelta = size;
                }
                adsReticleRect.gameObject.SetActive(reticleVisible);
            }
        }

        private void EnsureAdsReticle()
        {
            if (adsReticleRect != null)
                return;

            Canvas canvas = crosshairRoot != null
                ? crosshairRoot.GetComponentInParent<Canvas>(true)
                : GetComponentInParent<Canvas>(true);
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
                return;

            adsReticleCanvas = canvas.rootCanvas;
            GameObject reticle = new GameObject(
                "ADSReflexReticle", typeof(RectTransform), typeof(Image));
            reticle.layer = adsReticleCanvas.gameObject.layer;
            adsReticleRect = (RectTransform)reticle.transform;
            adsReticleRect.SetParent(adsReticleCanvas.transform, false);
            adsReticleRect.anchorMin = adsReticleRect.anchorMax = Vector2.one * .5f;
            adsReticleRect.pivot = Vector2.one * .5f;
            adsReticleRect.anchoredPosition = Vector2.zero;
            adsReticleRect.SetAsLastSibling();
            Image graphic = reticle.GetComponent<Image>();
            graphic.sprite = adsReticleSprite = HudSkin.Get("ADSReflexDot");
            graphic.color = Theme.Text;
            var outline = reticle.AddComponent<Outline>();
            outline.effectColor = Theme.Background;
            outline.effectDistance = Vector2.one * (.7f / Mathf.Max(.01f, adsReticleCanvas.scaleFactor));
            graphic.raycastTarget = false;
        }

        public void SetScopeVisible(bool visible)
        {
            SetAimHudVisible(visible, visible);
        }

        private void EnsureCrosshairReference()
        {
            if (crosshairRoot != null)
                return;

            foreach (Canvas candidateCanvas in FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
            {
                foreach (RectTransform child in candidateCanvas.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.name != "Crosshair")
                        continue;

                    crosshairRoot = child.gameObject;
                    return;
                }
            }
        }

        public bool ValidateCrosshairAlignment(out float pixelError)
        {
            EnsureCrosshairReference();
            RectTransform crosshairRect = crosshairRoot != null
                ? crosshairRoot.GetComponent<RectTransform>()
                : null;
            Canvas canvas = crosshairRect != null
                ? crosshairRect.GetComponentInParent<Canvas>(true)
                : null;
            if (crosshairRect == null || canvas == null)
            {
                pixelError = float.PositiveInfinity;
                return false;
            }

            Canvas.ForceUpdateCanvases();
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector3 crosshairWorldCenter = crosshairRect.TransformPoint(crosshairRect.rect.center);
            Vector2 crosshairPixel = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                crosshairWorldCenter);
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            pixelError = Vector2.Distance(crosshairPixel, screenCenter);
            return pixelError <= 0.5f;
        }

        private void ValidateCrosshairAlignmentIfNeeded()
        {
            Vector2 hudSize = peripheralRoot != null ? peripheralRoot.rect.size : Vector2.zero;
            if (validatedCrosshairScreenWidth == Screen.width
                && validatedCrosshairScreenHeight == Screen.height && validatedHudSize == hudSize)
            {
                return;
            }

            validatedCrosshairScreenWidth = Screen.width;
            validatedCrosshairScreenHeight = Screen.height;
            validatedHudSize = hudSize;
            RefreshInteractionLayout(CampaignHUD.Instance != null ? CampaignHUD.Instance.InteractionBandBottom : HudLayout.InteractionBottom);
            if (ValidateCrosshairAlignment(out float pixelError))
            {
                crosshairAlignmentWarningIssued = false;
                return;
            }

            if (crosshairAlignmentWarningIssued || float.IsPositiveInfinity(pixelError))
                return;

            crosshairAlignmentWarningIssued = true;
            GameLog.Warning(() =>
                $"[HUD] Crosshair is {pixelError:F2}px away from screen center at {Screen.width}x{Screen.height}; allowed error is 0.5px.");
        }

        public float RefreshInteractionLayout(float bottom)
        {
            if (interactionPromptText == null) return HudLayout.InteractionHeight;
            HudLayout.CentralBand(interactionPromptText.rectTransform, interactionPromptText.canvas, bottom, HudLayout.InteractionHeight);
            float height = Mathf.Max(HudLayout.InteractionHeight, interactionPromptText.preferredHeight);
            interactionPromptText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return height;
        }

        private void EnsureScopeOverlay()
        {
            if (scopeOverlayRoot != null)
            {
                if (scopeOverlayImage == null)
                    scopeOverlayImage = scopeOverlayRoot.GetComponentInChildren<Image>(true);
                return;
            }

            Canvas canvas = crosshairRoot != null
                ? crosshairRoot.GetComponentInParent<Canvas>(true)
                : FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
                return;

            GameObject root = new GameObject(
                "ScopeOverlay",
                typeof(RectTransform),
                typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            // The opaque optic frame must stay behind ammo, prompts and health text.
            rootRect.SetAsFirstSibling();
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;

            // AspectRatioFitter owns the fitted frame's anchors and position, so
            // keep that RectTransform centered. A separate child offsets the
            // authored reticle point without fighting the fitter's driven values.
            GameObject artworkFrame = new GameObject(
                "ScopeArtworkFrame",
                typeof(RectTransform),
                typeof(AspectRatioFitter));
            artworkFrame.transform.SetParent(rootRect, false);
            RectTransform frameRect = (RectTransform)artworkFrame.transform;
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = Vector2.zero;

            GameObject artwork = new GameObject(
                "ScopeArtwork",
                typeof(RectTransform),
                typeof(Image));
            artwork.transform.SetParent(frameRect, false);
            scopeOverlayImage = artwork.GetComponent<Image>();
            scopeOverlayImage.preserveAspect = true;
            scopeOverlayImage.raycastTarget = false;

            AspectRatioFitter fitter = artworkFrame.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            scopeOverlayRoot = root;
            scopeOverlayRoot.SetActive(false);
        }

        private bool LocalPlayerAlive => playerHealth != null && playerHealth.IsOwner && playerHealth.IsSpawned
            && !playerHealth.IsDead && playerHealth.LifeState == PlayerLifeState.Alive;

        private void OnLocalLifeStateChanged(PlayerLifeState previous, PlayerLifeState current)
        {
            RefreshMatch();
            ClearCombatFeedback();
            UpdateHealthUI(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void ClearCombatFeedback()
        {
            hitMarkerUntil = 0;
            vignetteAlpha = vignetteVelocity = 0;
            if (hitMarker != null) hitMarker.enabled = false;
            if (healthDangerBackground != null) healthDangerBackground.enabled = false;
            for (int i = 0; i < damageCues.Length; i++)
            {
                damageCues[i] = default;
                if (i < directionalDamageIndicators.Length && directionalDamageIndicators[i] != null)
                    directionalDamageIndicators[i].enabled = false;
            }
        }

        private bool TryDamageBearing(Vector3 source, out float angle)
        {
            angle = 0;
            if (playerHealth == null || !float.IsFinite(source.x) || !float.IsFinite(source.y) || !float.IsFinite(source.z)) return false;
            Vector3 direction = source - playerHealth.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .0001f || !float.IsFinite(direction.sqrMagnitude)) return false;
            Transform view = Camera.main != null ? Camera.main.transform : playerHealth.transform;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            // At vertical pitch, camera right still describes the owner's yaw.
            if (forward.sqrMagnitude < .0001f) forward = Vector3.Cross(view.right, Vector3.up);
            angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            return true;
        }

        private void OnDirectionalDamageReceived(Vector3 source)
        {
            if (!LocalPlayerAlive || !TryDamageBearing(source, out float bearing)) return;
            float now = Time.unscaledTime;
            int slot = -1, oldest = 0;
            for (int i = 0; i < damageCues.Length; i++)
            {
                if (damageCues[i].until > now && TryDamageBearing(damageCues[i].source, out float existing)
                    && Mathf.Abs(Mathf.DeltaAngle(existing, bearing)) <= 30f)
                { slot = i; break; }
                if (slot < 0 && damageCues[i].until <= now) slot = i;
                if (damageCues[i].until < damageCues[oldest].until) oldest = i;
            }
            damageCues[slot < 0 ? oldest : slot] = new DamageCue { source = source, until = now + 1f };
        }

        private void UpdateCombatFeedback()
        {
            bool alive = LocalPlayerAlive;
            if (!alive) ClearCombatFeedback();
            bool visible = alive && HudLayout.GameplayVisible;
            float now = Time.unscaledTime;
            if (hitMarker != null) hitMarker.enabled = visible && now < hitMarkerUntil;
            vignetteAlpha = Mathf.SmoothDamp(vignetteAlpha, alive ? vignetteTargetAlpha : 0,
                ref vignetteVelocity, .15f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (healthDangerBackground != null)
            {
                healthDangerBackground.color = SelectedUiTheme.WithAlpha(Theme.Danger, vignetteAlpha);
                healthDangerBackground.enabled = visible && vignetteAlpha > .001f;
            }
            for (int i = 0; i < damageCues.Length; i++)
            {
                if (i >= directionalDamageIndicators.Length || directionalDamageIndicators[i] == null) continue;
                Image image = directionalDamageIndicators[i];
                float remaining = damageCues[i].until - now;
                bool valid = remaining > 0 && TryDamageBearing(damageCues[i].source, out _);
                image.enabled = visible && valid;
                if (!valid) continue;
                TryDamageBearing(damageCues[i].source, out float angle);
                float radians = angle * Mathf.Deg2Rad;
                image.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * 128;
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
                Color tint = CriticalColor; tint.a = Mathf.Clamp01(remaining / .3f); image.color = tint;
            }
        }

        private static string FormatKeyName(KeyCode key)
        {
            return key switch
            {
                KeyCode.Mouse0 => "M0",
                KeyCode.Mouse1 => "M1",
                KeyCode.Mouse2 => "M2",
                KeyCode.Alpha0 => "0",
                KeyCode.Alpha1 => "1",
                KeyCode.Alpha2 => "2",
                KeyCode.Alpha3 => "3",
                KeyCode.Alpha4 => "4",
                KeyCode.Alpha5 => "5",
                KeyCode.Alpha6 => "6",
                KeyCode.Alpha7 => "7",
                KeyCode.Alpha8 => "8",
                KeyCode.Alpha9 => "9",
                _ => key.ToString().Replace("Keypad", "N")
            };
        }
    }
}

