using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class HUDManager : SceneSingleton<HUDManager>
    {
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
        [SerializeField] private GameObject waveAnnouncementPanel;
        [SerializeField] private TextMeshProUGUI waveAnnouncementText;

        [Header("Special Threat")]
        [SerializeField] private TextMeshProUGUI specialThreatWarningText;

        private static readonly Color TextColor = TacticalUiTheme.Text;
        private static readonly Color MutedColor = TacticalUiTheme.Muted;
        private static readonly Color AccentColor = TacticalUiTheme.Accent;
        private static readonly Color WarningColor = new Color(0.94f, 0.68f, 0.23f, 1f);
        private static readonly Color CriticalColor = new Color(0.95f, 0.24f, 0.24f, 1f);

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
        private bool crosshairAlignmentWarningIssued;
        private RectTransform adsReticleRect;
        private Canvas adsReticleCanvas;
        private Sprite adsReticleSprite;
        private Texture2D adsReticleTexture;
        private SurvivalHotbar survivalHotbar;
        private RectTransform peripheralRoot;
        private GameObject centerHudRoot;
        private InputManager promptInput;
        private TMP_Text primaryKeyLabel, secondaryKeyLabel;

        private void Start()
        {
            // Director phases are internal pacing state, not player-facing HUD information.
            if (phaseText != null)
            {
                phaseText.text = string.Empty;
                phaseText.gameObject.SetActive(false);
            }
            SpecialThreatSignal.Raised += OnSpecialThreatRaised;
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
            UpdateMatchFlowInfo();
        }

        private void Update()
        {
            if (promptInput != InputManager.Instance)
            {
                if (promptInput != null) promptInput.PromptBindingsChanged -= UpdateWeaponPromptBindings;
                promptInput = InputManager.Instance;
                if (promptInput != null) promptInput.PromptBindingsChanged += UpdateWeaponPromptBindings;
                UpdateWeaponPromptBindings();
            }
            if (healthText != null)
            {
                peripheralRoot ??= HudLayout.PeripheralRoot(healthText.canvas.rootCanvas);
                peripheralRoot.gameObject.SetActive(HudLayout.GameplayVisible);
                if (centerHudRoot == null && healthText.canvas.rootCanvas.transform.Find("MinimalHUDRoot") is Transform center)
                    centerHudRoot = center.gameObject;
                if (centerHudRoot != null) centerHudRoot.SetActive(HudLayout.GameplayVisible);
                if (adsReticleRect != null) adsReticleRect.GetComponent<Image>().enabled = HudLayout.GameplayVisible;
                if (scopeOverlayImage != null) scopeOverlayImage.enabled = HudLayout.GameplayVisible;
                if (interactionPromptText != null) HudLayout.CentralBand(interactionPromptText.rectTransform, healthText.canvas, HudLayout.InteractionBottom, 52);
            }
            TryAcquireLocalPlayerHealth();
            TryAcquireWeaponManager();
            TryAcquireSurvivalInventory();
            ValidateCrosshairAlignmentIfNeeded();

            if (weaponManager != null)
            {
                UpdateAmmoInfo();
            }

            UpdateCombatInfo();
            UpdateMatchFlowInfo();
            UpdateCombatFeedback();
            UpdateSpecialThreatWarning();
        }

        private void LateUpdate() => UpdateInteractionPromptVisibility();

        private void UpdateInteractionPromptVisibility()
        {
            if (interactionPromptText != null)
                interactionPromptText.gameObject.SetActive(interactionPromptRequested && HudLayout.GameplayVisible
                    && CampaignHUD.Instance?.HasInteractionMessage != true);
        }

        protected override void OnDestroy()
        {
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
            if (adsReticleSprite != null)
            {
                if (Application.isPlaying)
                    Destroy(adsReticleSprite);
                else
                    DestroyImmediate(adsReticleSprite);
            }
            if (adsReticleTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(adsReticleTexture);
                else
                    DestroyImmediate(adsReticleTexture);
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
            var network = NetworkManager.Singleton;
            var player = network != null && network.IsConnectedClient ? network.LocalClient?.PlayerObject : null;
            var localHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
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
            root.transform.SetParent(HudLayout.PeripheralRoot(canvas), false);
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
            if (WeaponManager.LocalInstance == null) return;

            GameObject currentObj = WeaponManager.LocalInstance.CurrentWeapon;
            GameObject unusedObj = WeaponManager.LocalInstance.UnusedWeapon;

            currentWeapon = currentObj != null ? currentObj.GetComponent<Weapon>() : null;
            unusedWeapon = unusedObj != null && unusedObj != currentObj ? unusedObj.GetComponent<Weapon>() : null;

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
            }

            if (ammoStatusText != null)
            {
                if (currentWeapon.CurrentAmmo <= 0 && currentWeapon.ReservedAmmo <= 0)
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
            target.sprite = icon;
            target.enabled = icon != null;
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
                interactionPromptText.text = text;
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
                healthText.text = $"{currentRounded}<size=65%> / {maxRounded}</size>";
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
            graphic.sprite = CreateAdsReticleSprite();
            graphic.color = Color.white;
            graphic.raycastTarget = false;
        }

        private Sprite CreateAdsReticleSprite()
        {
            if (adsReticleSprite != null)
                return adsReticleSprite;

            const int size = 32;
            adsReticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime_ADSReflexReticle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Vector2 center = Vector2.one * size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), center) / (size * .5f);
                Color pixel = new Color(.15f, 1f, .8f, .22f * (1f - Mathf.SmoothStep(.44f, 1f, distance)));
                if (distance <= .44f)
                    pixel = new Color(.02f, .06f, .07f, .9f); // contrast on light backgrounds
                if (distance <= .3f)
                    pixel = new Color(.15f, 1f, .8f, 1f); // 3 px reflex core in a 10 px image
                adsReticleTexture.SetPixel(x, y, pixel);
            }
            adsReticleTexture.Apply(false, true);
            adsReticleSprite = Sprite.Create(
                adsReticleTexture,
                new Rect(0f, 0f, size, size),
                Vector2.one * .5f,
                size);
            adsReticleSprite.name = "Runtime_ADSReflexReticle";
            return adsReticleSprite;
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
            if (validatedCrosshairScreenWidth == Screen.width
                && validatedCrosshairScreenHeight == Screen.height)
            {
                return;
            }

            validatedCrosshairScreenWidth = Screen.width;
            validatedCrosshairScreenHeight = Screen.height;
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
                healthDangerBackground.color = new Color(.78f, .05f, .05f, vignetteAlpha);
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

