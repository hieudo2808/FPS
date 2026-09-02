using NUnit.Framework;

namespace FPS.Tests
{
    public class FactoryMissionProgressionTests
    {
        [Test]
        public void UtilitiesThenLogistics_UnlocksSampleExactlyOnce()
        {
            FactoryMissionProgression mission = NewActiveMission();

            CompleteUtilities(mission);
            Assert.AreEqual(FactoryMissionState.BranchesActive, mission.State);

            CompleteLogistics(mission);
            Assert.AreEqual(FactoryMissionState.SampleUnlocked, mission.State);
            Assert.AreEqual(
                FactoryInteractionResult.AlreadyCompleted,
                mission.TryComplete(FactoryObjectiveId.LogisticsManifest));
            Assert.AreEqual(FactoryMissionState.SampleUnlocked, mission.State);
        }

        [Test]
        public void LogisticsThenUtilities_UnlocksSampleExactlyOnce()
        {
            FactoryMissionProgression mission = NewActiveMission();

            CompleteLogistics(mission);
            Assert.AreEqual(FactoryMissionState.BranchesActive, mission.State);

            CompleteUtilities(mission);
            Assert.AreEqual(FactoryMissionState.SampleUnlocked, mission.State);
            Assert.AreEqual(
                FactoryInteractionResult.AlreadyCompleted,
                mission.TryComplete(FactoryObjectiveId.UtilitiesGenerator));
        }

        [Test]
        public void SampleAndExtraction_AreStrictlyOrdered()
        {
            FactoryMissionProgression mission = NewActiveMission();
            Assert.AreEqual(
                FactoryInteractionResult.PrerequisiteMissing,
                mission.TryComplete(FactoryObjectiveId.ColdStorageSample));

            CompleteUtilities(mission);
            CompleteLogistics(mission);
            Assert.AreEqual(
                FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.ColdStorageSample));
            Assert.AreEqual(FactoryMissionState.SampleSecured, mission.State);
            Assert.AreEqual(
                FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.ExtractionRadio));
            Assert.AreEqual(FactoryMissionState.ExtractionActive, mission.State);
            Assert.True(mission.CompleteExtraction());
            Assert.AreEqual(FactoryMissionState.Completed, mission.State);
        }

        private static FactoryMissionProgression NewActiveMission()
        {
            FactoryMissionProgression mission = new();
            Assert.True(mission.FinishInsertion());
            return mission;
        }

        private static void CompleteUtilities(FactoryMissionProgression mission)
        {
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.UtilitiesBreakerWest));
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.UtilitiesBreakerEast));
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.UtilitiesGenerator));
        }

        private static void CompleteLogistics(FactoryMissionProgression mission)
        {
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.LogisticsManifest));
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.TryComplete(FactoryObjectiveId.LogisticsSecurityOverride));
        }
    }
}
