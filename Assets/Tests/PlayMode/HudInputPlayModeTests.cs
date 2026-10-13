using System.Collections;
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
