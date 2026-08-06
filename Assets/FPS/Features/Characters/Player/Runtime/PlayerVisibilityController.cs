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
        [Tooltip("Fallback first-person weapon used when a character prefab does not provide a dedicated slot.")]
        [SerializeField] private GameObject firstPersonWeapon;
        [Tooltip("Fallback third-person weapon used when a character prefab does not provide a dedicated slot.")]
        [SerializeField] private GameObject thirdPersonWeapon;

        [Tooltip("First-person weapon representation for each WeaponManager slot.")]
        [SerializeField] private GameObject[] firstPersonWeaponSlots;
        [Tooltip("Third-person weapon representation for each WeaponManager slot.")]
        [SerializeField] private GameObject[] thirdPersonWeaponSlots;

        [Header("Layer Names")]
        [SerializeField] private string firstPersonLayer = "FirstPerson";
        [SerializeField] private string thirdPersonLayer = "ThirdPerson";
        [SerializeField] private string weaponLayer = "Weapon";

        private bool isLocalPlayer;
        private WeaponManager weaponManager;

        public override void OnNetworkSpawn()
        {
            weaponManager = GetComponent<WeaponManager>();
            if (weaponManager != null)
                weaponManager.WeaponIndexChanged += HandleWeaponIndexChanged;

            SetupVisibility(IsOwner);
        }

        public override void OnNetworkDespawn()
        {
            if (weaponManager != null)
                weaponManager.WeaponIndexChanged -= HandleWeaponIndexChanged;

            weaponManager = null;
        }

        public void SetupVisibility(bool isLocal)
        {
            isLocalPlayer = isLocal;

            if (HasWeaponSlotRepresentations)
            {
                SetGroupVisible(firstPersonArms, isLocalPlayer);
                SetGroupVisible(thirdPersonBody, !isLocalPlayer);
                SetGroupVisible(firstPersonWeapon, false);
                SetGroupVisible(thirdPersonWeapon, false);

                SetLayerRecursively(firstPersonArms, firstPersonLayer);
                SetLayerRecursively(thirdPersonBody, thirdPersonLayer);
                RefreshWeaponVisibility(GetCurrentWeaponIndex());
                return;
            }

            if (isLocalPlayer)
            {
                SetGroupVisible(firstPersonArms, true);
                SetGroupVisible(firstPersonWeapon, true);
                SetGroupVisible(thirdPersonBody, false);
                SetGroupVisible(thirdPersonWeapon, false);

                SetLayerRecursively(firstPersonArms, firstPersonLayer);
                SetLayerRecursively(firstPersonWeapon, weaponLayer);
            }
            else
            {
                SetGroupVisible(firstPersonArms, false);
                SetGroupVisible(firstPersonWeapon, false);
                SetGroupVisible(thirdPersonBody, true);
                SetGroupVisible(thirdPersonWeapon, true);

                SetLayerRecursively(thirdPersonBody, thirdPersonLayer);
                SetLayerRecursively(thirdPersonWeapon, thirdPersonLayer);
            }
        }

        public void SetWeapons(GameObject fpWeapon, GameObject tpWeapon)
        {
            firstPersonWeapon = fpWeapon;
            thirdPersonWeapon = tpWeapon;

            SetupVisibility(isLocalPlayer);
        }

        private void HandleWeaponIndexChanged(int index)
        {
            RefreshWeaponVisibility(index);
        }

        private int GetCurrentWeaponIndex()
        {
            return weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
        }

        private bool HasWeaponSlotRepresentations =>
            firstPersonWeaponSlots != null && firstPersonWeaponSlots.Length > 0
            && thirdPersonWeaponSlots != null && thirdPersonWeaponSlots.Length > 0;

        private void RefreshWeaponVisibility(int index)
        {
            if (!HasWeaponSlotRepresentations)
                return;

            for (int i = 0; i < firstPersonWeaponSlots.Length; i++)
            {
                GameObject fpWeapon = firstPersonWeaponSlots[i];
                if (fpWeapon != null)
                {
                    SetGroupVisible(fpWeapon, isLocalPlayer && i == index);
                    if (isLocalPlayer && i == index)
                        SetLayerRecursively(fpWeapon, weaponLayer);
                }

                GameObject tpWeapon = i < thirdPersonWeaponSlots.Length ? thirdPersonWeaponSlots[i] : null;
                if (tpWeapon != null)
                {
                    SetGroupVisible(tpWeapon, !isLocalPlayer && i == index);
                    if (!isLocalPlayer && i == index)
                        SetLayerRecursively(tpWeapon, thirdPersonLayer);
                }
            }
        }

        private static void SetGroupVisible(GameObject target, bool visible)
        {
            if (target == null) return;

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }

            var animators = target.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].enabled = visible;
            }
        }

        private void SetLayerRecursively(GameObject obj, string layerName)
        {
            if (obj == null) return;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1) return;

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
