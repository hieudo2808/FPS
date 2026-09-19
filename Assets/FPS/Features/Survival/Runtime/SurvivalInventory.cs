using System;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum ThrowableKind : byte { Frag, Incendiary }
    public enum ConsumableKind : byte { Medkit, Antidote }

    /// <summary>Server inventory and a shared, atomic lifecycle for medical consumables.</summary>
    public sealed class SurvivalInventory : NetworkBehaviour
    {
        [SerializeField] private SurvivalProjectile projectilePrefab;
        public readonly NetworkVariable<byte> FragGrenadeCount = new(3);
        public readonly NetworkVariable<byte> IncendiaryGrenadeCount = new(3);
        public readonly NetworkVariable<byte> MedkitCount = new(2);
        public readonly NetworkVariable<byte> AntidoteCount = new(2);
        public readonly NetworkVariable<ThrowableKind> SelectedThrowable = new(ThrowableKind.Frag);
        private readonly NetworkVariable<byte> activeUse = new();
        private readonly NetworkVariable<double> useStarted = new();
        private readonly NetworkVariable<double> useDeadline = new();
        private readonly NetworkVariable<ulong> useTargetId = new(ulong.MaxValue);
        private readonly NetworkVariable<uint> lastRequest = new();
        private PlayerHealth health;
        private PlayerMovement movement;
        private PlayerInfectionController infection;
        private SurvivalInventory useTarget;
        private uint actorDamageRevision;
        private uint targetDamageRevision;
        private uint nextRequest;
        private double nextThrowTime;
        public event Action InventoryChanged;
        public bool IsUsingItem => activeUse.Value != 0;
        public ConsumableKind ActiveConsumable => activeUse.Value == 2 ? ConsumableKind.Antidote : ConsumableKind.Medkit;
        public bool IsSelfUse => IsSpawned && useTargetId.Value == NetworkObjectId;
        public float UseProgress => IsUsingItem ? Mathf.Clamp01((float)((ServerNow - useStarted.Value) / Math.Max(.01, useDeadline.Value - useStarted.Value))) : 0f;
        public double ServerNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<PlayerMovement>();
            infection = GetComponent<PlayerInfectionController>();
        }
        public override void OnNetworkSpawn()
        {
            nextRequest = lastRequest.Value;
            FragGrenadeCount.OnValueChanged += OnCountChanged;
            IncendiaryGrenadeCount.OnValueChanged += OnCountChanged;
            MedkitCount.OnValueChanged += OnCountChanged;
            AntidoteCount.OnValueChanged += OnCountChanged;
            SelectedThrowable.OnValueChanged += OnThrowableChanged;
            InventoryChanged?.Invoke();
        }
        public override void OnNetworkDespawn()
        {
            FragGrenadeCount.OnValueChanged -= OnCountChanged;
            IncendiaryGrenadeCount.OnValueChanged -= OnCountChanged;
            MedkitCount.OnValueChanged -= OnCountChanged;
            AntidoteCount.OnValueChanged -= OnCountChanged;
            SelectedThrowable.OnValueChanged -= OnThrowableChanged;
            useTarget = null;
        }
        private void OnCountChanged(byte oldValue, byte newValue) => InventoryChanged?.Invoke();
        private void OnThrowableChanged(ThrowableKind oldValue, ThrowableKind newValue) => InventoryChanged?.Invoke();
        public int Count(PickupType type) => type switch
        {
            PickupType.FragGrenade => FragGrenadeCount.Value,
            PickupType.IncendiaryGrenade => IncendiaryGrenadeCount.Value,
            PickupType.Medkit => MedkitCount.Value,
            PickupType.Antidote => AntidoteCount.Value,
            _ => 0
        };
        public bool CanAdd(PickupType type, int amount) => SurvivalRules.CanAdd(type, Count(type), amount);
        public bool TryAdd(PickupType type, byte amount)
        {
            if (!IsServer || !CanAdd(type, amount)) return false;
            switch (type)
            {
                case PickupType.FragGrenade: FragGrenadeCount.Value += amount; break;
                case PickupType.IncendiaryGrenade: IncendiaryGrenadeCount.Value += amount; break;
                case PickupType.Medkit: MedkitCount.Value += amount; break;
                case PickupType.Antidote: AntidoteCount.Value += amount; break;
                default: return false;
            }
            return true;
        }
        public void CycleThrowable()
        {
            if (IsOwner && IsSpawned) SelectThrowableServerRpc(SelectedThrowable.Value == ThrowableKind.Frag ? ThrowableKind.Incendiary : ThrowableKind.Frag);
        }
        [ServerRpc] private void SelectThrowableServerRpc(ThrowableKind kind)
        {
            if (kind is ThrowableKind.Frag or ThrowableKind.Incendiary) SelectedThrowable.Value = kind;
        }
        private uint NextRequest()
        {
            if (SurvivalRules.IsNewer(lastRequest.Value, nextRequest)) nextRequest = lastRequest.Value;
            return ++nextRequest;
        }
        private bool AcceptRequest(uint sequence)
        {
            if (!SurvivalRules.IsNewer(sequence, lastRequest.Value)) return false;
            // Remember rejected actions too, so delayed duplicate packets cannot become new actions.
            lastRequest.Value = sequence;
            return true;
        }
        public void RequestThrow(Vector3 direction)
        {
            if (IsOwner && IsSpawned) RequestThrowServerRpc(SelectedThrowable.Value, direction, NextRequest());
        }
        [ServerRpc] private void RequestThrowServerRpc(ThrowableKind kind, Vector3 direction, uint sequence)
        {
            if (!AcceptRequest(sequence) || !CanAct() || IsUsingItem || ServerNow < nextThrowTime
                || !SurvivalRules.ValidThrowDirection(direction, transform.forward) || projectilePrefab == null) return;
            if (kind != ThrowableKind.Frag && kind != ThrowableKind.Incendiary) return;
            var count = kind == ThrowableKind.Frag ? FragGrenadeCount : IncendiaryGrenadeCount;
            if (count.Value == 0) return;
            Vector3 eye = transform.position + Vector3.up * 1.45f;
            Vector3 origin = eye + direction.normalized * .45f;
            if (Physics.SphereCast(eye, .09f, direction.normalized, out RaycastHit hit, .45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                origin = hit.point + hit.normal * .11f;
            SurvivalProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
            projectile.InitializeServer(kind, OwnerClientId, NetworkObject, direction.normalized * 13f);
            projectile.NetworkObject.Spawn();
            count.Value--;
            nextThrowTime = ServerNow + .5;
            ThrowFeedbackClientRpc();
        }
        public void RequestUse(ConsumableKind kind, SurvivalInventory target = null)
        {
            if (!IsOwner || !IsSpawned || (target != null && !target.IsSpawned)) return;
            RequestUseServerRpc(kind, target != null ? target.NetworkObjectId : NetworkObjectId, NextRequest());
        }
        [ServerRpc] private void RequestUseServerRpc(ConsumableKind kind, ulong targetId, uint sequence)
        {
            if (!AcceptRequest(sequence) || !CanAct() || IsUsingItem || (kind != ConsumableKind.Medkit && kind != ConsumableKind.Antidote)) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject)) return;
            var target = targetObject.GetComponent<SurvivalInventory>();
            if (!CanTreat(target, kind)) return;
            useTarget = target;
            actorDamageRevision = health.DamageRevision;
            targetDamageRevision = target.health.DamageRevision;
            useTargetId.Value = targetId;
            useStarted.Value = ServerNow;
            useDeadline.Value = ServerNow + SurvivalRules.UseDuration(kind, target == this);
            activeUse.Value = (byte)((byte)kind + 1);
        }
        public bool CanTreat(SurvivalInventory target, ConsumableKind kind)
        {
            if (target == null || !target.IsSpawned || target.health == null || !target.health.CanUseCombat || health == null || !health.CanUseCombat) return false;
            if (kind == ConsumableKind.Medkit)
            {
                if (MedkitCount.Value == 0 || target.health.CurrentHealth >= target.health.MaxHealth) return false;
            }
            else if (kind != ConsumableKind.Antidote || AntidoteCount.Value == 0 || target.infection == null || !target.infection.IsInfected) return false;
            return target == this || ((target.transform.position - transform.position).sqrMagnitude <= 9f
                && SurvivalDamage.HasLineOfSight(transform.position + Vector3.up, target.transform.position + Vector3.up, transform, target.transform));
        }
        public void CancelUse() { if (IsOwner && IsSpawned) CancelUseServerRpc(); }
        [ServerRpc] private void CancelUseServerRpc() => CancelUseServer();
        public void CancelUseServer()
        {
            if (!IsServer) return;
            activeUse.Value = 0;
            useTargetId.Value = ulong.MaxValue;
            useTarget = null;
        }
        private bool CanAct() => health != null && health.CanUseCombat && !(movement != null && movement.IsSprinting)
            && !NetworkMatchStateManager.IsGameplayBlocked && CampaignMissionController.Instance?.BlocksInput != true;
        private void Update()
        {
            if (!IsServer || !IsSpawned || !IsUsingItem) return;
            if (!CanAct() || !CanTreat(useTarget, ActiveConsumable) || health.DamageRevision != actorDamageRevision
                || useTarget.health.DamageRevision != targetDamageRevision || (useTarget.movement != null && useTarget.movement.IsSprinting))
            { CancelUseServer(); return; }
            if (ServerNow < useDeadline.Value) return;
            ConsumableKind kind = ActiveConsumable;
            if (kind == ConsumableKind.Medkit) { useTarget.health.Heal(50f); MedkitCount.Value--; }
            else { useTarget.infection.TreatInfectionServer(40f); AntidoteCount.Value--; }
            CompleteUseClientRpc(useTarget.transform.position + Vector3.up, kind);
            CancelUseServer();
        }
        [ClientRpc] private void ThrowFeedbackClientRpc() { GetComponent<SurvivalPresentation>()?.PlayThrow(); }
        [ClientRpc] private void CompleteUseClientRpc(Vector3 point, ConsumableKind kind) => SurvivalEffects.Medical(point, kind);
        public void Capture(ref PlayerRuntimeSnapshot snapshot)
        {
            snapshot.survivalInventoryVersion = 1;
            snapshot.fragCount = FragGrenadeCount.Value;
            snapshot.incendiaryCount = IncendiaryGrenadeCount.Value;
            snapshot.medkitCount = MedkitCount.Value;
            snapshot.antidoteCount = AntidoteCount.Value;
            snapshot.selectedThrowable = SelectedThrowable.Value;
        }
        public void RestoreServer(PlayerRuntimeSnapshot snapshot)
        {
            if (!IsServer) return;
            CancelUseServer();
            if (snapshot.survivalInventoryVersion == 0) return;
            FragGrenadeCount.Value = (byte)Mathf.Clamp((int)snapshot.fragCount, 0, 3);
            IncendiaryGrenadeCount.Value = (byte)Mathf.Clamp((int)snapshot.incendiaryCount, 0, 3);
            MedkitCount.Value = (byte)Mathf.Clamp((int)snapshot.medkitCount, 0, 2);
            AntidoteCount.Value = (byte)Mathf.Clamp((int)snapshot.antidoteCount, 0, 2);
            SelectedThrowable.Value = snapshot.selectedThrowable == ThrowableKind.Incendiary ? ThrowableKind.Incendiary : ThrowableKind.Frag;
        }
    }
}
