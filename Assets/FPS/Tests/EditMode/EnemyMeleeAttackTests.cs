using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class EnemyMeleeAttackTests
    {
        private GameObject owner;
        private GameObject target;

        [TearDown]
        public void TearDown()
        {
            if (owner != null)
                Object.DestroyImmediate(owner);

            if (target != null)
                Object.DestroyImmediate(target);
        }

        [Test]
        public void ConsumeImpact_DoesNotEndCommittedAttackActionWindow()
        {
            EnemyMeleeAttack attack = CreateAttack();
            owner.transform.position = Vector3.zero;
            target.transform.position = Vector3.forward * 1.5f;

            Assert.IsTrue(attack.TryBegin(owner.transform, target.transform, 0f));
            Assert.IsTrue(attack.TryConsumeImpact(owner.transform, target.transform, 0.2f, true, out Transform hitTarget, out float damage));

            Assert.AreSame(target.transform, hitTarget);
            Assert.AreEqual(25f, damage, 0.001f);
            Assert.IsTrue(attack.IsActionLocked(0.2f),
                "Consuming the impact should not release movement before the attack clip/action window finishes.");
        }

        [Test]
        public void LockedFacing_RemainsAtCommittedAttackDirection()
        {
            EnemyMeleeAttack attack = CreateAttack();
            owner.transform.position = Vector3.zero;
            owner.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            target.transform.position = Vector3.forward * 1.5f;

            Assert.IsTrue(attack.TryBegin(owner.transform, target.transform, 0f));
            target.transform.position = new Vector3(6f, 0f, 1.5f);

            Assert.IsTrue(attack.TryGetLockedFacing(0.1f, out Vector3 facing));
            Assert.Greater(Vector3.Dot(facing, Vector3.forward), 0.95f);
        }

        [Test]
        public void ConsumeImpact_RejectsTargetThatEscapedRange()
        {
            EnemyMeleeAttack attack = CreateAttack();
            owner.transform.position = Vector3.zero;
            target.transform.position = Vector3.forward * 1.5f;

            Assert.IsTrue(attack.TryBegin(owner.transform, target.transform, 0f));
            target.transform.position = Vector3.forward * 6f;

            Assert.IsFalse(attack.TryConsumeImpact(owner.transform, target.transform, 0.2f, true, out Transform hitTarget, out float damage));
            Assert.IsNull(hitTarget);
            Assert.AreEqual(0f, damage, 0.001f);
        }

        private EnemyMeleeAttack CreateAttack()
        {
            owner = new GameObject("AttackOwner");
            target = new GameObject("AttackTarget");

            var attack = new EnemyMeleeAttack();
            attack.Configure(
                range: 2.5f,
                damage: 25f,
                cooldown: 1.5f,
                impactDelay: 0.1f,
                minimumImpactDelay: 0.1f,
                actionLockDuration: 0.9f,
                hitArcDegrees: 130f);

            return attack;
        }
    }
}
