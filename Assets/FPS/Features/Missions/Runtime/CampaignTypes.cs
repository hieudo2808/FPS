using System;
using UnityEngine;

namespace FPS
{
    public enum CampaignChapter : byte { Factory, Asylum, Laboratory }
    public enum CampaignPhase : byte { Insertion, Exploring, Preparing, Encounter, AwaitingParty, Transitioning, Completed, Failed }
    public enum CampaignTankStage : byte { Dormant, Pending, Active, Resolved }
    public enum CampaignObjectiveId : byte
    {
        None, FactoryIsolate, FactoryBackup, FactoryGenerator, FactoryManifest, FactoryShipping,
        FactoryCase, FactoryRoute, AsylumAccess, AsylumFuse, AsylumInstall, AsylumPower,
        AsylumPatient, AsylumTransfer, AsylumLift, LabTrace, LabIndex, LabPower,
        LabArchive, LabCase, LabTransmit
    }
    public enum CampaignResult : byte { Accepted, AlreadyDone, WrongChapter, WrongPhase, Prerequisite, WrongAnswer, InvalidActor, Busy, OutOfRange, Obstructed, Interrupted, AmmoUnavailable }

    /// <summary>Stable IDs for the shared campaign file journal. Keep the catalog below 64 entries.</summary>
    public enum CampaignFileId : byte
    {
        None,
        FactoryProcedure,
        FactoryManifest,
        FactoryB2Transfer,
        AsylumDeskLog,
        AsylumPatientRecord,
        AsylumMortuaryTransfer,
        LabIndex,
        LabPowerRecord,
        LabOriginalArchive,
        OptionalFactory01,
        OptionalFactory02,
        OptionalFactory03,
        OptionalAsylum01,
        OptionalAsylum02,
        OptionalAsylum03,
        OptionalAsylum04,
        OptionalLab01,
        OptionalLab02,
        OptionalLab03,
        OptionalLab04,
        OptionalLab05
    }

    public enum CampaignKeyItemId : byte
    {
        None,
        FactoryEvidenceCase,
        ServiceFuse,
        B2AccessCard,
        LabEvidenceCase
    }

    public enum CampaignDialogueId : byte
    {
        None,
        InsertionBriefing,
        FactoryChapter,
        AsylumChapter,
        LaboratoryChapter,
        FactoryGenerator,
        FactoryShipping,
        FactoryCase,
        AsylumAccess,
        AsylumPower,
        AsylumPatient,
        AsylumTransfer,
        LabPower,
        LabArchive,
        LabCase,
        FactoryEncounter,
        AsylumEncounter,
        LabEncounter,
        Boarding,
        AsylumTransition,
        Completed,
        EvidenceTransmitted,
        ClientSuppressionOrder,
        MiraRejectsOrder,
        OperatorDowned,
        TankWarning,
        ScreamerWarning,
        InfectorWarning,
        Revived
    }

    [Serializable]
    public sealed class CampaignState
    {
        public int version = 1;
        public uint revision;
        public CampaignChapter chapter;
        public CampaignPhase phase = CampaignPhase.Insertion;
        public ulong completed;
        public ulong discoveredFiles;
        public ushort teamDownedCount;
        public byte powerSelection = 3;
        public int checkpoint;
        public int supplyPartySize = 1;
        public bool evidenceTransmitted;
        public bool factoryTankDefeated;
        public CampaignTankStage tankStage;

        public bool Has(CampaignObjectiveId id) => id != CampaignObjectiveId.None && (completed & Bit(id)) != 0;
        public bool HasFile(CampaignFileId id) => id != CampaignFileId.None && (discoveredFiles & FileBit(id)) != 0;
        public static ulong Bit(CampaignObjectiveId id) => (int)id > 0 && (int)id < 64 ? 1UL << (int)id : 0;
        // ponytail: stable bit IDs support 63 non-None entries; use NetworkList only if the catalog outgrows this ceiling.
        public static ulong FileBit(CampaignFileId id) => (int)id > 0 && (int)id < 64 ? 1UL << (int)id : 0;
        public CampaignState Copy() => (CampaignState)MemberwiseClone();
    }

    /// <summary>Deterministic chapter and puzzle rules. No scene, timer or networking dependencies.</summary>
    public static class CampaignRules
    {
        public static CampaignResult CanDiscover(CampaignState state, CampaignFileDefinition file, bool actorAlive,
            bool sourceBound, bool sourceReachable)
        {
            if (state == null || file == null || file.id == CampaignFileId.None || !Enum.IsDefined(typeof(CampaignFileId), file.id)) return CampaignResult.Prerequisite;
            if (file.chapter != state.chapter) return CampaignResult.WrongChapter;
            if (state.phase != CampaignPhase.Exploring) return CampaignResult.WrongPhase;
            if (!actorAlive) return CampaignResult.InvalidActor;
            if (!sourceBound) return CampaignResult.Prerequisite;
            if (!sourceReachable) return CampaignResult.OutOfRange;
            return state.HasFile(file.id) ? CampaignResult.AlreadyDone : CampaignResult.Accepted;
        }

        public static bool IsEncounter(CampaignObjectiveId id) => id is CampaignObjectiveId.FactoryRoute or CampaignObjectiveId.AsylumLift or CampaignObjectiveId.LabTransmit;
        public static CampaignChapter ChapterOf(CampaignObjectiveId id) => id <= CampaignObjectiveId.FactoryRoute ? CampaignChapter.Factory
            : id <= CampaignObjectiveId.AsylumLift ? CampaignChapter.Asylum : CampaignChapter.Laboratory;
        public static bool UtilitiesReady(CampaignState s) => s.Has(CampaignObjectiveId.FactoryGenerator);
        public static bool LogisticsReady(CampaignState s) => s.Has(CampaignObjectiveId.FactoryShipping);
        public static bool LabSecured(CampaignState s) => s.Has(CampaignObjectiveId.LabArchive) && s.Has(CampaignObjectiveId.LabCase);

        public static bool ShouldArmTank(CampaignState state)
        {
            if (state == null || state.tankStage != CampaignTankStage.Dormant
                || state.phase is CampaignPhase.Insertion or CampaignPhase.Transitioning or CampaignPhase.Failed or CampaignPhase.Completed) return false;
            return state.chapter switch
            {
                CampaignChapter.Factory => UtilitiesReady(state) && LogisticsReady(state)
                    && !state.factoryTankDefeated && !state.Has(CampaignObjectiveId.FactoryCase),
                CampaignChapter.Asylum => state.Has(CampaignObjectiveId.AsylumTransfer),
                CampaignChapter.Laboratory => state.Has(CampaignObjectiveId.LabTransmit)
                    && state.phase is CampaignPhase.Encounter or CampaignPhase.AwaitingParty,
                _ => false
            };
        }

        public static bool TankAllowsExit(CampaignState state) => !ShouldArmTank(state)
            && state.tankStage != CampaignTankStage.Pending
            && (state.chapter != CampaignChapter.Factory || state.factoryTankDefeated || state.Has(CampaignObjectiveId.FactoryCase));

        public static CampaignResult CanUse(CampaignState s, CampaignObjectiveId id)
        {
            if (id == CampaignObjectiveId.None || !Enum.IsDefined(typeof(CampaignObjectiveId), id)) return CampaignResult.Prerequisite;
            if (ChapterOf(id) != s.chapter) return CampaignResult.WrongChapter;
            if (s.Has(id)) return CampaignResult.AlreadyDone;
            if (s.phase != CampaignPhase.Exploring) return CampaignResult.WrongPhase;
            bool ready = id switch
            {
                CampaignObjectiveId.FactoryBackup => s.Has(CampaignObjectiveId.FactoryIsolate),
                CampaignObjectiveId.FactoryGenerator => s.Has(CampaignObjectiveId.FactoryBackup),
                CampaignObjectiveId.FactoryCase => UtilitiesReady(s) && LogisticsReady(s) && s.factoryTankDefeated,
                CampaignObjectiveId.FactoryRoute => s.Has(CampaignObjectiveId.FactoryCase),
                CampaignObjectiveId.AsylumInstall => s.Has(CampaignObjectiveId.AsylumAccess) && s.Has(CampaignObjectiveId.AsylumFuse),
                CampaignObjectiveId.AsylumPower => s.Has(CampaignObjectiveId.AsylumInstall),
                CampaignObjectiveId.AsylumPatient => s.Has(CampaignObjectiveId.AsylumPower) && s.Has(CampaignObjectiveId.AsylumAccess),
                CampaignObjectiveId.AsylumTransfer => s.Has(CampaignObjectiveId.AsylumPatient),
                CampaignObjectiveId.AsylumLift => s.Has(CampaignObjectiveId.AsylumTransfer) && s.Has(CampaignObjectiveId.AsylumPower),
                CampaignObjectiveId.LabArchive => s.Has(CampaignObjectiveId.LabPower),
                CampaignObjectiveId.LabCase => s.Has(CampaignObjectiveId.LabArchive),
                CampaignObjectiveId.LabTransmit => LabSecured(s),
                _ => true
            };
            return ready ? CampaignResult.Accepted : CampaignResult.Prerequisite;
        }

        public static CampaignResult TryComplete(CampaignState s, CampaignObjectiveId id, int first = 0, int second = 0)
        {
            CampaignResult allowed = CanUse(s, id);
            if (allowed != CampaignResult.Accepted) return allowed;
            bool answer = id switch
            {
                CampaignObjectiveId.FactoryShipping => first == 1 && second == 1,
                CampaignObjectiveId.AsylumPatient => first == 1,
                CampaignObjectiveId.AsylumTransfer => first == 0,
                CampaignObjectiveId.LabPower => IsPowerAnswer(first),
                CampaignObjectiveId.LabArchive => first == 1,
                _ => true
            };
            if (!answer) return CampaignResult.WrongAnswer;
            if (id == CampaignObjectiveId.LabPower) s.powerSelection = (byte)first;
            s.completed |= CampaignState.Bit(id);
            if (id == CampaignObjectiveId.FactoryShipping) s.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryManifest);
            if (id == CampaignObjectiveId.LabArchive) s.completed |= CampaignState.Bit(CampaignObjectiveId.LabTrace) | CampaignState.Bit(CampaignObjectiveId.LabIndex);
            if (IsEncounter(id)) s.phase = CampaignPhase.Preparing;
            s.revision++;
            return CampaignResult.Accepted;
        }

        public static bool IsPowerAnswer(int mask) => mask == 13;
        public static int Load(int mask) => ((mask & 1) != 0 ? 2 : 0) + ((mask & 2) != 0 ? 4 : 0) + ((mask & 4) != 0 ? 2 : 0) + ((mask & 8) != 0 ? 2 : 0);
        public static bool CanSelectPower(int mask) => mask >= 0 && mask <= 15 && (mask & 1) != 0 && Load(mask) <= 6;
    }
}
