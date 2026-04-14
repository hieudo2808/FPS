using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class BodyAim : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Gắn các xương spine theo thứ tự từ dưới lên (Spine -> Spine1 -> Spine2)")]
        [SerializeField] private Transform[] spineBones;
        [Tooltip("Transform của weapon hoặc hand để offset vị trí khi nhìn lên/xuống")]
        [SerializeField] private Transform weaponTransform;

        [Header("Spine Rotation Settings")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
        [Tooltip("Góc xoay tối thiểu của SPINE (không phải camera). Giữ nhỏ hơn camera.")]
        [SerializeField] private float spineMinAngle = -25f;
        [Tooltip("Góc xoay tối đa của SPINE (không phải camera). Giữ nhỏ hơn camera.")]
        [SerializeField] private float spineMaxAngle = 35f;
        [Tooltip("Góc camera tối đa (để tính tỷ lệ)")]
        [SerializeField] private float cameraMaxAngle = 90f;
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float[] boneWeights;

        [Header("Weapon Offset (Giữ súng trong view)")]
        [Tooltip("Bật tính năng đẩy weapon lên/xuống khi aim")]
        [SerializeField] private bool enableWeaponOffset = true;
        [Tooltip("Vị trí weapon khi nhìn thẳng")]
        [SerializeField] private Vector3 weaponBasePosition = new Vector3(0.25f, -0.15f, 0.4f);
        [Tooltip("Offset thêm khi nhìn lên cao nhất")]
        [SerializeField] private Vector3 weaponUpOffset = new Vector3(0f, 0.15f, -0.1f);
        [Tooltip("Offset thêm khi nhìn xuống thấp nhất")]
        [SerializeField] private Vector3 weaponDownOffset = new Vector3(0f, -0.1f, 0.05f);

        [Header("Axis Configuration")]
        [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;

        private enum RotationAxis { X, Y, Z }

        private bool isAimingActive = true;
        private float currentAimAngle = 0f;
        private Vector3 currentWeaponOffset = Vector3.zero;

        private void Start()
        {
            if (boneWeights == null || boneWeights.Length != spineBones.Length)
            {
                boneWeights = new float[spineBones.Length];
                float equalWeight = 1f / spineBones.Length;
                for (int i = 0; i < boneWeights.Length; i++)
                {
                    boneWeights[i] = equalWeight;
                }
            }

            if (weaponTransform != null && weaponBasePosition == Vector3.zero)
            {
                weaponBasePosition = weaponTransform.localPosition;
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            if (!isAimingActive || cameraTransform == null) return;

            float cameraPitch = GetCameraPitch();

            RotateSpine(cameraPitch);

            if (enableWeaponOffset && weaponTransform != null)
            {
                OffsetWeapon(cameraPitch);
            }
        }

        private float GetCameraPitch()
        {
            float pitch = cameraTransform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            return pitch;
        }

        private void RotateSpine(float cameraPitch)
        {
            if (spineBones == null || spineBones.Length == 0) return;

            float normalizedPitch = Mathf.Clamp(cameraPitch / cameraMaxAngle, -1f, 1f);

            float targetAngle;
            if (normalizedPitch >= 0)
            {
                targetAngle = normalizedPitch * spineMaxAngle;
            }
            else
            {
                targetAngle = normalizedPitch * Mathf.Abs(spineMinAngle);
            }

            currentAimAngle = Mathf.Lerp(currentAimAngle, targetAngle, Time.deltaTime * smoothSpeed);

            for (int i = 0; i < spineBones.Length; i++)
            {
                if (spineBones[i] == null) continue;

                float boneAngle = currentAimAngle * boneWeights[i];
                Quaternion additionalRotation = GetRotationByAxis(boneAngle);

                spineBones[i].localRotation = spineBones[i].localRotation * additionalRotation * Quaternion.Euler(rotationOffset);
            }
        }

        private void OffsetWeapon(float cameraPitch)
        {
            float normalizedPitch = Mathf.Clamp(cameraPitch / cameraMaxAngle, -1f, 1f);

            Vector3 targetOffset;
            if (normalizedPitch >= 0)
            {
                targetOffset = Vector3.Lerp(Vector3.zero, weaponUpOffset, normalizedPitch);
            }
            else
            {
                targetOffset = Vector3.Lerp(Vector3.zero, weaponDownOffset, -normalizedPitch);
            }

            currentWeaponOffset = Vector3.Lerp(currentWeaponOffset, targetOffset, Time.deltaTime * smoothSpeed);
            weaponTransform.localPosition = weaponBasePosition + currentWeaponOffset;
        }

        private Quaternion GetRotationByAxis(float angle)
        {
            return rotationAxis switch
            {
                RotationAxis.X => Quaternion.Euler(angle, 0, 0),
                RotationAxis.Y => Quaternion.Euler(0, angle, 0),
                RotationAxis.Z => Quaternion.Euler(0, 0, angle),
                _ => Quaternion.Euler(angle, 0, 0)
            };
        }

        public void SetAimStatus(bool status)
        {
            isAimingActive = status;
        }

        public float CurrentAimAngle => currentAimAngle;
        public float NormalizedAimAngle => currentAimAngle / spineMaxAngle;
    }
}