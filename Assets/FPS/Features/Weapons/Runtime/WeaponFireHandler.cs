using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum FireRejectReason : byte
    {
        None,
        NotOwner,
        MatchBlocked,
        InvalidAim,
        InvalidTick,
        WrongWeapon,
        DuplicateSequence,
        CooldownOrAmmo,
        Equipping
    }

    public struct FireCommand : INetworkSerializable
    {
        public ushort sequence;
        public int estimatedServerTick;
        public uint inputSequence;
        public byte weaponSlot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref sequence);
            serializer.SerializeValue(ref estimatedServerTick);
            serializer.SerializeValue(ref inputSequence);
            serializer.SerializeValue(ref weaponSlot);
        }
    }

    public struct WeaponOwnerState : INetworkSerializable, IEquatable<WeaponOwnerState>
    {
        public byte slotIndex;
        public int magazineAmmo;
        public int reserveAmmo;
        public bool isReloading;
        public double reloadCompleteTime;
        public double equipCompleteTime;
        public ushort acknowledgedFireSequence;
        public FireRejectReason lastFireResult;
        public int authoritativeShotTick;

        public bool Equals(WeaponOwnerState other)
        {
            return slotIndex == other.slotIndex
                && magazineAmmo == other.magazineAmmo
                && reserveAmmo == other.reserveAmmo
                && isReloading == other.isReloading
                && reloadCompleteTime.Equals(other.reloadCompleteTime)
                && equipCompleteTime.Equals(other.equipCompleteTime)
                && acknowledgedFireSequence == other.acknowledgedFireSequence
                && lastFireResult == other.lastFireResult
                && authoritativeShotTick == other.authoritativeShotTick;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref slotIndex);
            serializer.SerializeValue(ref magazineAmmo);
            serializer.SerializeValue(ref reserveAmmo);
            serializer.SerializeValue(ref isReloading);
            serializer.SerializeValue(ref reloadCompleteTime);
            serializer.SerializeValue(ref equipCompleteTime);
            serializer.SerializeValue(ref acknowledgedFireSequence);
            serializer.SerializeValue(ref lastFireResult);
            serializer.SerializeValue(ref authoritativeShotTick);
        }
    }

    public struct WeaponPresentationState : INetworkSerializable, IEquatable<WeaponPresentationState>
    {
        public byte slotIndex;
        public bool isReloading;
        public double reloadCompleteTime;
        public double equipCompleteTime;
        public ushort shotSequence;

        public bool Equals(WeaponPresentationState other)
        {
            return slotIndex == other.slotIndex
                && isReloading == other.isReloading
                && reloadCompleteTime.Equals(other.reloadCompleteTime)
                && equipCompleteTime.Equals(other.equipCompleteTime)
                && shotSequence == other.shotSequence;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref slotIndex);
            serializer.SerializeValue(ref isReloading);
            serializer.SerializeValue(ref reloadCompleteTime);
            serializer.SerializeValue(ref equipCompleteTime);
            serializer.SerializeValue(ref shotSequence);
        }
    }

    public class WeaponFireHandler : NetworkBehaviour
    {
        private const float MaxFireOriginDistance = 2.5f;

        private readonly Dictionary<int, WeaponServerState> serverStates = new();
        private readonly double[] recentFireRequests = new double[64];
        private int fireRequestHead;
        private int fireRequestCount;
        private readonly NetworkVariable<WeaponOwnerState> ownerWeaponState = new(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<WeaponPresentationState> presentationState = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private WeaponManager weaponManager;
        private PlayerInfectionController infectionController;
        private bool restoredServerSnapshot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ushort verificationFireSequence;
#endif

        public int ServerMagazineAmmo => GetCurrentServerState(false)?.MagazineAmmo ?? 0;
        public int ServerReserveAmmo => GetCurrentServerState(false)?.ReserveAmmo ?? 0;
        public bool IsServerReloading => GetCurrentServerState(false)?.IsReloading(GetServerTime()) ?? false;
        public bool IsServerEquipping => GetCurrentServerState(false)?.IsEquipping(GetServerTime()) ?? false;

        public override void OnNetworkSpawn()
        {
            ownerWeaponState.OnValueChanged += HandleOwnerWeaponStateChanged;
            presentationState.OnValueChanged += HandlePresentationStateChanged;
            if (IsServer)
            {
                if (!restoredServerSnapshot)
                    BeginEquipForSlot(weaponManager != null ? weaponManager.CurrentWeaponIndex : 0, true);
                PublishOwnerState(FireRejectReason.None, 0, GetServerTick());
            }
            if (IsOwner)
                HandleOwnerWeaponStateChanged(default, ownerWeaponState.Value);
        }

        public override void OnNetworkDespawn()
        {
            ownerWeaponState.OnValueChanged -= HandleOwnerWeaponStateChanged;
            presentationState.OnValueChanged -= HandlePresentationStateChanged;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
                return;

            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            WeaponServerState state = GetCurrentServerState(false);
            if (weapon == null || weapon.Data == null || state == null || state.NextReloadEventTime < 0.0)
                return;

            double reloadDue = state.NextReloadEventTime;
            if (reloadDue < 0.0 || GetServerTime() < reloadDue)
                return;

            state.AdvanceReloadIfReady(weapon.Data, GetServerTime());
            PublishOwnerState(FireRejectReason.None, state.LastAcceptedFireSequence, GetServerTick());
            UpdateServerTelemetry();
        }

        [ServerRpc]
        public void RequestFireServerRpc(FireCommand command, ServerRpcParams serverRpcParams = default)
        {
            try
            {
                TryProcessRuntimeFire(command, serverRpcParams.Receive.SenderClientId);
            }
            catch (System.Exception ex)
            {
                GameLog.Error($"[WeaponFireHandler] Exception in RequestFireServerRpc: {ex.Message}");
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void RequestVerificationFire()
        {
            if (!IsOwner || !IsSpawned)
                return;

            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement == null || !movement.TryGetConfirmedFireReference(
                    out uint inputSequence, out int inputTick))
                return;

            int slot = weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
            RequestFireServerRpc(new FireCommand
            {
                sequence = unchecked(++verificationFireSequence),
                estimatedServerTick = inputTick,
                inputSequence = inputSequence,
                weaponSlot = (byte)Mathf.Clamp(slot, 0, byte.MaxValue)
            });
        }
#endif

        private bool TryProcessRuntimeFire(FireCommand command, ulong senderClientId)
        {
            if (senderClientId != OwnerClientId)
            {
                PublishOwnerState(FireRejectReason.NotOwner, command.sequence, GetServerTick());
                return false;
            }

            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            int currentSlot = weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
            if (weapon == null || weapon.Data == null || command.weaponSlot != currentSlot)
            {
                PublishOwnerState(FireRejectReason.WrongWeapon, command.sequence, GetServerTick());
                return false;
            }

            WeaponServerState state = GetCurrentServerState(true);
            if (state == null || !state.CanAcceptFireSequence(command.sequence))
            {
                PublishOwnerState(FireRejectReason.DuplicateSequence, command.sequence, GetServerTick());
                return false;
            }

            double now = GetServerTime();
            if (state.IsEquipping(now))
            {
                PublishOwnerState(FireRejectReason.Equipping, command.sequence, GetServerTick());
                return false;
            }

            if (!TryConsumeFireRequestBudget(weapon.Data, now))
            {
                PublishOwnerState(FireRejectReason.CooldownOrAmmo, command.sequence, GetServerTick());
                return false;
            }

            if (!NetworkMatchStateManager.IsGameplayActive)
            {
                PublishOwnerState(FireRejectReason.MatchBlocked, command.sequence, GetServerTick());
                return false;
            }

            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement == null || !movement.TryBuildServerAim(
                    command.estimatedServerTick,
                    command.inputSequence,
                    out Vector3 origin,
                    out Vector3 direction,
                    out bool aimed))
            {
                PublishOwnerState(FireRejectReason.InvalidAim, command.sequence, GetServerTick());
                return false;
            }

            double rttSeconds = 0.0;
            if (NetworkManager != null && NetworkManager.IsListening && NetworkManager.NetworkConfig.NetworkTransport != null)
                rttSeconds = NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(senderClientId) / 1000.0;

            int currentServerTick = GetServerTick();
            int estimatedTick = command.estimatedServerTick > 0 ? command.estimatedServerTick : currentServerTick;
            if (currentServerTick - estimatedTick > (int)(NetworkGameplayPolicy.SimulationHz * NetworkGameplayPolicy.MaxRewindSeconds))
            {
                estimatedTick = currentServerTick;
            }

            bool okTick = LagCompensationManager.TryResolveRewindTime(
                    now,
                    currentServerTick,
                    estimatedTick,
                    NetworkManager != null && NetworkManager.NetworkConfig.TickRate > 0 ? (int)NetworkManager.NetworkConfig.TickRate : NetworkGameplayPolicy.SimulationHz,
                    rttSeconds,
                    out double rewindTime);
            if (!okTick)
            {
                PublishOwnerState(FireRejectReason.InvalidTick, command.sequence, currentServerTick);
                return false;
            }

            bool accepted = TryProcessFireServer(
                origin,
                direction,
                command.estimatedServerTick,
                rewindTime,
                command.sequence,
                emitEffects: true,
                senderClientId: senderClientId,
                validateSender: true,
                clientTimeAlreadyResolved: true,
                aimed: aimed);

            PublishOwnerState(
                accepted ? FireRejectReason.None : FireRejectReason.CooldownOrAmmo,
                command.sequence,
                GetServerTick());
            return accepted;
        }

        private bool TryConsumeFireRequestBudget(WeaponData weaponData, double now)
        {
            while (fireRequestCount > 0
                && now - recentFireRequests[fireRequestHead] >= 1.0)
            {
                fireRequestHead = (fireRequestHead + 1) % recentFireRequests.Length;
                fireRequestCount--;
            }

            float interval = weaponData != null ? weaponData.FireInterval : 0.001f;
            int burstAllowance = weaponData != null ? Mathf.Max(4, weaponData.burstCount * 2) : 4;
            int allowedPerSecond = Mathf.Clamp(Mathf.CeilToInt(1.5f / interval) + burstAllowance,
                15, recentFireRequests.Length);
            if (fireRequestCount >= allowedPerSecond)
                return false;

            int tail = (fireRequestHead + fireRequestCount) % recentFireRequests.Length;
            recentFireRequests[tail] = now;
            fireRequestCount++;
            return true;
        }

        public bool ProcessFireServerForTests(
            Vector3 spawnPosition,
            Vector3 direction,
            int clientShotTick = 0,
            double clientShotLocalTime = 0.0,
            ushort fireSequence = 0,
            ulong senderClientId = 0,
            bool validateSender = false,
            bool aimed = false)
        {
            return TryProcessFireServer(
                spawnPosition,
                direction,
                clientShotTick,
                clientShotLocalTime,
                fireSequence,
                false,
                senderClientId,
                validateSender,
                clientTimeAlreadyResolved: false,
                aimed: aimed);
        }

        private bool TryProcessFireServer(
            Vector3 spawnPosition,
            Vector3 direction,
            int clientShotTick,
            double clientShotLocalTime,
            ushort fireSequence,
            bool emitEffects,
            ulong senderClientId,
            bool validateSender,
            bool clientTimeAlreadyResolved,
            bool aimed)
        {
            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            if (weapon == null || weapon.Data == null) return false;
            if (validateSender && senderClientId != OwnerClientId) return false;
            if (!NetworkMatchStateManager.IsGameplayActive) return false;

            double now = GetServerTime();

            if (!IsValidDirection(direction)) return false;

            float dist = Vector3.Distance(transform.position, spawnPosition);
            if (dist > MaxFireOriginDistance)
            {
                GameLog.Warning(() => $"[WeaponManager] Rejecting fire request from player {OwnerClientId}. Distance {dist}m exceeds limit.");
                return false;
            }

            WeaponServerState serverState = GetCurrentServerState(true);
            if (serverState == null || !serverState.TryConsumeFire(
                    weapon.Data, now, fireSequence, enforceSequence: validateSender))
                return false;

            if (infectionController == null)
                infectionController = GetComponent<PlayerInfectionController>();
            infectionController?.CancelActiveTreatmentServer();

            UpdateServerTelemetry();

            direction = direction.normalized;
            WeaponData weaponData = weapon.Data;
            int hitMask = GetHitMask(weaponData);
            float maximumRange = Mathf.Max(0.01f, weaponData.maximumRange);
            int projectileCount = Mathf.Max(1, weaponData.projectileCount);
            float spreadAngle = weaponData.GetSpreadAngle(aimed)
                * (infectionController != null ? infectionController.WeaponSwayMultiplier : 1f);
            uint shotSeed = WeaponBallistics.BuildShotSeed(
                validateSender ? senderClientId : OwnerClientId,
                fireSequence,
                (byte)Mathf.Clamp(weaponManager != null ? weaponManager.CurrentWeaponIndex : 0, 0, byte.MaxValue));
            double rewindTime = clientTimeAlreadyResolved
                ? clientShotLocalTime
                : LagCompensationManager.ResolveRewindTime(now, clientShotLocalTime);

            bool appliedDamage = false;
            bool anyHeadshot = false;
            float totalAppliedDamage = 0f;
            ulong attackerClientId = validateSender ? senderClientId : OwnerClientId;
            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                Vector3 projectileDirection = WeaponBallistics.GetProjectileDirection(
                    direction, spreadAngle, shotSeed, projectileIndex);
                bool currentHitFound = Physics.Raycast(
                    spawnPosition,
                    projectileDirection,
                    out RaycastHit currentHit,
                    maximumRange,
                    hitMask,
                    QueryTriggerInteraction.Ignore);
                float blockingDistance = currentHitFound ? currentHit.distance : maximumRange;

                DamageInfo pelletDamage = default;
                bool pelletApplied;
                if (LagCompensationManager.TryRaycast(
                        spawnPosition,
                        projectileDirection,
                        maximumRange,
                        hitMask,
                        rewindTime,
                        blockingDistance,
                        out LagCompensatedHit lagHit))
                {
                    pelletApplied = TryApplyDamage(
                        weaponData,
                        lagHit,
                        attackerClientId,
                        out pelletDamage);
                }
                else
                {
                    pelletApplied = currentHitFound && TryApplyDamage(
                        weaponData,
                        currentHit,
                        attackerClientId,
                        out pelletDamage);
                }

                if (!pelletApplied)
                    continue;

                appliedDamage = true;
                anyHeadshot |= pelletDamage.isHeadshot;
                totalAppliedDamage += pelletDamage.amount;
            }

            if (emitEffects)
            {
                FireEffectsClientRpc(spawnPosition, direction, fireSequence, aimed);
                if (appliedDamage)
                {
                    SendHitConfirmedToAttacker(
                        attackerClientId,
                        anyHeadshot ? HitboxZone.Head : HitboxZone.Body,
                        totalAppliedDamage);
                }
            }

            PlayerHealth shooterHealth = GetComponent<PlayerHealth>();
            NetworkGameManager.Instance?.Telemetry?.RecordShot(
                shooterHealth != null ? shooterHealth.StablePlayerId : default,
                GetServerTick(),
                appliedDamage,
                appliedDamage && anyHeadshot);

            NetworkDiagnostics.Emit(
                "fire_result",
                NetworkGameManager.Instance != null ? NetworkGameManager.Instance.State : SessionState.InMatch,
                $"Accepted:sequence={fireSequence}",
                shooterHealth != null ? shooterHealth.StablePlayerId : default);

            return true;
        }

        public void HandleServerWeaponSwitched(int previousSlotIndex, int slotIndex, bool beginEquip = true)
        {
            if (!IsServer)
                return;

            if (previousSlotIndex != slotIndex)
                GetServerState(previousSlotIndex, false)?.CancelReload();

            BeginEquipForSlot(slotIndex, beginEquip);
            PublishOwnerState(FireRejectReason.None, 0, GetServerTick());
            UpdateServerTelemetry();
        }

        public void HandleServerPrimaryWeaponReplaced(bool primaryIsEquipped)
        {
            if (!IsServer)
                return;

            GetServerState(0, false)?.CancelReload();
            serverStates.Remove(0);
            if (primaryIsEquipped)
                BeginEquipForSlot(0, true);
            PublishOwnerState(FireRejectReason.None, 0, GetServerTick());
            UpdateServerTelemetry();
        }

        private void BeginEquipForSlot(int slotIndex, bool beginEquip)
        {
            Weapon weapon = GetWeapon(slotIndex);
            WeaponServerState state = GetServerState(slotIndex, weapon != null);
            if (beginEquip && weapon != null && weapon.Data != null && state != null)
                state.BeginEquip(weapon.Data, GetServerTime());
        }

        public WeaponRuntimeSnapshot CaptureWeaponSnapshot(int slotIndex)
        {
            Weapon weapon = GetWeapon(slotIndex);
            WeaponServerState state = GetServerState(slotIndex, weapon != null);
            return state != null
                ? state.Capture((byte)Mathf.Clamp(slotIndex, 0, byte.MaxValue), weapon != null ? weapon.Data : null)
                : default;
        }

        public void RestoreServerSnapshot(PlayerRuntimeSnapshot snapshot)
        {
            restoredServerSnapshot = true;
            if (snapshot.weaponSlot0.definitionId.Length > 0)
                RestoreSlot(snapshot.weaponSlot0);
            if (snapshot.weaponSlot1.definitionId.Length > 0)
                RestoreSlot(snapshot.weaponSlot1);
        }

        private void RestoreSlot(WeaponRuntimeSnapshot snapshot)
        {
            int slot = snapshot.slotIndex;
            Weapon weapon = GetWeapon(slot);
            if (weapon == null)
                return;

            WeaponServerState state = GetServerState(slot, false);
            if (state == null)
            {
                state = new WeaponServerState();
                serverStates[slot] = state;
            }

            // Unity 6000.5 removed the legacy instance-ID API; entity IDs remain stable for this runtime state key.
            state.Restore(snapshot, weapon.GetEntityId().GetHashCode());
            state.CompleteReloadIfReady(weapon.Data, GetServerTime());
        }

        [ServerRpc]
        public void RequestReloadServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;
            TryBeginServerReload();
        }

        public bool BeginServerReloadForTests()
        {
            return TryBeginServerReload();
        }

        private bool TryBeginServerReload()
        {
            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            if (weapon == null || weapon.Data == null) return false;

            WeaponServerState state = GetCurrentServerState(true);
            if (infectionController == null)
                infectionController = GetComponent<PlayerInfectionController>();
            float timingMultiplier = infectionController != null
                ? infectionController.ReloadSpeedMultiplier
                : 1f;
            bool accepted = state != null
                && state.TryBeginReload(weapon.Data, GetServerTime(), timingMultiplier);
            if (accepted)
                infectionController?.CancelActiveTreatmentServer();
            PublishOwnerState(FireRejectReason.None, state?.LastAcceptedFireSequence ?? 0, GetServerTick());
            UpdateServerTelemetry();
            return accepted;
        }

        public bool CanReceiveAmmoServer()
        {
            return IsServer && GetCurrentWeaponAndEnsureServerState() != null;
        }

        public bool AddReserveAmmoServer(int amount)
        {
            if (!IsServer || amount <= 0)
                return false;

            if (GetCurrentWeaponAndEnsureServerState() == null)
                return false;

            WeaponServerState state = GetCurrentServerState(true);
            if (state == null)
                return false;

            state.AddReserveAmmo(amount);
            PublishOwnerState(FireRejectReason.None, 0, GetServerTick());
            UpdateServerTelemetry();
            return true;
        }

        public void InitializeServerWeaponStateForTests(int magazineAmmo, int reserveAmmo, double nextFireTime = 0.0)
        {
            Weapon weapon = GetCurrentWeapon();
            WeaponServerState state = GetCurrentServerState(true);
            state?.InitializeForTests(weapon != null ? weapon.GetEntityId().GetHashCode() : 0, magazineAmmo, reserveAmmo, nextFireTime);
        }

        public void CompleteServerReloadIfReadyForTests()
        {
            CompleteServerReloadIfReady();
        }

        [ClientRpc]
        private void FireEffectsClientRpc(
            Vector3 aimOrigin,
            Vector3 aimDirection,
            ushort shotSequence,
            bool aimed)
        {
            if (IsOwner) return;

            Weapon weapon = GetCurrentWeapon();
            if (weapon == null) return;

            int slot = weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
            weapon.SpawnVisualProjectiles(
                aimOrigin,
                aimDirection,
                shotSequence,
                OwnerClientId,
                (byte)Mathf.Clamp(slot, 0, byte.MaxValue),
                aimed);
            weapon.PlayMuzzleEffect();
            weapon.PlayShootSound();
        }

        private bool TryApplyDamage(WeaponData weaponData, RaycastHit hit, ulong attackerClientId, out DamageInfo damageInfo)
        {
            damageInfo = default;
            HitboxSegment segment = hit.collider.GetComponentInParent<HitboxSegment>();
            IDamageable damageable = segment != null
                ? segment.DamageTarget
                : hit.collider.GetComponentInParent<IDamageable>();

            if (damageable == null)
                return false;

            if (damageable is PlayerHealth playerHealth && IsOwnedByShooter(playerHealth, attackerClientId))
                return false;

            EnemyHitbox legacyHitbox = segment == null
                ? hit.collider.GetComponentInParent<EnemyHitbox>()
                : null;
            HitboxZone zone = ResolveZone(segment, legacyHitbox);
            float multiplier = ResolveMultiplier(segment, legacyHitbox, zone);
            float finalDamage = weaponData.EvaluateBaseDamage(hit.distance) * multiplier;

            damageInfo = new DamageInfo(
                finalDamage,
                attackerClientId,
                GetAttackerPlayerIndex(attackerClientId),
                hit.point,
                isHeadshot: zone == HitboxZone.Head,
                reactionTime: 0f,
                damageType: weaponData.damageType,
                hitZone: zone,
                damageMultiplier: multiplier);

            DamageFilter damageFilter = hit.collider.GetComponentInParent<DamageFilter>();
            if (damageFilter != null && !damageFilter.Allows(damageInfo))
                return false;

            if (damageable is IAttributedDamageable attributedDamageable)
            {
                attributedDamageable.TakeDamage(damageInfo);
                return true;
            }

            damageable.TakeDamage(finalDamage);
            return true;
        }

        private bool TryApplyDamage(WeaponData weaponData, LagCompensatedHit hit, ulong attackerClientId, out DamageInfo damageInfo)
        {
            damageInfo = default;
            IDamageable damageable = hit.damageTarget ?? hit.segment?.DamageTarget;
            if (damageable == null)
                return false;

            if (damageable is PlayerHealth playerHealth && IsOwnedByShooter(playerHealth, attackerClientId))
                return false;

            float multiplier = hit.damageMultiplier > 0f
                ? hit.damageMultiplier
                : HitboxSegment.GetDefaultMultiplier(hit.zone);
            float finalDamage = weaponData.EvaluateBaseDamage(hit.distance) * multiplier;
            damageInfo = new DamageInfo(
                finalDamage,
                attackerClientId,
                GetAttackerPlayerIndex(attackerClientId),
                hit.point,
                isHeadshot: hit.zone == HitboxZone.Head,
                reactionTime: 0f,
                damageType: weaponData.damageType,
                hitZone: hit.zone,
                damageMultiplier: multiplier);

            DamageFilter damageFilter = hit.segment != null
                ? hit.segment.GetComponentInParent<DamageFilter>()
                : null;
            if (damageFilter != null && !damageFilter.Allows(damageInfo))
                return false;

            if (damageable is IAttributedDamageable attributedDamageable)
            {
                attributedDamageable.TakeDamage(damageInfo);
                return true;
            }

            damageable.TakeDamage(finalDamage);
            return true;
        }

        private static HitboxZone ResolveZone(HitboxSegment segment, EnemyHitbox legacyHitbox)
        {
            if (segment != null)
                return segment.Zone;

            if (legacyHitbox != null && legacyHitbox.IsHeadshot)
                return HitboxZone.Head;

            return HitboxZone.Body;
        }

        private static float ResolveMultiplier(HitboxSegment segment, EnemyHitbox legacyHitbox, HitboxZone zone)
        {
            if (segment != null)
                return segment.DamageMultiplier;

            if (legacyHitbox != null)
                return HitboxSegment.GetDefaultMultiplier(zone);

            return 1f;
        }

        private int GetAttackerPlayerIndex(ulong attackerClientId)
        {
            var profile = PlayerProfiler.Instance?.GetProfileByClientId(attackerClientId);
            return profile != null ? profile.playerIndex : -1;
        }

        private Weapon GetCurrentWeaponAndEnsureServerState()
        {
            Weapon weapon = GetCurrentWeapon();
            if (weapon == null || weapon.Data == null)
                return null;

            GetCurrentServerState(true)?.EnsureInitialized(weapon.GetEntityId().GetHashCode(), weapon.Data);
            return weapon;
        }

        private WeaponServerState GetCurrentServerState(bool initialize)
        {
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            int slot = weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
            return GetServerState(slot, initialize);
        }

        private WeaponServerState GetServerState(int slot, bool initialize)
        {
            if (!serverStates.TryGetValue(slot, out WeaponServerState state))
            {
                if (!initialize)
                    return null;

                state = new WeaponServerState();
                serverStates.Add(slot, state);
            }

            if (initialize)
            {
                Weapon weapon = GetWeapon(slot);
                if (weapon != null && weapon.Data != null)
                    state.EnsureInitialized(weapon.GetEntityId().GetHashCode(), weapon.Data);
            }

            return state;
        }

        private Weapon GetWeapon(int slot)
        {
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();
            return weaponManager != null ? weaponManager.GetWeapon(slot) : null;
        }

        private void PublishOwnerState(FireRejectReason result, ushort sequence, int shotTick)
        {
            if (!IsServer || !IsSpawned)
                return;

            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            int slot = weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
            WeaponServerState state = GetServerState(slot, true);
            if (state == null)
                return;

            ownerWeaponState.Value = new WeaponOwnerState
            {
                slotIndex = (byte)Mathf.Clamp(slot, 0, byte.MaxValue),
                magazineAmmo = state.MagazineAmmo,
                reserveAmmo = state.ReserveAmmo,
                isReloading = state.IsReloading(GetServerTime()),
                reloadCompleteTime = state.ReloadCompleteTime,
                equipCompleteTime = state.EquipCompleteTime,
                acknowledgedFireSequence = sequence,
                lastFireResult = result,
                authoritativeShotTick = shotTick
            };
            presentationState.Value = new WeaponPresentationState
            {
                slotIndex = (byte)Mathf.Clamp(slot, 0, byte.MaxValue),
                isReloading = state.IsReloading(GetServerTime()),
                reloadCompleteTime = state.ReloadCompleteTime,
                equipCompleteTime = state.EquipCompleteTime,
                shotSequence = state.LastAcceptedFireSequence
            };

            if (result != FireRejectReason.None)
            {
                PlayerHealth health = GetComponent<PlayerHealth>();
                NetworkDiagnostics.Emit(
                    "fire_reject",
                    NetworkGameManager.Instance != null ? NetworkGameManager.Instance.State : SessionState.InMatch,
                    $"{result}:sequence={sequence}",
                    health != null ? health.StablePlayerId : default);
            }
        }

        private void HandleOwnerWeaponStateChanged(WeaponOwnerState previous, WeaponOwnerState current)
        {
            if (!IsOwner)
                return;

            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();
            weaponManager?.ApplyAuthoritativeWeaponState(current);
        }

        private void HandlePresentationStateChanged(
            WeaponPresentationState previous,
            WeaponPresentationState current)
        {
            if (IsOwner)
                return;

            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();
            weaponManager?.ApplyPresentationState(previous, current);
        }

        private void UpdateServerTelemetry()
        {
            Weapon weapon = GetCurrentWeapon();
            WeaponServerState state = GetCurrentServerState(false);
            if (weapon == null || weapon.Data == null || state == null)
                return;

            GetComponent<PlayerCombatTelemetry>()?.ApplyWeaponState(
                state.IsReloading(GetServerTime()),
                state.MagazineAmmo,
                weapon.Data.magazineSize,
                GetServerTime());
        }

        private int GetServerTick()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Tick
                : Mathf.FloorToInt((float)(Time.timeAsDouble * NetworkGameplayPolicy.SimulationHz));
        }

        private Weapon GetCurrentWeapon()
        {
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            if (weaponManager == null || weaponManager.WeaponCount == 0)
                return null;

            GameObject currentWeaponGo = weaponManager.CurrentWeapon;
            return currentWeaponGo != null ? currentWeaponGo.GetComponent<Weapon>() : null;
        }

        private void CompleteServerReloadIfReady()
        {
            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            if (weapon == null || weapon.Data == null) return;

            GetCurrentServerState(true)?.CompleteReloadIfReady(weapon.Data, GetServerTime());
            PublishOwnerState(FireRejectReason.None, 0, GetServerTick());
            UpdateServerTelemetry();
        }

        private static bool IsValidDirection(Vector3 direction)
        {
            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z)) return false;
            if (float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || float.IsInfinity(direction.z)) return false;
            return direction.sqrMagnitude > 0.0001f;
        }

        private static int GetHitMask(WeaponData weaponData)
        {
            return weaponData.hitMask.value != 0
                ? weaponData.hitMask.value
                : Physics.DefaultRaycastLayers;
        }

        private bool IsOwnedByShooter(PlayerHealth playerHealth, ulong attackerClientId)
        {
            NetworkObject targetNetworkObject = playerHealth.NetworkObject;
            return targetNetworkObject != null && targetNetworkObject.OwnerClientId == attackerClientId;
        }

        private void SendHitConfirmedToAttacker(
            ulong attackerClientId,
            HitboxZone zone,
            float finalDamage)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            HitConfirmedClientRpc(
                zone,
                finalDamage,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { attackerClientId }
                    }
                });
        }

        [ClientRpc]
        private void HitConfirmedClientRpc(
            HitboxZone zone,
            float finalDamage,
            ClientRpcParams clientRpcParams = default)
        {
            if (HUDManager.HasInstance)
                HUDManager.Instance.ShowHitConfirmed(zone, finalDamage);
        }

        private double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }
    }
}
