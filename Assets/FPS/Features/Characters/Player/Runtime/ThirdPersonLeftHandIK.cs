using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FPS
{
    /// <summary>
    /// Keeps the support hand on the authored grip target for weapons that opt
    /// into this rig. Weapons whose canonical Humanoid clips already animate
    /// both hands, such as Odin equip/reload, disable the rig per presentation.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ThirdPersonLeftHandIK : MonoBehaviour
    {
        [Header("Rig References")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private Rig rig;
        [SerializeField] private TwoBoneIKConstraint constraint;
        [SerializeField] private Transform targetProxy;

        [Header("Blending")]
        [SerializeField, Range(0f, 1f)] private float holdWeight = 1f;
        [SerializeField, Min(0f)] private float manipulationReleaseDelay = 0.1f;

        [Header("Animation-Driven Weight (Opt In)")]
        [Tooltip(
            "Keeps the rig graph active and lets an AnimationClip curve drive "
            + "TwoBoneIKConstraint.weight. This also works in Animation Window preview.")]
        [SerializeField] private bool weightDrivenByAnimation;

        private WeaponManager weaponManager;
        private Transform boundTarget;
        private bool directBindingDisabled;
        private bool rigEnabledForWeapon = true;
        private float lastManipulationTime = float.NegativeInfinity;

        private bool HasRequiredRigReferences =>
            characterAnimator != null
            && rigBuilder != null
            && rig != null
            && constraint != null;

        public bool HasValidReferences =>
            HasRequiredRigReferences
            && (targetProxy != null
                || (!directBindingDisabled && constraint.data.target != null));

        public Transform BoundTarget => boundTarget;
        public Transform BoundIKTarget =>
            targetProxy != null
                ? (boundTarget != null ? targetProxy : null)
                : (!directBindingDisabled && constraint != null
                    ? constraint.data.target
                    : null);
        public bool SupportsDynamicWeaponBinding => targetProxy != null;
        public bool UsesAnimationDrivenWeight => weightDrivenByAnimation;

        private void Awake()
        {
            weaponManager = GetComponent<WeaponManager>();
            if (targetProxy == null && constraint != null)
            {
                boundTarget = constraint.data.target;
                directBindingDisabled = false;
            }
            if (rig != null)
                rig.weight = holdWeight;
            if (!weightDrivenByAnimation && constraint != null)
                constraint.weight = holdWeight;
        }

        private void Update()
        {
            if (!HasRequiredRigReferences)
                return;

            if (!rigEnabledForWeapon || !rigBuilder.enabled)
                return;

            if (weightDrivenByAnimation)
            {
                bool canApplyIK = HasValidReferences
                    && characterAnimator.isActiveAndEnabled;
                rig.weight = holdWeight;
                SetRigLayerActive(canApplyIK);
                return;
            }

            if (!HasValidReferences)
                return;

            bool manipulatingWeapon = IsManipulatingWeapon();
            if (manipulatingWeapon)
                lastManipulationTime = Time.time;

            bool releaseDelayElapsed =
                Time.time - lastManipulationTime >= manipulationReleaseDelay;
            bool shouldApplyIK = boundTarget != null
                && characterAnimator.isActiveAndEnabled
                && !manipulatingWeapon
                && releaseDelayElapsed;
            rig.weight = holdWeight;
            SetRigLayerActive(shouldApplyIK);
        }

        /// <summary>
        /// Rebinds the support-hand constraint to the active third-person weapon.
        /// Proxy rigs re-parent their proxy without rebuilding. Direct rigs use
        /// an explicitly configured existing weapon child and rebuild because
        /// that target belongs to the nested weapon Animator hierarchy.
        /// </summary>
        public void BindWeapon(GameObject weaponObject)
        {
            ThirdPersonWeaponGrip grip = weaponObject != null
                ? weaponObject.GetComponentInChildren<ThirdPersonWeaponGrip>(true)
                : null;
            Transform nextTarget = grip != null && grip.IsValid
                ? grip.LeftHandTarget
                : null;

            if (targetProxy == null)
            {
                BindDirectTarget(grip, nextTarget);
                RebuildRigGraphAfterControllerChange();
                RestoreAnimationDrivenRigLayerAfterDirectBind();
                return;
            }

            if (nextTarget == boundTarget)
                return;

            boundTarget = nextTarget;
            if (!HasValidReferences)
                return;

            SetRigLayerActive(false);
            if (boundTarget != null)
            {
                targetProxy.SetParent(boundTarget, false);
                targetProxy.localPosition = DivideByLossyScale(
                    grip.HandPositionOffset,
                    boundTarget.lossyScale);
                targetProxy.localRotation = grip.HandRotationOffset;
                targetProxy.localScale = Vector3.one;
            }

            TwoBoneIKConstraintData data = constraint.data;
            data.target = targetProxy;
            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = false;
            constraint.data = data;
        }

        private void BindDirectTarget(
            ThirdPersonWeaponGrip grip,
            Transform nextTarget)
        {
            Transform nextIKTarget = grip != null
                ? grip.DirectIKTarget
                : null;
            boundTarget = nextTarget;
            directBindingDisabled = nextIKTarget == null;

            SetRigLayerActive(false);
            if (constraint == null || nextIKTarget == null)
                return;

            TwoBoneIKConstraintData data = constraint.data;
            data.target = nextIKTarget;
            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = false;
            constraint.data = data;
        }

        private void RebuildRigGraphAfterControllerChange()
        {
            if (rigBuilder == null
                || characterAnimator == null
                || !rigBuilder.isActiveAndEnabled
                || !characterAnimator.isActiveAndEnabled)
            {
                return;
            }

            // PlayerVisibilityController assigns the weapon-specific character
            // controller immediately before BindWeapon. Animation Rigging's
            // playable graph still references the previous Animator graph until
            // it is rebuilt, so a direct target inside the nested gun Animator
            // otherwise appears configured but does not solve at runtime.
            rigBuilder.Clear();
            if (!rigBuilder.Build())
            {
                Debug.LogWarning(
                    "Failed to rebuild the third-person left-hand rig graph.",
                    this);
            }
        }

        private void RestoreAnimationDrivenRigLayerAfterDirectBind()
        {
            // Animation Window temporarily disables the character Animator while
            // sampling a clip. In that state the graph cannot be rebuilt, but
            // leaving this layer inactive also prevents Animation Rigging's
            // preview pass from solving the newly assigned direct target.
            // Keep the graph layer available; the clip's constraint-weight
            // curve remains responsible for releasing the hand during reload.
            if (weightDrivenByAnimation)
                SetRigLayerActive(HasValidReferences);
        }

#if UNITY_EDITOR
        public void Configure(
            Animator animator,
            RigBuilder builder,
            Rig rigLayer,
            TwoBoneIKConstraint ikConstraint,
            Transform proxy)
        {
            characterAnimator = animator;
            rigBuilder = builder;
            rig = rigLayer;
            constraint = ikConstraint;
            targetProxy = proxy;
            boundTarget = null;
            directBindingDisabled = false;
        }

        public void ConfigureAnimationDrivenWeight(bool enabled)
        {
            SetAnimationDrivenWeight(enabled);
        }

        public void ConfigureRigEnabled(bool enabled)
        {
            SetRigEnabled(enabled);
        }
#endif

        /// <summary>
        /// Enables the authored support-hand rig for the selected weapon. This
        /// is separate from animation-driven weight so legacy proxy rigs can
        /// remain enabled without requiring clip-authored weight curves.
        /// </summary>
        public void SetRigEnabled(bool enabled)
        {
            rigEnabledForWeapon = enabled;
            if (rigBuilder == null || rigBuilder.enabled == enabled)
            {
                if (!enabled)
                    SetRigLayerActive(false);
                return;
            }

            if (!enabled)
            {
                SetRigLayerActive(false);
                if (rigBuilder.isActiveAndEnabled)
                    rigBuilder.Clear();
                rigBuilder.enabled = false;
                return;
            }

            rigBuilder.enabled = true;
        }

        /// <summary>
        /// Selects the support-hand policy for the active weapon. Controllers
        /// with authored rig-weight curves use animation-driven mode; canonical
        /// body clips use gameplay-state release during equip and reload.
        /// </summary>
        public void SetAnimationDrivenWeight(bool enabled)
        {
            weightDrivenByAnimation = enabled;
            if (!enabled && constraint != null)
                constraint.weight = holdWeight;
        }

        private static Vector3 DivideByLossyScale(
            Vector3 worldUnitOffset,
            Vector3 lossyScale)
        {
            return new Vector3(
                SafeDivide(worldUnitOffset.x, lossyScale.x),
                SafeDivide(worldUnitOffset.y, lossyScale.y),
                SafeDivide(worldUnitOffset.z, lossyScale.z));
        }

        private static float SafeDivide(float value, float scale)
        {
            return Mathf.Abs(scale) > 0.00001f ? value / scale : 0f;
        }

        private void SetRigLayerActive(bool active)
        {
            if (rigBuilder == null || rig == null)
                return;

            foreach (RigLayer layer in rigBuilder.layers)
            {
                if (layer.rig == rig)
                {
                    layer.active = active;
                    return;
                }
            }
        }

        private bool IsManipulatingWeapon()
        {
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            Weapon weapon = weaponManager != null
                ? weaponManager.GetWeapon(weaponManager.CurrentWeaponIndex)
                : null;
            return weapon != null
                && (weapon.IsReloading || weapon.IsEquipPresentationActive);
        }

    }
}
