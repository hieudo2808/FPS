using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed partial class CampaignMissionController : NetworkBehaviour
    {
        public static CampaignMissionController Instance { get; private set; }
        public CampaignSettings settings;
        public CampaignChapterRoot[] chapters;
        public CampaignSupply[] supplies;
        public CampaignDoor[] doors;
        public CampaignInteractable[] fileSources = Array.Empty<CampaignInteractable>();
        private readonly NetworkVariable<ulong> openDoorMask = new();
        private readonly NetworkVariable<FixedString4096Bytes> replicatedState = new();
        private readonly NetworkVariable<FixedString4096Bytes> replicatedSummary = new();
        private readonly NetworkVariable<double> phaseStarted = new();
        private readonly NetworkVariable<int> readyCount = new();
        private readonly NetworkVariable<bool> transferLocked = new();
        private NetworkList<FixedString64Bytes> claimedSupplies;
        private CampaignState state = new();
        private CampaignCheckpoint checkpoint;
        private readonly Dictionary<ulong, Hold> holds = new();
        private readonly HashSet<ulong> releaseRequired = new();
        private readonly HashSet<ulong> ready = new();
        private readonly HashSet<ulong> knownPlayers = new();
        private readonly Dictionary<ulong, ulong> clientPlayers = new();
        private readonly Dictionary<ulong, double> disconnected = new();
        private readonly Dictionary<ulong, PlayerRuntimeSnapshot> rosterSnapshots = new();
        private double boardingSince = -1, closingSince = -1, countdownSince = -1, readingUntil, nextBeatAt;
        private float updateBudget;
        private bool initialized;
        private bool transferred;
        private CampaignRunSummary runSummary = new();
        private double campaignStartedAt;
        private int attemptKillBaseline;
        private ulong publishedFiles;
        private readonly Dictionary<CampaignDialogueId, double> dialogueHistory = new();
        private SpecialInfectedRegistry dialogueSpecialRegistry;
        public event Action Changed;
        public CampaignState State => state;
        public CampaignRunSummary RunSummary => runSummary;
        public CampaignContentCatalog ContentCatalog => settings != null ? settings.contentCatalog : null;
        public CampaignDialogueDefinition Dialogue(CampaignDialogueId id) => ContentCatalog?.FindDialogue(id);
        public CampaignChapterRoot Current => chapters != null && chapters.Length > (int)state.chapter ? chapters[(int)state.chapter] : null;
        public double Now => IsSpawned && NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public double PhaseStarted => phaseStarted.Value;
        public float Remaining => Mathf.Max(0, settings.Duration(state.chapter) - (float)(Now - phaseStarted.Value));
        public int ReadyCount => readyCount.Value;
        public bool CanResumeSavedCampaign => IsServer && (state.phase == CampaignPhase.Failed || state.chapter == CampaignChapter.Factory && state.checkpoint == 0 && state.completed == 0);
        public bool IsFinale => state.phase == CampaignPhase.Encounter;
        public bool SuppressSpawns => !initialized || state.phase is CampaignPhase.Insertion or CampaignPhase.Preparing or CampaignPhase.AwaitingParty or CampaignPhase.Transitioning or CampaignPhase.Completed or CampaignPhase.Failed || Now < readingUntil;
        public bool BlocksInput => state.phase is CampaignPhase.Insertion or CampaignPhase.Completed or CampaignPhase.Failed || transferLocked.Value;
        public IEnumerable<PlayerHealth> Players => NetworkManager == null ? Array.Empty<PlayerHealth>()
            : IsServer ? NetworkManager.ConnectedClientsList.Select(c => c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerHealth>() : null).Where(p => p != null)
            : NetworkManager.SpawnManager.SpawnedObjectsList.Where(o => o.IsPlayerObject)
                .Select(o => o.GetComponent<PlayerHealth>()).Where(p => p != null && p.IsSpawned);
        public int ActivePlayerCount => Players.Count(p => p.LifeState == PlayerLifeState.Alive && p.IsInputReady);
        public CampaignChapter SnapshotChapter => state.phase == CampaignPhase.Transitioning && transferred && state.chapter < CampaignChapter.Laboratory
            ? state.chapter + 1 : state.chapter;

        private sealed class Hold
        {
            public CampaignObjectiveId objective;
            public int first, second;
            public double started, lease;
            public uint damage;
            public ushort shot0, shot1;
            public byte action; // 0 objective, 1 revive, 2 medicine
            public ulong target;
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            claimedSupplies = new NetworkList<FixedString64Bytes>();
            insertionRoster = new NetworkList<CampaignInsertionParticipant>();
        }
        public override void OnNetworkSpawn()
        {
            CampaignHUD.Instance?.ResetDialogue();
            replicatedState.OnValueChanged += OnState;
            replicatedSummary.OnValueChanged += OnSummary;
            if (IsServer)
            {
                initialized = false; insertionReady.Value = false; insertionRoster.Clear();
                state = new CampaignState(); publishedFiles = 0; BeginAttempt(); phaseStarted.Value = Now; Publish();
                NetworkManager.OnClientDisconnectCallback += OnDisconnected;
            }
            else
            {
                Decode(replicatedState.Value);
                if (replicatedSummary.Value.Length > 0) OnSummary(default, replicatedSummary.Value);
            }
        }
        public override void OnNetworkDespawn()
        {
            ReleaseTankTracking();
            CampaignHUD.Instance?.ResetDialogue();
            if (dialogueSpecialRegistry != null) dialogueSpecialRegistry.OnSpecialSpawned -= OnSpecialSpawned;
            dialogueSpecialRegistry = null;
            replicatedState.OnValueChanged -= OnState;
            replicatedSummary.OnValueChanged -= OnSummary;
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnDisconnected;
        }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            claimedSupplies?.Dispose();
            insertionRoster?.Dispose();
            base.OnDestroy();
        }
        private void OnState(FixedString4096Bytes before, FixedString4096Bytes after) => Decode(after);
        private void OnSummary(FixedString4096Bytes before, FixedString4096Bytes after)
        {
            runSummary = after.Length > 0 ? JsonUtility.FromJson<CampaignRunSummary>(after.ToString()) : null;
            Changed?.Invoke();
        }
        private void Decode(FixedString4096Bytes json)
        {
            ulong previousFiles = state != null ? state.discoveredFiles : 0;
            bool hadState = initialized;
            if (json.Length > 0) state = JsonUtility.FromJson<CampaignState>(json.ToString());
            if (!IsServer) initialized = true;
            if (!IsServer && hadState && state != null && (state.discoveredFiles & ~previousFiles) != 0)
                CampaignHUD.Instance?.ShowMessage("NEW FILE ADDED");
            Changed?.Invoke();
            ConfigureChapterServices();
            RefreshDocuments();
        }
        private void Publish()
        {
            DiscoverAutomaticFiles();
            if ((state.discoveredFiles & ~publishedFiles) != 0) CampaignHUD.Instance?.ShowMessage("NEW FILE ADDED");
            publishedFiles = state.discoveredFiles;
            state.revision++;
            replicatedState.Value = new FixedString4096Bytes(JsonUtility.ToJson(state));
            Changed?.Invoke();
            ConfigureChapterServices();
            RefreshDocuments();
        }

        private void EmitDialogue(CampaignDialogueId id)
        {
            if (!IsServer || id == CampaignDialogueId.None) return;
            CampaignDialogueDefinition definition = Dialogue(id);
            if (!CampaignDialogue.TryRecord(definition, Now, dialogueHistory)) return;
            DialogueRpc(id);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void DialogueRpc(CampaignDialogueId id)
        {
            CampaignHUD.Instance?.EnqueueDialogue(id);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ResetDialogueRpc() => CampaignHUD.Instance?.ResetDialogue();

        private void OnSpecialSpawned(GameObject special)
        {
            if (!IsServer || special == null || state.phase is CampaignPhase.Completed or CampaignPhase.Failed) return;
            SpecialInfectedBase brain = special.GetComponent<SpecialInfectedBase>();
            if (brain == null) return;
            EmitDialogue(brain.Type switch
            {
                SpecialType.Tank => CampaignDialogueId.TankWarning,
                SpecialType.Screamer => CampaignDialogueId.ScreamerWarning,
                SpecialType.Infector => CampaignDialogueId.InfectorWarning,
                _ => CampaignDialogueId.None
            });
        }

        private void EmitPhaseDialogue(CampaignPhase phase, CampaignPhase previous)
        {
            if (phase == previous) return;
            if (phase == CampaignPhase.Exploring)
            {
                EmitDialogue(state.chapter == CampaignChapter.Factory
                    ? CampaignDialogueId.FactoryChapter
                    : state.chapter == CampaignChapter.Asylum ? CampaignDialogueId.AsylumChapter : CampaignDialogueId.LaboratoryChapter);
            }
            else if (phase == CampaignPhase.Encounter)
            {
                EmitDialogue(state.chapter == CampaignChapter.Factory
                    ? CampaignDialogueId.FactoryEncounter
                    : state.chapter == CampaignChapter.Asylum ? CampaignDialogueId.AsylumEncounter : CampaignDialogueId.LabEncounter);
            }
            else if (phase == CampaignPhase.AwaitingParty) EmitDialogue(CampaignDialogueId.Boarding);
            else if (phase == CampaignPhase.Transitioning && state.chapter == CampaignChapter.Asylum) EmitDialogue(CampaignDialogueId.AsylumTransition);
            else if (phase == CampaignPhase.Completed) EmitDialogue(CampaignDialogueId.Completed);
        }

        private void DiscoverAutomaticFiles()
        {
            CampaignFileDefinition[] files = ContentCatalog?.files;
            if (files == null) return;
            for (int i = 0; i < files.Length; i++)
            {
                CampaignFileDefinition file = files[i];
                if (file != null && file.required && file.id != CampaignFileId.None
                    && file.autoDiscoverAfter != CampaignObjectiveId.None
                    && state.Has(file.autoDiscoverAfter))
                    state.discoveredFiles |= CampaignState.FileBit(file.id);
            }
        }
        public void ConfigureChapterServices()
        {
            if (Current == null) return;
            DirectorSpawnService.Instance?.SetCampaignAnchors(Current.anchors, Current.spawnMinimumDistance, Current.spawnMaximumDistance);
            var recovery = FindFirstObjectByType<WorldRecoveryService>();
            recovery?.Configure(Current.recoveries);
            NetworkSpawnManager.Instance?.SetCampaignSpawnPoints(Current.arrivals);
        }
        private void SetPhase(CampaignPhase phase)
        {
            CampaignPhase previous = state.phase;
            state.phase = phase; phaseStarted.Value = Now; boardingSince = closingSince = countdownSince = -1;
            if (phase != CampaignPhase.Transitioning) transferLocked.Value = false;
            if (phase is CampaignPhase.Preparing or CampaignPhase.AwaitingParty or CampaignPhase.Completed or CampaignPhase.Failed)
                AIDirector.Instance?.EndCampaignEncounter();
            holds.Clear();
            if (phase == CampaignPhase.Failed) { ReleaseTankTracking(); ResetDialogueRpc(); }
            if (phase is CampaignPhase.Completed or CampaignPhase.Failed) PublishRunSummary();
            Publish();
            EmitPhaseDialogue(phase, previous);
        }

        private void PublishRunSummary()
        {
            runSummary = new CampaignRunSummary
            {
                result = state.phase,
                durationSeconds = Mathf.Max(0f, (float)(Now - campaignStartedAt)),
                rosterSize = InsertionPartySize,
                survivors = Players.Count(p => p != null && IsInsertionParticipant(p.StablePlayerId.Value) && p.LifeState == PlayerLifeState.Alive),
                teamDowns = state.teamDownedCount,
                zombieKills = Mathf.Max(0, (AIDirector.Instance != null ? AIDirector.Instance.TotalKills : 0) - attemptKillBaseline),
                filesRecorded = CountBits(state.discoveredFiles),
                optionalFilesFound = ContentCatalog?.files?.Count(f => f != null && !f.required && state.HasFile(f.id)) ?? 0,
                optionalFilesTotal = ContentCatalog?.files?.Count(f => f != null && !f.required) ?? 0,
                factoryCaseSecured = state.Has(CampaignObjectiveId.FactoryCase),
                labCaseSecured = state.Has(CampaignObjectiveId.LabCase)
            };
            runSummary.members = InsertionPartyMembers();
            if (IsServer) replicatedSummary.Value = new FixedString4096Bytes(JsonUtility.ToJson(runSummary));
        }

        private void BeginAttempt()
        {
            campaignStartedAt = Now;
            attemptKillBaseline = AIDirector.Instance != null ? AIDirector.Instance.TotalKills : 0;
            state.teamDownedCount = 0;
            runSummary = null;
            replicatedSummary.Value = default;
            dialogueHistory.Clear();
            ResetDialogueRpc();
        }

        private CampaignMemberResult[] InsertionPartyMembers()
        {
            if (insertionRoster == null || insertionRoster.Count == 0) return Array.Empty<CampaignMemberResult>();
            var result = new CampaignMemberResult[insertionRoster.Count];
            for (int i = 0; i < insertionRoster.Count; i++)
            {
                var entry = insertionRoster[i];
                var player = Players.FirstOrDefault(p => p != null && p.StablePlayerId.Value == entry.playerId);
                var life = player != null ? player.LifeState
                    : rosterSnapshots.TryGetValue(entry.playerId, out var saved) ? saved.lifeState : PlayerLifeState.Dead;
                result[i] = new CampaignMemberResult
                {
                    playerId = entry.playerId,
                    character = entry.character,
                    connected = entry.connected,
                    lifeState = life
                };
            }
            return result;
        }

        private static int CountBits(ulong value)
        {
            int count = 0;
            while (value != 0) { count += (int)(value & 1); value >>= 1; }
            return count;
        }

        private void Update()
        {
            if (!IsSpawned || settings == null) return;
            ApplyDoorTargets();
            if(IsServer && doors!=null)
            {
                ulong mask=0;
                for(int i=0;i<doors.Length&&i<64;i++)if(doors[i]!=null&&doors[i].DesiredOpen(state))mask|=1UL<<i;
                openDoorMask.Value=mask;
            }
            if (!IsServer || !NetworkMatchStateManager.IsGameplayActive) return;
            if (dialogueSpecialRegistry == null && SpecialInfectedRegistry.Instance != null)
            {
                dialogueSpecialRegistry = SpecialInfectedRegistry.Instance;
                dialogueSpecialRegistry.OnSpecialSpawned += OnSpecialSpawned;
            }
            if (!initialized)
            {
                if (state.phase == CampaignPhase.Insertion)
                {
                    if (!PrepareInsertionParty()) return;
                }
                else if (!Players.Any(p => p.IsInputReady)) return;
                foreach (var p in Players)
                {
                    knownPlayers.Add(p.StablePlayerId.Value);
                    if (state.phase == CampaignPhase.Insertion && !IsInsertionParticipant(p.StablePlayerId.Value)) p.SetCampaignSpectating();
                }
                initialized = true; phaseStarted.Value = Now; ConfigureChapterServices();
                if (state.phase == CampaignPhase.Insertion) EmitDialogue(CampaignDialogueId.InsertionBriefing);
            }
            updateBudget += Time.deltaTime;
            if (updateBudget < .1f) return;
            updateBudget = 0;
            TrackPlayers();
            UpdateHolds();
            if (state.phase is not CampaignPhase.Insertion and not CampaignPhase.Transitioning and not CampaignPhase.Completed and not CampaignPhase.Failed
                && Players.Any() && !Players.Any(p => p.LifeState == PlayerLifeState.Alive) && !HasDisconnectGrace())
            { SetPhase(CampaignPhase.Failed); return; }
            UpdateTankEncounter();
            switch (state.phase)
            {
                case CampaignPhase.Insertion:
                    if (Now - phaseStarted.Value >= settings.insertionSeconds)
                    { SetPhase(CampaignPhase.Exploring); SaveCheckpoint(0); }
                    break;
                case CampaignPhase.Preparing: UpdatePreparation(); break;
                case CampaignPhase.Encounter: UpdateEncounter(); break;
                case CampaignPhase.AwaitingParty: UpdateBoarding(); break;
                case CampaignPhase.Transitioning: UpdateTransition(); break;
            }
        }
        private void TrackPlayers()
        {
            foreach (var p in Players)
            {
                if (!p.IsInputReady) continue;
                ulong id = p.StablePlayerId.Value;
                clientPlayers[p.OwnerClientId] = id;
                if (knownPlayers.Add(id))
                {
                    if (state.chapter != CampaignChapter.Factory || state.phase != CampaignPhase.Insertion || !IsInsertionParticipant(id))
                        p.SetCampaignSpectating();
                }
                ReconnectInsertionParticipant(p);
                disconnected.Remove(id);
                RememberPlayerSnapshot(p.CaptureRuntimeSnapshot());
            }
        }
        public void RememberPlayerSnapshot(PlayerRuntimeSnapshot snapshot)
        {
            if (!IsServer || !snapshot.sessionPlayerId.IsValid || snapshot.lifeState == PlayerLifeState.Spectating) return;
            rosterSnapshots[snapshot.sessionPlayerId.Value] = snapshot;
        }
        private void OnDisconnected(ulong client)
        {
            DisconnectInsertionParticipant(client);
            holds.Remove(client); releaseRequired.Remove(client); ready.Remove(client); readyCount.Value = ready.Count;
            // A grace timer prevents a transient disconnect from immediately closing a gate.
            if (clientPlayers.TryGetValue(client, out ulong id)) disconnected[id] = Now + settings.disconnectGraceSeconds;
            clientPlayers.Remove(client);
        }
        private bool HasDisconnectGrace() => disconnected.Any(p => p.Value > Now);

        public bool AllowsAnchor(DirectorSpawnAnchor anchor) => Current != null && Current.anchors != null && Array.IndexOf(Current.anchors, anchor) >= 0;
        public bool AllowsSpecial(string typeName)
        {
            if (SuppressSpawns || state.chapter == CampaignChapter.Asylum
                || CampaignRules.ShouldArmTank(state) || state.tankStage is CampaignTankStage.Pending or CampaignTankStage.Active
                || SpecialInfectedRegistry.Instance?.HasLivingSpecial == true) return false;
            return !string.IsNullOrEmpty(typeName) && typeName.IndexOf("tank", StringComparison.OrdinalIgnoreCase) < 0;
        }
        private void UpdatePreparation()
        {
            if (state.tankStage == CampaignTankStage.Pending) { countdownSince = -1; return; }
            var people = Players.Where(p => p.LifeState != PlayerLifeState.Dead && p.LifeState != PlayerLifeState.Spectating).ToArray();
            bool evadeTank = state.chapter == CampaignChapter.Asylum && state.tankStage == CampaignTankStage.Active;
            bool all = people.Length > 0 && !HasDisconnectGrace() && people.All(p => p.CanUseCombat
                && Current.Contains(Current.preparationArea, p.transform.position) && (evadeTank || ready.Contains(p.OwnerClientId)));
            if (!all) { countdownSince = -1; return; }
            if (countdownSince < 0) countdownSince = Now;
            if (Now - countdownSince < settings.preparationSeconds) return;
            SaveCheckpoint((int)state.chapter * 2 + 1, preparing: true);
            ready.Clear(); readyCount.Value = 0;
            SetPhase(CampaignPhase.Encounter);
            AIDirector.Instance?.RequestCrescendo("campaign finale", settings.Duration(state.chapter));
        }
        private void UpdateEncounter()
        {
            double elapsed = Now - phaseStarted.Value;
            if (state.chapter == CampaignChapter.Laboratory && elapsed >= 55 && !state.evidenceTransmitted)
            { state.evidenceTransmitted = true; Publish(); EmitDialogue(CampaignDialogueId.EvidenceTransmitted); }
            if (elapsed >= settings.Duration(state.chapter)) SetPhase(CampaignPhase.AwaitingParty);
        }
        private void UpdateBoarding()
        {
            if (!CampaignRules.TankAllowsExit(state)) { boardingSince = -1; return; }
            var people = Players.Where(p => p.LifeState != PlayerLifeState.Dead && p.LifeState != PlayerLifeState.Spectating).ToArray();
            bool all = people.Length > 0 && !HasDisconnectGrace() && people.All(p => p.CanUseCombat && Current.Contains(Current.boardingArea, p.transform.position));
            if (!all) { boardingSince = -1; return; }
            if (boardingSince < 0) boardingSince = Now;
            if (Now - boardingSince >= settings.boardingSeconds) { transferred = false; SetPhase(CampaignPhase.Transitioning); }
        }
        private void UpdateTransition()
        {
            if (!transferLocked.Value)
            {
                var people = Players.Where(p => !p.IsDead).ToArray();
                if (HasDisconnectGrace() || people.Length == 0 || people.Any(p => !p.CanUseCombat || !Current.Contains(Current.boardingArea, p.transform.position)))
                { SetPhase(CampaignPhase.AwaitingParty); return; }
            }
            if (Current.entryDoor != null && !Current.entryDoor.IsClosed) { closingSince = -1; return; }
            if (closingSince < 0) { closingSince = Now; transferLocked.Value = true; }
            if (Now - closingSince < settings.transferSeconds) return;
            if (state.chapter == CampaignChapter.Laboratory) { ClearEnemies(); SetPhase(CampaignPhase.Completed); return; }
            if (!transferred)
            {
                ClearEnemies();
                var next = chapters[(int)state.chapter + 1];
                transferred = true;
                int slot = 0;
                foreach (var p in Players)
                {
                    Transform target = next.arrivals[slot++ % next.arrivals.Length];
                    // The factory connection is traversed on foot. The B2 cabin relocates the same network objects.
                    bool relocate = state.chapter == CampaignChapter.Asylum || p.IsDead;
                    p.RelocateCampaign(relocate ? target.position : p.transform.position, relocate ? target.rotation : p.transform.rotation);
                    if (p.IsDead) p.ReviveCampaign(settings.reviveHealth);
                }
                return;
            }
            if (HasDisconnectGrace()) return;
            if (Players.Any(p => p.CampaignTransferPending || !p.IsInputReady))
            {
                if (Now - closingSince > settings.transferSeconds + 20) SetPhase(CampaignPhase.Failed);
                return;
            }
            state.chapter++;
            state.tankStage = CampaignTankStage.Dormant;
            SetPhase(CampaignPhase.Exploring);
            readingUntil = Now + settings.readingGraceSeconds;
            SaveCheckpoint((int)state.chapter * 2);
        }

        public PlayerRuntimeSnapshot PrepareReconnect(PlayerRuntimeSnapshot saved, ulong client)
        {
            // Retry may have rewound the checkpoint while this peer was disconnected.
            if (rosterSnapshots.TryGetValue(saved.sessionPlayerId.Value, out var retained)) saved = retained;
            knownPlayers.Add(saved.sessionPlayerId.Value);
            clientPlayers[client] = saved.sessionPlayerId.Value;
            disconnected.Remove(saved.sessionPlayerId.Value);
            var destination = chapters[(int)SnapshotChapter];
            bool sameChapter = saved.campaignChapter == (byte)((int)SnapshotChapter + 1);
            // Validate after choosing the retained snapshot; otherwise it can overwrite the
            // spawn service's validated position with a stale wall/fall/closed-chapter pose.
            if (sameChapter && CampaignPlacement.TryReconnectPosition(destination, saved.position, saved.sessionPlayerId.Value, out var safe))
            { saved.position = safe; return saved; }
            var target = destination.arrivals[(int)(client % (ulong)destination.arrivals.Length)];
            saved.position = target.position; saved.rotation = target.rotation;
            saved.campaignChapter = (byte)((int)SnapshotChapter + 1);
            if (!sameChapter && saved.lifeState != PlayerLifeState.Alive)
            { saved.lifeState = PlayerLifeState.Alive; saved.health = settings.reviveHealth; saved.lifeStateDeadline = 0; }
            return saved;
        }
        private void ApplyDoorTargets()
        {
            if (chapters == null) return;
            foreach (var chapter in chapters)
            {
                if (chapter == null) continue;
                bool active = chapter.chapter == state.chapter;
                bool before = chapter.chapter < state.chapter;
                chapter.entryDoor?.SetOpen(active && state.phase == CampaignPhase.AwaitingParty);
                chapter.exitDoor?.SetOpen(before || (active && state.phase == CampaignPhase.Completed));
            }
        }
        public bool ResolvedDoorOpen(CampaignDoor door)
        {
            int index=doors!=null?Array.IndexOf(doors,door):-1;
            return index>=0&&index<64&&(openDoorMask.Value&(1UL<<index))!=0;
        }

        public CampaignInteractable Objective(CampaignObjectiveId id) => chapters?.SelectMany(c => c.objectives ?? Array.Empty<CampaignInteractable>()).FirstOrDefault(o => o != null && o.objectiveId == id);
        public CampaignInteractable Document(CampaignFileId id) => fileSources?.FirstOrDefault(o => o != null && o.fileId == id);

        private CampaignInteractable ReachableFileSource(CampaignFileDefinition definition, PlayerHealth actor)
        {
            CampaignInteractable source = (fileSources ?? Array.Empty<CampaignInteractable>()).FirstOrDefault(item =>
                item != null && item.isActiveAndEnabled && item.ProvidesFile(definition, ContentCatalog)
                && item.InReach(actor, settings.interactionRange));
            if (source != null || !definition.required) return source;
            // Required clues may be surfaced by a terminal/console instead of a loose paper pickup.
            // The catalog's objective.file link is the authority; never accept an ID without a reachable scene object.
            return (Current != null ? Current.objectives ?? Array.Empty<CampaignInteractable>() : Array.Empty<CampaignInteractable>())
                .FirstOrDefault(item => item != null && item.isActiveAndEnabled
                    && item.ProvidesFile(definition, ContentCatalog)
                    && item.InReach(actor, settings.interactionRange));
        }

        private void RefreshDocuments()
        {
            foreach (var item in fileSources ?? Array.Empty<CampaignInteractable>())
                if (item != null) item.SetRecorded(state.HasFile(item.fileId));
        }

        public void RegisterTeamDowned(PlayerHealth player)
        {
            if (!IsServer || player == null || !IsInsertionParticipant(player.StablePlayerId.Value)) return;
            if (state.teamDownedCount < ushort.MaxValue) state.teamDownedCount++;
            Publish();
            EmitDialogue(CampaignDialogueId.OperatorDowned);
        }

        public void RequestDiscoverFile(CampaignFileId id) { if (IsSpawned) DiscoverFileRpc((byte)id); }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void DiscoverFileRpc(byte rawId, RpcParams rpc = default)
        {
            if (!Enum.IsDefined(typeof(CampaignFileId), rawId)) return;
            CampaignFileId id = (CampaignFileId)rawId;
            CampaignFileDefinition definition = ContentCatalog?.FindFile(id);
            if (definition == null || BlocksInput || settings == null) return;
            if (!TryPlayer(rpc.Receive.SenderClientId, out var actor) || !actor.CanUseCombat) return;
            // The client sends only an ID. The server chooses a registered, reachable source;
            // optional lore never receives the objective/console fallback.
            CampaignInteractable document = ReachableFileSource(definition, actor);
            CampaignResult result = CampaignRules.CanDiscover(state, definition, actor.LifeState == PlayerLifeState.Alive,
                document != null && IsInsertionParticipant(actor.StablePlayerId.Value), document != null);
            if (result == CampaignResult.Accepted)
            {
                state.discoveredFiles |= CampaignState.FileBit(id);
                Publish();
            }
            if (result == CampaignResult.Accepted || result == CampaignResult.AlreadyDone)
                FileDiscoveryFeedbackRpc(id, result, state.revision, RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
            else Feedback(rpc.Receive.SenderClientId, result);
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void FileDiscoveryFeedbackRpc(CampaignFileId id, CampaignResult result, uint revision, RpcParams rpc = default)
        {
            TeamInventoryUI.Instance?.AcknowledgeFile(id, revision);
            CampaignHUD.Instance?.ShowMessage(result == CampaignResult.AlreadyDone ? "FILE ALREADY RECORDED" : "NEW FILE ADDED");
        }
        public void RequestPowerSelection(int mask) => PowerSelectionRpc(mask);
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void PowerSelectionRpc(int mask, RpcParams rpc = default)
        {
            if (CampaignRules.CanUse(state, CampaignObjectiveId.LabPower) != CampaignResult.Accepted
                || !CampaignRules.CanSelectPower(mask) || !TryPlayer(rpc.Receive.SenderClientId, out var actor)) return;
            var item = Objective(CampaignObjectiveId.LabPower);
            if (item == null || !actor.CanUseCombat || !item.InReach(actor, settings.interactionRange)) return;
            state.powerSelection = (byte)mask; Publish();
        }
        public void RequestHold(CampaignObjectiveId id, int first, int second, bool held) => HoldRpc((byte)id, first, second, held);
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void HoldRpc(byte objective, int first, int second, bool held, RpcParams rpc = default)
        {
            ulong client = rpc.Receive.SenderClientId;
            if (!held) { holds.Remove(client); releaseRequired.Remove(client); return; }
            if (releaseRequired.Contains(client)) return;
            if (!TryPlayer(client, out var actor)) return;
            var id = (CampaignObjectiveId)objective;
            var item = Objective(id);
            var result = CampaignRules.CanUse(state, id);
            if (!actor.CanUseCombat || BlocksInput) return;
            if (item == null || result != CampaignResult.Accepted) { Feedback(client, result); return; }
            if (!item.InReach(actor, settings.interactionRange)) { Feedback(client, CampaignResult.OutOfRange); return; }
            if (holds.TryGetValue(client, out var existing) && existing.action == 0 && existing.objective == id && existing.first == first && existing.second == second)
            { existing.lease = Now + .7; return; }
            if (holds.Any(h => h.Key != client && h.Value.action == 0 && h.Value.objective == id)) { Feedback(client, CampaignResult.Busy); return; }
            holds[client] = NewHold(actor, id, first, second);
        }
        private Hold NewHold(PlayerHealth p, CampaignObjectiveId id = CampaignObjectiveId.None, int first = 0, int second = 0)
        {
            var snapshot = p.CaptureRuntimeSnapshot();
            return new Hold { objective = id, first = first, second = second, started = Now, lease = Now + .7, damage = p.DamageRevision,
                shot0 = snapshot.weaponSlot0.lastAcceptedFireSequence, shot1 = snapshot.weaponSlot1.lastAcceptedFireSequence };
        }
        private void UpdateHolds()
        {
            foreach (var pair in holds.ToArray())
            {
                var h = pair.Value;
                if (!TryPlayer(pair.Key, out var actor) || !actor.CanUseCombat || h.lease < Now || h.damage != actor.DamageRevision)
                { holds.Remove(pair.Key); releaseRequired.Add(pair.Key); Feedback(pair.Key, CampaignResult.Interrupted); continue; }
                var snapshot = actor.CaptureRuntimeSnapshot();
                if (snapshot.weaponSlot0.lastAcceptedFireSequence != h.shot0 || snapshot.weaponSlot1.lastAcceptedFireSequence != h.shot1)
                { holds.Remove(pair.Key); releaseRequired.Add(pair.Key); Feedback(pair.Key, CampaignResult.Interrupted); continue; }
                if (h.action != 0) { UpdateAid(pair.Key, actor, h); continue; }
                var item = Objective(h.objective);
                if (item == null || !item.InReach(actor, settings.interactionRange)) { holds.Remove(pair.Key); releaseRequired.Add(pair.Key); Feedback(pair.Key, CampaignResult.Interrupted); continue; }
                if (Now - h.started < item.holdSeconds) continue;
                bool preparation = CampaignRules.IsEncounter(h.objective);
                if (preparation && !Current.Contains(Current.preparationArea, actor.transform.position)) { holds.Remove(pair.Key); continue; }
                var result = CampaignRules.TryComplete(state, h.objective, h.first, h.second);
                holds.Remove(pair.Key);
                Feedback(pair.Key, result);
                if (result != CampaignResult.Accepted) continue;
                if (preparation) { ready.Clear(); readyCount.Value = 0; phaseStarted.Value = Now; }
                else if (h.objective is CampaignObjectiveId.FactoryGenerator or CampaignObjectiveId.FactoryShipping or CampaignObjectiveId.FactoryCase or CampaignObjectiveId.LabPower)
                {
                    if (Now >= nextBeatAt) { AIDirector.Instance?.RequestCrescendo("campaign objective", 15); nextBeatAt = Now + settings.beatRestSeconds + 15; }
                }
                else readingUntil = Math.Max(readingUntil, Now + settings.readingGraceSeconds);
                Publish();
                EmitDialogue(CampaignDialogue.ForObjective(h.objective));
                if (h.objective == CampaignObjectiveId.LabTransmit)
                {
                    EmitDialogue(CampaignDialogueId.ClientSuppressionOrder);
                    EmitDialogue(CampaignDialogueId.MiraRejectsOrder);
                }
            }
        }
        public void RequestAid(ulong target, bool medicine, bool held) => AidRpc(target, medicine, held);
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void AidRpc(ulong target, bool medicine, bool held, RpcParams rpc = default)
        {
            ulong client = rpc.Receive.SenderClientId;
            if (!held) { holds.Remove(client); releaseRequired.Remove(client); return; }
            if (medicine || releaseRequired.Contains(client)) return;
            if (BlocksInput || !TryPlayer(client, out var actor) || !actor.CanUseCombat) return;
            const byte action = 1;
            if (holds.TryGetValue(client, out var old) && old.action == action && old.target == target) { old.lease = Now + .7; return; }
            var h = NewHold(actor); h.action = action; h.target = target; holds[client] = h;
        }
        private void UpdateAid(ulong client, PlayerHealth actor, Hold h)
        {
            var target = Players.FirstOrDefault(p => p.NetworkObjectId == h.target);
            if (target == null || target.LifeState != PlayerLifeState.Downed || Vector3.Distance(actor.transform.position, target.transform.position) > settings.interactionRange
                || Physics.Linecast(actor.transform.position + Vector3.up * 1.4f, target.transform.position + Vector3.up * .8f, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<PlayerHealth>() != target)
            { holds.Remove(client); releaseRequired.Add(client); Feedback(client, CampaignResult.Interrupted); return; }
            if (Now - h.started >= settings.reviveSeconds) { target.ReviveCampaign(settings.reviveHealth); holds.Remove(client); Feedback(client, CampaignResult.Accepted); EmitDialogue(CampaignDialogueId.Revived); }
        }
        public void RequestReady(bool value) => ReadyRpc(value);
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void ReadyRpc(bool value, RpcParams rpc = default)
        {
            if (state.tankStage is CampaignTankStage.Active or CampaignTankStage.Pending) return;
            if (state.phase != CampaignPhase.Preparing || !TryPlayer(rpc.Receive.SenderClientId, out var actor) || !actor.CanUseCombat) return;
            if (value) ready.Add(rpc.Receive.SenderClientId); else ready.Remove(rpc.Receive.SenderClientId);
            readyCount.Value = ready.Count;
        }
        private bool TryPlayer(ulong client, out PlayerHealth player)
        {
            player = null;
            if (NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(client, out var c) || c.PlayerObject == null) return false;
            player = c.PlayerObject.GetComponent<PlayerHealth>(); return player != null;
        }
        private void Feedback(ulong target, CampaignResult result) => FeedbackRpc(target, (byte)result);
        [Rpc(SendTo.ClientsAndHost)]
        private void FeedbackRpc(ulong target, byte result)
        { if (NetworkManager.LocalClientId == target) CampaignHUD.Instance?.ShowResult((CampaignResult)result); }

        public bool IsSupplyClaimed(string id) => claimedSupplies != null && claimedSupplies.Contains(new FixedString64Bytes(id));
        public void RequestSupply(string id, bool medicine) => SupplyRpc(new FixedString64Bytes(id), medicine);
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SupplyRpc(FixedString64Bytes id, bool medicine, RpcParams rpc = default)
        {
            var item = supplies?.FirstOrDefault(s => s != null && s.supplyId == id.ToString());
            if (item == null || !item.IsAvailable || item.chapter != state.chapter || BlocksInput || IsSupplyClaimed(item.supplyId) || !TryPlayer(rpc.Receive.SenderClientId, out var p) || !p.CanUseCombat || !item.InReach(p, settings.interactionRange)) return;
            if (medicine && !item.medicineOnly && !item.chooseReward) return;
            var inventory = p.GetComponent<SurvivalInventory>();
            if (inventory != null && inventory.IsUsingItem) return;
            PickupType reward = medicine || item.medicineOnly ? PickupType.Medkit : item.survivalReward;
            bool granted = SurvivalRules.Capacity(reward) > 0
                ? inventory != null && inventory.TryAdd(reward, 1)
                : reward == PickupType.Ammo && item.TryGiveAmmoServer(p.GetComponent<WeaponFireHandler>());
            if (granted)
            {
                claimedSupplies.Add(id);
                SupplyFeedbackRpc(item.transform.position, (int)reward - (int)PickupType.FragGrenade);
                Feedback(rpc.Receive.SenderClientId, CampaignResult.Accepted);
            }
            else if (reward == PickupType.Ammo)
                Feedback(rpc.Receive.SenderClientId, CampaignResult.AmmoUnavailable);
        }
        [Rpc(SendTo.ClientsAndHost)]
        private void SupplyFeedbackRpc(Vector3 point, int index)
        {
            var catalog = SurvivalCatalog.Load();
            if (catalog != null && index >= 0 && index < catalog.pickupClips.Length)
                SurvivalEffects.PlayOneShot(catalog.pickupClips[index], point, .4f);
        }
        private void SaveCheckpoint(int index, bool preparing = false)
        {
            state.checkpoint = index;
            if(index % 2 == 0) state.supplyPartySize = Mathf.Clamp(Players.Count(p=>p.LifeState!=PlayerLifeState.Spectating),1,4);
            var saved = state.Copy(); saved.phase = CampaignPhase.Exploring;
            if (saved.tankStage == CampaignTankStage.Active) saved.tankStage = CampaignTankStage.Pending;
            if (preparing)
            {
                var last = state.chapter == CampaignChapter.Factory ? CampaignObjectiveId.FactoryRoute : state.chapter == CampaignChapter.Asylum ? CampaignObjectiveId.AsylumLift : CampaignObjectiveId.LabTransmit;
                saved.completed &= ~CampaignState.Bit(last);
                if (state.chapter == CampaignChapter.Laboratory) saved.tankStage = CampaignTankStage.Dormant;
            }
            var connected = Players.Where(p => p.LifeState != PlayerLifeState.Spectating)
                .OrderBy(p => p.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? 0 : 1).ToArray();
            foreach (var p in connected) RememberPlayerSnapshot(p.CaptureRuntimeSnapshot());
            checkpoint = new CampaignCheckpoint { state = saved, players = connected.Select(CampaignPlayerSave.Capture).ToList() };
            foreach (var retained in rosterSnapshots.Values.OrderBy(s => s.sessionPlayerId.Value))
            {
                if (checkpoint.players.Count >= 4) break;
                if (checkpoint.players.Any(p => p.playerId == retained.sessionPlayerId.Value)) continue;
                var absent = CampaignPlayerSave.Capture(retained);
                // No teammate can revive a disconnected actor after the reservation grace.
                if (absent.lifeState == PlayerLifeState.Downed) { absent.lifeState = PlayerLifeState.Dead; absent.lifeStateDeadline = 0; }
                checkpoint.players.Add(absent);
            }
            foreach (var id in claimedSupplies) checkpoint.claimedSupplies.Add(id.ToString());
            // Entering a new session must not erase the existing continue slot before the host can load it.
            try { if (index != 0 || !System.IO.File.Exists(CampaignCheckpoint.SavePath)) checkpoint.Save(); }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { Debug.LogError("Campaign save failed: " + e.Message); }
            Publish();
        }
        public void RequestRetry(bool fromDisk = false) => RetryRpc(fromDisk);
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RetryRpc(bool fromDisk, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId || (state.phase != CampaignPhase.Failed && !fromDisk)) return;
            if (fromDisk && !CanResumeSavedCampaign) return;
            if (fromDisk && !CampaignCheckpoint.TryLoad(out checkpoint, out var error)) { Debug.LogWarning(error); return; }
            if (checkpoint == null) return;
            ClearEnemies(); holds.Clear(); releaseRequired.Clear(); ready.Clear(); readyCount.Value = 0;
            state = checkpoint.state.Copy(); claimedSupplies.Clear();
            if (state.Has(CampaignObjectiveId.FactoryCase)) state.factoryTankDefeated = true; // Old checkpoints predate scripted Tanks.
            if (state.tankStage == CampaignTankStage.Active) state.tankStage = CampaignTankStage.Pending;
            transferLocked.Value = false; transferred = false;
            rosterSnapshots.Clear();
            foreach (var savedPlayer in checkpoint.players)
                rosterSnapshots[savedPlayer.playerId] = savedPlayer.Restore(new SessionPlayerId(savedPlayer.playerId));
            foreach (string id in checkpoint.claimedSupplies) claimedSupplies.Add(new FixedString64Bytes(id));
            int slot = 0;
            foreach (var p in Players)
            {
                var saved = checkpoint.players.FirstOrDefault(s => s.playerId == p.StablePlayerId.Value);
                // Only the local host may inherit a previous local session's host snapshot.
                if (saved == null && fromDisk && p.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId)
                { saved = checkpoint.players[0]; rosterSnapshots.Remove(saved.playerId); }
                if (saved != null)
                {
                    var restored = saved.Restore(p.StablePlayerId);
                    rosterSnapshots[p.StablePlayerId.Value] = restored;
                    p.RestoreCampaignSnapshot(PrepareReconnect(restored, p.OwnerClientId));
                }
                else p.SetCampaignSpectating();
                slot++;
            }
            transferLocked.Value = false; transferred = false; disconnected.Clear();
            BeginAttempt();
            phaseStarted.Value = Now; readingUntil = Now + settings.readingGraceSeconds; Publish();
            CheckpointRestoredRpc();
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void CheckpointRestoredRpc()
        {
            TeamInventoryUI.Instance?.CancelPendingFile();
            CampaignHUD.Instance?.ShowMessage("CHECKPOINT RESTORED");
        }
        private void ClearEnemies()
        {
            ReleaseTankTracking();
            AIDirector.Instance?.EndCampaignEncounter();
            foreach (var enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (enemy != null && enemy.gameObject.activeInHierarchy) ZombiePoolManager.Instance?.ReturnZombie(enemy.gameObject);
            AIDirector.Instance?.ReconcileActivePopulation();
        }
    }
}
