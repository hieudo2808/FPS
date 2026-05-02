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