using System.Collections;
using System.Linq;
using FPS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS.Tests
{
    public sealed class HudInputPlayModeTests
    {
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [SetUp]
        public void RouteSyntheticInputWithoutGameViewFocus()
        {
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void RestoreInputRouting()
        {
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        }

        [UnityTest]
        public IEnumerator LateCampaignBindingUsesEventsAndUnsubscribesWhenDisabled()
        {
            var root = new GameObject("Late HUD source fixture");
            var inventory = root.AddComponent<TeamInventoryUI>();
            var settings = ScriptableObject.CreateInstance<CampaignSettings>();
            var catalog = ScriptableObject.CreateInstance<CampaignContentCatalog>();
            try
            {
                var campaign = root.AddComponent<CampaignMissionController>();
                campaign.enabled = false;
                campaign.State.phase = CampaignPhase.Exploring;
                settings.contentCatalog = catalog; campaign.settings = settings;
                catalog.files = new[] { new CampaignFileDefinition { id = CampaignFileId.FactoryManifest, title = "Event fixture file", body = "Synthetic event binding evidence" } };
                var changed = typeof(CampaignMissionController).GetField("Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                int ListenerCount()
                {
                    var listeners = ((System.Action)changed.GetValue(campaign))?.GetInvocationList();
                    int count = 0;
                    if (listeners != null) foreach (var listener in listeners) if (ReferenceEquals(listener.Target, inventory)) count++;
                    return count;
                }
                Assert.That(ListenerCount(), Is.EqualTo(1), "UI binds even when created before its manager");
                inventory.Toggle();
                foreach (var button in inventory.GetComponentsInChildren<Button>())
                    if (button.name == "FilesTab") button.onClick.Invoke();
                campaign.State.discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest);
                campaign.State.revision++;
                ((System.Action)changed.GetValue(campaign))?.Invoke();
                Assert.That(inventory.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Event fixture file"), Is.True,
                    "Published data is rendered synchronously without waiting for Update");
                for (int i = 0; i < 20; i++)
                {
                    inventory.enabled = false;
                    Assert.That(ListenerCount(), Is.Zero);
                    inventory.enabled = true;
                    Assert.That(ListenerCount(), Is.EqualTo(1));
                    inventory.Toggle(); inventory.Close();
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(settings); Object.Destroy(catalog); UiStateStack.Clear();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InventoryBackClosesOnlyTheTopOverlayAndRestoresFocus()
        {
            var root = new GameObject("HUD overlay regression");
            Keyboard keyboard = null;
            var cursorLock = Cursor.lockState;
            bool cursorVisible = Cursor.visible;
            CampaignSettings settings = null;
            CampaignContentCatalog catalog = null;
            try
            {
                var input = root.AddComponent<InputManager>();
                input.ActionsAsset.RemoveAllBindingOverrides();
                input.ActionsAsset.FindAction("Pause").ApplyBindingOverride(0, "<Keyboard>/escape");
                keyboard = InputSystem.AddDevice<Keyboard>();
                var campaign = root.AddComponent<CampaignMissionController>();
                campaign.enabled = false;
                campaign.State.phase = CampaignPhase.Exploring;
                settings = ScriptableObject.CreateInstance<CampaignSettings>();
                catalog = ScriptableObject.CreateInstance<CampaignContentCatalog>();
                catalog.objectives = new[] { new CampaignObjectiveDefinition { id = CampaignObjectiveId.FactoryIsolate, title = "Inspect the isolation panel" } };
                settings.contentCatalog = catalog; campaign.settings = settings;
                var inventory = root.AddComponent<TeamInventoryUI>();
                var menu = root.AddComponent<InGameMenuUI>();
                var pause = new GameObject("Fixture pause", typeof(RectTransform), typeof(Image), typeof(Button));
                pause.transform.SetParent(root.transform);
                typeof(InGameMenuUI).GetField("pausePanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(menu, pause);
                var settingsPanel = new GameObject("Fixture settings", typeof(RectTransform), typeof(Image), typeof(Button));
                settingsPanel.transform.SetParent(root.transform);
                typeof(InGameMenuUI).GetField("settingsPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(menu, settingsPanel);
                var eventRoot = new GameObject("HUD fixture EventSystem", typeof(EventSystem));
                eventRoot.transform.SetParent(root.transform);
                var selection = new GameObject("Previous focus");
                selection.transform.SetParent(root.transform);
                yield return null;
                EventSystem.current.SetSelectedGameObject(selection);
                inventory.Toggle();
                Assert.That(TeamInventoryUI.IsOpen && InputManager.GameplayInputBlocked, Is.True);
                Assert.That(Cursor.visible, Is.True);
                foreach (var button in inventory.GetComponentsInChildren<Button>())
                    if (button.name == "MissionsTab") button.onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in inventory.GetComponentsInChildren<TMPro.TMP_Text>())
                    if (label.name == "Name" || label.name == "Status")
                    {
                        label.ForceMeshUpdate();
                        Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.preferredHeight), "Checklist row must have room to render its label.");
                        Assert.That(label.isTextOverflowing, Is.False);
                    }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.That(TeamInventoryUI.IsOpen || InGameMenuUI.IsMenuOpen || InputManager.GameplayInputBlocked, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(selection));
                Assert.That(Cursor.lockState, Is.EqualTo(cursorLock));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.That(InGameMenuUI.IsMenuOpen, Is.True);
                menu.ResumeGame();
                Assert.That(InputManager.GameplayInputBlocked, Is.False);
                input.ActionsAsset.FindAction("Pause").ApplyBindingOverride(0, "<Keyboard>/p");
                input.ActionsAsset.FindAction("Journal").ApplyBindingOverride(0, "<Keyboard>/j");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(input.GetPromptDisplayName("Pause"), Is.EqualTo("P").IgnoreCase);
                Assert.That(input.GetPromptDisplayName("Journal"), Is.EqualTo("J").IgnoreCase);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.That(InGameMenuUI.IsMenuOpen, Is.False, "Old Pause binding no longer opens the menu");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
                yield return null;
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Pause));
                float simulationScale = Time.timeScale;
                var pauseSelection = EventSystem.current.currentSelectedGameObject;
                typeof(InGameMenuUI).GetMethod("OpenSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, null);
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Settings));
                yield return null;
                UiStateStack.TryBack();
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Pause));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(pauseSelection));
                yield return null;
                UiStateStack.TryBack();
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Gameplay));
                Assert.That(Time.timeScale, Is.EqualTo(simulationScale), "Menus do not pause multiplayer simulation");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                Object.Destroy(root);
                if (settings != null) Object.Destroy(settings);
                if (catalog != null) Object.Destroy(catalog);
                Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
                InputManager.CampaignInputBlocked = false;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PromptsSwitchOnDerivedGamepadAndReportUnboundActions()
        {
            const string layout = "HudRegressionPad";
            var root = new GameObject("HUD input PlayMode regression");
            Gamepad pad = null;
            Keyboard keyboard = null;
            try
            {
                var input = root.AddComponent<InputManager>();
                input.ActionsAsset.RemoveAllBindingOverrides();
                InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"Gamepad\"}");
                pad = (Gamepad)InputSystem.AddDevice(layout);
                keyboard = InputSystem.AddDevice<Keyboard>();
                var fire = input.ActionsAsset.FindAction("Fire");
                fire.AddBinding("<" + layout + ">/buttonSouth");
                yield return null;
                int changes = 0;
                input.PromptBindingsChanged += () => changes++;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                Assert.That(input.PromptUsesGamepad, Is.True);
                Assert.That(changes, Is.GreaterThan(0));
                // Disable the generic binding so this also proves derived-layout matching.
                for (int i = 0; i < fire.bindings.Count; i++)
                    if (fire.bindings[i].path.StartsWith("<Gamepad>")) fire.ApplyBindingOverride(i, "");
                Assert.That(input.GetPromptDisplayName("Fire"), Is.Not.EqualTo("--"));
                Assert.That(input.GetPromptBindingPath("Fire"), Does.StartWith("<" + layout + ">"));
                var journal = input.ActionsAsset.FindAction("Journal");
                for (int i = 0; i < journal.bindings.Count; i++) journal.ApplyBindingOverride(i, "");
                Assert.That(input.GetPromptDisplayName("Journal"), Is.EqualTo("--"));
                Assert.That(input.GetPromptBindingPath("Journal"), Is.Empty);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return null;
                Assert.That(input.PromptUsesGamepad, Is.False);
                Assert.That(input.GetPromptBindingPath("Aim"), Is.EqualTo("<Mouse>/rightButton").IgnoreCase);
            }
            finally
            {
                if (pad != null) InputSystem.RemoveDevice(pad);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveLayout(layout);
                Object.Destroy(root);
            }
            yield return null;
        }
    }
}
