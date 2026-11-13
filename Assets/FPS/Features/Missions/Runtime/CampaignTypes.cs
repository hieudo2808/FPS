using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public enum CampaignChapter : byte { Factory, Asylum, Laboratory }
    public enum CampaignPhase : byte { Insertion, Exploring, Preparing, Encounter, AwaitingParty, Transitioning, Completed, Failed }
    public enum CampaignTankStage : byte { Dormant, Pending, Active, Resolved, SpawnFailed }
    public enum CampaignTankWaitReason : byte { None, NoActor, Reading, Protection, SpecialBusy, NoSpawnService, NoSafePosition, SpawnRejected }
    public enum CampaignFactoryBranch : byte { None, Power, Logistics }
    public enum CampaignGateWaitReason : byte { None, Tank, NoLivingOperator, DisconnectGrace, Revive, EnterVolume, Ready, Countdown }
    [Serializable]
    public struct CampaignPartyGateStatus
    {
        public CampaignGateWaitReason reason;
        public int eligibleCount, insideCount, readyCount;
        public bool overrideActive, canCountdown;
        public float timeoutRemaining, countdownRemaining;
    }
    public enum CampaignObjectiveId : byte
    {
        None, FactoryIsolate, FactoryBackup, FactoryGenerator, FactoryManifest, FactoryShipping,
        FactoryCase, FactoryRoute, AsylumAccess, AsylumFuse, AsylumInstall, AsylumPower,
        AsylumPatient, AsylumTransfer, AsylumLift, LabTrace, LabIndex, LabPower,
        LabArchive, LabCase, LabTransmit
    }
    public enum CampaignResult : byte { Accepted, AlreadyDone, WrongChapter, WrongPhase, Prerequisite, WrongAnswer, InvalidActor, Busy, OutOfRange, Obstructed, Interrupted, AmmoUnavailable, MissingSelection }
    [Flags] public enum CampaignShippingGroup : byte { None = 0, ShipmentSeal = 1, DestinationReceivingTime = 2 }

    public readonly struct CampaignAvailability
    {
        public readonly CampaignResult Result;
        public readonly string Reason;
        public readonly CampaignObjectiveId[] RequiredObjectiveIds;
        public readonly CampaignFileId RequiredFileId;
        public readonly bool RequiresTankDefeat;
        public bool IsLocked => Result != CampaignResult.Accepted && Result != CampaignResult.AlreadyDone;

        public CampaignAvailability(CampaignResult result, string reason, CampaignObjectiveId[] required = null,
            CampaignFileId file = CampaignFileId.None, bool tank = false)
        { Result = result; Reason = reason; RequiredObjectiveIds = required ?? Array.Empty<CampaignObjectiveId>(); RequiredFileId = file; RequiresTankDefeat = tank; }
    }

    public readonly struct CampaignObjectiveFeedback
    {
        public readonly CampaignObjectiveId Objective;
        public readonly CampaignResult Result;
        public readonly uint Revision;
        public readonly CampaignShippingGroup MissingGroups, MismatchedGroups;
        public CampaignObjectiveFeedback(CampaignObjectiveId objective, CampaignResult result, uint revision,
            CampaignShippingGroup missing = CampaignShippingGroup.None, CampaignShippingGroup mismatch = CampaignShippingGroup.None)
        { Objective = objective; Result = result; Revision = revision; MissingGroups = missing; MismatchedGroups = mismatch; }
    }

    // RPC delivery and replicated state may arrive in either order.
    public sealed class CampaignFeedbackBuffer
    {
        private readonly List<CampaignObjectiveFeedback> pending = new();
        private readonly HashSet<(uint, CampaignObjectiveId)> received = new();
        private uint baseline;
        public void Reset(uint revision = 0) { pending.Clear(); received.Clear(); baseline = revision; }
        public void Add(CampaignObjectiveFeedback feedback, CampaignState state, Action<CampaignObjectiveFeedback> deliver)
        {
            if (feedback.Result == CampaignResult.Accepted
                && (feedback.Revision <= baseline || !received.Add((feedback.Revision, feedback.Objective)))) return;
            pending.Add(feedback);
            Apply(state, deliver);
        }
        public void Apply(CampaignState state, Action<CampaignObjectiveFeedback> deliver)
        {
            if (state == null) return;
            for (int i = 0; i < pending.Count;)
            {
                var feedback = pending[i];
                if (feedback.Revision > state.revision) { i++; continue; }
                pending.RemoveAt(i);
                if (feedback.Objective == CampaignObjectiveId.FactoryShipping
                    && feedback.Result is CampaignResult.WrongAnswer or CampaignResult.MissingSelection
                    && (state.chapter != CampaignChapter.Factory || state.phase != CampaignPhase.Exploring
                        || state.Has(CampaignObjectiveId.FactoryShipping))) continue;
                if (feedback.Result != CampaignResult.Accepted || CampaignRules.IsObjectiveComplete(state, feedback.Objective))
                    deliver(feedback);
            }
        }
    }

    public sealed class CampaignHintProgress
    {
        public float Seconds { get; private set; }
        private CampaignObjectiveId target;
        private ulong files, completed;
        private CampaignChapter chapter;
        private bool exploring;
        public void Observe(CampaignState state, CampaignObjectiveId next)
        {
            if (state == null) { Seconds = 0; target = CampaignObjectiveId.None; exploring = false; return; }
            if (target != next || files != state.discoveredFiles || completed != state.completed || chapter != state.chapter) Seconds = 0;
            target = next; files = state.discoveredFiles; completed = state.completed; chapter = state.chapter;
            exploring = state.phase == CampaignPhase.Exploring;
        }
        public void Advance(float delta, bool paused)
        { if (!paused && exploring && target != CampaignObjectiveId.None) Seconds += Math.Max(0, delta); }
        public void Tick(CampaignState state, CampaignObjectiveId next, float delta, bool paused)
        { Observe(state, next); Advance(delta, paused); }
    }

    public sealed class CampaignTankAttempt
    {
        private double last = -1;
        public double Elapsed { get; private set; }
        public void Reset() { last = -1; Elapsed = 0; }
        public void Tick(double now, bool paused)
        {
            if (!paused && last >= 0) Elapsed += Math.Max(0, now - last);
            last = now;
        }
        public bool Fallback(double timeout) => Elapsed >= Math.Max(1, timeout) * .5;
        public bool TimedOut(double timeout) => Elapsed >= Math.Max(1, timeout);
        // Escalation: 0 strict, 1 reversed anchors, 2 relaxed placement, 3 the encounter cannot be staged at all.
        public int Tier(double timeout) => Elapsed >= Math.Max(1, timeout) * 3 ? 3 : TimedOut(timeout) ? 2 : Fallback(timeout) ? 1 : 0;
    }

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
        public int version = 3;
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
        public CampaignTankWaitReason tankWaitReason;
        public float tankSpawnRemaining;
        public bool tankFallback;
        public CampaignFactoryBranch lastFactoryBranch;
        public CampaignPartyGateStatus partyGate;

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
        /// <summary>One ammo crate refills half the held weapon's reserve, never less than a magazine or more than it can carry.</summary>
        public static int SupplyAmmo(int magazineSize, int reserveCapacity) => reserveCapacity <= 0 ? 0
            : Math.Min(reserveCapacity, Math.Max(Math.Min(magazineSize, reserveCapacity), (reserveCapacity + 1) / 2));

        /// <summary>Station index encoded in a supply id: "Factory_1_2" and "survival-Factory_1_3-0" are both station 1.</summary>
        public static int SupplyStation(string supplyId)
        {
            if (string.IsNullOrEmpty(supplyId)) return -1;
            string[] parts = (supplyId.StartsWith("survival-", StringComparison.Ordinal) ? supplyId.Substring(9) : supplyId).Split('_');
            return parts.Length >= 2 && int.TryParse(parts[1], out int station) && station >= 0 && station < 8 ? station : -1;
        }

        /// <summary>The squad is struggling enough for the Director to open a station's reserve.</summary>
        public static bool SquadNeedsReserve(float averageHealth01, int downed, float secondsSinceLastDown, float averageReserveAmmo01) =>
            downed > 0 || secondsSinceLastDown >= 0f && secondsSinceLastDown <= 30f || averageHealth01 < .5f || averageReserveAmmo01 < .25f;

        /// <summary>Base supplies scale with party size; reserve supplies appear only where the Director released their station.</summary>
        public static bool SupplyWanted(bool reserve, int minimumPartySize, int partySize, bool stationReleased) =>
            reserve ? stationReleased : partySize >= minimumPartySize;

        public static int HintTier(float seconds) => seconds >= 135 ? 3 : seconds >= 90 ? 2 : seconds >= 45 ? 1 : 0;
        public static string HintText(CampaignState state, CampaignObjectiveDefinition definition, float seconds)
        {
            int tier = HintTier(seconds);
            if (definition == null || tier == 0) return string.Empty;
            string text = tier == 3 && !string.IsNullOrWhiteSpace(definition.hint3) ? definition.hint3
                : tier >= 2 && !string.IsNullOrWhiteSpace(definition.hint2) ? definition.hint2 : definition.hint;
            var available = state != null ? Availability(state, definition.id) : default;
            return available.IsLocked && state != null ? "Requires: " + available.Reason + ". " + text : text;
        }
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

        // The one thing each chapter is for; shown on the tracker and as the Missions group title.
        public static string ChapterName(CampaignChapter chapter) => chapter switch
        { CampaignChapter.Factory => "FACTORY", CampaignChapter.Asylum => "NORTHBRIDGE", _ => "B2 / LABORATORY" };
        public static string ChapterGoal(CampaignChapter chapter) => chapter switch
        { CampaignChapter.Factory => "Recover the evidence case", CampaignChapter.Asylum => "Call the B2 lift", _ => "Transmit the evidence" };
        public static bool IsEncounter(CampaignObjectiveId id) => id is CampaignObjectiveId.FactoryRoute or CampaignObjectiveId.AsylumLift or CampaignObjectiveId.LabTransmit;
        public static CampaignChapter ChapterOf(CampaignObjectiveId id) => id <= CampaignObjectiveId.FactoryRoute ? CampaignChapter.Factory
            : id <= CampaignObjectiveId.AsylumLift ? CampaignChapter.Asylum : CampaignChapter.Laboratory;
        public static bool UtilitiesReady(CampaignState s) => s.Has(CampaignObjectiveId.FactoryGenerator);
        public static bool LogisticsReady(CampaignState s) => s.Has(CampaignObjectiveId.FactoryShipping);
        public static bool IsObjectiveComplete(CampaignState s, CampaignObjectiveId id) => s != null
            && (id == CampaignObjectiveId.FactoryManifest ? s.HasFile(CampaignFileId.FactoryManifest) : s.Has(id));
        public static CampaignFactoryBranch FactoryBranchOf(CampaignObjectiveId id) => id switch
        {
            CampaignObjectiveId.FactoryIsolate or CampaignObjectiveId.FactoryBackup or CampaignObjectiveId.FactoryGenerator => CampaignFactoryBranch.Power,
            CampaignObjectiveId.FactoryManifest or CampaignObjectiveId.FactoryShipping => CampaignFactoryBranch.Logistics,
            _ => CampaignFactoryBranch.None
        };
        private static readonly CampaignObjectiveId[] PowerSteps = { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryGenerator };
        private static readonly CampaignObjectiveId[] LogisticsSteps = { CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
        public static int FactoryProgress(CampaignState s, CampaignFactoryBranch branch)
        {
            int count = 0;
            foreach (var id in branch == CampaignFactoryBranch.Power ? PowerSteps : branch == CampaignFactoryBranch.Logistics ? LogisticsSteps : Array.Empty<CampaignObjectiveId>())
                if (IsObjectiveComplete(s, id)) count++;
            return count;
        }
        public static CampaignObjectiveId FactoryNext(CampaignState s, CampaignFactoryBranch branch)
        {
            foreach (var id in branch == CampaignFactoryBranch.Power ? PowerSteps : branch == CampaignFactoryBranch.Logistics ? LogisticsSteps : Array.Empty<CampaignObjectiveId>())
                if (CanUse(s, id) == CampaignResult.Accepted) return id;
            return CampaignObjectiveId.None;
        }
        public static bool CanSwitchFactoryBranch(CampaignState s) => FactoryNext(s, CampaignFactoryBranch.Power) != CampaignObjectiveId.None
            && FactoryNext(s, CampaignFactoryBranch.Logistics) != CampaignObjectiveId.None;
        public static void RecordFile(CampaignState s, CampaignFileId id)
        {
            s.discoveredFiles |= CampaignState.FileBit(id);
            if (id == CampaignFileId.FactoryManifest) s.lastFactoryBranch = CampaignFactoryBranch.Logistics;
        }
        public static CampaignState RestoreCheckpoint(CampaignState snapshot, uint previousRevision)
        {
            var restored = snapshot.Copy();
            restored.revision = Math.Max(previousRevision, restored.revision);
            restored.partyGate = default;
            return restored;
        }
        public static CampaignPartyGateStatus EvaluatePartyGate(bool preparation, int eligible, int inside, int ready, int qualified,
            int downed, bool grace, bool tankAllows, float timeoutRemaining)
        {
            bool timedOut = timeoutRemaining <= 0;
            bool all = eligible > 0 && (!grace || timedOut) && qualified == eligible;
            bool canCountdown = tankAllows && (all || timedOut && qualified > 0);
            return new CampaignPartyGateStatus
            {
                eligibleCount = eligible, insideCount = inside, readyCount = ready, overrideActive = timedOut,
                canCountdown = canCountdown, timeoutRemaining = Math.Max(0, (float)Math.Ceiling(timeoutRemaining)),
                reason = !tankAllows ? CampaignGateWaitReason.Tank : eligible == 0 ? CampaignGateWaitReason.NoLivingOperator
                    : canCountdown ? CampaignGateWaitReason.Countdown : grace && !timedOut ? CampaignGateWaitReason.DisconnectGrace
                    : downed > 0 && !timedOut ? CampaignGateWaitReason.Revive
                    : preparation && inside > 0 && qualified == 0 ? CampaignGateWaitReason.Ready : inside < eligible ? CampaignGateWaitReason.EnterVolume
                    : preparation ? CampaignGateWaitReason.Ready : CampaignGateWaitReason.EnterVolume
            };
        }
        public static string PartyGateText(CampaignPartyGateStatus gate, bool preparation)
        {
            string volume = preparation ? "preparation area" : "boarding area";
            string reason = gate.reason switch
            {
                CampaignGateWaitReason.Tank => "A Tank is closing in",
                CampaignGateWaitReason.NoLivingOperator => "Waiting for a controllable living operator",
                CampaignGateWaitReason.DisconnectGrace => "Waiting for disconnected operators to reconnect",
                CampaignGateWaitReason.Revive => "Revive downed operators before departure",
                CampaignGateWaitReason.EnterVolume => "Enter the " + volume,
                CampaignGateWaitReason.Ready => "Ready up in the preparation area",
                CampaignGateWaitReason.Countdown => $"Remain in the {volume} / {gate.countdownRemaining:0}s countdown",
                _ => "Waiting for server gate status"
            };
            // One action line and one count line; a solo operator has nobody to ready up for.
            return reason + $"\n{gate.insideCount}/{gate.eligibleCount} in zone"
                + (preparation && gate.eligibleCount > 1 ? $" · READY {gate.readyCount}/{gate.eligibleCount}" : "");
        }
        public static bool CanOpenShipping(CampaignState s) => s != null && s.HasFile(CampaignFileId.FactoryManifest)
            && s.chapter == CampaignChapter.Factory && s.phase == CampaignPhase.Exploring;
        public static CampaignShippingGroup ShippingMissing(int first, int second) => (first < 0 ? CampaignShippingGroup.ShipmentSeal : 0)
            | (second < 0 ? CampaignShippingGroup.DestinationReceivingTime : 0);
        public static CampaignShippingGroup ShippingMismatch(int first, int second) => (first != 1 ? CampaignShippingGroup.ShipmentSeal : 0)
            | (second != 1 ? CampaignShippingGroup.DestinationReceivingTime : 0);
        public static string ShippingGroupNames(CampaignShippingGroup groups) => groups switch
        {
            CampaignShippingGroup.ShipmentSeal => "Shipment / Seal",
            CampaignShippingGroup.DestinationReceivingTime => "Destination / Receiving time",
            _ => "Shipment / Seal; Destination / Receiving time"
        };
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
            && state.tankStage is not (CampaignTankStage.Pending or CampaignTankStage.SpawnFailed)
            && (state.chapter != CampaignChapter.Factory || state.factoryTankDefeated || state.Has(CampaignObjectiveId.FactoryCase));

        public static bool TankIsNext(CampaignState state) => state != null && state.chapter == CampaignChapter.Factory
            && state.phase == CampaignPhase.Exploring && UtilitiesReady(state) && LogisticsReady(state)
            && !state.factoryTankDefeated && !state.Has(CampaignObjectiveId.FactoryCase);
        // Last resort after every placement tier failed: the squad is never left waiting on a Tank that cannot exist.
        public static void SkipUnstageableTank(CampaignState state)
        {
            state.tankStage = CampaignTankStage.Resolved;
            state.tankWaitReason = CampaignTankWaitReason.None; state.tankSpawnRemaining = 0; state.tankFallback = false;
            if (state.chapter == CampaignChapter.Factory) state.factoryTankDefeated = true;
        }
        public static bool ConfirmTankDefeat(CampaignState state, bool server, bool trackedInstance, bool isDead)
        {
            if (!server || !trackedInstance || !isDead || state.tankStage != CampaignTankStage.Active
                || state.phase is CampaignPhase.Failed or CampaignPhase.Completed) return false;
            state.tankStage = CampaignTankStage.Resolved;
            state.tankWaitReason = CampaignTankWaitReason.None; state.tankSpawnRemaining = 0;
            if (state.chapter == CampaignChapter.Factory) state.factoryTankDefeated = true;
            return true;
        }
        public static void ResetUnverifiedFactoryTank(CampaignState state)
        {
            if (state.chapter != CampaignChapter.Factory) return;
            state.factoryTankDefeated = false;
            state.tankStage = state.Has(CampaignObjectiveId.FactoryCase) ? CampaignTankStage.Resolved : CampaignTankStage.Dormant;
            state.tankWaitReason = CampaignTankWaitReason.None; state.tankSpawnRemaining = 0; state.tankFallback = false;
        }
        public static string TankStatus(CampaignState state) => state.tankStage == CampaignTankStage.Active
            ? "Defeat the Tank before collecting the case" : "Stay alert. A Tank is closing in.";

        public static CampaignFileId ComparisonSource(CampaignObjectiveId id) => id switch
        {
            CampaignObjectiveId.FactoryShipping => CampaignFileId.FactoryManifest,
            CampaignObjectiveId.AsylumPatient => CampaignFileId.AsylumPatientRecord,
            CampaignObjectiveId.AsylumTransfer => CampaignFileId.AsylumMortuaryTransfer,
            CampaignObjectiveId.LabPower => CampaignFileId.LabPowerRecord,
            CampaignObjectiveId.LabArchive => CampaignFileId.LabOriginalArchive,
            _ => CampaignFileId.None
        };
        public static bool CanReadComparisonSource(CampaignState state, CampaignObjectiveId id) => state != null
            && id != CampaignObjectiveId.FactoryShipping && ComparisonSource(id) != CampaignFileId.None
            && !state.HasFile(ComparisonSource(id)) && CanUsePrerequisites(state, id) == CampaignResult.Accepted;

        public static CampaignResult CanUse(CampaignState s, CampaignObjectiveId id)
        {
            var result = CanUsePrerequisites(s, id);
            var source = ComparisonSource(id);
            return result == CampaignResult.Accepted && source != CampaignFileId.None && !s.HasFile(source)
                ? CampaignResult.Prerequisite : result;
        }

        private static CampaignResult CanUsePrerequisites(CampaignState s, CampaignObjectiveId id)
        {
            if (s == null) return CampaignResult.Prerequisite;
            if (id == CampaignObjectiveId.None || !Enum.IsDefined(typeof(CampaignObjectiveId), id)) return CampaignResult.Prerequisite;
            if (ChapterOf(id) != s.chapter) return CampaignResult.WrongChapter;
            if (id == CampaignObjectiveId.FactoryManifest && s.HasFile(CampaignFileId.FactoryManifest)) return CampaignResult.AlreadyDone;
            if (s.Has(id)) return CampaignResult.AlreadyDone;
            if (s.phase != CampaignPhase.Exploring) return CampaignResult.WrongPhase;
            bool ready = id switch
            {
                CampaignObjectiveId.FactoryBackup => s.Has(CampaignObjectiveId.FactoryIsolate),
                CampaignObjectiveId.FactoryGenerator => s.Has(CampaignObjectiveId.FactoryBackup),
                CampaignObjectiveId.FactoryShipping => s.HasFile(CampaignFileId.FactoryManifest),
                CampaignObjectiveId.FactoryCase => UtilitiesReady(s) && LogisticsReady(s) && s.factoryTankDefeated,
                CampaignObjectiveId.FactoryRoute => s.Has(CampaignObjectiveId.FactoryCase),
                CampaignObjectiveId.AsylumInstall => s.Has(CampaignObjectiveId.AsylumAccess) && s.Has(CampaignObjectiveId.AsylumFuse),
                CampaignObjectiveId.AsylumPower => s.Has(CampaignObjectiveId.AsylumInstall),
                CampaignObjectiveId.AsylumPatient => s.Has(CampaignObjectiveId.AsylumPower) && s.Has(CampaignObjectiveId.AsylumAccess)
                    && s.HasFile(CampaignFileId.AsylumDeskLog),
                CampaignObjectiveId.AsylumTransfer => s.Has(CampaignObjectiveId.AsylumPatient),
                CampaignObjectiveId.AsylumLift => s.Has(CampaignObjectiveId.AsylumTransfer) && s.Has(CampaignObjectiveId.AsylumPower),
                CampaignObjectiveId.LabArchive => s.Has(CampaignObjectiveId.LabPower) && s.HasFile(CampaignFileId.LabIndex),
                CampaignObjectiveId.LabCase => s.Has(CampaignObjectiveId.LabArchive),
                CampaignObjectiveId.LabTransmit => LabSecured(s),
                _ => true
            };
            return ready ? CampaignResult.Accepted : CampaignResult.Prerequisite;
        }

        public static CampaignAvailability Availability(CampaignState s, CampaignObjectiveId id)
        {
            var result = CanUse(s, id);
            if (s == null) return new(result, "wait for campaign state");
            if (id == CampaignObjectiveId.None || !Enum.IsDefined(typeof(CampaignObjectiveId), id)) return new(result, "a valid objective");
            if (result == CampaignResult.WrongChapter) return new(result, "reach this objective's chapter");
            if (result == CampaignResult.WrongPhase) return new(result, "return to exploration");
            if (result == CampaignResult.AlreadyDone) return new(result, "already recorded");
            if (result == CampaignResult.Accepted) return new(result, "");
            if (id == CampaignObjectiveId.FactoryShipping)
                return new(result, "read Delivery Manifest", new[] { CampaignObjectiveId.FactoryManifest }, CampaignFileId.FactoryManifest);
            if (id == CampaignObjectiveId.AsylumPatient && s.Has(CampaignObjectiveId.AsylumPower) && s.Has(CampaignObjectiveId.AsylumAccess)
                && !s.HasFile(CampaignFileId.AsylumDeskLog))
                return new(result, "read Security Desk Log", new[] { CampaignObjectiveId.AsylumAccess }, CampaignFileId.AsylumDeskLog);
            if (id == CampaignObjectiveId.LabArchive && s.Has(CampaignObjectiveId.LabPower) && !s.HasFile(CampaignFileId.LabIndex))
                return new(result, "read B2 Archive Index", new[] { CampaignObjectiveId.LabIndex }, CampaignFileId.LabIndex);
            if (CanReadComparisonSource(s, id))
                return new(result, "read " + (id switch
                {
                    CampaignObjectiveId.AsylumPatient => "Intake Record",
                    CampaignObjectiveId.AsylumTransfer => "Mortuary Transfer Record",
                    CampaignObjectiveId.LabPower => "Emergency Power Allocation",
                    _ => "Original Incident Archive"
                }), file: ComparisonSource(id));
            if (id == CampaignObjectiveId.FactoryBackup)
                return new(result, "complete Power / Isolate", new[] { CampaignObjectiveId.FactoryIsolate });
            if (id == CampaignObjectiveId.FactoryGenerator)
                return new(result, "complete Power / Backup", new[] { CampaignObjectiveId.FactoryBackup });
            if (id == CampaignObjectiveId.FactoryCase)
            {
                var objectives = new System.Collections.Generic.List<CampaignObjectiveId>(2);
                var reasons = new System.Collections.Generic.List<string>(3);
                if (!UtilitiesReady(s)) { objectives.Add(CampaignObjectiveId.FactoryGenerator); reasons.Add("complete Power"); }
                if (!LogisticsReady(s)) { objectives.Add(CampaignObjectiveId.FactoryShipping); reasons.Add("complete Logistics"); }
                if (!s.factoryTankDefeated) reasons.Add("defeat the Tank");
                return new(result, string.Join("; ", reasons), objectives.ToArray(), tank: !s.factoryTankDefeated);
            }
            if (id == CampaignObjectiveId.FactoryRoute)
                return new(result, "recover the evidence case", new[] { CampaignObjectiveId.FactoryCase });
            return new(result, OtherLockReason(id));
        }

        public static string LockReason(CampaignState s, CampaignObjectiveId id) => Availability(s, id).Reason;

        private static string OtherLockReason(CampaignObjectiveId id)
        {
            if (id == CampaignObjectiveId.AsylumInstall) return "recover the service fuse and access log";
            if (id == CampaignObjectiveId.AsylumPower) return "install the service fuse";
            if (id == CampaignObjectiveId.AsylumPatient) return "power the records room";
            if (id == CampaignObjectiveId.AsylumTransfer) return "record the patient entry first";
            if (id == CampaignObjectiveId.AsylumLift) return "complete the transfer and take the lift card";
            if (id == CampaignObjectiveId.LabArchive) return "power the archive console";
            if (id == CampaignObjectiveId.LabCase) return "record the archive";
            if (id == CampaignObjectiveId.LabTransmit) return "secure the lab case";
            return "finish the current objective";
        }

        public static CampaignResult TryComplete(CampaignState s, CampaignObjectiveId id, int first = 0, int second = 0)
        {
            CampaignResult allowed = CanUse(s, id);
            if (allowed != CampaignResult.Accepted) return allowed;
            if (id == CampaignObjectiveId.FactoryShipping && ShippingMissing(first, second) != CampaignShippingGroup.None)
                return CampaignResult.MissingSelection;
            bool answer = id switch
            {
                CampaignObjectiveId.FactoryShipping => ShippingMismatch(first, second) == CampaignShippingGroup.None,
                CampaignObjectiveId.AsylumPatient => first == 1,
                CampaignObjectiveId.AsylumTransfer => first == 0,
                CampaignObjectiveId.LabPower => IsPowerAnswer(first),
                CampaignObjectiveId.LabArchive => first == 1,
                _ => true
            };
            if (!answer) return CampaignResult.WrongAnswer;
            if (id == CampaignObjectiveId.LabPower) s.powerSelection = (byte)first;
            var branch = FactoryBranchOf(id);
            if (branch != CampaignFactoryBranch.None) s.lastFactoryBranch = branch;
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
