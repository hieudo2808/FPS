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
            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].SetActive(i == currentWeaponIndex);
            }

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

            weapons[currentWeaponIndex].gameObject.SetActive(false);
            currentWeaponIndex = newIndex;
            weapons[currentWeaponIndex].gameObject.SetActive(true);

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