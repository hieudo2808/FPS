using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnitySkills;

[assembly: InternalsVisibleTo("FPS.AgentPlaytest.Tests.Editor")]

namespace FPS.AgentPlaytest
{
    [InputControlLayout(canRunInBackground = true)]
    public class AgentKeyboard : Keyboard { }

    [InputControlLayout(canRunInBackground = true)]
    public class AgentMouse : Mouse { }

    // Editor-only input devices feed the same actions as the player's hardware.
    [InitializeOnLoad]
    public static class AgentPlaytestSkills
    {
        public sealed class Result
        {
            public bool success;
            public string error;
            public int commandId;
            public string status;
            public int inputUpdates;
        }

        private static AgentKeyboard keyboard;
        private static AgentMouse mouse;
        private static InputActionAsset boundActions;
        private static InputDevice[] originalActionDevices;
        private static InputSettings originalSettings;
        private static InputSettings sessionSettings;
        private static HideFlags originalSettingsFlags;
        private static bool originalRunInBackground;
        private static KeyboardState heldKeys;
        private static MouseState heldMouse;
        private static Vector2 look;
        private static double deadline;
        private static bool running;
        private static int commandId;
        private static int inputUpdates;
        private static string status = "idle";

        static AgentPlaytestSkills()
        {
            InputSystem.onBeforeUpdate += BeforeInputUpdate;
            EditorApplication.update += CheckLifecycle;
            EditorApplication.playModeStateChanged += _ => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += () => Stop();
            EditorApplication.quitting += () => Stop();
        }

        [UnitySkill("fps_playtest_input",
            "Hold virtual keyboard keys and mouse buttons for seconds (0.05..10) in Play Mode. " +
            "keys: comma-separated Key enum names, e.g. W,LeftShift,F,Space. " +
            "mouseButtons: Left,Right,Middle. lookX/lookY: one relative mouse delta in pixels (positive Y looks up). " +
            "pointerX/pointerY: optional normalized Game View coordinates, origin top-left, both -1 to keep position. " +
            "Returns immediately; poll fps_playtest_status. Use scene_screenshot to observe. " +
            "No gameplay state is read or bypassed. See Documents/AgentPlaytest/README.md.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Execute,
            Tags = new[] { "playtest", "input", "keyboard", "mouse", "agent" },
            Outputs = new[] { "success", "error", "commandId", "status", "inputUpdates" },
            MutatesScene = true, RiskLevel = "medium")]
        public static Result Input(float seconds = 0.2f, string keys = "", float lookX = 0,
            float lookY = 0, string mouseButtons = "", float pointerX = -1, float pointerY = -1)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                return Error("Requires running, unpaused Play Mode.");
            if (InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsManually)
                return Error("Requires automatic Input System updates.");
            Result result = Begin(seconds, keys, lookX, lookY, mouseButtons, pointerX, pointerY,
                EditorApplication.timeSinceStartup);
            if (result.success) BindActions(InputManager.Instance?.ActionsAsset);
            return result;
        }

        internal static void BindActions(InputActionAsset actions)
        {
            if (actions == null || actions == boundActions) return;
            RestoreActions();
            boundActions = actions;
            if (actions.devices.HasValue)
            {
                var devices = actions.devices.Value;
                originalActionDevices = new InputDevice[devices.Count];
                for (int i = 0; i < devices.Count; i++) originalActionDevices[i] = devices[i];
            }
            else originalActionDevices = null;
            actions.devices = new InputDevice[] { keyboard, mouse };
        }

        private static void RestoreActions()
        {
            if (boundActions != null) boundActions.devices = originalActionDevices;
            boundActions = null;
            originalActionDevices = null;
        }

        internal static Result Begin(float seconds, string keys, float lookX, float lookY,
            string mouseButtons, float pointerX, float pointerY, double now)
        {
            if (running) return Error("An input command is running. Wait for completion or call fps_playtest_stop.");
            if (!float.IsFinite(seconds) || seconds < 0.05f || seconds > 10)
                return Error("seconds must be finite and between 0.05 and 10.");
            if (!float.IsFinite(lookX) || !float.IsFinite(lookY) || Mathf.Abs(lookX) > 1000 || Mathf.Abs(lookY) > 1000)
                return Error("lookX/lookY must be finite and between -1000 and 1000 pixels.");
            bool hasPointer = pointerX != -1 || pointerY != -1;
            if (hasPointer && (!float.IsFinite(pointerX) || !float.IsFinite(pointerY)
                || pointerX < 0 || pointerX > 1 || pointerY < 0 || pointerY > 1))
                return Error("Set both pointer coordinates to 0..1, or both to -1.");

            KeyboardState nextKeys = default;
            foreach (string token in (keys ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string name = token.Trim();
                if (name.Length == 0 || !char.IsLetter(name[0]) || !Enum.TryParse(name, true, out Key key)
                    || key == Key.None || !Enum.IsDefined(typeof(Key), key))
                    return Error("Unknown key: " + token + ". Use Key enum names such as W, LeftShift, Space, Digit1.");
                nextKeys.Press(key);
            }
            MouseState nextMouse = default;
            foreach (string token in (mouseButtons ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string name = token.Trim();
                if (name.Length == 0 || !char.IsLetter(name[0]) || !Enum.TryParse(name, true, out MouseButton button)
                    || !Enum.IsDefined(typeof(MouseButton), button))
                    return Error("Unknown mouse button: " + token + ". Use Left, Right, Middle, Forward or Back.");
                nextMouse = nextMouse.WithButton(button);
            }

            if (sessionSettings == null)
            {
                originalSettings = InputSystem.settings;
                originalSettingsFlags = originalSettings.hideFlags;
                sessionSettings = UnityEngine.Object.Instantiate(originalSettings);
                sessionSettings.hideFlags = HideFlags.HideAndDontSave;
                sessionSettings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // Virtual devices keep working in the background; hardware keeps Unity's normal reset policy.
                sessionSettings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
                // Input System destroys HideAndDontSave settings when replaced; retain the original for restoration.
                if (originalSettingsFlags == HideFlags.HideAndDontSave)
                    originalSettings.hideFlags = HideFlags.DontSave;
                InputSystem.settings = sessionSettings;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
            }
            if (keyboard == null || !keyboard.added)
            {
                InputSystem.RegisterLayout<AgentKeyboard>();
                keyboard = InputSystem.AddDevice<AgentKeyboard>();
            }
            if (mouse == null || !mouse.added)
            {
                InputSystem.RegisterLayout<AgentMouse>();
                mouse = InputSystem.AddDevice<AgentMouse>();
                heldMouse.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
            nextMouse.position = hasPointer
                ? new Vector2(pointerX * Screen.width, (1 - pointerY) * Screen.height)
                : heldMouse.position;
            heldKeys = nextKeys;
            heldMouse = nextMouse;
            look = new Vector2(lookX, lookY);
            deadline = now + seconds;
            commandId++;
            inputUpdates = 0;
            running = true;
            status = "running";
            return Status();
        }

        [UnitySkill("fps_playtest_status", "Return input command progress only; does not inspect game or quest state.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Query, ReadOnly = true,
            Mode = SkillMode.SemiAuto,
            Outputs = new[] { "success", "error", "commandId", "status", "inputUpdates" })]
        public static Result Status() => new Result
        {
            success = true, commandId = commandId, status = status, inputUpdates = inputUpdates
        };

        [UnitySkill("fps_playtest_stop", "Release all agent input and remove the virtual devices. Safe to call repeatedly.",
            Category = SkillCategory.Editor, Operation = SkillOperation.Execute,
            MutatesScene = true, RiskLevel = "medium",
            Outputs = new[] { "success", "error", "commandId", "status", "inputUpdates" })]
        public static Result Stop()
        {
            running = false;
            status = "stopped";
            RestoreActions();
            if (keyboard != null && keyboard.added)
            {
                InputSystem.ResetDevice(keyboard);
                InputSystem.RemoveDevice(keyboard);
            }
            if (mouse != null && mouse.added)
            {
                InputSystem.ResetDevice(mouse);
                InputSystem.RemoveDevice(mouse);
            }
            keyboard = null;
            mouse = null;
            if (sessionSettings != null)
            {
                if (InputSystem.settings == sessionSettings && originalSettings != null)
                    InputSystem.settings = originalSettings;
                if (originalSettings != null) originalSettings.hideFlags = originalSettingsFlags;
                if (sessionSettings != null) UnityEngine.Object.DestroyImmediate(sessionSettings);
                sessionSettings = null;
                originalSettings = null;
                Application.runInBackground = originalRunInBackground;
            }
            return Status();
        }

        private static Result Error(string message) => new Result
        {
            success = false, error = message, commandId = commandId, status = status, inputUpdates = inputUpdates
        };

        private static void BeforeInputUpdate()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            var update = InputState.currentUpdateType;
            if (update == InputUpdateType.Dynamic || update == InputUpdateType.Fixed)
                Advance(EditorApplication.timeSinceStartup);
        }

        internal static void Advance(double now)
        {
            if (!running) return;
            if (keyboard == null || !keyboard.added || mouse == null || !mouse.added)
            {
                Stop();
                status = "device_lost";
                return;
            }
            // Release before resuming gameplay after a slow frame; never replay an expired hold.
            bool expired = now >= deadline;
            var mouseState = expired ? new MouseState { position = heldMouse.position } : heldMouse;
            mouseState.delta = !expired && inputUpdates == 0 ? look : Vector2.zero;
            InputSystem.QueueStateEvent(keyboard, expired ? default : heldKeys);
            InputSystem.QueueStateEvent(mouse, mouseState);
            if (expired)
            {
                running = false;
                status = inputUpdates == 0 ? "expired_before_input" : "completed";
            }
            else inputUpdates++;
        }

        private static void CheckLifecycle()
        {
            if (keyboard == null && mouse == null) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
                Stop();
            else if (running && EditorApplication.timeSinceStartup > deadline + 1)
            {
                Stop();
                status = "input_update_timeout";
            }
        }
    }
}
