using NUnit.Framework;

namespace FPS.Tests
{
    public sealed class AdaptivePacingTests
    {
        static DirectorInput Healthy => new DirectorInput(1, 0, 0, 999, 0, 0);
        static DirectorStateMachine Combat()
        {
            var fsm = new DirectorStateMachine(DirectorPolicy.Default);
            fsm.Advance(3, Healthy);
            fsm.Advance(15, Healthy);
            Assert.That(fsm.Phase, Is.EqualTo(DirectorPhase.Combat));
            return fsm;
        }

        [Test]
        public void HighPressure_InCombatEndsEscalation_InsteadOfStartingPeak()
        {
            var fsm = Combat();
            var result = fsm.Advance(0, new DirectorInput(.5f, 0, 0, 999, 4, 80));
            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.Relax), "Intensity is a safety signal, not a request for more pressure.");
            Assert.That(result.EnteredRelax, Is.True);
        }

        [Test]
        public void HighPressure_InCalmAlsoProtects()
        {
            var fsm = new DirectorStateMachine(DirectorPolicy.Default);
            Assert.That(fsm.Advance(0, new DirectorInput(.5f, 0, 0, 999, 4, 80)).Phase,
                Is.EqualTo(DirectorPhase.Relax));
        }

        [Test]
        public void LowHealthWithoutThreatOrRecentDamage_DoesNotLockRelax()
        {
            var fsm = Combat();
            Assert.That(fsm.Advance(0, new DirectorInput(.1f, 0, 0, 999, 0, 27)).Phase,
                Is.EqualTo(DirectorPhase.Combat));
        }

        [Test]
        public void Relax_DoesNotExitWhileSomeoneIsStillInDownGrace()
        {
            var fsm = Combat();
            fsm.Advance(0, new DirectorInput(.5f, 0, 0, 0, 0, 0));
            Assert.That(fsm.Phase, Is.EqualTo(DirectorPhase.Relax));
            var result = fsm.Advance(21, new DirectorInput(.5f, 0, 0, 1, 0, 0));
            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(result.EnteredRelax, Is.False, "Holding Relax cannot issue another evaluation.");
        }

        [Test]
        public void SpecialGate_OpensAtFiveSecondsInPeak_AndClosesOnExit()
        {
            var fsm = Combat();
            fsm.Advance(60, Healthy);
            Assert.That(fsm.Phase, Is.EqualTo(DirectorPhase.Peak));
            Assert.That(fsm.Advance(4.99f, Healthy).Decision.SpecialGateOpen, Is.False);
            Assert.That(fsm.Advance(.01f, Healthy).Decision.SpecialGateOpen, Is.True);
            Assert.That(fsm.Advance(10, Healthy).Decision.SpecialGateOpen, Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void EndFromEveryPhase_IsIdempotentAndCannotAdvanceUntilReset(int phase)
        {
            var fsm = new DirectorStateMachine(DirectorPolicy.Default);
            if (phase >= 1) fsm.Advance(3, Healthy);
            if (phase >= 2) fsm.Advance(15, Healthy);
            if (phase >= 3) fsm.Advance(60, Healthy);
            if (phase >= 4) fsm.Advance(15, Healthy);
            fsm.End(); fsm.End();
            var stopped = fsm.Advance(999, Healthy);
            Assert.That(stopped.Decision.SpawnRateMultiplier, Is.Zero);
            Assert.That(stopped.Decision.SpecialGateOpen, Is.False);
            Assert.That(stopped.EnteredRelax, Is.False);
            fsm.Reset();
            Assert.That(fsm.Advance(0, Healthy).Phase, Is.EqualTo(DirectorPhase.Calm));
        }

        [Test]
        public void SuspendedAndNoActors_FreezePacingAndCloseGates()
        {
            var fsm = Combat();
            fsm.Advance(12, Healthy, DirectorRunState.Suspended);
            Assert.That(fsm.PhaseElapsedSeconds, Is.Zero);
            var none = new DirectorInput(0, 0, 0, 999, 0, 0, participantCount: 0);
            Assert.That(fsm.Advance(60, none).Decision.SpawnRateMultiplier, Is.Zero);
            Assert.That(fsm.PhaseElapsedSeconds, Is.Zero);
        }

        [Test]
        public void CrescendoIsDeferredThroughMandatoryRelax_AndConsumedOnce()
        {
            var fsm = Combat();
            fsm.Advance(0, new DirectorInput(.5f, 0, 0, 999, 4, 80), serverTime: 18);
            fsm.RequestCrescendo(100);
            Assert.That(fsm.Advance(19, Healthy, serverTime: 37).Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(fsm.Advance(1, Healthy, serverTime: 38).Phase, Is.EqualTo(DirectorPhase.Peak));
            fsm.RequestCrescendo(100);
            fsm.Advance(14, Healthy, serverTime: 52);
            Assert.That(fsm.Advance(1, Healthy, serverTime: 53).Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(fsm.Advance(20, Healthy, serverTime: 73).Phase, Is.EqualTo(DirectorPhase.Calm));
        }

        [Test]
        public void RelaxExitRequiresThreeContinuousSafeSeconds()
        {
            var fsm = Combat();
            fsm.Advance(0, new DirectorInput(.5f, 0, 0, 999, 4, 80));
            fsm.Advance(20, new DirectorInput(1, 0, 0, 999, 0, 40));
            Assert.That(fsm.Advance(2.99f, Healthy).Phase, Is.EqualTo(DirectorPhase.Relax));
            fsm.Advance(.1f, new DirectorInput(1, 0, 0, 999, 0, 0, recoverySafe: false));
            Assert.That(fsm.Advance(2.99f, Healthy).Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(fsm.Advance(.02f, Healthy).Phase, Is.EqualTo(DirectorPhase.Calm));
        }
    }
}
