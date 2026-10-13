#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace FPS.Tests
{
    public sealed class WeaponAimInputTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator EveryAdsWeaponTogglesOnPressAndRequiresFreshPressAfterInterruption()
        {
            var inputRoot = new GameObject("ADS input regression");
            var input = InputManager.Instance != null ? InputManager.Instance : inputRoot.AddComponent<InputManager>();
            string previousBindings = input.ActionsAsset.SaveBindingOverridesAsJson();
            var previousDevices = input.ActionsAsset.devices;
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            bool wasBlocked = InputManager.GameplayInputBlocked;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            GameObject player = null;
            try
            {
                var aim = input.ActionsAsset.FindAction("Aim");
                for (int i = 0; i < aim.bindings.Count; i++) aim.ApplyBindingOverride(i, "");
                aim.ApplyBindingOverride(0, "<Mouse>/rightButton");
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.EnableDevice(mouse);
                input.ActionsAsset.devices = new InputDevice[] { mouse };
                aim.Enable();
                InputManager.GameplayInputBlocked = false;
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab"));
                var health = player.GetComponent<PlayerHealth>();
                ((NetworkVariable<bool>)typeof(PlayerHealth).GetField("networkInputReady", Private).GetValue(health)).Value = true;
                Assert.That(health.CanUseCombat, Is.True);
                Assert.That(NetworkMatchStateManager.IsGameplayActive, Is.True);
                var manager = player.GetComponent<WeaponManager>();
                player.GetComponent<PlayerVisibilityController>().SetupVisibility(true);
                var weapons = player.GetComponentsInChildren<Weapon>(true);
                int tested = 0;
                foreach (var weapon in weapons)
                {
                    if (weapon.Data == null || !weapon.Data.supportsAim) continue;
                    foreach (var other in weapons) other.gameObject.SetActive(other == weapon);
                    weapon.BindFirstPersonPresentation(player.GetComponent<MouseMovement>().BodyCam, manager.FirstPersonArmsAnimator);
                    weapon.PrepareFirstPersonAnimation();
                    weapon.SetOwner(true);
                    // Drive the real aim update once per input frame without also firing/reloading.
                    weapon.enabled = false;
                    yield return Button(false);
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.True, weapon.name + " first press");
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.True, weapon.name + " held press cannot toggle again");
                    yield return Button(false);
                    Assert.That(weapon.IsAimRequested, Is.True, weapon.name + " release keeps ADS");
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.False, weapon.name + " second press exits ADS");
                    yield return Button(false);
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.True);
                    weapon.SetCombatAvailability(false);
                    weapon.SetCombatAvailability(true);
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.False, "Combat interruption while held requires release and fresh press.");
                    yield return Button(false);
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.True);
                    InputManager.GameplayInputBlocked = true;
                    yield return Button(true);
                    InputManager.GameplayInputBlocked = false;
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.False, "Closing UI while RMB is held must not enter ADS.");
                    yield return Button(false);
                    yield return Button(true);
                    if (weapon.Data.exitAimAfterShot)
                    {
                        Invoke(weapon, "BeginExitAimAfterShot");
                        yield return Button(true);
                        Assert.That(weapon.IsAimRequested, Is.False, "Single-shot scope stays lowered until a fresh press.");
                        yield return Button(false);
                        yield return Button(true);
                        Assert.That(weapon.IsAimRequested, Is.True);
                    }
                    // Authoritative reload also clears the latch without reopening on release.
                    weapon.SetLocalAmmoState(weapon.Data.magazineSize, 10, true);
                    Assert.That(weapon.IsAimRequested, Is.False);
                    weapon.SetLocalAmmoState(weapon.Data.magazineSize, 10, false);
                    yield return Button(false);
                    Assert.That(weapon.IsAimRequested, Is.False);
                    yield return Button(true);
                    Assert.That(weapon.IsAimRequested, Is.True);
                    yield return Button(false);
                    weapon.SetOwner(false);
                    tested++;

                    IEnumerator Button(bool pressed)
                    {
                        var state = new MouseState();
                        if (pressed) state = state.WithButton(MouseButton.Right);
                        InputSystem.QueueStateEvent(mouse, state);
                        InputSystem.Update();
                        Assert.That(mouse.rightButton.isPressed, Is.EqualTo(pressed), "Synthetic RMB state");
                        Assert.That(input.GetAimInput(), Is.EqualTo(pressed), "Aim action must receive the synthetic RMB");
                        Invoke(weapon, "UpdateAimPresentation");
                        yield return null;
                    }
                }
                Assert.That(tested, Is.GreaterThanOrEqualTo(2), "Includes Warden and Operator.");
            }
            finally
            {
                input.ActionsAsset.LoadBindingOverridesFromJson(previousBindings);
                input.ActionsAsset.devices = previousDevices;
                InputSystem.settings.backgroundBehavior = backgroundBehavior;
                InputManager.GameplayInputBlocked = wasBlocked;
                InputSystem.RemoveDevice(mouse);
                if (player != null) Object.Destroy(player);
                Object.Destroy(inputRoot);
            }
            yield return null;
        }

        private static void Invoke(Weapon weapon, string method) => typeof(Weapon).GetMethod(method, Private).Invoke(weapon, null);
    }
}
#endif
