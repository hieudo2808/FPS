using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace FPS
{
    public class WeaponInputHandler : NetworkBehaviour
    {
        private WeaponManager _weaponManager;
        private WeaponManager weaponManager => _weaponManager != null ? _weaponManager : (_weaponManager = GetComponent<WeaponManager>());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Vector3 ThirdPersonDebugCameraOffset =
            new Vector3(0f, 0.35f, -3f);

        [Header("Development Preview")]
        [Tooltip("Allows F5-F10 third-person preview input when this player is placed directly in a scene without being spawned by Netcode.")]
        [SerializeField] private bool enableOfflineDebugInput;

        private bool thirdPersonDebugView;
        private bool debugCameraStateCaptured;
        private Vector3 firstPersonCameraLocalPosition;
        private bool firstPersonWeaponCameraEnabled;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (!IsSpawned && enableOfflineDebugInput)
            {
                PlayerVisibilityController visibility =
                    GetComponent<PlayerVisibilityController>();
                visibility?.RefreshWeaponPresentation(
                    weaponManager.CurrentWeaponIndex);
            }
        }
#endif

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if ((IsSpawned && IsOwner) || (!IsSpawned && enableOfflineDebugInput))
            {
                HandleThirdPersonDebugView();
                HandlePrimaryWeaponDebugSelection();
            }
#endif

            if (!IsOwner || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;

            if (InputManager.Instance == null || weaponManager == null || !weaponManager.IsSpawned)
                return;

            if (InputManager.Instance.GetWeapon1InputDown())
            {
                weaponManager.RequestEquipWeaponServerRpc(0);
            }
            else if (InputManager.Instance.GetWeapon2InputDown())
            {
                weaponManager.RequestEquipWeaponServerRpc(1);
            }
            else if (InputManager.Instance.GetInspectInputDown())
            {
                weaponManager.TryInspectCurrentWeapon();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandleThirdPersonDebugView()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f5Key.wasPressedThisFrame)
                return;

            ToggleThirdPersonDebugView();
        }

        private void ToggleThirdPersonDebugView()
        {
            MouseMovement mouseMovement = GetComponent<MouseMovement>();
            PlayerVisibilityController visibility =
                GetComponent<PlayerVisibilityController>();
            if (mouseMovement == null || mouseMovement.BodyCam == null || visibility == null)
            {
                GameLog.Warning(() =>
                    "[WeaponDebug] Cannot toggle 3P view because camera or visibility references are missing.");
                return;
            }

            if (!debugCameraStateCaptured)
            {
                firstPersonCameraLocalPosition =
                    mouseMovement.BodyCam.transform.localPosition;
                firstPersonWeaponCameraEnabled =
                    mouseMovement.WeaponCam != null && mouseMovement.WeaponCam.enabled;
                debugCameraStateCaptured = true;
            }

            thirdPersonDebugView = !thirdPersonDebugView;
            mouseMovement.BodyCam.transform.localPosition = thirdPersonDebugView
                ? ThirdPersonDebugCameraOffset
                : firstPersonCameraLocalPosition;
            if (mouseMovement.WeaponCam != null)
                mouseMovement.WeaponCam.enabled =
                    !thirdPersonDebugView && firstPersonWeaponCameraEnabled;

            visibility.SetupVisibility(!thirdPersonDebugView);
            GameLog.Info(() => thirdPersonDebugView
                ? "[WeaponDebug] 3P inspection enabled. F6 Vandal, F7 Operator, F8 Odin, F9 Bucky, F10 Classic."
                : "[WeaponDebug] First-person view restored.");
        }

        private void HandlePrimaryWeaponDebugSelection()
        {
            // Inventory mutations remain server-authoritative. This shortcut is
            // intentionally Host-only for spawned players and is compiled out
            // of release builds. An explicitly enabled, unspawned scene player
            // uses the same path for deterministic editor preview.
            if (weaponManager == null
                || Keyboard.current == null
                || (weaponManager.IsSpawned && !weaponManager.IsServer))
                return;

            PrimaryWeaponId? selectedWeapon = null;
            if (Keyboard.current.f6Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Vandal;
            else if (Keyboard.current.f7Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Operator;
            else if (Keyboard.current.f8Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Odin;
            else if (Keyboard.current.f9Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Bucky;
            else if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                weaponManager.SetEquippedWeaponServer(1);
                GetComponent<PlayerVisibilityController>()?
                    .RefreshWeaponPresentation(weaponManager.CurrentWeaponIndex);
                GameLog.Info(() => "[WeaponDebug] Equipped Classic.");
                return;
            }

            if (!selectedWeapon.HasValue)
                return;

            PrimaryWeaponId weaponId = selectedWeapon.Value;
            if (!weaponManager.TryReplacePrimaryWeaponServer(weaponId))
            {
                GameLog.Warning(() => $"[WeaponDebug] {weaponId} is not configured as a primary candidate.");
                return;
            }

            if (weaponManager.CurrentWeaponIndex != 0)
                weaponManager.SetEquippedWeaponServer(0);

            GetComponent<PlayerVisibilityController>()?
                .RefreshWeaponPresentation(weaponManager.CurrentWeaponIndex);

            GameLog.Info(() => $"[WeaponDebug] Equipped primary {weaponId}.");
        }

        private void OnDisable()
        {
            if (!thirdPersonDebugView || !debugCameraStateCaptured)
                return;

            MouseMovement mouseMovement = GetComponent<MouseMovement>();
            if (mouseMovement != null && mouseMovement.BodyCam != null)
            {
                mouseMovement.BodyCam.transform.localPosition =
                    firstPersonCameraLocalPosition;
                if (mouseMovement.WeaponCam != null)
                    mouseMovement.WeaponCam.enabled = firstPersonWeaponCameraEnabled;
            }

            GetComponent<PlayerVisibilityController>()?.SetupVisibility(true);
            thirdPersonDebugView = false;
        }
#endif
    }
}
