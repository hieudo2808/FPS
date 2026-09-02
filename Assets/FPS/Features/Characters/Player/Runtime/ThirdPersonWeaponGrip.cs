using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Describes the authored support-hand pose relative to a third-person
    /// weapon target. The source animation target is an anchor, not the hand
    /// bone itself, so its position and rotation offsets must be preserved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonWeaponGrip : MonoBehaviour
    {
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Vector3 handPositionOffset;
        [SerializeField] private Vector3 handRotationOffset;
        [Tooltip(
            "Optional existing child transform used by direct-target rigs. "
            + "Proxy-based rigs continue to use Left Hand Target plus the offsets.")]
        [SerializeField] private Transform directIKTarget;

        public Transform LeftHandTarget => leftHandTarget;
        public Transform DirectIKTarget => directIKTarget;
        public Vector3 HandPositionOffset => handPositionOffset;
        public Quaternion HandRotationOffset =>
            Quaternion.Euler(handRotationOffset);
        public bool IsValid => leftHandTarget != null;

#if UNITY_EDITOR
        public void Configure(
            Transform target,
            Vector3 positionOffset,
            Vector3 rotationOffset)
        {
            leftHandTarget = target;
            handPositionOffset = positionOffset;
            handRotationOffset = rotationOffset;
        }

        public void ConfigureDirectIKTarget(Transform target)
        {
            directIKTarget = target;
        }
#endif
    }
}
