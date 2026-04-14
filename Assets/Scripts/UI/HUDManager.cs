using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class HUDManager : SceneSingleton<HUDManager>
    {
        [Header("In-Game Menu")]
        [SerializeField] private Button disconnectButton;
        [Header("Ammo")]
        [SerializeField] private TextMeshProUGUI currentAmmo;
        [SerializeField] private TextMeshProUGUI reservedAmmo;
        [SerializeField] private Image ammoTypeUI;

        [Header("Weapon")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image unusedWeaponIcon;

        [Header("Throwables")]
        [SerializeField] private TextMeshProUGUI grenadeCount;
        
        [Header("Kill Counter")]
        [SerializeField] private TextMeshProUGUI killCountText;

        [Header("Network Info")]
        [SerializeField] private TextMeshProUGUI playerCountText;

        private WeaponManager weaponManager;
        private Weapon currentWeapon, unusedWeapon;

        void Start()
        {
            // Try to get local player's WeaponManager
            weaponManager = WeaponManager.LocalInstance;
            if (weaponManager != null)
                UpdateWeaponUI();

            // Disconnect button
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        private void Update()
        {
            // Escape key → disconnect
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnDisconnectClicked();
                return;
            }

            // Re-acquire if not found yet (player may spawn later)
            if (weaponManager == null)
            {
                weaponManager = WeaponManager.LocalInstance;
                if (weaponManager != null)
                    UpdateWeaponUI();
            }

            if (weaponManager != null)
            {
                UpdateAmmoInfo();
            }

            UpdateKillCount();
            UpdatePlayerCount();
        }

        private void OnDisconnectClicked()
        {
            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.Disconnect();
        }

        public void UpdateWeaponUI()
        {
            if (WeaponManager.LocalInstance == null) return;

            var currentObj = WeaponManager.LocalInstance.CurrentWeapon;
            var unusedObj = WeaponManager.LocalInstance.UnusedWeapon;

            if (currentObj != null) 
                currentWeapon = currentObj.GetComponent<Weapon>();
            else 
                currentWeapon = null;

            // Only set unused weapon if it's different from current
            if (unusedObj != null && unusedObj != currentObj) 
                unusedWeapon = unusedObj.GetComponent<Weapon>();
            else 
                unusedWeapon = null;
        }

        public void UpdateAmmoInfo()
        {
            if (currentWeapon != null)
            {
                if (currentAmmo != null) currentAmmo.text = currentWeapon.CurrentAmmo.ToString();
                if (reservedAmmo != null) reservedAmmo.text = currentWeapon.ReservedAmmo.ToString();
                if (weaponIcon != null && currentWeapon.WeaponIcon != null) 
                {
                    weaponIcon.sprite = currentWeapon.WeaponIcon;
                    weaponIcon.enabled = true;
                }
            }
            else
            {
                if (weaponIcon != null) weaponIcon.enabled = false;
            }

            if (unusedWeapon != null)
            {
                if (unusedWeaponIcon != null && unusedWeapon.WeaponIcon != null)
                {
                    unusedWeaponIcon.sprite = unusedWeapon.WeaponIcon;
                    unusedWeaponIcon.enabled = true;
                }
            }
            else
            {
                if (unusedWeaponIcon != null) unusedWeaponIcon.enabled = false;
            }
        }
        
        private void UpdateKillCount()
        {
            if (killCountText != null && AIDirector.Instance != null)
            {
                killCountText.text = $"Kills: {AIDirector.Instance.TotalKills}";
            }
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && NetworkGameManager.HasInstance)
            {
                playerCountText.text = $"Players: {NetworkGameManager.Instance.ConnectedPlayerCount}";
            }
        }
    }
}