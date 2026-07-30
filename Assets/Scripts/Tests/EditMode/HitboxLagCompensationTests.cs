using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class HitboxLagCompensationTests
    {
        private readonly List<Object> objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in objectsToDestroy)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void HitboxSegment_UsesDefaultMultiplierForZone()
        {
            var go = new GameObject("HeadHitbox");
            objectsToDestroy.Add(go);
            var segment = go.AddComponent<HitboxSegment>();

            typeof(HitboxSegment)
                .GetField("zone", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(segment, HitboxZone.Head);

            Assert.AreEqual(2f, segment.DamageMultiplier);
            Assert.True(segment.IsHeadshot);
        }

        [Test]
        public void LagCompensation_ClampsOldShotTimes()
        {
            double now = 10.0;
            double rewind = LagCompensationManager.ResolveRewindTime(now, 9.0);

            Assert.AreEqual(now - NetworkGameplayPolicy.MaxRewindSeconds, rewind, 0.0001);
            Assert.AreEqual(now, LagCompensationManager.ResolveRewindTime(now, 11.0), 0.0001);
        }

        [Test]
        public void LagCompensation_RaycastsAgainstHistoricalHitboxBounds()
        {
            var targetGo = new GameObject("LagTarget");
            objectsToDestroy.Add(targetGo);
            var receiver = targetGo.AddComponent<TestDamageReceiver>();
            var segment = targetGo.AddComponent<HitboxSegment>();
            var collider = targetGo.AddComponent<BoxCollider>();
            var target = targetGo.AddComponent<LagCompensatedTarget>();

            typeof(HitboxSegment)
                .GetField("zone", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(segment, HitboxZone.Chest);
            Assert.NotNull(receiver);
            Assert.NotNull(collider);

            targetGo.transform.position = new Vector3(0f, 0f, 4f);
            Physics.SyncTransforms();
            target.RefreshSegments();
            target.SampleForTests(1.0);

            targetGo.transform.position = new Vector3(6f, 0f, 4f);
            Physics.SyncTransforms();
            target.SampleForTests(2.0);

            bool hit = LagCompensationManager.TryRaycast(
                Vector3.zero,
                Vector3.forward,
                20f,
                Physics.DefaultRaycastLayers,
                1.0,
                20f,
                out LagCompensatedHit lagHit);

            Assert.True(hit);
            Assert.AreEqual(HitboxZone.Chest, lagHit.zone);
            Assert.AreEqual(receiver, lagHit.damageTarget);
        }

        private sealed class TestDamageReceiver : MonoBehaviour, IDamageable
        {
            public bool IsDead => false;
            public void TakeDamage(float amount) { }
        }
    }
}
