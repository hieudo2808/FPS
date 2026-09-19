using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class SurvivalRulesTests
    {
        [Test] public void CapacitiesAndAddRulesRejectOverflow()
        {
            Assert.That(SurvivalRules.Capacity(PickupType.FragGrenade), Is.EqualTo(3));
            Assert.That(SurvivalRules.Capacity(PickupType.Medkit), Is.EqualTo(2));
            Assert.That(SurvivalRules.CanAdd(PickupType.FragGrenade, 2, 1), Is.EqualTo(true));
            Assert.That(SurvivalRules.CanAdd(PickupType.FragGrenade, 3, 1), Is.EqualTo(false));
            Assert.That(SurvivalRules.CanAdd(PickupType.Medkit, 1, 2), Is.EqualTo(false));
        }
        [Test] public void RequestSequenceIsMonotonicAndIdempotent()
        {
            Assert.That(SurvivalRules.IsNewer(1, 0), Is.True);
            Assert.That(SurvivalRules.IsNewer(1, 1), Is.False);
            Assert.That(SurvivalRules.IsNewer(0, uint.MaxValue), Is.True);
            Assert.That(SurvivalRules.IsNewer(0, 2), Is.False);
        }
        [Test] public void FragFalloffMatchesCenterAndEdge()
        {
            Assert.That(SurvivalRules.FragDamage(0), Is.EqualTo(100f).Within(.001f));
            Assert.That(SurvivalRules.FragDamage(4), Is.EqualTo(25f).Within(.001f));
            Assert.That(SurvivalRules.FragDamage(8), Is.EqualTo(0f));
        }
        [Test] public void MedicalDurationsSeparateSelfAndAssist()
        {
            Assert.That(SurvivalRules.UseDuration(ConsumableKind.Medkit, true), Is.EqualTo(4f));
            Assert.That(SurvivalRules.UseDuration(ConsumableKind.Medkit, false), Is.EqualTo(2.5f));
            Assert.That(SurvivalRules.UseDuration(ConsumableKind.Antidote, true), Is.EqualTo(5f));
            Assert.That(SurvivalRules.UseDuration(ConsumableKind.Antidote, false), Is.EqualTo(3f));
        }
        [Test] public void ThrowValidationRejectsNanAndBackwardsYaw()
        {
            Assert.That(SurvivalRules.ValidThrowDirection(new Vector3(float.NaN, 0, 1), Vector3.forward), Is.False);
            Assert.That(SurvivalRules.ValidThrowDirection(Vector3.back, Vector3.forward), Is.False);
            Assert.That(SurvivalRules.ValidThrowDirection(Vector3.forward, Vector3.forward), Is.True);
        }
        [Test] public void InfectionTreatmentClampsAndResetsSepsisTimer()
        {
            var go = new GameObject("InfectionTest");
            var infection = go.AddComponent<PlayerInfectionController>();
            try { infection.SetInfectionServer(30f); infection.TreatInfectionServer(40f); Assert.That(infection.CurrentInfection, Is.EqualTo(0f).Within(.001f)); }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
