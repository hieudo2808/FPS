using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FPS
{
    /// <summary>
    /// Single owner for the project's Input System actions. Gameplay code should query
    /// this facade instead of reading devices directly so rebinding and menu blocking
    /// have one consistent implementation.
    /// </summary>
    [ExecuteAlways]
    public sealed class InputManager : MonoBehaviour
    {
        private const string InputAssetResource = "FPSInputActions";
        private const string BindingOverridesKey = "InputBindings";

        public static InputManager Instance { get; private set; }
        private static bool menuInputBlocked;

        public static bool MatchInputBlocked { get; set; }
        public static bool CinematicInputBlocked { get; set; }

        public static bool GameplayInputBlocked
        {
            get => menuInputBlocked || MatchInputBlocked || CinematicInputBlocked;
            set => menuInputBlocked = value;
        }

        private InputActionAsset actionAsset;
        private InputActionMap gameplayMap;
        private readonly Dictionary<string, InputAction> actions = new(StringComparer.Ordinal);
        private InputActionRebindingExtensions.RebindingOperation activeRebind;

        public InputActionAsset ActionsAsset => actionAsset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Keep the runtime singleton strict, but allow isolated editor
                // tests to create a fresh facade without receiving a destroyed
                // component whose action map was never initialized.
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (Instance == null)
            {
                Instance = this;
                if (Application.isPlaying)
                    DontDestroyOnLoad(gameObject);
            }
            InitializeActions();
        }

        private void OnEnable()
        {
            if (gameplayMap == null || actionAsset == null)
                InitializeActions();
            EnableGameplayMapSafely();
        }

        private void OnDisable()
        {
            CancelInteractiveRebind();
            DisableGameplayMapSafely();
        }

        private void OnDestroy()
        {
            CancelInteractiveRebind();
            DisableGameplayMapSafely();
            if (actionAsset != null)
            {
                if (Application.isPlaying)
                    Destroy(actionAsset);
                else
                    DestroyImmediate(actionAsset);
            }

            if (Instance == this)
                Instance = null;
        }

        private void InitializeActions()
        {
            InputActionAsset source = Resources.Load<InputActionAsset>(InputAssetResource);
#if UNITY_EDITOR
            // Resources.Load can lag one editor refresh for a newly imported
            // .inputactions file. AssetDatabase is an editor-only fallback;
            // player builds continue to use the Resources path above.
            if (source == null)
                source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    "Assets/Resources/FPSInputActions.inputactions");
#endif
            actionAsset = source != null ? Instantiate(source) : CreateFallbackAsset();
            gameplayMap = actionAsset.FindActionMap("Gameplay", throwIfNotFound: false);
            if (gameplayMap == null)
                gameplayMap = actionAsset.AddActionMap("Gameplay");

            actions.Clear();
            foreach (InputAction action in gameplayMap.actions)
                actions[action.name] = action;

            LoadBindingOverrides();
            EnableGameplayMapSafely();
        }

        private void EnableGameplayMapSafely()
        {
            // A map cloned from an input-actions asset must belong to the
            // current asset state before Enable() is called. During domain
            // reload/ExecuteAlways, Unity can briefly expose the old map and
            // produce "Map must be contained in state" / out-of-range errors.
            if (actionAsset == null || gameplayMap == null || gameplayMap.asset != actionAsset)
                return;
            try { actionAsset.Enable(); }
            catch (InvalidOperationException)
            {
                // The next enable/domain-reload pass will recreate the map.
                gameplayMap = null;
            }
        }

        private void DisableGameplayMapSafely()
        {
            if (actionAsset == null || gameplayMap == null || gameplayMap.asset != actionAsset)
                return;
            try { actionAsset.Disable(); }
            catch (InvalidOperationException) { }
        }

        private static InputActionAsset CreateFallbackAsset()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Gameplay");

            InputAction move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            InputAction look = map.AddAction("Look", InputActionType.Value);
            look.expectedControlType = "Vector2";
            look.AddBinding("<Mouse>/delta");
            map.FindAction("Look").AddBinding("<Gamepad>/rightStick");

            AddButton(map, "Fire", "<Mouse>/leftButton");
            map.FindAction("Fire").AddBinding("<Gamepad>/rightTrigger");
            AddButton(map, "Reload", "<Keyboard>/r");
            AddButton(map, "Inspect", "<Keyboard>/y");
            AddButton(map, "Aim", "<Mouse>/rightButton");
            AddButton(map, "Weapon1", "<Keyboard>/1");
            AddButton(map, "Weapon2", "<Keyboard>/2");
            AddButton(map, "Jump", "<Keyboard>/space");
            AddButton(map, "Interact", "<Keyboard>/f");
            AddButton(map, "Grenade", "<Keyboard>/g");
            AddButton(map, "Sprint", "<Keyboard>/leftShift");
            AddButton(map, "Pause", "<Keyboard>/escape");
            return asset;
        }

        private static void AddButton(InputActionMap map, string actionName, string binding)
        {
            InputAction action = map.AddAction(actionName, InputActionType.Button);
            action.expectedControlType = "Button";
            action.AddBinding(binding);
        }

        private InputAction GetAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return null;

            if (actions.TryGetValue(actionName, out InputAction action))
                return action;

            action = gameplayMap?.FindAction(actionName, throwIfNotFound: false);
            if (action != null)
                actions[actionName] = action;
            return action;
        }

        private bool IsPressed(string actionName)
        {
            return GetAction(actionName)?.IsPressed() == true;
        }

        private bool WasPressedThisFrame(string actionName)
        {
            return GetAction(actionName)?.WasPressedThisFrame() == true;
        }

        public Vector2 GetMove()
        {
            return GameplayInputBlocked
                ? Vector2.zero
                : GetAction("Move")?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        public Vector2 GetLookDelta()
        {
            return GameplayInputBlocked
                ? Vector2.zero
                : GetAction("Look")?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        public bool GetFireInput() => !GameplayInputBlocked && IsPressed("Fire");
        public bool GetFireInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Fire");
        public bool GetReloadInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Reload");
        public bool GetReloadInput() => !GameplayInputBlocked && IsPressed("Reload");
        public bool GetInspectInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Inspect");
        // Raw held state is intentional: Weapon owns the gameplay-block check
        // but still needs to track the physical release edge while a menu or
        // match-state block is active, otherwise holding RMB through unblock
        // would create a synthetic new toggle.
        public bool GetAimInput() => IsPressed("Aim");
        public bool GetAimInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Aim");
        public bool GetWeapon1InputDown() => !GameplayInputBlocked && WasPressedThisFrame("Weapon1");
        public bool GetWeapon2InputDown() => !GameplayInputBlocked && WasPressedThisFrame("Weapon2");
        public bool GetInteractInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Interact");
        public bool GetJumpInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Jump");
        public bool GetGrenadeInputDown() => !GameplayInputBlocked && WasPressedThisFrame("Grenade");
        public bool GetSprintInput() => !GameplayInputBlocked && IsPressed("Sprint");
        public bool GetPauseInputDown() => WasPressedThisFrame("Pause");

        public string GetBindingDisplayName(string actionName)
        {
            InputAction action = GetAction(actionName);
            if (action == null)
                return "--";

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                string path = string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath;
                if (!string.IsNullOrEmpty(path))
                    return InputControlPath.ToHumanReadableString(
                        path,
                        InputControlPath.HumanReadableStringOptions.OmitDevice);
            }

            return "--";
        }

        /// <summary>
        /// Compatibility helper for existing editor tests and non-interactive defaults.
        /// Runtime input still flows through Input System actions.
        /// </summary>
        public KeyCode GetKeyForAction(string actionName)
        {
            InputAction action = GetAction(actionName);
            if (action == null)
                return KeyCode.None;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                string path = string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath;
                return PathToKeyCode(path);
            }

            return KeyCode.None;
        }

        /// <summary>
        /// Compatibility bridge for existing defaults/tests. New UI should use
        /// StartInteractiveRebind so devices other than keyboards are supported.
        /// </summary>
        public void RebindKey(string actionName, KeyCode newKey)
        {
            string path = KeyCodeToPath(newKey);
            if (string.IsNullOrEmpty(path))
                return;

            InputAction action = GetAction(actionName);
            if (action == null)
                return;

            int bindingIndex = FindFirstRebindableBinding(action);
            if (bindingIndex < 0)
                return;

            action.ApplyBindingOverride(bindingIndex, path);
            // Preserve the legacy compatibility key used by existing saves and
            // editor tooling while the authoritative representation remains the
            // Input System override JSON.
            PlayerPrefs.SetString($"Input_{actionName}", newKey.ToString());
            SaveBindingOverrides();
        }

        public bool StartInteractiveRebind(string actionName, Action<bool> completed)
        {
            CancelInteractiveRebind();
            InputAction action = GetAction(actionName);
            int bindingIndex = action == null ? -1 : FindFirstRebindableBinding(action);
            if (action == null || bindingIndex < 0)
            {
                completed?.Invoke(false);
                return false;
            }

            action.Disable();
            activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(operation =>
                {
                    operation.Dispose();
                    activeRebind = null;
                    action.Enable();
                    completed?.Invoke(false);
                })
                .OnComplete(operation =>
                {
                    SaveBindingOverrides();
                    operation.Dispose();
                    activeRebind = null;
                    action.Enable();
                    completed?.Invoke(true);
                });
            activeRebind.Start();
            return true;
        }

        public void CancelInteractiveRebind()
        {
            if (activeRebind == null)
                return;

            activeRebind.Cancel();
            activeRebind.Dispose();
            activeRebind = null;
        }

        private static int FindFirstRebindableBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                    return i;
            }

            return -1;
        }

        private void SaveBindingOverrides()
        {
            if (actionAsset == null)
                return;

            PlayerPrefs.SetString(BindingOverridesKey, actionAsset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        private void LoadBindingOverrides()
        {
            if (actionAsset == null)
                return;

            if (PlayerPrefs.HasKey(BindingOverridesKey))
            {
                string json = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                    actionAsset.LoadBindingOverridesFromJson(json);
            }

            // Migrate the pre-Input-System per-action keys when no override for
            // that action is present in the JSON asset.
            foreach (InputAction action in actions.Values)
            {
                string legacyKey = $"Input_{action.name}";
                if (!PlayerPrefs.HasKey(legacyKey))
                    continue;

                if (!Enum.TryParse(PlayerPrefs.GetString(legacyKey), out KeyCode key))
                    continue;

                string path = KeyCodeToPath(key);
                int bindingIndex = FindFirstRebindableBinding(action);
                if (!string.IsNullOrEmpty(path) && bindingIndex >= 0)
                    action.ApplyBindingOverride(bindingIndex, path);
            }
        }

        private static string KeyCodeToPath(KeyCode key)
        {
            return key switch
            {
                KeyCode.Mouse0 => "<Mouse>/leftButton",
                KeyCode.Mouse1 => "<Mouse>/rightButton",
                KeyCode.Mouse2 => "<Mouse>/middleButton",
                KeyCode.Space => "<Keyboard>/space",
                KeyCode.LeftShift => "<Keyboard>/leftShift",
                KeyCode.Escape => "<Keyboard>/escape",
                KeyCode.JoystickButton0 => "<Joystick>/button0",
                KeyCode.JoystickButton1 => "<Joystick>/button1",
                KeyCode.JoystickButton2 => "<Joystick>/button2",
                KeyCode.JoystickButton3 => "<Joystick>/button3",
                KeyCode.JoystickButton4 => "<Joystick>/button4",
                KeyCode.JoystickButton5 => "<Joystick>/button5",
                KeyCode.JoystickButton6 => "<Joystick>/button6",
                KeyCode.JoystickButton7 => "<Joystick>/button7",
                KeyCode.JoystickButton8 => "<Joystick>/button8",
                KeyCode.JoystickButton9 => "<Joystick>/button9",
                KeyCode.Alpha0 => "<Keyboard>/0",
                KeyCode.Alpha1 => "<Keyboard>/1",
                KeyCode.Alpha2 => "<Keyboard>/2",
                KeyCode.Alpha3 => "<Keyboard>/3",
                KeyCode.Alpha4 => "<Keyboard>/4",
                KeyCode.Alpha5 => "<Keyboard>/5",
                KeyCode.Alpha6 => "<Keyboard>/6",
                KeyCode.Alpha7 => "<Keyboard>/7",
                KeyCode.Alpha8 => "<Keyboard>/8",
                KeyCode.Alpha9 => "<Keyboard>/9",
                _ => key >= KeyCode.A && key <= KeyCode.Z
                    ? $"<Keyboard>/{key.ToString().ToLowerInvariant()}"
                    : null
            };
        }

        private static KeyCode PathToKeyCode(string path)
        {
            if (string.IsNullOrEmpty(path))
                return KeyCode.None;

            string normalized = path.ToLowerInvariant();
            return normalized switch
            {
                "<mouse>/leftbutton" => KeyCode.Mouse0,
                "<mouse>/rightbutton" => KeyCode.Mouse1,
                "<mouse>/middlebutton" => KeyCode.Mouse2,
                "<keyboard>/space" => KeyCode.Space,
                "<keyboard>/leftshift" => KeyCode.LeftShift,
                "<keyboard>/escape" => KeyCode.Escape,
                "<joystick>/button0" => KeyCode.JoystickButton0,
                "<joystick>/button1" => KeyCode.JoystickButton1,
                "<joystick>/button2" => KeyCode.JoystickButton2,
                "<joystick>/button3" => KeyCode.JoystickButton3,
                "<joystick>/button4" => KeyCode.JoystickButton4,
                "<joystick>/button5" => KeyCode.JoystickButton5,
                "<joystick>/button6" => KeyCode.JoystickButton6,
                "<joystick>/button7" => KeyCode.JoystickButton7,
                "<joystick>/button8" => KeyCode.JoystickButton8,
                "<joystick>/button9" => KeyCode.JoystickButton9,
                "<keyboard>/0" => KeyCode.Alpha0,
                "<keyboard>/1" => KeyCode.Alpha1,
                "<keyboard>/2" => KeyCode.Alpha2,
                "<keyboard>/3" => KeyCode.Alpha3,
                "<keyboard>/4" => KeyCode.Alpha4,
                "<keyboard>/5" => KeyCode.Alpha5,
                "<keyboard>/6" => KeyCode.Alpha6,
                "<keyboard>/7" => KeyCode.Alpha7,
                "<keyboard>/8" => KeyCode.Alpha8,
                "<keyboard>/9" => KeyCode.Alpha9,
                _ => ParseLetterKey(normalized)
            };
        }

        private static KeyCode ParseLetterKey(string path)
        {
            const string prefix = "<keyboard>/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length != prefix.Length + 1)
                return KeyCode.None;

            string name = path.Substring(prefix.Length).ToUpperInvariant();
            return Enum.TryParse(name, out KeyCode key) ? key : KeyCode.None;
        }
    }
}
