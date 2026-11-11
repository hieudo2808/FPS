using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPS.AgentPlaytest.Tests
{
    public class AgentPlaytestInputTests : InputTestFixture
    {
        [TearDown]
        public void ReleaseAgentDevices() => AgentPlaytestSkills.Stop();

        [Test]
        public void HeldKeysDriveActionsAcrossFramesAndReleaseOnDeadline()
        {
            Assert.That(AgentPlaytestSkills.Begin(1, "W,LeftShift", 0, 0, "", -1, -1, 10).success);
            using var move = new InputAction(binding: "<Keyboard>/w");
            move.Enable();
            AgentPlaytestSkills.Advance(10);
            InputSystem.Update();
            Assert.That(move.IsPressed());
            Assert.That(Keyboard.current.leftShiftKey.isPressed);
            AgentPlaytestSkills.Advance(10.5);
            InputSystem.Update();
            Assert.That(move.IsPressed());
            AgentPlaytestSkills.Advance(11);
            InputSystem.Update();
            Assert.That(move.IsPressed(), Is.False);
            Assert.That(Keyboard.current.leftShiftKey.isPressed, Is.False);
            Assert.That(AgentPlaytestSkills.Status().status, Is.EqualTo("completed"));
        }

        [Test]
        public void LookIsOneDeltaAndMouseButtonsStayHeldUntilReleased()
        {
            AgentPlaytestSkills.Begin(1, "", 12, -4, "Left,Right", 0.25f, 0.75f, 10);
            AgentPlaytestSkills.Advance(10);
            InputSystem.Update();
            Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(new Vector2(12, -4)));
            Assert.That(Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed);
            AgentPlaytestSkills.Advance(10.5);
            InputSystem.Update();
            Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(Vector2.zero));
            Assert.That(Mouse.current.leftButton.isPressed);
            AgentPlaytestSkills.Advance(11);
            InputSystem.Update();
            Assert.That(Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed, Is.False);
        }

        [Test]
        public void BoundActionsIgnorePhysicalMouseUntilAgentStops()
        {
            var physicalMouse = InputSystem.AddDevice<Mouse>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                var map = actions.AddActionMap("Gameplay");
                var look = map.AddAction("Look", InputActionType.Value);
                look.AddBinding("<Mouse>/delta");
                actions.devices = new InputDevice[] { physicalMouse };
                actions.Enable();

                Assert.That(AgentPlaytestSkills.Begin(1, "", 12, 0, "", -1, -1, 10).success);
                AgentPlaytestSkills.BindActions(actions);
                InputSystem.QueueDeltaStateEvent(physicalMouse.delta, new Vector2(50, 0));
                InputSystem.Update();
                Assert.That(look.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));

                AgentPlaytestSkills.Advance(10);
                InputSystem.Update();
                Assert.That(look.ReadValue<Vector2>(), Is.EqualTo(new Vector2(12, 0)));
            }
            finally
            {
                AgentPlaytestSkills.Stop();
                Assert.That(actions.devices.HasValue, Is.True);
                Assert.That(actions.devices.Value.Count, Is.EqualTo(1));
                Assert.That(actions.devices.Value[0], Is.SameAs(physicalMouse));
                Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void StopCancelsActionsAndRemovesOnlyAgentDevices()
        {
            var originalSettings = InputSystem.settings;
            var originalBackground = Application.runInBackground;
            var physicalKeyboard = InputSystem.AddDevice<Keyboard>();
            AgentPlaytestSkills.Begin(1, "F", 0, 0, "", -1, -1, 10);
            using var interact = new InputAction(binding: "<Keyboard>/f");
            interact.Enable();
            AgentPlaytestSkills.Advance(10);
            InputSystem.Update();
            Assert.That(interact.IsPressed());
            AgentPlaytestSkills.Stop();
            Assert.That(interact.IsPressed(), Is.False);
            Assert.That(physicalKeyboard.added);
            Assert.That(InputSystem.devices.Count, Is.EqualTo(1));
            Assert.That(InputSystem.settings, Is.SameAs(originalSettings));
            Assert.That(Application.runInBackground, Is.EqualTo(originalBackground));
            Assert.That(AgentPlaytestSkills.Stop().success);
        }

        [Test]
        public void InvalidCommandsDoNotCreateDevicesOrOverwriteActiveInput()
        {
            Assert.That(AgentPlaytestSkills.Begin(float.NaN, "W", 0, 0, "", -1, -1, 10).success, Is.False);
            Assert.That(AgentPlaytestSkills.Begin(1, "NotAKey", 0, 0, "", -1, -1, 10).success, Is.False);
            Assert.That(AgentPlaytestSkills.Begin(1, "W", float.PositiveInfinity, 0, "", -1, -1, 10).success, Is.False);
            Assert.That(AgentPlaytestSkills.Begin(1, "W", 0, 0, "", 0.5f, -1, 10).success, Is.False);
            Assert.That(InputSystem.devices.Count, Is.Zero);
            var first = AgentPlaytestSkills.Begin(1, "W", 0, 0, "", -1, -1, 10);
            Assert.That(AgentPlaytestSkills.Begin(1, "S", 0, 0, "", -1, -1, 10).success, Is.False);
            Assert.That(AgentPlaytestSkills.Status().commandId, Is.EqualTo(first.commandId));
        }

        [Test]
        public void ExpiredCommandDoesNotDeliverALatePress()
        {
            AgentPlaytestSkills.Begin(1, "F", 20, 0, "Left", -1, -1, 10);
            AgentPlaytestSkills.Advance(12);
            InputSystem.Update();
            Assert.That(Keyboard.current.fKey.isPressed || Mouse.current.leftButton.isPressed, Is.False);
            Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(Vector2.zero));
            Assert.That(AgentPlaytestSkills.Status().status, Is.EqualTo("expired_before_input"));
        }
    }
}
