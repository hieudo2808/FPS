using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Điều khiển visibility của First Person Arms vs Third Person Body.
    /// Sử dụng cho multiplayer: Local player thấy FPS Arms, other players thấy Full Body.
    /// </summary>
    public class PlayerVisibilityController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Container chứa FPS Arms + Weapon (Local player thấy)")]
        [SerializeField] private GameObject firstPersonArms;
        [Tooltip("Full body model - Survivalist (Other players thấy)")]
        [SerializeField] private GameObject thirdPersonBody;
        
        [Header("Weapons")]
        [Tooltip("Weapon trong FirstPersonArms")]
        [SerializeField] private GameObject firstPersonWeapon;
        [Tooltip("Weapon gắn ở tay Full Body")]
        [SerializeField] private GameObject thirdPersonWeapon;

        [Header("Layer Names")]
        [SerializeField] private string firstPersonLayer = "FirstPerson";
        [SerializeField] private string thirdPersonLayer = "ThirdPerson";
        [SerializeField] private string weaponLayer = "Weapon";
        [SerializeField] private string defaultLayer = "Default";

        [Header("Settings")]
        [Tooltip("Có phải local player không? Để true cho single player.")]
        [SerializeField] private bool isLocalPlayer = true;

        private void Start()
        {
            SetupVisibility(isLocalPlayer);
        }

        /// <summary>
        /// Gọi method này khi biết đây có phải local player hay không (multiplayer)
        /// </summary>
        public void SetupVisibility(bool isLocal)
        {
            isLocalPlayer = isLocal;

            if (isLocalPlayer)
            {
                // Local player: Thấy FPS Arms, không thấy Third Person Body trong camera của mình
                if (firstPersonArms != null)
                {
                    firstPersonArms.SetActive(true);
                    SetLayerRecursively(firstPersonArms, firstPersonLayer);
                }

                if (firstPersonWeapon != null)
                {
                    firstPersonWeapon.SetActive(true);
                    SetLayerRecursively(firstPersonWeapon, weaponLayer);
                }

                if (thirdPersonBody != null)
                {
                    // Giữ active cho shadow và network sync, nhưng đổi layer
                    thirdPersonBody.SetActive(true);
                    SetLayerRecursively(thirdPersonBody, thirdPersonLayer);
                }

                if (thirdPersonWeapon != null)
                {
                    thirdPersonWeapon.SetActive(true);
                    SetLayerRecursively(thirdPersonWeapon, thirdPersonLayer);
                }
            }
            else
            {
                // Other players: Không thấy FPS Arms của họ, chỉ thấy Full Body
                if (firstPersonArms != null)
                {
                    firstPersonArms.SetActive(false);
                }

                if (firstPersonWeapon != null)
                {
                    firstPersonWeapon.SetActive(false);
                }

                if (thirdPersonBody != null)
                {
                    thirdPersonBody.SetActive(true);
                    SetLayerRecursively(thirdPersonBody, defaultLayer);
                }

                if (thirdPersonWeapon != null)
                {
                    thirdPersonWeapon.SetActive(true);
                    SetLayerRecursively(thirdPersonWeapon, defaultLayer);
                }
            }
        }

        /// <summary>
        /// Đổi layer cho GameObject và tất cả children
        /// </summary>
        private void SetLayerRecursively(GameObject obj, string layerName)
        {
            if (obj == null) return;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                Debug.LogWarning($"Layer '{layerName}' không tồn tại! Hãy tạo layer trong Project Settings.");
                return;
            }

            SetLayerRecursivelyInternal(obj, layer);
        }

        private void SetLayerRecursivelyInternal(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursivelyInternal(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Chuyển đổi weapons giữa FPS và Third Person (dùng khi đổi súng)
        /// </summary>
        public void SetWeapons(GameObject fpWeapon, GameObject tpWeapon)
        {
            firstPersonWeapon = fpWeapon;
            thirdPersonWeapon = tpWeapon;

            if (isLocalPlayer)
            {
                if (fpWeapon != null) SetLayerRecursively(fpWeapon, weaponLayer);
                if (tpWeapon != null) SetLayerRecursively(tpWeapon, thirdPersonLayer);
            }
        }
    }
}
