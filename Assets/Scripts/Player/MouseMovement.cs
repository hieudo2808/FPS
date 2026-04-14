using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class MouseMovement : NetworkBehaviour
    {
        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 100f;
        [SerializeField] private float minSensitivity = 10f;
        [SerializeField] private float maxSensitivity = 500f;

        [Header("Rotation Limits")]
        [SerializeField] private float minRotationX = -90f;
        [SerializeField] private float maxRotationX = 90f;

        [Header("References")]
        [SerializeField] private Camera bodyCam;
        [SerializeField] private Camera weaponCam;

        private float xRotation = 0f;
        private float yRotation = 0f;

        public static MouseMovement LocalInstance { get; private set; }

        public float Sensitivity => mouseSensitivity;
        public float MinSensitivity => minSensitivity;
        public float MaxSensitivity => maxSensitivity;
        public float YRotation => yRotation;

        public Camera BodyCam => bodyCam;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                LocalInstance = this;

                if (PlayerPrefs.HasKey("MouseSensitivity"))
                {
                    mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity");
                }

                // Enable camera only for local player
                if (bodyCam != null)
                {
                    bodyCam.enabled = true;
                    // Set audio listener
                    var listener = bodyCam.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = true;
                }

                if (weaponCam != null)
                {
                    weaponCam.enabled = true;
                }
            }
            else
            {
                // Disable camera for remote players
                if (bodyCam != null)
                {
                    bodyCam.enabled = false;
                    var listener = bodyCam.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }

                if (weaponCam != null)
                {
                    weaponCam.enabled = false;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this)
            {
                LocalInstance = null;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }
            else Cursor.lockState = CursorLockMode.Locked;

            // 1. Remove Time.deltaTime, it makes sensitivity depend on FPS.
            // 2. Use GetAxisRaw for true un-smoothed raw mouse input.
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minRotationX, maxRotationX);

            yRotation += mouseX;

            bodyCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.localRotation = Quaternion.Euler(0, yRotation, 0f);
        }

        public void ApplyRecoil(float pitchOffset, float yawOffset)
        {
            if (!IsOwner) return;
            
            xRotation -= pitchOffset;
            xRotation = Mathf.Clamp(xRotation, minRotationX, maxRotationX);

            yRotation += yawOffset;
        }

        public void SetSensitivity(float newSensitivity)
        {
            mouseSensitivity = Mathf.Clamp(newSensitivity, minSensitivity, maxSensitivity);
            PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
            PlayerPrefs.Save();
        }

        public void SetSensitivityNormalized(float normalized01)
        {
            float newSens = Mathf.Lerp(minSensitivity, maxSensitivity, normalized01);
            SetSensitivity(newSens);
        }

        public float GetSensitivityNormalized()
        {
            return Mathf.InverseLerp(minSensitivity, maxSensitivity, mouseSensitivity);
        }
    }
}