using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum InfectionStage
    {
        None = 0,         // 0%: Khỏe mạnh
        Incubation = 1,   // 1% - 30%: Ủ bệnh (Pulse nhẹ, HUD icon)
        Symptomatic = 2,  // 31% - 70%: Phát triệu chứng (-5% Speed, +10% Sway, -10% Reload)
        Critical = 3,     // 71% - 99%: Nguy kịch (Ho phát Noise 15m, -10% Speed, +20% Sway)
        Sepsis = 4        // 100%: Noise 30m, -5 HP/5s, khóa sprint; không có forced-death deadline
    }

    public class PlayerInfectionController : NetworkBehaviour
    {
        public const float MaxInfection = 100f;
        public const float IncubationThreshold = 1f;
        public const float SymptomaticThreshold = 31f;
        public const float CriticalThreshold = 71f;
        public const float SepsisThreshold = 100f;

        [Header("Infection Settings")]
        [SerializeField, Range(0f, 100f)] private float initialInfection = 0f;
        [SerializeField] private float passiveProgressionPerSecond = 0f; // Mặc định không tự tăng để tránh death spiral ngẫu nhiên

        [Header("Critical Stage Settings")]
        [SerializeField] private float coughInterval = 9f;
        [SerializeField] private float coughNoiseRadius = 15f;
        [SerializeField] private bool enableContagion = false;
        [SerializeField] private float contagionRadius = 2.5f;
        [SerializeField] private float contagionExposureRequired = 5f;
        [SerializeField] private float contagionAmount = 15f;

        [Header("Sepsis Stage Settings")]
        [SerializeField] private float sepsisNoiseInterval = 3f;
        [SerializeField] private float sepsisNoiseRadius = 30f;
        [SerializeField] private float sepsisDrainInterval = 5f;
        [SerializeField] private float sepsisDamagePerTick = 5f;

        [Header("Treatment Settings")]
        [SerializeField] private float selfTreatmentDuration = 7f;
        [SerializeField] private float teammateTreatmentDuration = 3f;
        [SerializeField] private float selfTreatmentReduction = 60f;
        [SerializeField] private float teammateTreatmentReduction = 70f;

        [Header("Audio & SFX")]
        [SerializeField] private AudioClip coughSound;
        [SerializeField] private AudioClip sepsisHeartbeatSound;
        [SerializeField] private AudioClip implantHitSound;
        [SerializeField] private AudioClip treatmentCompleteSound;

        // =========================================================
        // REPLICATED NETWORK STATE
        // =========================================================
        private readonly NetworkVariable<float> networkInfection = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<InfectionStage> networkStage = new(
            InfectionStage.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> networkSepsisTimer = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> networkTreatmentKind = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> networkTreatmentStart = new(
            0.0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> networkTreatmentDeadline = new(
            0.0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // =========================================================
        // LOCAL TIMERS & STATE
        // =========================================================
        private float lastCoughTime;
        private float lastSepsisNoiseTime;
        private float lastSepsisDrainTime;
        private float sepsisTimeRemaining;
        private readonly Dictionary<ulong, float> nearbyTeammateExposures = new();

        private bool isTreatingSelf;
        private float selfTreatmentProgress;
        private bool isTreatingTeammate;
        private float teammateTreatmentProgress;
        private PlayerInfectionController currentTreatmentTarget;
        private double treatmentStartServerTime;
        private double treatmentDeadlineServerTime;
        private float treatmentStartHealth;

        private float localInfection;
        private InfectionStage localStage = InfectionStage.None;
        private float localSepsisTimer;

        // Offline/edit-mode instances do not have a NetworkBehaviour binding. Keep their
        // state in memory instead of writing a NetworkVariable before NGO has spawned them.
        private bool HasBoundNetworkState => IsSpawned
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsListening;

        private bool CanMutateAuthoritativeState => !HasBoundNetworkState || IsServer;

        [SerializeField] private PlayerHealth cachedHealth;
        [SerializeField] private PlayerMovement cachedMovement;

        // =========================================================
        // EVENTS
        // =========================================================
        public event Action<float, float, InfectionStage> OnInfectionChanged;
        public event Action<InfectionStage, InfectionStage> OnStageChanged;
        public event Action<Vector3, float> OnNoiseEmittedServer;
        public event Action<float> OnSepsisDrainServer;
        public event Action OnSepsisExpiredServer;

        // Static event for global AI awareness (zombies listen to this)
        public static event Action<Vector3, float, PlayerInfectionController> GlobalInfectionNoiseServer;

        // =========================================================
        // PUBLIC GETTERS
        // =========================================================
        public float CurrentInfection => HasBoundNetworkState ? networkInfection.Value : localInfection;
        public InfectionStage CurrentStage => HasBoundNetworkState ? networkStage.Value : localStage;
        public float SepsisTimeRemaining => HasBoundNetworkState ? networkSepsisTimer.Value : localSepsisTimer;
        public bool IsInfected => CurrentInfection > 0.01f;
        public bool IsCritical => CurrentStage >= InfectionStage.Critical;
        public bool IsSepsis => CurrentStage == InfectionStage.Sepsis;
        public bool IsTreatingSelf => HasBoundNetworkState ? networkTreatmentKind.Value == 1 : isTreatingSelf;
        public bool IsTreatingTeammate => HasBoundNetworkState ? networkTreatmentKind.Value == 2 : isTreatingTeammate;
        public float SelfTreatmentProgress => selfTreatmentProgress;
        public float TeammateTreatmentProgress => teammateTreatmentProgress;
        public float ActiveTreatmentProgress => GetTreatmentProgress();

        // Gameplay Modifiers
        public float MovementSpeedMultiplier
        {
            get
            {
                switch (CurrentStage)
                {
                    case InfectionStage.Symptomatic: return 0.95f;
                    case InfectionStage.Critical: return 0.90f;
                    case InfectionStage.Sepsis: return 0.75f;
                    default: return 1.0f;
                }
            }
        }

        public float WeaponSwayMultiplier
        {
            get
            {
                switch (CurrentStage)
                {
                    case InfectionStage.Symptomatic: return 1.10f;
                    case InfectionStage.Critical: return 1.20f;
                    case InfectionStage.Sepsis: return 1.50f;
                    default: return 1.0f;
                }
            }
        }

        public float ReloadSpeedMultiplier
        {
            get
            {
                switch (CurrentStage)
                {
                    case InfectionStage.Symptomatic: return 1.10f;
                    case InfectionStage.Critical: return 1.20f;
                    case InfectionStage.Sepsis: return 1.40f;
                    default: return 1.0f;
                }
            }
        }

        public bool CanSprint => CurrentStage != InfectionStage.Sepsis;

        private void Awake()
        {
            if (cachedHealth == null || cachedMovement == null)
            {
                GameLog.Error($"[Infection] {name} is missing authored PlayerHealth/PlayerMovement references.");
                enabled = false;
                return;
            }

            sepsisTimeRemaining = 0f;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                if (initialInfection > 0f)
                {
                    SetInfectionServer(initialInfection);
                }
            }

            networkInfection.OnValueChanged += HandleInfectionValueChanged;
            networkStage.OnValueChanged += HandleStageValueChanged;
        }

        public override void OnNetworkDespawn()
        {
            networkInfection.OnValueChanged -= HandleInfectionValueChanged;
            networkStage.OnValueChanged -= HandleStageValueChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            bool canRunServer = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                ? IsServer
                : true; // Offline / TestMode support

            if (canRunServer)
            {
                ServerUpdate();
            }

            UpdateTreatmentPresentation();
        }

        // =========================================================
        // SERVER LOGIC
        // =========================================================
        private void ServerUpdate()
        {
            if (passiveProgressionPerSecond > 0f && IsInfected && !IsSepsis)
            {
                AddInfectionServer(passiveProgressionPerSecond * Time.deltaTime);
            }

            InfectionStage stage = CurrentStage;

            if (stage == InfectionStage.Critical)
            {
                UpdateCriticalServer();
            }
            else if (stage == InfectionStage.Sepsis)
            {
                UpdateSepsisServer();
            }

            UpdateTreatmentServer();
        }

        private void UpdateCriticalServer()
        {
            // 1. Periodic Cough Noise Event
            if (Time.time - lastCoughTime >= coughInterval)
            {
                lastCoughTime = Time.time;
                EmitNoiseServer(coughNoiseRadius);
            }

            if (enableContagion)
                UpdateContagionServer();
        }

        private void UpdateContagionServer()
        {
            if (PlayerProfiler.Instance == null) return;

            var profiles = PlayerProfiler.Instance.AllProfiles;
            if (profiles == null) return;

            Vector3 myPos = transform.position;

            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile?.playerTransform == null || profile.playerTransform == transform)
                    continue;

                float distance = Vector3.Distance(myPos, profile.playerTransform.position);
                ulong targetId = profile.clientId;

                if (distance <= contagionRadius)
                {
                    if (!nearbyTeammateExposures.ContainsKey(targetId))
                        nearbyTeammateExposures[targetId] = 0f;

                    nearbyTeammateExposures[targetId] += Time.deltaTime;

                    if (nearbyTeammateExposures[targetId] >= contagionExposureRequired)
                    {
                        nearbyTeammateExposures[targetId] = 0f;
                        var targetInfection = profile.playerTransform.GetComponent<PlayerInfectionController>();
                        if (targetInfection != null)
                        {
                            targetInfection.AddInfectionServer(contagionAmount);
                            GameLog.Info(() => $"[Infection] Contagion spread to client {targetId} (+{contagionAmount}%)");
                        }
                    }
                }
                else
                {
                    nearbyTeammateExposures.Remove(targetId);
                }
            }
        }

        private void UpdateSepsisServer()
        {
            // 1. Periodic Horde Magnet Noise Pulse (Radius 30m every 3s)
            if (Time.time - lastSepsisNoiseTime >= sepsisNoiseInterval)
            {
                lastSepsisNoiseTime = Time.time;
                EmitNoiseServer(sepsisNoiseRadius);
            }

            // 2. Health Drain (5 HP every 5s)
            if (Time.time - lastSepsisDrainTime >= sepsisDrainInterval)
            {
                lastSepsisDrainTime = Time.time;
                if (cachedHealth != null && !cachedHealth.IsDead)
                {
                    cachedHealth.TakeDamage(sepsisDamagePerTick);
                    OnSepsisDrainServer?.Invoke(sepsisDamagePerTick);
                }
            }

        }

        private void EmitNoiseServer(float radius)
        {
            Vector3 pos = transform.position;
            OnNoiseEmittedServer?.Invoke(pos, radius);
            GlobalInfectionNoiseServer?.Invoke(pos, radius, this);

            // Trigger sound on clients if available
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                PlayCoughClientRpc(pos);
            }
        }

        [ClientRpc]
        private void PlayCoughClientRpc(Vector3 position)
        {
            if (coughSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXSound(coughSound, 1f);
            }
        }

        // =========================================================
        // INFECTION MUTATION API (SERVER-AUTHORITATIVE)
        // =========================================================
        public void AddInfectionServer(float amount)
        {
            if (!CanMutateAuthoritativeState || amount <= 0f) return;
            float newInfection = Mathf.Clamp(CurrentInfection + amount, 0f, MaxInfection);
            SetInfectionServer(newInfection);
        }

        public void TreatInfectionServer(float amount)
        {
            if (!CanMutateAuthoritativeState || amount <= 0f) return;
            float newInfection = Mathf.Clamp(CurrentInfection - amount, 0f, MaxInfection);
            SetInfectionServer(newInfection);

            if (newInfection < SepsisThreshold)
                localSepsisTimer = 0f;
        }

        public void CureServer()
        {
            if (!CanMutateAuthoritativeState) return;
            SetInfectionServer(0f);
            sepsisTimeRemaining = 0f;
            localSepsisTimer = 0f;
            if (HasBoundNetworkState)
                networkSepsisTimer.Value = 0f;
        }

        public void SetInfectionServer(float amount)
        {
            if (!CanMutateAuthoritativeState) return;

            float clamped = Mathf.Clamp(amount, 0f, MaxInfection);
            InfectionStage newStage = CalculateStage(clamped);

            float prevInfection = CurrentInfection;
            InfectionStage prevStage = CurrentStage;

            localInfection = clamped;
            localStage = newStage;

            if (HasBoundNetworkState)
            {
                networkInfection.Value = clamped;
                if (networkStage.Value != newStage)
                {
                    networkStage.Value = newStage;
                }
            }

            if (!HasBoundNetworkState)
            {
                if (!Mathf.Approximately(prevInfection, clamped))
                    OnInfectionChanged?.Invoke(prevInfection, clamped, newStage);
                if (prevStage != newStage)
                    OnStageChanged?.Invoke(prevStage, newStage);
            }
        }

        public static InfectionStage CalculateStage(float infectionAmount)
        {
            if (infectionAmount >= SepsisThreshold)
                return InfectionStage.Sepsis;
            if (infectionAmount >= CriticalThreshold)
                return InfectionStage.Critical;
            if (infectionAmount >= SymptomaticThreshold)
                return InfectionStage.Symptomatic;
            if (infectionAmount >= IncubationThreshold)
                return InfectionStage.Incubation;

            return InfectionStage.None;
        }

        // =========================================================
        // TREATMENT ACTIONS & RPCs
        // =========================================================
        public void StartSelfTreatment()
        {
            if (!IsInfected) return;
            if (HasBoundNetworkState)
                StartTreatmentServerRpc(NetworkObjectId);
            else
                BeginTreatmentServer(this);
        }

        public void CancelSelfTreatment()
        {
            RequestCancelTreatment();
        }

        public void StartTeammateTreatment(PlayerInfectionController target)
        {
            if (target == null || !target.IsInfected) return;
            if (HasBoundNetworkState)
                StartTreatmentServerRpc(target.NetworkObjectId);
            else
                BeginTreatmentServer(target);
        }

        public void CancelTeammateTreatment()
        {
            RequestCancelTreatment();
        }

        private void RequestCancelTreatment()
        {
            if (HasBoundNetworkState)
                CancelTreatmentServerRpc();
            else
                CancelTreatmentServer();
        }

        [ServerRpc]
        private void StartTreatmentServerRpc(ulong targetNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || NetworkManager?.SpawnManager == null)
                return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
                return;
            BeginTreatmentServer(targetObject.GetComponent<PlayerInfectionController>());
        }

        [ServerRpc]
        private void CancelTreatmentServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                CancelTreatmentServer();
        }

        private void BeginTreatmentServer(PlayerInfectionController target)
        {
            if (!CanMutateAuthoritativeState || !IsValidTreatmentTarget(target))
                return;

            currentTreatmentTarget = target;
            isTreatingSelf = target == this;
            isTreatingTeammate = target != this;
            treatmentStartServerTime = GetServerTime();
            treatmentDeadlineServerTime = treatmentStartServerTime
                + (isTreatingSelf ? selfTreatmentDuration : teammateTreatmentDuration);
            treatmentStartHealth = cachedHealth != null ? cachedHealth.CurrentHealth : 0f;
            if (HasBoundNetworkState)
            {
                networkTreatmentKind.Value = isTreatingSelf ? (byte)1 : (byte)2;
                networkTreatmentStart.Value = treatmentStartServerTime;
                networkTreatmentDeadline.Value = treatmentDeadlineServerTime;
            }
        }

        private void UpdateTreatmentServer()
        {
            if (!isTreatingSelf && !isTreatingTeammate)
                return;
            if (!IsValidTreatmentTarget(currentTreatmentTarget)
                || (cachedHealth != null && cachedHealth.CurrentHealth < treatmentStartHealth)
                || (cachedMovement != null && cachedMovement.IsSprinting))
            {
                CancelTreatmentServer();
                return;
            }

            if (GetServerTime() < treatmentDeadlineServerTime)
                return;

            currentTreatmentTarget.TreatInfectionServer(
                isTreatingSelf ? selfTreatmentReduction : teammateTreatmentReduction);
            CancelTreatmentServer();
        }

        private bool IsValidTreatmentTarget(PlayerInfectionController target)
        {
            if (target == null || !target.IsInfected || cachedHealth == null
                || cachedHealth.IsDead || cachedHealth.LifeState != PlayerLifeState.Alive)
                return false;

            PlayerHealth targetHealth = target.cachedHealth;
            if (targetHealth == null || targetHealth.IsDead || targetHealth.LifeState != PlayerLifeState.Alive)
                return false;
            if (target != this && (target.transform.position - transform.position).sqrMagnitude > 9f)
                return false;
            return target == this || HasTreatmentLineOfSight(target);
        }

        private bool HasTreatmentLineOfSight(PlayerInfectionController target)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 destination = target.transform.position + Vector3.up;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance <= 0.01f) return true;
            if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.transform == target.transform || hit.transform.IsChildOf(target.transform);
        }

        private void CancelTreatmentServer()
        {
            currentTreatmentTarget = null;
            isTreatingSelf = false;
            isTreatingTeammate = false;
            treatmentStartServerTime = 0.0;
            treatmentDeadlineServerTime = 0.0;
            selfTreatmentProgress = 0f;
            teammateTreatmentProgress = 0f;
            if (HasBoundNetworkState)
            {
                networkTreatmentKind.Value = 0;
                networkTreatmentStart.Value = 0.0;
                networkTreatmentDeadline.Value = 0.0;
            }
        }

        public void CancelActiveTreatmentServer()
        {
            if (CanMutateAuthoritativeState)
                CancelTreatmentServer();
        }

        private void UpdateTreatmentPresentation()
        {
            float progress = GetTreatmentProgress();
            selfTreatmentProgress = IsTreatingSelf ? progress : 0f;
            teammateTreatmentProgress = IsTreatingTeammate ? progress : 0f;
        }

        private float GetTreatmentProgress()
        {
            double start = HasBoundNetworkState ? networkTreatmentStart.Value : treatmentStartServerTime;
            double deadline = HasBoundNetworkState ? networkTreatmentDeadline.Value : treatmentDeadlineServerTime;
            if (deadline <= start)
                return 0f;
            return Mathf.Clamp01((float)((GetServerTime() - start) / (deadline - start)));
        }

        private double GetServerTime()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
        }

        // =========================================================
        // REPLICATED VALUE CHANGED HANDLERS
        // =========================================================
        private void HandleInfectionValueChanged(float oldVal, float newVal)
        {
            OnInfectionChanged?.Invoke(oldVal, newVal, CalculateStage(newVal));
        }

        private void HandleStageValueChanged(InfectionStage oldStage, InfectionStage newStage)
        {
            OnStageChanged?.Invoke(oldStage, newStage);
        }
    }
}
