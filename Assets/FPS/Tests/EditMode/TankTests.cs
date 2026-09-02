using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class TankTests
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
        public void Tank_HasCorrectSpecialType()
        {
            Assert.AreEqual(SpecialType.Tank, tank.Type);
        }

        [Test]
        public void Tank_UsesExactPerPlayerHealthInsteadOfGenericMultiplier()
        {
            var field = typeof(SpecialInfectedBase).GetField("specialHPMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            float multiplier = (float)field.GetValue(tank);
            Assert.AreEqual(1f, multiplier);
            Assert.AreEqual(2500f, tank.HealthPerPlayer);
        }

        [TestCase(0, 2500f)]
        [TestCase(1, 2500f)]
        [TestCase(2, 5000f)]
        [TestCase(3, 7500f)]
        [TestCase(4, 10000f)]
        [TestCase(5, 10000f)]
        public void Tank_MaxHealth_ClampsAndScalesExactly(int players, float expected)
        {
            Assert.AreEqual(expected, SI_Tank.CalculateTankMaxHealth(players));
        }

        [Test]
        public void Tank_AllowedInSoloMode()
        {
            Assert.IsTrue(tank.AllowedInSoloMode);
        }

        [Test]
        public void Tank_ShouldSpawn_FalseWhenNoDirector()
        {
            Assert.IsFalse(tank.ShouldSpawn(null));
        }

        [Test]
        public void Tank_DoesNotHaveEmptyUpdateOverride()
        {
            var method = typeof(SI_Tank).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Null(method, "SI_Tank should use the server-authoritative base update loop.");
        }

        [Test]
        public void Tank_HeavySwing_HasWindup()
        {
            Assert.Greater(tank.HeavySwingWindup, 0f);
            Assert.Greater(tank.HeavySwingDamage, 0f);
            Assert.Greater(tank.HeavySwingKnockbackForce, 0f);
        }

        [Test]
        public void Tank_Slam_HasAoERadius()
        {
            Assert.Greater(tank.SlamRadius, 0f);
            Assert.Greater(tank.SlamDamage, 0f);
            Assert.Greater(tank.SlamKnockbackForce, 0f);
            Assert.Greater(tank.SlamCooldown, 0f);
        }

        [Test]
        public void Tank_Slam_DamageLessThanHeavySwing()
        {
            Assert.Less(tank.SlamDamage, tank.HeavySwingDamage);
        }

        [Test]
        public void Tank_StaggerThreshold_IsPositive()
        {
            Assert.AreEqual(375f, tank.StaggerDamageThreshold);
            Assert.Greater(tank.StaggerWindow, 0f);
        }

        [Test]
        public void Tank_StaggerDuration_InRange()
        {
            Assert.GreaterOrEqual(tank.StaggerDuration, 1.0f);
            Assert.LessOrEqual(tank.StaggerDuration, 1.5f);
        }

        [Test]
        public void Tank_TeamHealthAverage_CountsDownedAsZero_AndAllowsBoundary()
        {
            var team = new List<PlayerTeamHealthSnapshot>
            {
                new PlayerTeamHealthSnapshot(100f, 100f, false),
                new PlayerTeamHealthSnapshot(0f, 100f, true),
                new PlayerTeamHealthSnapshot(0f, 100f, true),
                new PlayerTeamHealthSnapshot(0f, 100f, true)
            };

            Assert.IsTrue(SI_Tank.CalculateAverageTeamHealth(team, out float average));
            Assert.AreEqual(0.25f, average);
        }

        [Test]
        public void Tank_TeamHealthAverage_FailsClosedWithoutResolvableTeam()
        {
            Assert.IsFalse(SI_Tank.CalculateAverageTeamHealth(null, out _));
            Assert.IsFalse(SI_Tank.CalculateAverageTeamHealth(
                new[] { new PlayerTeamHealthSnapshot(25f, 0f, false) }, out _));
        }
    }
}
