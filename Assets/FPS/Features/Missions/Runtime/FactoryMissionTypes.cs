using System;

namespace FPS
{
    public enum FactoryMissionState : byte
    {
        Insertion,
        BranchesActive,
        SampleUnlocked,
        SampleSecured,
        ExtractionActive,
        Completed,
        Failed
    }

    public enum FactoryObjectiveId : byte
    {
        UtilitiesBreakerWest,
        UtilitiesBreakerEast,
        UtilitiesGenerator,
        LogisticsManifest,
        LogisticsSecurityOverride,
        ColdStorageSample,
        ExtractionRadio
    }

    [Flags]
    public enum FactoryObjectiveFlags : ushort
    {
        None = 0,
        UtilitiesBreakerWest = 1 << 0,
        UtilitiesBreakerEast = 1 << 1,
        UtilitiesGenerator = 1 << 2,
        LogisticsManifest = 1 << 3,
        LogisticsSecurityOverride = 1 << 4,
        ColdStorageSample = 1 << 5,
        ExtractionRadio = 1 << 6
    }

    public enum FactoryInteractionResult : byte
    {
        Accepted,
        AlreadyCompleted,
        WrongMissionState,
        PrerequisiteMissing,
        InvalidInteractor,
        OutOfRange,
        LineOfSightBlocked,
        UnknownObjective
    }

    /// <summary>
    /// Pure mission rules. The NetworkBehaviour owns replication and validation while this
    /// class keeps the progression deterministic and EditMode-testable.
    /// </summary>
    public sealed class FactoryMissionProgression
    {
        private const FactoryObjectiveFlags UtilitiesMask =
            FactoryObjectiveFlags.UtilitiesBreakerWest
            | FactoryObjectiveFlags.UtilitiesBreakerEast
            | FactoryObjectiveFlags.UtilitiesGenerator;

        private const FactoryObjectiveFlags LogisticsMask =
            FactoryObjectiveFlags.LogisticsManifest
            | FactoryObjectiveFlags.LogisticsSecurityOverride;

        public FactoryMissionState State { get; private set; } = FactoryMissionState.Insertion;
        public FactoryObjectiveFlags CompletedObjectives { get; private set; }
        public bool UtilitiesComplete => (CompletedObjectives & UtilitiesMask) == UtilitiesMask;
        public bool LogisticsComplete => (CompletedObjectives & LogisticsMask) == LogisticsMask;

        public void Reset()
        {
            State = FactoryMissionState.Insertion;
            CompletedObjectives = FactoryObjectiveFlags.None;
        }

        public bool FinishInsertion()
        {
            if (State != FactoryMissionState.Insertion)
                return false;

            State = FactoryMissionState.BranchesActive;
            return true;
        }

        public FactoryInteractionResult TryComplete(FactoryObjectiveId objectiveId)
        {
            FactoryObjectiveFlags flag = ToFlag(objectiveId);
            if (flag == FactoryObjectiveFlags.None)
                return FactoryInteractionResult.UnknownObjective;
            if ((CompletedObjectives & flag) != 0)
                return FactoryInteractionResult.AlreadyCompleted;

            if (!IsAllowedInCurrentState(objectiveId))
                return GetBlockedReason(objectiveId);

            CompletedObjectives |= flag;

            if (State == FactoryMissionState.BranchesActive && UtilitiesComplete && LogisticsComplete)
                State = FactoryMissionState.SampleUnlocked;
            else if (objectiveId == FactoryObjectiveId.ColdStorageSample)
                State = FactoryMissionState.SampleSecured;
            else if (objectiveId == FactoryObjectiveId.ExtractionRadio)
                State = FactoryMissionState.ExtractionActive;

            return FactoryInteractionResult.Accepted;
        }

        public bool CompleteExtraction()
        {
            if (State != FactoryMissionState.ExtractionActive)
                return false;

            State = FactoryMissionState.Completed;
            return true;
        }

        public bool FailMission()
        {
            if (State is FactoryMissionState.Completed or FactoryMissionState.Failed)
                return false;

            State = FactoryMissionState.Failed;
            return true;
        }

        public bool CanInteract(FactoryObjectiveId objectiveId)
        {
            FactoryObjectiveFlags flag = ToFlag(objectiveId);
            return flag != FactoryObjectiveFlags.None
                && (CompletedObjectives & flag) == 0
                && IsAllowedInCurrentState(objectiveId);
        }

        public static FactoryObjectiveFlags ToFlag(FactoryObjectiveId objectiveId)
        {
            return objectiveId switch
            {
                FactoryObjectiveId.UtilitiesBreakerWest => FactoryObjectiveFlags.UtilitiesBreakerWest,
                FactoryObjectiveId.UtilitiesBreakerEast => FactoryObjectiveFlags.UtilitiesBreakerEast,
                FactoryObjectiveId.UtilitiesGenerator => FactoryObjectiveFlags.UtilitiesGenerator,
                FactoryObjectiveId.LogisticsManifest => FactoryObjectiveFlags.LogisticsManifest,
                FactoryObjectiveId.LogisticsSecurityOverride => FactoryObjectiveFlags.LogisticsSecurityOverride,
                FactoryObjectiveId.ColdStorageSample => FactoryObjectiveFlags.ColdStorageSample,
                FactoryObjectiveId.ExtractionRadio => FactoryObjectiveFlags.ExtractionRadio,
                _ => FactoryObjectiveFlags.None
            };
        }

        private bool IsAllowedInCurrentState(FactoryObjectiveId objectiveId)
        {
            return objectiveId switch
            {
                FactoryObjectiveId.UtilitiesBreakerWest
                    or FactoryObjectiveId.UtilitiesBreakerEast
                    or FactoryObjectiveId.UtilitiesGenerator
                    or FactoryObjectiveId.LogisticsManifest
                    or FactoryObjectiveId.LogisticsSecurityOverride
                    => State == FactoryMissionState.BranchesActive,
                FactoryObjectiveId.ColdStorageSample => State == FactoryMissionState.SampleUnlocked,
                FactoryObjectiveId.ExtractionRadio => State == FactoryMissionState.SampleSecured,
                _ => false
            };
        }

        private FactoryInteractionResult GetBlockedReason(FactoryObjectiveId objectiveId)
        {
            if (objectiveId == FactoryObjectiveId.ColdStorageSample
                && State == FactoryMissionState.BranchesActive
                && (!UtilitiesComplete || !LogisticsComplete))
            {
                return FactoryInteractionResult.PrerequisiteMissing;
            }

            if (objectiveId == FactoryObjectiveId.ExtractionRadio
                && (byte)State < (byte)FactoryMissionState.SampleSecured)
            {
                return FactoryInteractionResult.PrerequisiteMissing;
            }

            return FactoryInteractionResult.WrongMissionState;
        }
    }
}
