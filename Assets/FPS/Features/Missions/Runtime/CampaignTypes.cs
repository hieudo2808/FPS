using System;

namespace FPS
{
    public enum CampaignChapter : byte { Factory, Asylum, Laboratory }
    public enum CampaignPhase : byte { Insertion, Exploring, Preparing, Encounter, AwaitingParty, Transitioning, Completed, Failed }
    public enum CampaignObjectiveId : byte
    {
        None, FactoryIsolate, FactoryBackup, FactoryGenerator, FactoryManifest, FactoryShipping,
        FactoryCase, FactoryRoute, AsylumAccess, AsylumFuse, AsylumInstall, AsylumPower,
        AsylumPatient, AsylumTransfer, AsylumLift, LabTrace, LabIndex, LabPower,
        LabArchive, LabCase, LabTransmit
    }
    public enum CampaignResult : byte { Accepted, AlreadyDone, WrongChapter, WrongPhase, Prerequisite, WrongAnswer, InvalidActor, Busy, OutOfRange, Obstructed, Interrupted }

    [Serializable]
    public sealed class CampaignState
    {
        public int version = 1;
        public uint revision;
        public CampaignChapter chapter;
        public CampaignPhase phase = CampaignPhase.Insertion;
        public ulong completed;
        public byte powerSelection = 3;
        public int checkpoint;
        public int supplyPartySize = 1;
        public bool evidenceTransmitted;

        public bool Has(CampaignObjectiveId id) => id != CampaignObjectiveId.None && (completed & Bit(id)) != 0;
        public static ulong Bit(CampaignObjectiveId id) => 1UL << (int)id;
        public CampaignState Copy() => (CampaignState)MemberwiseClone();
    }

    /// <summary>Deterministic chapter and puzzle rules. No scene, timer or networking dependencies.</summary>
    public static class CampaignRules
    {
        public static bool IsEncounter(CampaignObjectiveId id) => id is CampaignObjectiveId.FactoryRoute or CampaignObjectiveId.AsylumLift or CampaignObjectiveId.LabTransmit;
        public static CampaignChapter ChapterOf(CampaignObjectiveId id) => id <= CampaignObjectiveId.FactoryRoute ? CampaignChapter.Factory
            : id <= CampaignObjectiveId.AsylumLift ? CampaignChapter.Asylum : CampaignChapter.Laboratory;
        public static bool UtilitiesReady(CampaignState s) => s.Has(CampaignObjectiveId.FactoryGenerator);
        public static bool LogisticsReady(CampaignState s) => s.Has(CampaignObjectiveId.FactoryShipping);
        public static bool LabSecured(CampaignState s) => s.Has(CampaignObjectiveId.LabArchive) && s.Has(CampaignObjectiveId.LabCase);

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
                CampaignObjectiveId.FactoryCase => UtilitiesReady(s) && LogisticsReady(s),
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
