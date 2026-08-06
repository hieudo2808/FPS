using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPS
{
    public class MouseMovement : NetworkBehaviour
    {
        private const string MouseSensitivityKey = "MouseSensitivity";

        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minSensitivity = 0.1f;
        [SerializeField] private float maxSensitivity = 10f;

        [Header("Rotation Limits")]
        [SerializeField] private float minRotationX = -90f;
        [SerializeField] private float maxRotationX = 90f;

        [Header("References")]
        [SerializeField] private Camera bodyCam;
        [SerializeField] private Camera weaponCam;

        private float xRotation = 0f;
        private float yRotation = 0f;
        private bool subscribedToSettings;

        public static MouseMovement LocalInstance { get; private set; }

        public float Sensitivity => mouseSensitivity;
        public float MinSensitivity => minSensitivity;
        public float MaxSensitivity => maxSensitivity;
        public float YRotation => yRotation;
        public float XRotation => xRotation;

        public Camera BodyCam => bodyCam;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                LocalInstance = this;
                SubscribeToSettings();

                // Enable camera only for local player
                if (bodyCam != null)
                {
                    bodyCam.enabled = true;
                    // Set audio listener
                    var listener = bodyCam.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = true;
                    DisableOtherAudioListeners(listener);
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
            bool wasOwner = IsOwner;
            DisableLocalCameraAndListener();
            if (wasOwner && LocalInstance == this)
            {
                UnsubscribeFromSettings();

                LocalInstance = null;
                EnableFallbackAudioListener();
            }
        }

        private void DisableLocalCameraAndListener()
        {
            if (bodyCam != null)
            {
                bodyCam.enabled = false;
                AudioListener listener = bodyCam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }

            if (weaponCam != null)
                weaponCam.enabled = false;
        }

        private static void DisableOtherAudioListeners(AudioListener localListener)
        {
            if (localListener == null)
                return;

            AudioListener[] listeners = FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != localListener)
                    listeners[i].enabled = false;
            }
        }

        private static void EnableFallbackAudioListener()
        {
            AudioManager audioManager = FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
            AudioListener fallback = audioManager != null ? audioManager.FallbackAudioListener : null;
            if (fallback == null)
                return;

            fallback.enabled = true;
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != fallback)
                    listeners[i].enabled = false;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // SettingsManager may be loaded after the network player (for example
            // when returning from the lobby). Retry the lifecycle subscription
            // until it exists so a later settings change is never lost.
            if (!subscribedToSettings)
                SubscribeToSettings();

            if (InputManager.GameplayInputBlocked)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }
            else Cursor.lockState = CursorLockMode.Locked;

            // Input System reports raw mouse delta. Deliberately do not multiply by
            // Time.deltaTime: sensitivity is a device-space multiplier, not a speed.
            Vector2 lookDelta = InputManager.Instance != null
                ? InputManager.Instance.GetLookDelta()
                : Vector2.zero;
            float mouseX = lookDelta.x * mouseSensitivity;
            float mouseY = lookDelta.y * mouseSensitivity;

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
            PlayerPrefs.SetFloat(MouseSensitivityKey, mouseSensitivity);
            PlayerPrefs.Save();
        }

        private void ApplySettingsSensitivity(float newSensitivity)
        {
            if (newSensitivity >= 10f && newSensitivity > maxSensitivity)
            {
                // Values written by the old 10..500 runtime scale are converted once
                // instead of being collapsed to the new maximum.
                newSensitivity = Mathf.Lerp(minSensitivity, maxSensitivity,
                    Mathf.InverseLerp(10f, 500f, newSensitivity));
            }
            mouseSensitivity = Mathf.Clamp(newSensitivity, minSensitivity, maxSensitivity);
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

        private void SubscribeToSettings()
        {
            if (!IsOwner || subscribedToSettings)
                return;

            if (SettingsManager.Instance != null)
            {
                ApplySettingsSensitivity(SettingsManager.Instance.MouseSensitivity);
                SettingsManager.Instance.OnSensitivityChanged += ApplySettingsSensitivity;
                subscribedToSettings = true;
            }
            else if (PlayerPrefs.HasKey(MouseSensitivityKey))
            {
                ApplySettingsSensitivity(PlayerPrefs.GetFloat(MouseSensitivityKey));
            }
        }

        private void UnsubscribeFromSettings()
        {
            if (!subscribedToSettings || SettingsManager.Instance == null)
                return;

            SettingsManager.Instance.OnSensitivityChanged -= ApplySettingsSensitivity;
            subscribedToSettings = false;
        }
    }
}
