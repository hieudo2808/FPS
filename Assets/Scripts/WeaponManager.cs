using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class WeaponManager : Singleton<WeaponManager>
    {
        [SerializeField] private List<GameObject> weapons;
        [SerializeField] private Animator characterAnimation;
        private int currentWeaponIndex = 0;

        public GameObject CurrentWeapon => weapons[currentWeaponIndex];
        public GameObject UnusedWeapon => weapons[(currentWeaponIndex + 1) % weapons.Count];
        public Animator CharacterAnimation => characterAnimation;

        private void Start()
        {
            // Khởi tạo và ẩn tất cả vũ khí trừ vũ khí đầu tiên
            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].SetActive(i == currentWeaponIndex);
            }

            // Cập nhật UI ban đầu
            HUDManager.Instance.UpdateWeaponUI();
        }

        private void Update()
        {
            if (weapons.Count > 0)
                currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, weapons.Count - 1);

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2))
            {
                SwitchToNextWeapon();
            }
        }

        private void SwitchWeapon(int newIndex)
        {
            if (newIndex == currentWeaponIndex || newIndex >= weapons.Count) return;

            // Ẩn vũ khí hiện tại
            weapons[currentWeaponIndex].gameObject.SetActive(false);

            // Hiện vũ khí mới
            currentWeaponIndex = newIndex;
            weapons[currentWeaponIndex].gameObject.SetActive(true);

            // Cập nhật UI
            HUDManager.Instance.UpdateWeaponUI();
        }

        private void SwitchToNextWeapon()
        {
            int index = (currentWeaponIndex + 1) % weapons.Count;
            SwitchWeapon(index);
        }

        public void AddWeapon(GameObject newWeapon)
        {
            if (weapons.Count < 2)
            {
                weapons.Add(newWeapon);
            }
        }
    }
}