using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class TankStaggerTests
    {
        private GameObject gameObject;
        private SI_Tank tank;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Tank");
            tank = gameObject.AddComponent<SI_Tank>();
        }

        [TearDown]
        public void TearDown()
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Stagger_NotTriggered_BelowThreshold()
        {
            tank.RecordDamage(374f, 10f);
            bool triggered = tank.CheckAndTriggerStagger(10f);

            Assert.IsFalse(triggered);
            Assert.IsFalse(tank.IsStaggered);
        }

        [Test]
        public void Stagger_Triggered_AtThreshold()
        {
            tank.RecordDamage(375f, 10f);
            bool triggered = tank.CheckAndTriggerStagger(10f);

            Assert.IsTrue(triggered);
            Assert.IsTrue(tank.IsStaggered);
        }

        [Test]
        public void Stagger_DamageDuringStagger_DoesNotAccumulate()
        {
            tank.RecordDamage(375f, 10f);
            Assert.IsTrue(tank.CheckAndTriggerStagger(10f));

            tank.RecordDamage(1000f, 11f);

            Assert.AreEqual(0f, tank.AccumulatedDamage);
        }

        [Test]
        public void Stagger_ImmunityBlocksDamageForFiveSecondsAfterRecovery()
        {
            tank.RecordDamage(375f, 10f);
            Assert.IsTrue(tank.CheckAndTriggerStagger(10f));

            typeof(SI_Tank).GetField("isStaggered", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(tank, false);
            tank.RecordDamage(375f, 16f);

            Assert.AreEqual(0f, tank.AccumulatedDamage,
                "Damage before stagger duration + five-second immunity expires must be ignored.");
            tank.RecordDamage(100f, 16.25f);
            Assert.Greater(tank.AccumulatedDamage, 0f);
        }

        [Test]
        public void Stagger_DamageWindow_Resets()
        {
            // First hit at t=10s
            tank.RecordDamage(100f, 10f);
            // Second hit at t=15s (beyond 3s window)
            tank.RecordDamage(100f, 15f);

            bool triggered = tank.CheckAndTriggerStagger(15f);

            Assert.IsFalse(triggered, "Damage outside stagger window should expire and not trigger stagger.");
            Assert.IsFalse(tank.IsStaggered);
        }

        [Test]
        public void Stagger_Duration_MatchesConfig()
        {
            Assert.GreaterOrEqual(tank.StaggerDuration, 1.0f);
            Assert.LessOrEqual(tank.StaggerDuration, 1.5f);
        }

        [Test]
        public void Stagger_BlocksAbilityUse()
        {
            tank.TriggerStagger();
            Assert.IsTrue(tank.IsStaggered);

            var canUseMethod = typeof(SI_Tank).GetMethod("CanUseAbility", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(canUseMethod);

            bool canUse = (bool)canUseMethod.Invoke(tank, null);
            Assert.IsFalse(canUse, "Tank should not be able to use ability while staggered.");
        }
    }
}
