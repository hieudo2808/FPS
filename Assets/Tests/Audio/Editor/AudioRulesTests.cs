using System.Collections.Generic;
using NUnit.Framework;

namespace FPS.Tests
{
    public sealed class AudioRulesTests
    {
        [Test]
        public void PickIndexNeverRepeatsAndReachesEveryTake()
        {
            for (int count = 2; count <= 6; count++)
            for (int previous = -1; previous < count; previous++)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i <= 100; i++)
                {
                    int pick = AudioRules.PickIndex(count, previous, i / 100f);
                    Assert.That(pick, Is.InRange(0, count - 1));
                    if (previous >= 0) Assert.That(pick, Is.Not.EqualTo(previous), $"count {count} previous {previous}");
                    seen.Add(pick);
                }
                Assert.That(seen.Count, Is.EqualTo(previous >= 0 ? count - 1 : count), $"count {count} previous {previous}");
            }
            Assert.That(AudioRules.PickIndex(1, 0, 0.7f), Is.EqualTo(0));
            Assert.That(AudioRules.PickIndex(0, -1, 0.7f), Is.EqualTo(-1));
        }

        [Test]
        public void DirectorPhaseMapsToMusicAndTankOverridesEverything()
        {
            Assert.That(AudioRules.CueFor(DirectorPhase.Calm, false), Is.EqualTo(MusicCue.Calm));
            Assert.That(AudioRules.CueFor(DirectorPhase.Relax, false), Is.EqualTo(MusicCue.Calm));
            Assert.That(AudioRules.CueFor(DirectorPhase.BuildUp, false), Is.EqualTo(MusicCue.Build));
            Assert.That(AudioRules.CueFor(DirectorPhase.Combat, false), Is.EqualTo(MusicCue.Combat));
            Assert.That(AudioRules.CueFor(DirectorPhase.Peak, false), Is.EqualTo(MusicCue.Combat));
            foreach (DirectorPhase phase in System.Enum.GetValues(typeof(DirectorPhase)))
                Assert.That(AudioRules.CueFor(phase, true), Is.EqualTo(MusicCue.Tank));
        }

        [Test]
        public void MusicRisesAtOnceAndStepsDownOnlyAfterTheHold()
        {
            Assert.That(AudioRules.Settle(MusicCue.Calm, MusicCue.Combat, 0f, 8f), Is.EqualTo(MusicCue.Combat));
            Assert.That(AudioRules.Settle(MusicCue.Combat, MusicCue.Tank, 0f, 8f), Is.EqualTo(MusicCue.Tank));
            Assert.That(AudioRules.Settle(MusicCue.Combat, MusicCue.Calm, 7.9f, 8f), Is.EqualTo(MusicCue.Combat));
            Assert.That(AudioRules.Settle(MusicCue.Combat, MusicCue.Calm, 8f, 8f), Is.EqualTo(MusicCue.Calm));
            Assert.That(AudioRules.Settle(MusicCue.None, MusicCue.Calm, 0f, 8f), Is.EqualTo(MusicCue.Calm));
            Assert.That(AudioRules.Settle(MusicCue.Tank, MusicCue.None, 0f, 8f), Is.EqualTo(MusicCue.None), "Leaving gameplay stops at once");
        }

        [Test]
        public void ReloadCuesFollowTheTimeline()
        {
            AudioRules.MagazineReloadCues(0.9f, 1.75f, 1f, out float start, out float insert, out float end);
            Assert.That(start, Is.LessThan(insert));
            Assert.That(insert, Is.EqualTo(0.78f).Within(1e-4f), "Magazine in just before the server commits the ammo");
            Assert.That(end, Is.GreaterThan(insert).And.LessThan(1.75f));
            AudioRules.MagazineReloadCues(0.9f, 1.75f, 1.5f, out _, out float slowInsert, out _);
            Assert.That(slowInsert, Is.EqualTo(insert * 1.5f).Within(1e-4f), "An infected (slower) reload stretches the cues");
            AudioRules.MagazineReloadCues(2.5f, 2.5f, 1f, out _, out float lateCommit, out _);
            Assert.That(lateCommit, Is.EqualTo(1.75f).Within(1e-4f), "Ammo committed at the very end: the magazine still goes in at 70%");

            float previous = -1f;
            for (int k = 0; k < 5; k++)
            {
                float shell = AudioRules.ShellInsertCue(0.2167f, 0.4667f, k, 1f);
                Assert.That(shell, Is.GreaterThan(previous));
                Assert.That(shell, Is.LessThan(0.2167f + (k + 1) * 0.4667f), "Each shell sounds before the round is counted");
                previous = shell;
            }
            Assert.That(AudioRules.ShellReloadEndCue(0.2167f, 0.4667f, 1.0667f, 5, 1f), Is.GreaterThan(previous));
        }

        [Test]
        public void FootstepsAndSlider()
        {
            Assert.That(AudioRules.StrideLength(1f), Is.EqualTo(1.5f));
            Assert.That(AudioRules.StrideLength(8f), Is.EqualTo(2.4f));
            Assert.That(AudioRules.FootstepVolume(6f), Is.EqualTo(1f));
            Assert.That(AudioRules.FootstepVolume(1f), Is.LessThan(0.5f));
            Assert.That(AudioRules.SliderToDb(0f), Is.EqualTo(-80f));
            Assert.That(AudioRules.SliderToDb(1f), Is.EqualTo(0f));
            Assert.That(AudioRules.SliderToDb(0.5f), Is.EqualTo(-12.04f).Within(0.01f));
        }
    }
}
