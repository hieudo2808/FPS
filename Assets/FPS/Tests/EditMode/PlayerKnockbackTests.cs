using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class PlayerKnockbackTests
    {
        private GameObject playerGo;
        private PlayerMovement movement;
        private CharacterController controller;

        [SetUp]
        public void SetUp()
        {
            playerGo = new GameObject("Player");
            controller = playerGo.AddComponent<CharacterController>();
            movement = playerGo.AddComponent<PlayerMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (playerGo != null)
            {
                Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void Knockback_ZeroWhenNoForce()
        {
            Assert.AreEqual(Vector3.zero, movement.ExternalVelocity);
        }

        [Test]
        public void Knockback_AppliesExternalVelocity()
        {
            Vector3 force = new Vector3(8f, 2f, 0f);
            movement.ApplyKnockback(force);

            Assert.AreEqual(force, movement.ExternalVelocity);
        }

        [Test]
        public void Knockback_DecaysOverTime()
        {
            Vector3 force = new Vector3(10f, 0f, 0f);
            movement.ApplyKnockback(force);

            var method = typeof(PlayerMovement).GetMethod("SimulateTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            PlayerInputPayload input = new PlayerInputPayload { move = Vector2.zero };
            method.Invoke(movement, new object[] { input, 0.1f });

            Assert.Less(movement.ExternalVelocity.magnitude, force.magnitude, "External velocity should decay after tick simulation.");
        }

        [Test]
        public void Knockback_DecaysToZeroEventually()
        {
            Vector3 force = new Vector3(5f, 0f, 0f);
            movement.ApplyKnockback(force);

            var method = typeof(PlayerMovement).GetMethod("SimulateTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            PlayerInputPayload input = new PlayerInputPayload { move = Vector2.zero };
            // Simulate 20 ticks (1 second)
            for (int i = 0; i < 20; i++)
            {
                method.Invoke(movement, new object[] { input, 0.05f });
            }

            Assert.AreEqual(Vector3.zero, movement.ExternalVelocity, "External velocity should decay to zero within ~1s.");
        }

        [Test]
        public void Knockback_RejectsNonFiniteForce_AndClampsMagnitude()
        {
            Assert.IsFalse(movement.TryApplyServerKnockback(new Vector3(float.NaN, 0f, 0f)));
            Assert.AreEqual(Vector3.zero, movement.ExternalVelocity);

            Assert.IsTrue(movement.TryApplyServerKnockback(Vector3.right * 100f));
            Assert.AreEqual(20f, movement.ExternalVelocity.magnitude, 0.001f);
        }
    }
}
