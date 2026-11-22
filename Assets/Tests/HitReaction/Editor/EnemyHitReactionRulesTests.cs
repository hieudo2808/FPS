using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class EnemyHitReactionRulesTests
    {
        const float Max = 100f;

        static EnemyHitReactionKind Resolve(HitboxZone zone, float damage, bool headshot = false, bool windup = false,
            bool allowed = true, bool alive = true, float since = -1f) =>
            EnemyHitReactionRules.Resolve(zone, headshot, damage, Max, alive, allowed, windup, since);

        [Test] public void LightBodyHitOnlyFlinches() => Assert.That(Resolve(HitboxZone.Chest, 10f), Is.EqualTo(EnemyHitReactionKind.Flinch));
        [Test] public void AnyLegHitStumbles() => Assert.That(Resolve(HitboxZone.Leg, 5f), Is.EqualTo(EnemyHitReactionKind.Stumble));
        [Test] public void HeavyHitStumblesWhenNotAttacking() => Assert.That(Resolve(HitboxZone.Body, 25f), Is.EqualTo(EnemyHitReactionKind.Stumble));
        [Test] public void HeavyHitBreaksAWindup() => Assert.That(Resolve(HitboxZone.Body, 25f, windup: true), Is.EqualTo(EnemyHitReactionKind.Interrupt));
        [Test] public void LightLegHitDoesNotBreakAWindup() => Assert.That(Resolve(HitboxZone.Leg, 10f, windup: true), Is.EqualTo(EnemyHitReactionKind.Flinch));
        [Test] public void HeadshotOfFifteenPercentBreaksAWindup() => Assert.That(Resolve(HitboxZone.Head, 15f, headshot: true, windup: true), Is.EqualTo(EnemyHitReactionKind.Interrupt));
        [Test] public void LightHeadshotOnlyFlinchesAWindup() => Assert.That(Resolve(HitboxZone.Head, 10f, headshot: true, windup: true), Is.EqualTo(EnemyHitReactionKind.Flinch));

        [Test]
        public void GameplayReactionsHaveACooldownSoAutomaticFireCannotLockAnEnemy()
        {
            Assert.That(Resolve(HitboxZone.Leg, 50f, since: 0.2f), Is.EqualTo(EnemyHitReactionKind.Flinch));
            Assert.That(Resolve(HitboxZone.Body, 50f, windup: true, since: 1.4f), Is.EqualTo(EnemyHitReactionKind.Flinch));
            Assert.That(Resolve(HitboxZone.Leg, 5f, since: EnemyHitReactionRules.GameplayCooldown), Is.EqualTo(EnemyHitReactionKind.Stumble));
        }

        [Test]
        public void EnemiesWithTheirOwnBrainOnlyFlinch()
        {
            Assert.That(Resolve(HitboxZone.Leg, 80f, allowed: false), Is.EqualTo(EnemyHitReactionKind.Flinch));
            Assert.That(Resolve(HitboxZone.Body, 80f, windup: true, allowed: false), Is.EqualTo(EnemyHitReactionKind.Flinch));
        }

        [Test]
        public void DeadOrUnhurtEnemiesDoNotReact()
        {
            Assert.That(Resolve(HitboxZone.Body, 50f, alive: false), Is.EqualTo(EnemyHitReactionKind.None));
            Assert.That(Resolve(HitboxZone.Body, 0f), Is.EqualTo(EnemyHitReactionKind.None));
        }

        [Test]
        public void OnlyBodyImpactsReact()
        {
            Assert.That(EnemyHitReactionRules.Reacts(DamageType.Bullet), Is.True);
            Assert.That(EnemyHitReactionRules.Reacts(DamageType.Explosion), Is.True);
            Assert.That(EnemyHitReactionRules.Reacts(DamageType.Melee), Is.False);
            Assert.That(EnemyHitReactionRules.Reacts(DamageType.Environment), Is.False, "Fire damage over time must not flinch every tick");
            Assert.That(EnemyHitReactionRules.Reacts(DamageType.Unspecified), Is.False);
        }

        [Test]
        public void StrengthReachesOneAtTheHeavyThreshold()
        {
            Assert.That(EnemyHitReactionRules.Strength(12.5f, Max), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(EnemyHitReactionRules.Strength(25f, Max), Is.EqualTo(1f));
            Assert.That(EnemyHitReactionRules.Strength(400f, Max), Is.EqualTo(1f));
            Assert.That(EnemyHitReactionRules.Strength(0f, Max), Is.EqualTo(0f));
        }

        [Test]
        public void DirectionAndOffsetSurviveQuantization()
        {
            for (int degrees = 0; degrees < 360; degrees += 7)
            {
                Vector3 direction = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                Vector3 back = EnemyHitReactionRules.DequantizeYaw(EnemyHitReactionRules.QuantizeYaw(direction));
                Assert.That(Vector3.Angle(direction, back), Is.LessThan(1.5f), $"yaw {degrees}");
            }
            Assert.That(EnemyHitReactionRules.QuantizeCentimetres(0.123f), Is.EqualTo((sbyte)12));
            Assert.That(EnemyHitReactionRules.QuantizeCentimetres(-3f), Is.EqualTo((sbyte)-127));
        }

        [Test]
        public void FlinchRisesThenEasesBackToRest()
        {
            Assert.That(EnemyHitReactionPresenter.Envelope(0f, 0.05f, 0.3f), Is.EqualTo(0f));
            Assert.That(EnemyHitReactionPresenter.Envelope(0.05f, 0.05f, 0.3f), Is.EqualTo(1f).Within(1e-4f));
            float mid = EnemyHitReactionPresenter.Envelope(0.2f, 0.05f, 0.3f);
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(EnemyHitReactionPresenter.Envelope(0.35f, 0.05f, 0.3f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(EnemyHitReactionPresenter.Envelope(0.4f, 0.05f, 0.3f), Is.EqualTo(0f));
        }

        [Test]
        public void ReplicatedStateCarriesTheHit()
        {
            var state = new EnemyReplicatedState
            {
                actionSequence = 7,
                hitSequence = 200,
                hitKind = EnemyHitReactionKind.Interrupt,
                hitSegment = 3,
                hitOffsetX = -12, hitOffsetY = 40, hitOffsetZ = 127,
                hitYaw = 191,
                hitStrength = 255
            };
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteNetworkSerializable(state);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out EnemyReplicatedState back);
            Assert.That(back, Is.EqualTo(state));
            var other = state;
            other.hitSequence++;
            Assert.That(other.Equals(state), Is.False, "A new hit must dirty the NetworkVariable");
        }
    }
}
