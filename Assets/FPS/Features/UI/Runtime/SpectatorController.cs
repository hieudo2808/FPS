using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPS
{
    /// <summary>Owner-only spectator camera for campaign players after death.</summary>
    public sealed class SpectatorController : MonoBehaviour
    {
        private readonly List<PlayerHealth> targets = new();
        private CampaignMissionController campaign;
        private PlayerHealth owner;
        private Camera localBodyCamera;
        private Camera spectatorCamera;
        private int targetIndex;
        private float nextTargetRefresh;

        private void Awake()
        {
            spectatorCamera = new GameObject("SpectatorCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            spectatorCamera.transform.SetParent(transform, false);
            spectatorCamera.enabled = false;
            spectatorCamera.fieldOfView = 70f;
            spectatorCamera.GetComponent<AudioListener>().enabled = false;
        }

        private void OnDestroy()
        {
            if (spectatorCamera != null) Destroy(spectatorCamera.gameObject);
        }

        private void Update()
        {
            campaign ??= CampaignMissionController.Instance;
            owner ??= Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>();
            if (owner == null || campaign == null) return;
            MouseMovement mouse = owner.GetComponent<MouseMovement>();
            localBodyCamera = mouse != null ? mouse.BodyCam : null;
            bool shouldSpectate = owner.LifeState is PlayerLifeState.Dead or PlayerLifeState.Spectating;
            if (!shouldSpectate)
            {
                if (spectatorCamera.enabled) StopSpectating();
                return;
            }

            if (Time.unscaledTime >= nextTargetRefresh)
            {
                nextTargetRefresh = Time.unscaledTime + .5f;
                RefreshTargets();
            }
            if (targets.Count == 0)
            {
                spectatorCamera.enabled = false;
                return;
            }
            if (!spectatorCamera.enabled) StartSpectating();

            InputAction fire = InputManager.Instance?.ActionsAsset?.FindAction("Fire");
            InputAction aim = InputManager.Instance?.ActionsAsset?.FindAction("Aim");
            if (fire?.WasPressedThisFrame() == true) targetIndex = (targetIndex + 1) % targets.Count;
            if (aim?.WasPressedThisFrame() == true) targetIndex = (targetIndex + targets.Count - 1) % targets.Count;

            PlayerHealth target = targets[Mathf.Clamp(targetIndex, 0, targets.Count - 1)];
            if (target == null) return;
            Vector3 desired = target.transform.position + target.transform.right * 2.2f + Vector3.up * 1.8f - target.transform.forward * .8f;
            spectatorCamera.transform.position = Vector3.Lerp(spectatorCamera.transform.position, desired, 8f * Time.unscaledDeltaTime);
            spectatorCamera.transform.rotation = Quaternion.Slerp(spectatorCamera.transform.rotation,
                Quaternion.LookRotation(target.transform.position + Vector3.up * 1.3f - spectatorCamera.transform.position, Vector3.up), 8f * Time.unscaledDeltaTime);
        }

        private void RefreshTargets()
        {
            targets.Clear();
            foreach (PlayerHealth player in campaign.Players)
                if (player != null && player != owner && player.LifeState == PlayerLifeState.Alive && player.IsInputReady)
                    targets.Add(player);
            targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, targets.Count - 1));
        }

        private void StartSpectating()
        {
            if (localBodyCamera != null) localBodyCamera.enabled = false;
            spectatorCamera.enabled = true;
            spectatorCamera.GetComponent<AudioListener>().enabled = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void StopSpectating()
        {
            spectatorCamera.enabled = false;
            spectatorCamera.GetComponent<AudioListener>().enabled = false;
            if (localBodyCamera != null) localBodyCamera.enabled = true;
        }
    }
}
