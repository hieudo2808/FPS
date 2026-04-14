using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponManager : NetworkBehaviour
    {
        [SerializeField] private List<GameObject> weapons;
        [SerializeField] private Animator characterAnimation;

        // Biến mạng: Khi Server đổi số này, TẤT CẢ client sẽ tự động update
        private NetworkVariable<int> networkedWeaponIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        public static WeaponManager LocalInstance { get; private set; }

        public GameObject CurrentWeapon => weapons[networkedWeaponIndex.Value];
        public GameObject UnusedWeapon => weapons[(networkedWeaponIndex.Value + 1) % weapons.Count];
        public Animator CharacterAnimation => characterAnimation;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) LocalInstance = this;

            // Lắng nghe sự kiện đổi súng từ Server
            networkedWeaponIndex.OnValueChanged += OnWeaponChanged;

            // Khởi tạo vũ khí ban đầu cho mọi người
            UpdateWeaponVisibility(networkedWeaponIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this) LocalInstance = null;
            networkedWeaponIndex.OnValueChanged -= OnWeaponChanged;
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Xử lý Input đổi súng
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2))
            {
                RequestSwitchWeaponServerRpc();
            }
        }

        [ServerRpc]
        private void RequestSwitchWeaponServerRpc()
        {
            // Server đổi giá trị -> kích hoạt OnValueChanged trên toàn bộ máy
            networkedWeaponIndex.Value = (networkedWeaponIndex.Value + 1) % weapons.Count;
        }

        private void OnWeaponChanged(int oldIndex, int newIndex)
        {
            UpdateWeaponVisibility(newIndex);
        }

        private void UpdateWeaponVisibility(int index)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].SetActive(i == index);
            }

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateWeaponUI();
        }

        public void AddWeapon(GameObject newWeapon)
        {
            if (weapons.Count < 2) weapons.Add(newWeapon);
        }
    }
}