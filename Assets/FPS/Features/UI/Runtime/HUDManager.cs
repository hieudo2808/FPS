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
        [SerializeField] private Image treatmentProgressFill;
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
        [SerializeField] private TextMeshProUGUI hitMarkerText;
        [SerializeField] private float hitMarkerDuration = 0.18f;

        [Header("Aiming")]
        [SerializeField] private GameObject crosshairRoot;
        [SerializeField] private GameObject scopeOverlayRoot;
        [SerializeField] private Image scopeOverlayImage;

        [Header("Prompts")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;
        [SerializeField] private GameObject waveAnnouncementPanel;
        [SerializeField] private TextMeshProUGUI waveAnnouncementText;

        [Header("Special Threat")]
        [SerializeField] private TextMeshProUGUI specialThreatWarningText;

        private static readonly Color TextColor = TacticalUiTheme.Text;
        private static readonly Color MutedColor = TacticalUiTheme.Muted;
        private static readonly Color AccentColor = TacticalUiTheme.Accent;
        // Unity's sprite pivot uses bottom-left normalized coordinates. The
        // reticle crossing in HUDScope.png is at image-space y = 486 from top.
        private static readonly Vector2 OperatorScopeReticleNormalizedPosition =
            new Vector2(0.5f, 538f / 1024f);
        private static readonly Color WarningColor = new Color(0.94f, 0.68f, 0.23f, 1f);
        private static readonly Color CriticalColor = new Color(0.95f, 0.24f, 0.24f, 1f);

        private WeaponManager weaponManager;
        private Weapon currentWeapon;
        private Weapon unusedWeapon;
        private PlayerHealth playerHealth;
        private PlayerInfectionController playerInfection;
        private SurvivalInventory survivalInventory;
        private float hitMarkerTimer;
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

            if (ammoTypeUI != null)
            {
                ammoTypeUI.enabled = false;
            }

            if (hitMarkerText != null)
            {
                hitMarkerText.gameObject.SetActive(false);
            }

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
            UpdateHitMarkerTimer();
            UpdateTreatmentUI();
            UpdateSurvivalInventoryUI();
            UpdateSpecialThreatWarning();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

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
            rect.SetParent(healthText != null && healthText.canvas != null ? healthText.canvas.transform : transform, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -144f);
            rect.sizeDelta = new Vector2(520f, 52f);

            specialThreatWarningText = warning.GetComponent<TextMeshProUGUI>();
            specialThreatWarningText.alignment = TextAlignmentOptions.Center;
            specialThreatWarningText.fontSize = 28f;
            specialThreatWarningText.fontStyle = FontStyles.Bold;
            specialThreatWarningText.raycastTarget = false;
            specialThreatWarningText.color = WarningColor;
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
            if (weaponManager != null) return;

            weaponManager = WeaponManager.LocalInstance;
            if (weaponManager != null)
            {
                UpdateWeaponUI();
            }
        }

        private void TryAcquireLocalPlayerHealth()
        {
            if (playerHealth != null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient) return;

            NetworkClient localClient = NetworkManager.Singleton.LocalClient;
            if (localClient == null || localClient.PlayerObject == null) return;

            playerHealth = localClient.PlayerObject.GetComponent<PlayerHealth>();
            if (playerHealth == null) return;

            playerHealth.HealthChangedEvent += UpdateHealthUI;
            UpdateHealthUI(playerHealth.CurrentHealth, playerHealth.MaxHealth);

            playerInfection = localClient.PlayerObject.GetComponent<PlayerInfectionController>();
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
            if (survivalInventory != null) return;
            NetworkClient client = NetworkManager.Singleton?.LocalClient;
            NetworkObject player = client?.PlayerObject;
            if (player == null) return;
            survivalInventory = player.GetComponent<SurvivalInventory>();
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
            root.transform.SetParent(canvas.transform, false);
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
                unusedWeaponNameText.color = unusedWeapon != null ? MutedColor : new Color(0.35f, 0.43f, 0.48f, 1f);
            }
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
            interactionPromptText.gameObject.SetActive(visible);
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
                healthText.text = $"{currentRounded}/{maxRounded}";
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

            if (healthDangerBackground != null)
            {
                bool showDanger = percent <= 0.45f;
                healthDangerBackground.enabled = showDanger;
                healthDangerBackground.color = percent <= 0.25f
                    ? new Color(0.78f, 0.05f, 0.05f, 0.42f)
                    : new Color(0.78f, 0.18f, 0.05f, 0.30f);
            }
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

        private void UpdateTreatmentUI()
        {
            if (treatmentProgressFill == null)
                return;
            float progress = survivalInventory != null && survivalInventory.IsUsingItem ? survivalInventory.UseProgress : (playerInfection != null ? playerInfection.ActiveTreatmentProgress : 0f);
            treatmentProgressFill.fillAmount = progress;
            treatmentProgressFill.gameObject.SetActive(progress > 0f && progress < 1f);
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
            if (hitMarkerText == null)
                return;

            hitMarkerText.text = zone == HitboxZone.Head
                ? $"HEADSHOT {Mathf.CeilToInt(finalDamage)}"
                : $"+{Mathf.CeilToInt(finalDamage)}";
            hitMarkerText.color = zone == HitboxZone.Head ? WarningColor : AccentColor;
            hitMarkerText.gameObject.SetActive(true);
            hitMarkerTimer = hitMarkerDuration;
        }

        public void SetAimHudVisible(
            bool aiming,
            bool showScopeOverlay,
            Sprite scopeSprite = null,
            bool showAdsReticle = false)
        {
            EnsureCrosshairReference();
            if (showScopeOverlay)
                EnsureScopeOverlay(scopeSprite);
            if (scopeOverlayImage != null && scopeSprite != null)
                scopeOverlayImage.sprite = scopeSprite;
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

        private void EnsureScopeOverlay(Sprite scopeSprite)
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
                "OperatorScopeOverlay",
                typeof(RectTransform),
                typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.SetAsLastSibling();
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
            RectTransform artworkRect = (RectTransform)artwork.transform;
            artworkRect.anchorMin = Vector2.one * 0.5f - OperatorScopeReticleNormalizedPosition;
            artworkRect.anchorMax = Vector2.one * 1.5f - OperatorScopeReticleNormalizedPosition;
            artworkRect.pivot = OperatorScopeReticleNormalizedPosition;
            artworkRect.offsetMin = Vector2.zero;
            artworkRect.offsetMax = Vector2.zero;

            scopeOverlayImage = artwork.GetComponent<Image>();
            scopeOverlayImage.sprite = scopeSprite;
            scopeOverlayImage.preserveAspect = true;
            scopeOverlayImage.raycastTarget = false;

            AspectRatioFitter fitter = artworkFrame.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = scopeSprite != null && scopeSprite.rect.height > 0f
                ? scopeSprite.rect.width / scopeSprite.rect.height
                : 1.5f;

            scopeOverlayRoot = root;
            scopeOverlayRoot.SetActive(false);
        }

        private void UpdateHitMarkerTimer()
        {
            if (hitMarkerText == null || hitMarkerTimer <= 0f)
                return;

            hitMarkerTimer -= Time.deltaTime;
            if (hitMarkerTimer <= 0f)
                hitMarkerText.gameObject.SetActive(false);
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

