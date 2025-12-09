using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class HUDManager : Singleton<HUDManager>
    {
        [Header("Ammo")]
        [SerializeField] private TextMeshProUGUI currentAmmo;
        [SerializeField] private TextMeshProUGUI reservedAmmo;
        [SerializeField] private Image ammoTypeUI;

        [Header("Weapon")]
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Image unusedWeaponIcon;

        [Header("Throwables")]
        [SerializeField] private TextMeshProUGUI grenadeCount;

        private WeaponManager weaponManager;
        private Weapon currentWeapon, unusedWeapon;

        void Start()
        {
            weaponManager = WeaponManager.Instance;
            UpdateWeaponUI();
        }

        private void Update()
        {
            if (weaponManager != null) {
                UpdateAmmoInfo();
            }
        }

        public void UpdateWeaponUI()
        {
            if (WeaponManager.Instance == null) return;

            currentWeapon = WeaponManager.Instance.CurrentWeapon.GetComponent<Weapon>();
            unusedWeapon = WeaponManager.Instance.UnusedWeapon.GetComponent<Weapon>();
        }

        public void UpdateAmmoInfo()
        {
            if (currentWeapon != null)
            {
                currentAmmo.text = currentWeapon.CurrentAmmo.ToString();
                reservedAmmo.text = currentWeapon.ReservedAmmo.ToString();
                weaponIcon.sprite = currentWeapon.WeaponIcon;
            }

            if (unusedWeapon != null)
            {
                unusedWeaponIcon.sprite = unusedWeapon.WeaponIcon;
            }
        }
    }
}