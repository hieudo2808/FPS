using System;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FactoryMissionController : NetworkBehaviour
    {
        public static FactoryMissionController Instance { get; private set; }

        [Header("Operation Cold Ledger")]
        [SerializeField, Min(1f)] private float insertionDurationSeconds = 18f;
        [SerializeField, Min(10f)] private float extractionDurationSeconds = 85f;
        [SerializeField, Min(0.05f)] private float extractionSyncInterval = 0.25f;

        private readonly NetworkVariable<FactoryMissionState> missionState = new(
            FactoryMissionState.Insertion,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ushort> completedObjectiveBits = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> extractionRemainingSeconds = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> stateStartedServerTime = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly FactoryMissionProgression progression = new();
        private FactoryMissionState localState = FactoryMissionState.Insertion;
        private FactoryObjectiveFlags localCompletedObjectives;
        private float localExtractionRemaining;
        private double localStateStartedTime;
        private float extractionSyncTimer;

        public event Action<FactoryMissionState, FactoryMissionState> MissionStateChanged;
        public event Action<FactoryObjectiveFlags, FactoryObjectiveFlags> CompletedObjectivesChanged;
        public event Action<FactoryObjectiveId, FactoryInteractionResult> ObjectiveResolved;

        private bool HasBoundNetworkState => IsSpawned
            && NetworkManager != null
            && NetworkManager.IsListening;

        public FactoryMissionState State => HasBoundNetworkState ? missionState.Value : localState;
        public FactoryObjectiveFlags CompletedObjectives => HasBoundNetworkState
            ? (FactoryObjectiveFlags)completedObjectiveBits.Value
            : localCompletedObjectives;
        public float ExtractionRemainingSeconds => HasBoundNetworkState
            ? extractionRemainingSeconds.Value
            : localExtractionRemaining;
        public double StateStartedServerTime => HasBoundNetworkState
            ? stateStartedServerTime.Value
            : localStateStartedTime;
        public bool IsCinematicBlockingGameplay => State == FactoryMissionState.Insertion
            || State == FactoryMissionState.Completed;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ResetLocalMission();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            missionState.OnValueChanged += HandleNetworkStateChanged;
            completedObjectiveBits.OnValueChanged += HandleNetworkObjectivesChanged;

            if (IsServer)
            {
                progression.Reset();
                extractionSyncTimer = 0f;
                SyncAuthoritativeState(forceTimestamp: true);
            }

            ApplyPresentationState(State, State);
        }

        public override void OnNetworkDespawn()
        {
            missionState.OnValueChanged -= HandleNetworkStateChanged;
            completedObjectiveBits.OnValueChanged -= HandleNetworkObjectivesChanged;
        }

        private void Update()
        {
            if (!CanRunAuthority())
                return;

            if (progression.State == FactoryMissionState.Insertion)
            {
                if (GetServerTime() - localStateStartedTime >= insertionDurationSeconds
                    && progression.FinishInsertion())
                {
                    SyncAuthoritativeState(forceTimestamp: true);
                }
                return;
            }

            if (progression.State != FactoryMissionState.ExtractionActive)
                return;

            localExtractionRemaining = Mathf.Max(0f, localExtractionRemaining - Time.deltaTime);
            extractionSyncTimer += Time.deltaTime;

            if (localExtractionRemaining <= 0f)
            {
                if (progression.CompleteExtraction())
                    SyncAuthoritativeState(forceTimestamp: true);
                return;
            }

            if (extractionSyncTimer >= extractionSyncInterval)
            {
                extractionSyncTimer = 0f;
                if (HasBoundNetworkState)
                    extractionRemainingSeconds.Value = localExtractionRemaining;
            }
        }

        public bool CanInteract(FactoryObjectiveId objectiveId)
        {
            if (HasBoundNetworkState && !IsServer)
            {
                FactoryObjectiveFlags flag = FactoryMissionProgression.ToFlag(objectiveId);
                if (flag == FactoryObjectiveFlags.None || (CompletedObjectives & flag) != 0)
                    return false;

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

            return progression.CanInteract(objectiveId);
        }

        public void RequestInteraction(FactoryObjectiveId objectiveId, NetworkObject interactorObject)
        {
            if (HasBoundNetworkState)
            {
                RequestInteractionRpc((byte)objectiveId);
                return;
            }

            TryResolveInteraction(objectiveId, interactorObject);
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractionRpc(byte objectiveValue, RpcParams rpcParams = default)
        {
            if (!Enum.IsDefined(typeof(FactoryObjectiveId), objectiveValue))
                return;

            if (!TryGetSenderPlayer(rpcParams.Receive.SenderClientId, out NetworkObject playerObject))
            {
                ObjectiveResolved?.Invoke((FactoryObjectiveId)objectiveValue, FactoryInteractionResult.InvalidInteractor);
                return;
            }

            TryResolveInteraction((FactoryObjectiveId)objectiveValue, playerObject);
        }

        public FactoryInteractionResult CompleteObjectiveForTests(FactoryObjectiveId objectiveId)
        {
            return TryResolveInteraction(objectiveId, null, skipSpatialValidation: true);
        }

        public void FinishInsertionForTests()
        {
            if (progression.FinishInsertion())
                SyncAuthoritativeState(forceTimestamp: true);
        }

        private FactoryInteractionResult TryResolveInteraction(
            FactoryObjectiveId objectiveId,
            NetworkObject interactorObject,
            bool skipSpatialValidation = false)
        {
            if (!CanRunAuthority())
                return FactoryInteractionResult.InvalidInteractor;

            FactoryObjectiveInteractable interactable = FindObjective(objectiveId);
            if (!skipSpatialValidation)
            {
                if (interactable == null)
                    return FactoryInteractionResult.UnknownObjective;

                FactoryInteractionResult validation = interactable.ValidateServerInteraction(interactorObject);
                if (validation != FactoryInteractionResult.Accepted)
                {
                    ObjectiveResolved?.Invoke(objectiveId, validation);
                    return validation;
                }
            }

            FactoryMissionState previousState = progression.State;
            bool utilitiesBefore = progression.UtilitiesComplete;
            bool logisticsBefore = progression.LogisticsComplete;
            FactoryInteractionResult result = progression.TryComplete(objectiveId);

            if (result == FactoryInteractionResult.Accepted)
            {
                if (progression.State == FactoryMissionState.ExtractionActive)
                {
                    localExtractionRemaining = extractionDurationSeconds;
                    extractionSyncTimer = 0f;
                }

                bool stateChanged = previousState != progression.State;
                SyncAuthoritativeState(forceTimestamp: stateChanged);
                RequestDirectorBeat(objectiveId, utilitiesBefore, logisticsBefore);
            }

            ObjectiveResolved?.Invoke(objectiveId, result);
            return result;
        }

        private void RequestDirectorBeat(
            FactoryObjectiveId objectiveId,
            bool utilitiesWereComplete,
            bool logisticsWereComplete)
        {
            if (AIDirector.Instance == null)
                return;

            if (!utilitiesWereComplete && progression.UtilitiesComplete)
                AIDirector.Instance.RequestCrescendo("Utilities restored", 20f);
            if (!logisticsWereComplete && progression.LogisticsComplete)
                AIDirector.Instance.RequestCrescendo("Manifest recovered", 20f);
            if (objectiveId == FactoryObjectiveId.ColdStorageSample)
                AIDirector.Instance.RequestCrescendo("T-9 sample secured", 28f);
            if (objectiveId == FactoryObjectiveId.ExtractionRadio)
                AIDirector.Instance.RequestCrescendo("Extraction finale", extractionDurationSeconds);
        }

        private FactoryObjectiveInteractable FindObjective(FactoryObjectiveId objectiveId)
        {
            FactoryObjectiveInteractable[] objectives = GetComponentsInChildren<FactoryObjectiveInteractable>(true);
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i].ObjectiveId == objectiveId)
                    return objectives[i];
            }

            return null;
        }

        private bool TryGetSenderPlayer(ulong senderClientId, out NetworkObject playerObject)
        {
            playerObject = null;
            if (NetworkManager == null
                || !NetworkManager.IsListening
                || !NetworkManager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client))
            {
                return false;
            }

            playerObject = client.PlayerObject;
            return playerObject != null;
        }

        private void SyncAuthoritativeState(bool forceTimestamp)
        {
            FactoryMissionState previous = localState;
            FactoryObjectiveFlags previousObjectives = localCompletedObjectives;
            localState = progression.State;
            localCompletedObjectives = progression.CompletedObjectives;

            if (forceTimestamp)
                localStateStartedTime = GetServerTime();

            if (HasBoundNetworkState)
            {
                if (forceTimestamp)
                    stateStartedServerTime.Value = localStateStartedTime;
                completedObjectiveBits.Value = (ushort)localCompletedObjectives;
                extractionRemainingSeconds.Value = localExtractionRemaining;
                missionState.Value = localState;
            }

            if (previous != localState && !HasBoundNetworkState)
                ApplyPresentationState(previous, localState);
            if (previousObjectives != localCompletedObjectives && !HasBoundNetworkState)
                CompletedObjectivesChanged?.Invoke(previousObjectives, localCompletedObjectives);
        }

        private void HandleNetworkStateChanged(FactoryMissionState previous, FactoryMissionState next)
        {
            localState = next;
            localStateStartedTime = stateStartedServerTime.Value;
            ApplyPresentationState(previous, next);
        }

        private void HandleNetworkObjectivesChanged(ushort previous, ushort next)
        {
            localCompletedObjectives = (FactoryObjectiveFlags)next;
            CompletedObjectivesChanged?.Invoke(
                (FactoryObjectiveFlags)previous,
                localCompletedObjectives);
        }

        private void ApplyPresentationState(FactoryMissionState previous, FactoryMissionState next)
        {
            MissionStateChanged?.Invoke(previous, next);
        }

        private void ResetLocalMission()
        {
            progression.Reset();
            localState = progression.State;
            localCompletedObjectives = FactoryObjectiveFlags.None;
            localExtractionRemaining = 0f;
            localStateStartedTime = GetServerTime();
        }

        private bool CanRunAuthority()
        {
            return !HasBoundNetworkState || IsServer;
        }

        private double GetServerTime()
        {
            return HasBoundNetworkState ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        }
    }
}
