using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class WeaponTimingTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("Bucky", 1.1f, .75f, 1.75f, 1.3333334f, 2.1666667f)]
        [TestCase("Classic", 6.75f, .75f, 1.75f, 1f, 2.1166666f)]
        [TestCase("Odin", 12f, 1.25f, 5f, 2.8333333f, 6f)]
        [TestCase("Operator", .6f, 1.5f, 3.7f, 2.3333333f, 4.5f)]
        [TestCase("Vandal", 9.75f, 1f, 2.5f, 1.65f, 2.9333334f)]
        [TestCase("Warden", 6.5f, 1f, 2.5f, 2.3333333f, 3.8f)]
        public void SavedDefinitionsMatchSourceClocks(string name, float rate, float equip,
            float reload, float equipClip, float reloadClip)
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                $"Assets/FPS/Features/Weapons/Content/{name}/{name}.asset");
            Assert.That(data, Is.Not.Null);
            Assert.That(data.RoundsPerSecond, Is.EqualTo(rate).Within(.0001f));
            Assert.That(data.EquipDuration, Is.EqualTo(equip).Within(.0001f));
            Assert.That(data.ReloadDuration, Is.EqualTo(reload).Within(.0001f));
            Assert.That(data.EquipAnimationDuration, Is.EqualTo(equipClip).Within(.0001f));
            Assert.That(data.ReloadAnimationDuration, Is.EqualTo(reloadClip).Within(.0001f));
        }

        [Test]
        public void LegacyDefinitionsFallBackToGameplayTiming()
        {
            var data = NewData();
            try
            {
                Assert.That(data.EquipAnimationDuration, Is.EqualTo(data.EquipDuration));
                Assert.That(data.ReloadAnimationDuration, Is.EqualTo(data.ReloadDuration));
            }
            finally { Object.DestroyImmediate(data); }
        }

        [Test]
        public void ServerUnlocksAndCommitsWithoutCompressingTheVisualTail()
        {
            var data = NewData();
            try
            {
                data.ApplyPresentationTimings(2.3333333f, 3.8f);
                var server = EmptyMagazine(data);
                server.BeginEquip(data, 10d);
                Assert.That(server.TryConsumeFire(data, 10.99d), Is.False);
                Assert.That(server.EquipCompleteTime, Is.EqualTo(11d));
                Assert.That(server.TryBeginReload(data, 11d), Is.True);
                var timeline = server.ReloadTimeline;
                Assert.That(timeline.NormalizedTime(data, 13.5d, false),
                    Is.EqualTo(2.5f / 3.8f).Within(.0001f));
                server.AdvanceReloadIfReady(data, 13.5d);
                Assert.That(server.IsReloading(13.5d), Is.False);
                Assert.That(server.MagazineAmmo, Is.EqualTo(data.magazineSize));
                Assert.That(timeline.ShouldContinueAfterGameplay(data, 13.5d), Is.True);
                Assert.That(server.TryConsumeFire(data, 13.5d), Is.True);
                Assert.That(server.TryConsumeFire(data, 13.6d), Is.False);
                Assert.That(timeline.NormalizedTime(data, 14.8d, false), Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(data); }
        }

        [Test]
        public void DelayedSnapshotsKeepTheOriginalClockAndInfectionMultiplier()
        {
            var data = NewData();
            try
            {
                data.ApplyPresentationTimings(2.3333333f, 3.8f);
                var timeline = WeaponReloadTimeline.Begin(100d, 2f, 0);
                Assert.That(timeline.NormalizedTime(data, 103.8d, false), Is.EqualTo(.5f).Within(.0001f));
                Assert.That(timeline.GameplayCompleteTime(data), Is.EqualTo(105d));
                Assert.That(timeline.PresentationCompleteTime(data), Is.EqualTo(107.6d).Within(.0001d));
                Assert.That(timeline.ShouldContinueAfterGameplay(data, 104d), Is.False, "An early end cancels.");
                Assert.That(timeline.ShouldContinueAfterGameplay(data, 106d), Is.True);
                Assert.That(timeline.ShouldContinueAfterGameplay(data, 108d), Is.False);
            }
            finally { Object.DestroyImmediate(data); }
        }

        [Test]
        public void BuckyKeepsInsertMarkersAndCanInterruptAfterTheFirstShell()
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Bucky/Bucky.asset");
            Assert.That(data.reloadMode, Is.EqualTo(ReloadMode.PerShell));
            var server = EmptyMagazine(data);
            Assert.That(server.TryBeginReload(data, 100d, 2f), Is.True);
            var timeline = server.ReloadTimeline;
            double firstInsert = 100d + 2d * (data.PerShellOpeningDuration + data.PerShellInterval);
            server.AdvanceReloadIfReady(data, firstInsert + .0001d);
            Assert.That(server.MagazineAmmo, Is.EqualTo(1));
            Assert.That(timeline.NormalizedTime(data, 100d + data.PerShellOpeningDuration, false),
                Is.EqualTo(.05f).Within(.0001f));
            Assert.That(timeline.PresentationCompleteTime(data) - timeline.GameplayCompleteTime(data),
                Is.EqualTo(2d * (data.ReloadAnimationDuration - data.ReloadDuration)).Within(.0001d));
            Assert.That(server.TryConsumeFire(data, firstInsert + .0001d), Is.True);
            Assert.That(server.ReloadTimeline.IsValid, Is.False);
            server.AdvanceReloadIfReady(data, 1000d);
            Assert.That(server.MagazineAmmo, Is.Zero, "Cancelled inserts cannot add more ammo.");
        }

        [TestCase("Fire")]
        [TestCase("Equip")]
        [TestCase("Inspect")]
        [TestCase("ReloadComplete")]
        public void NewActionsCancelTheFirstPersonReloadTail(string action)
        {
            var root = new GameObject("WeaponTimingTest");
            root.SetActive(false);
            var data = NewData();
            try
            {
                var weapon = root.AddComponent<Weapon>();
                Set(weapon, "weaponData", data);
                Set(weapon, "reloadPresentationTimeline", WeaponReloadTimeline.Begin(0d, 1f, 0));
                Invoke(weapon, "TriggerAnimation", action);
                Assert.That(Get<WeaponReloadTimeline>(weapon, "reloadPresentationTimeline").IsValid, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        }

        [Test]
        public void FirstPersonCompletionRetainsOnlyTheNaturalVisualTail()
        {
            var root = new GameObject("WeaponTimingTailTest");
            root.SetActive(false);
            var data = NewData();
            try
            {
                data.ApplyPresentationTimings(2.3333333f, 3.8f);
                var weapon = root.AddComponent<Weapon>();
                Set(weapon, "weaponData", data);
                double now = Time.timeAsDouble;
                var tail = WeaponReloadTimeline.Begin(now - 2.6d, 1f, 0);
                Set(weapon, "reloadPresentationTimeline", tail);
                Invoke(weapon, "CompleteReloadPresentation");
                Assert.That(Get<WeaponReloadTimeline>(weapon, "reloadPresentationTimeline"), Is.EqualTo(tail));
                Set(weapon, "reloadPresentationTimeline", WeaponReloadTimeline.Begin(now - 4d, 1f, 0));
                Invoke(weapon, "UpdatePerShellReloadPresentation");
                Assert.That(Get<WeaponReloadTimeline>(weapon, "reloadPresentationTimeline").IsValid, Is.False);
                var newReload = WeaponReloadTimeline.Begin(now, 1f, 0);
                Set(weapon, "reloadPresentationTimeline", newReload);
                Set(weapon, "isReloading", true);
                Invoke(weapon, "UpdatePerShellReloadPresentation");
                Assert.That(Get<WeaponReloadTimeline>(weapon, "reloadPresentationTimeline"), Is.EqualTo(newReload));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisablingWeaponOrCombatClearsThePresentationClock(bool combatDisable)
        {
            var root = new GameObject("WeaponTimingDisableTest");
            root.SetActive(false);
            try
            {
                var weapon = root.AddComponent<Weapon>();
                Set(weapon, "reloadPresentationTimeline", WeaponReloadTimeline.Begin(0d, 1f, 0));
                if (combatDisable) weapon.SetCombatAvailability(false);
                else Invoke(weapon, "OnDisable");
                Assert.That(Get<WeaponReloadTimeline>(weapon, "reloadPresentationTimeline").IsValid, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ThirdPersonPreservesNaturalTailButCancelsOnAcceptedShotOrSwitch()
        {
            var root = new GameObject("WeaponTimingManagerTest");
            root.SetActive(false);
            var data = NewData();
            try
            {
                data.ApplyPresentationTimings(2.3333333f, 3.8f);
                var weapon = root.AddComponent<Weapon>();
                Set(weapon, "weaponData", data);
                var manager = root.AddComponent<WeaponManager>();
                Set(manager, "weapons", new List<GameObject> { root });
                double now = Time.timeAsDouble;
                var tail = WeaponReloadTimeline.Begin(now - 2.6d, 1f, 0);
                manager.SetReloadPresentationTimeline(tail);
                manager.TriggerAnimation("Reload");
                manager.SetReloadPresentationTimeline(default);
                Assert.That(Get<WeaponReloadTimeline>(manager, "thirdPersonReloadTimeline"), Is.EqualTo(tail));
                manager.CompleteActionsForAcceptedShot();
                Assert.That(Get<WeaponReloadTimeline>(manager, "thirdPersonReloadTimeline").IsValid, Is.False);
                manager.SetReloadPresentationTimeline(tail);
                manager.TriggerAnimation("Reload");
                Invoke(manager, "ResetThirdPersonActionState");
                Assert.That(Get<WeaponReloadTimeline>(manager, "thirdPersonReloadTimeline").IsValid, Is.False);
                Invoke(manager, "ApplyThirdPersonActionTiming", false, -1d, now + .4d);
                Assert.That(Get<double>(manager, "thirdPersonEquipStartedAt"), Is.EqualTo(now - .6d).Within(.0001d));
                Assert.That(Get<double>(manager, "thirdPersonEquipCompleteTime"),
                    Is.EqualTo(now - .6d + 2.3333333d).Within(.0001d));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        }

        [Test]
        public void OdinKeepsContinuousFireAndDistinctInspectDurations()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/FPS/Features/Weapons/Content/Grenade/Controllers/CloveGrenade_FP.controller");
            var layer = System.Array.Find(controller.layers, item => item.name == "Odin");
            var fire = System.Array.Find(layer.stateMachine.states, item => item.state.name == "Fire").state;
            var inspect = System.Array.Find(layer.stateMachine.states, item => item.state.name == "Inspect").state;
            Assert.That(fire.speed, Is.Zero);
            Assert.That(((AnimationClip)inspect.motion).length / inspect.speed, Is.EqualTo(7.5f).Within(.0001f));
            var gun = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/FPS/Features/Weapons/Content/Odin/Animation/OdinAnim.controller");
            inspect = System.Array.Find(gun.layers[0].stateMachine.states, item => item.state.name == "Inspect").state;
            Assert.That(((AnimationClip)inspect.motion).length / inspect.speed, Is.EqualTo(7f).Within(.0001f));
        }

        private static WeaponData NewData()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            data.magazineSize = 2;
            data.totalAmmo = 10;
            data.ApplyBakedFireInterval(1f / 6.5f);
            data.ApplyBakedAnimationTimings(1f, 2.5f, 2.5f, 0f, 0f, 0f);
            return data;
        }

        private static WeaponServerState EmptyMagazine(WeaponData data)
        {
            var server = new WeaponServerState();
            server.EnsureInitialized(1, data);
            for (int i = 0; i < data.magazineSize; i++)
                Assert.That(server.TryConsumeFire(data, i * (double)data.FireInterval), Is.True);
            return server;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static T Get<T>(object target, string field) =>
            (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);
        private static void Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);
    }
}
