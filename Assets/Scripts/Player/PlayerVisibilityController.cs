using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerVisibilityController : NetworkBehaviour
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

        private bool isLocalPlayer;

        public override void OnNetworkSpawn()
        {
            SetupVisibility(IsOwner);
        }

        public void SetupVisibility(bool isLocal)
        {
            isLocalPlayer = isLocal;

            if (isLocalPlayer)
            {
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

                if (thirdPersonBody != null) thirdPersonBody.SetActive(false);
                if (thirdPersonWeapon != null) thirdPersonWeapon.SetActive(false);
            }
            else
            {                
                if (firstPersonArms != null) firstPersonArms.SetActive(false);
                if (firstPersonWeapon != null) firstPersonWeapon.SetActive(false);

                if (thirdPersonBody != null)
                {
                    thirdPersonBody.SetActive(true);
                    SetLayerRecursively(thirdPersonBody, thirdPersonLayer);
                }
                if (thirdPersonWeapon != null)
                {
                    thirdPersonWeapon.SetActive(true);
                    SetLayerRecursively(thirdPersonWeapon, thirdPersonLayer);
                }
            }
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                if (firstPersonArms != null && firstPersonArms.activeSelf) 
                    firstPersonArms.SetActive(false);
                
                if (firstPersonWeapon != null && firstPersonWeapon.activeSelf) 
                    firstPersonWeapon.SetActive(false);
            }
            else
            {
                if (thirdPersonBody != null && thirdPersonBody.activeSelf) 
                    thirdPersonBody.SetActive(false);
            }
        }

        public void SetWeapons(GameObject fpWeapon, GameObject tpWeapon)
        {
            firstPersonWeapon = fpWeapon;
            thirdPersonWeapon = tpWeapon;

            SetupVisibility(isLocalPlayer);
        }

        private void SetLayerRecursively(GameObject obj, string layerName)
        {
            if (obj == null) return;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                Debug.LogWarning($"[Visibility] Layer '{layerName}' không tồn tại! Hãy tạo trong Project Settings.");
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
    }
}