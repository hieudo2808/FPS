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

        private static readonly Color TextColor = new Color(0.92f, 0.96f, 0.97f, 1f);
        private static readonly Color MutedColor = new Color(0.55f, 0.65f, 0.70f, 1f);
        private static readonly Color AccentColor = new Color(0.12f, 0.82f, 0.75f, 1f);
        private static readonly Color WarningColor = new Color(0.94f, 0.68f, 0.23f, 1f);
        private static readonly Color CriticalColor = new Color(0.95f, 0.24f, 0.24f, 1f);

        private WeaponManager weaponManager;
        private Weapon currentWeapon;
        private Weapon unusedWeapon;
        private PlayerHealth playerHealth;
        private float hitMarkerTimer;

        private void Start()
        {
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
            UpdateAmmoInfo();
            UpdateCombatInfo();
            UpdateMatchFlowInfo();
        }

        private void Update()
        {
            TryAcquireLocalPlayerHealth();
            TryAcquireWeaponManager();

            if (weaponManager != null)
            {
                UpdateAmmoInfo();
            }

            UpdateCombatInfo();
            UpdateMatchFlowInfo();
            UpdateHitMarkerTimer();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (disconnectButton != null)
            {
                disconnectButton.onClick.RemoveListener(OnDisconnectClicked);
            }

            UnsubscribeHealth();
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
                healthFill.enabled = false;
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

        private void UpdateCombatInfo()
        {
            if (NetworkMatchStateManager.HasInstance && NetworkMatchStateManager.Instance.State != NetworkMatchState.Playing)
            {
                if (phaseText != null)
                    phaseText.text = FormatMatchState(NetworkMatchStateManager.Instance);
            }
            else if (AIDirector.Instance != null)
            {
                if (phaseText != null)
                    phaseText.text = $"{AIDirector.Instance.CurrentPhase.ToString().ToUpperInvariant()} PHASE";
            }

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
                if (phaseText != null && !NetworkMatchStateManager.HasInstance) phaseText.text = "-- PHASE";
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
                // Placeholder until grenade inventory exists.
                grenadeCount.text = "2";
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
            if (matchManager != null && matchStateText != null)
            {
                // Gameplay sạch như game thật: chỉ hiện banner khi GAME OVER.
                bool showState = matchManager.State == NetworkMatchState.GameOver;
                if (showState)
                    matchStateText.text = FormatMatchState(matchManager);
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

        private static string FormatMatchState(NetworkMatchStateManager matchManager)
        {
            return matchManager.State switch
            {
                NetworkMatchState.Warmup => $"WARMUP {Mathf.CeilToInt(matchManager.WarmupRemainingSeconds)}",
                NetworkMatchState.Playing => "PLAYING",
                NetworkMatchState.GameOver => "GAME OVER",
                NetworkMatchState.Loading => "LOADING",
                _ => "LOBBY"
            };
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

        public void SetAimHudVisible(bool aiming, bool showScopeOverlay, Sprite scopeSprite = null)
        {
            EnsureCrosshairReference();
            if (showScopeOverlay)
                EnsureScopeOverlay(scopeSprite);
            if (scopeOverlayImage != null && scopeSprite != null)
                scopeOverlayImage.sprite = scopeSprite;
            if (scopeOverlayRoot != null)
                scopeOverlayRoot.SetActive(showScopeOverlay);
            if (crosshairRoot != null)
                crosshairRoot.SetActive(!aiming);
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

            GameObject artwork = new GameObject(
                "ScopeArtwork",
                typeof(RectTransform),
                typeof(Image),
                typeof(AspectRatioFitter));
            artwork.transform.SetParent(rootRect, false);
            RectTransform artworkRect = (RectTransform)artwork.transform;
            artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
            artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
            artworkRect.pivot = new Vector2(0.5f, 0.5f);
            artworkRect.anchoredPosition = Vector2.zero;

            scopeOverlayImage = artwork.GetComponent<Image>();
            scopeOverlayImage.sprite = scopeSprite;
            scopeOverlayImage.preserveAspect = true;
            scopeOverlayImage.raycastTarget = false;

            AspectRatioFitter fitter = artwork.GetComponent<AspectRatioFitter>();
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
