using System;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum ThirdPersonCharacterRigMode
    {
        PreserveCurrent = 0,
        AuthoredAvatar = 1,
        GenericPathBound = 2
    }

    [Serializable]
    public sealed class ThirdPersonWeaponPresentation
    {
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private GameObject weaponObject;
        [SerializeField] private RuntimeAnimatorController characterController;
        [Tooltip(
            "Select how the character Animator evaluates this weapon's clips. "
            + "Generic Path Bound deliberately clears the Avatar so direct "
            + "Transform curves can animate auxiliary weapon bones and hands.")]
        [SerializeField] private ThirdPersonCharacterRigMode characterRigMode;
        [Tooltip(
            "Avatar restored for Authored Avatar mode. Keep this authored per "
            + "presentation so switching away from a Generic weapon is deterministic.")]
        [SerializeField] private Avatar characterAvatar;
        [Tooltip(
            "Enable the authored third-person support-hand rig for this weapon. "
            + "Disable when the canonical humanoid animation already owns both "
            + "hands, such as Odin equip/reload.")]
        [SerializeField] private bool useLeftHandIK = true;
        [Tooltip(
            "Use clip-authored Animation Rigging weight curves for this weapon. "
            + "Disable for weapons such as Odin whose canonical equip/reload "
            + "clips must drive the support hand without an IK override.")]
        [SerializeField] private bool animationDrivenLeftHandIK = true;

        public WeaponData WeaponData => weaponData;
        public GameObject WeaponObject => weaponObject;
        public RuntimeAnimatorController CharacterController => characterController;
        public ThirdPersonCharacterRigMode CharacterRigMode => characterRigMode;
        public Avatar CharacterAvatar => characterAvatar;
        public bool UseLeftHandIK => useLeftHandIK;
        public bool AnimationDrivenLeftHandIK => animationDrivenLeftHandIK;
    }

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
        [Tooltip("Third-person visual and body controller selected by WeaponData. "
            + "Use this for primary candidates that share slot 0.")]
        [SerializeField] private ThirdPersonWeaponPresentation[] thirdPersonWeaponPresentations;

        [Header("Layer Names")]
        [SerializeField] private string firstPersonLayer = "FirstPerson";
        [SerializeField] private string thirdPersonLayer = "ThirdPerson";
        [SerializeField] private string weaponLayer = "Weapon";

        private bool isLocalPlayer;
        private bool thirdPersonAiming;
        private bool hasThirdPersonAimState;
        private WeaponManager weaponManager;

        public GameObject FirstPersonArms => firstPersonArms;
        public GameObject[] FirstPersonWeaponSlots => firstPersonWeaponSlots;
        public GameObject[] ThirdPersonWeaponSlots => thirdPersonWeaponSlots;
        public ThirdPersonWeaponPresentation[] ThirdPersonWeaponPresentations =>
            thirdPersonWeaponPresentations;

        /// <summary>
        /// Plays a presentation-only action on the equipped third-person weapon.
        /// The character body Animator is driven independently by WeaponManager.
        /// </summary>
        public void TriggerThirdPersonWeaponAnimation(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            if (triggerName == "Reload" || triggerName == "Equip")
                SetThirdPersonAiming(false);

            GameObject weaponObject = GetThirdPersonWeapon(GetCurrentWeaponIndex());
            if (weaponObject == null)
                return;

            Animator animator = weaponObject.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                return;

            int parameterHash = Animator.StringToHash(triggerName);
            if (triggerName == "Reload" || triggerName == "Equip")
                ResetAnimatorTriggerIfPresent(animator, "Fire");
            if (triggerName == "Reload")
                ResetAnimatorTriggerIfPresent(animator, "ReloadComplete");
            if (triggerName == "Equip")
                ResetAnimatorTriggerIfPresent(animator, "EquipComplete");
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash != parameterHash
                    || parameter.type != AnimatorControllerParameterType.Trigger)
                    continue;

                animator.ResetTrigger(parameterHash);
                animator.SetTrigger(parameterHash);
                return;
            }
        }

        public void SetThirdPersonWeaponAnimationFloat(
            string parameterName,
            float value)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return;

            GameObject weaponObject = GetThirdPersonWeapon(
                GetCurrentWeaponIndex());
            Animator animator = weaponObject != null
                ? weaponObject.GetComponentInChildren<Animator>(true)
                : null;
            SetAnimatorFloatIfPresent(animator, parameterName, value);
        }

        private static void ResetAnimatorTriggerIfPresent(
            Animator animator,
            string triggerName)
        {
            int triggerHash = Animator.StringToHash(triggerName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == triggerHash
                    && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.ResetTrigger(triggerHash);
                    return;
                }
            }
        }

        public void SetThirdPersonAiming(bool aiming)
        {
            if (hasThirdPersonAimState && thirdPersonAiming == aiming)
                return;

            thirdPersonAiming = aiming;
            hasThirdPersonAimState = true;
            ApplyThirdPersonAiming();
        }

        private void ApplyThirdPersonAiming()
        {

            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();
            SetAnimatorBoolIfPresent(
                weaponManager != null ? weaponManager.CharacterAnimation : null,
                "Aiming",
                thirdPersonAiming);

            GameObject weaponObject = GetThirdPersonWeapon(GetCurrentWeaponIndex());
            Animator weaponAnimator = weaponObject != null
                ? weaponObject.GetComponentInChildren<Animator>(true)
                : null;
            SetAnimatorBoolIfPresent(
                weaponAnimator,
                "Aiming",
                thirdPersonAiming);
        }

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
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            if (isLocalPlayer)
            {
                string armsName = firstPersonArms != null ? firstPersonArms.name : "NULL";
                GameLog.Info(() => $"[PlayerVisual] root={name} firstPersonArms={armsName} slots={HasWeaponSlotRepresentations}");
            }

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

        private GameObject GetThirdPersonWeapon(int index)
        {
            ThirdPersonWeaponPresentation presentation =
                GetThirdPersonWeaponPresentation(index);
            if (presentation?.WeaponObject != null)
                return presentation.WeaponObject;

            if (thirdPersonWeaponSlots != null && index >= 0 && index < thirdPersonWeaponSlots.Length)
                return thirdPersonWeaponSlots[index];
            return thirdPersonWeapon;
        }

        private ThirdPersonWeaponPresentation GetThirdPersonWeaponPresentation(int index)
        {
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            WeaponData activeData = weaponManager?.GetWeapon(index)?.Data;
            if (activeData == null || thirdPersonWeaponPresentations == null)
                return null;

            foreach (ThirdPersonWeaponPresentation presentation in thirdPersonWeaponPresentations)
            {
                if (presentation?.WeaponData == activeData)
                    return presentation;
            }

            return null;
        }

        private bool HasWeaponSlotRepresentations =>
            firstPersonWeaponSlots != null && firstPersonWeaponSlots.Length > 0
            && ((thirdPersonWeaponPresentations != null
                    && thirdPersonWeaponPresentations.Length > 0)
                || (thirdPersonWeaponSlots != null
                    && thirdPersonWeaponSlots.Length > 0));

        public void RefreshWeaponPresentation(int index)
        {
            RefreshWeaponVisibility(index);
        }

        private void RefreshWeaponVisibility(int index)
        {
            if (!HasWeaponSlotRepresentations)
                return;

            if (thirdPersonWeaponSlots != null)
            {
                foreach (GameObject weapon in thirdPersonWeaponSlots)
                    SetPresentationVisible(weapon, false);
            }

            if (thirdPersonWeaponPresentations != null)
            {
                foreach (ThirdPersonWeaponPresentation presentation in thirdPersonWeaponPresentations)
                    SetPresentationVisible(presentation?.WeaponObject, false);
            }

            for (int i = 0; i < firstPersonWeaponSlots.Length; i++)
            {
                GameObject fpWeapon = firstPersonWeaponSlots[i];
                if (fpWeapon != null)
                {
                    SetGroupVisible(fpWeapon, isLocalPlayer && i == index);
                    if (isLocalPlayer && i == index)
                        SetLayerRecursively(fpWeapon, weaponLayer);
                }
            }

            GameObject selectedThirdPersonWeapon = GetThirdPersonWeapon(index);
            ThirdPersonWeaponPresentation selectedPresentation =
                GetThirdPersonWeaponPresentation(index);
            GameObject selectedFirstPersonWeapon = index >= 0
                && index < firstPersonWeaponSlots.Length
                ? firstPersonWeaponSlots[index]
                : null;
            if (selectedThirdPersonWeapon != null
                && selectedThirdPersonWeapon != selectedFirstPersonWeapon)
            {
                SetPresentationVisible(
                    selectedThirdPersonWeapon,
                    !isLocalPlayer);
                if (!isLocalPlayer)
                    SetLayerRecursively(selectedThirdPersonWeapon, thirdPersonLayer);
            }

            ApplyCharacterAnimationProfile(selectedPresentation);

            ApplyThirdPersonAiming();

            ThirdPersonLeftHandIK leftHandIK =
                GetComponent<ThirdPersonLeftHandIK>();
            if (leftHandIK != null)
            {
                // Prefabs without presentation entries keep their legacy proxy
                // rig. An authored presentation can explicitly opt out when its
                // canonical Humanoid clips already animate both hands.
                leftHandIK.SetRigEnabled(
                    selectedPresentation?.UseLeftHandIK ?? true);
                leftHandIK.SetAnimationDrivenWeight(
                    selectedPresentation?.AnimationDrivenLeftHandIK ?? false);
                leftHandIK.BindWeapon(selectedThirdPersonWeapon);
            }
        }

        private void ApplyCharacterAnimationProfile(
            ThirdPersonWeaponPresentation presentation)
        {
            if (presentation == null)
                return;

            Animator characterAnimator = weaponManager?.CharacterAnimation;
            if (characterAnimator == null)
                return;

            bool controllerChanged = presentation.CharacterController != null
                && characterAnimator.runtimeAnimatorController
                    != presentation.CharacterController;
            bool avatarChanged = false;

            switch (presentation.CharacterRigMode)
            {
                case ThirdPersonCharacterRigMode.AuthoredAvatar:
                    avatarChanged = characterAnimator.avatar
                        != presentation.CharacterAvatar;
                    break;

                case ThirdPersonCharacterRigMode.GenericPathBound:
                    avatarChanged = characterAnimator.avatar != null;
                    break;
            }

            if (!controllerChanged && !avatarChanged)
                return;

            // Set both parts of the authored profile before rebinding. Rebind is
            // required when changing Avatar mode; otherwise Mecanim can retain
            // stale HumanStream bindings from the previous weapon.
            if (controllerChanged)
            {
                characterAnimator.runtimeAnimatorController =
                    presentation.CharacterController;
            }

            if (avatarChanged)
            {
                characterAnimator.avatar = presentation.CharacterRigMode
                    == ThirdPersonCharacterRigMode.GenericPathBound
                        ? null
                        : presentation.CharacterAvatar;
            }

            characterAnimator.Rebind();
            characterAnimator.Update(0f);
        }

        private static void SetAnimatorBoolIfPresent(
            Animator animator,
            string parameterName,
            bool value)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            int parameterHash = Animator.StringToHash(parameterName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash
                    && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(parameterHash, value);
                    return;
                }
            }
        }

        private static void SetAnimatorFloatIfPresent(
            Animator animator,
            string parameterName,
            float value)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            int parameterHash = Animator.StringToHash(parameterName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash
                    && parameter.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat(parameterHash, value);
                    return;
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

        private static void SetPresentationVisible(
            GameObject target,
            bool visible)
        {
            if (target == null)
                return;

            target.SetActive(visible);
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
