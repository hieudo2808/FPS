using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponFireHandler : NetworkBehaviour
    {
        private const float MaxFireOriginDistance = 5.0f;
        private const float MaxRaycastDistance = 500f;

        private readonly WeaponServerState serverState = new WeaponServerState();
        private WeaponManager weaponManager;

        public int ServerMagazineAmmo => serverState.MagazineAmmo;
        public int ServerReserveAmmo => serverState.ReserveAmmo;
        public bool IsServerReloading => serverState.IsReloading(GetServerTime());

        [ServerRpc]
        public void RequestFireServerRpc(
            Vector3 spawnPosition,
            Vector3 direction,
            int clientShotTick = 0,
            double clientShotLocalTime = 0.0,
            ushort fireSequence = 0,
            ServerRpcParams serverRpcParams = default)
        {
            TryProcessFireServer(
                spawnPosition,
                direction,
                clientShotTick,
                clientShotLocalTime,
                fireSequence,
                emitEffects: true,
                senderClientId: serverRpcParams.Receive.SenderClientId,
                validateSender: true);
        }

        public bool ProcessFireServerForTests(
            Vector3 spawnPosition,
            Vector3 direction,
            int clientShotTick = 0,
            double clientShotLocalTime = 0.0,
            ushort fireSequence = 0,
            ulong senderClientId = 0,
            bool validateSender = false)
        {
            return TryProcessFireServer(
                spawnPosition,
                direction,
                clientShotTick,
                clientShotLocalTime,
                fireSequence,
                false,
                senderClientId,
                validateSender);
        }

        private bool TryProcessFireServer(
            Vector3 spawnPosition,
            Vector3 direction,
            int clientShotTick,
            double clientShotLocalTime,
            ushort fireSequence,
            bool emitEffects,
            ulong senderClientId,
            bool validateSender)
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

            if (!serverState.TryConsumeFire(weapon.Data, now))
                return false;

            direction = direction.normalized;
            int hitMask = GetHitMask(weapon.Data);
            bool currentHitFound = Physics.Raycast(
                spawnPosition,
                direction,
                out RaycastHit currentHit,
                MaxRaycastDistance,
                hitMask,
                QueryTriggerInteraction.Ignore);

            DamageInfo appliedDamageInfo = default;
            bool appliedDamage = false;
            double rewindTime = LagCompensationManager.ResolveRewindTime(now, clientShotLocalTime);
            float blockingDistance = currentHitFound ? currentHit.distance : MaxRaycastDistance;

            if (LagCompensationManager.TryRaycast(
                    spawnPosition,
                    direction,
                    MaxRaycastDistance,
                    hitMask,
                    rewindTime,
                    blockingDistance,
                    out LagCompensatedHit lagHit))
            {
                appliedDamage = TryApplyDamage(
                    weapon.Data,
                    lagHit,
                    validateSender ? senderClientId : OwnerClientId,
                    out appliedDamageInfo);
            }
            else if (currentHitFound)
            {
                appliedDamage = TryApplyDamage(
                    weapon.Data,
                    currentHit,
                    validateSender ? senderClientId : OwnerClientId,
                    out appliedDamageInfo);
            }

            if (emitEffects)
            {
                FireEffectsClientRpc(spawnPosition, direction);
                if (appliedDamage)
                    SendHitConfirmedToAttacker(validateSender ? senderClientId : OwnerClientId, appliedDamageInfo);
            }

            return true;
        }

        [ServerRpc]
        public void RequestReloadServerRpc()
        {
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

            return serverState.TryBeginReload(weapon.Data, GetServerTime());
        }

        public void AddReserveAmmoServer(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0) return;

            GetCurrentWeaponAndEnsureServerState();
            serverState.AddReserveAmmo(amount);
        }

        public void InitializeServerWeaponStateForTests(int magazineAmmo, int reserveAmmo, double nextFireTime = 0.0)
        {
            Weapon weapon = GetCurrentWeapon();
            serverState.InitializeForTests(weapon != null ? weapon.GetInstanceID() : 0, magazineAmmo, reserveAmmo, nextFireTime);
        }

        public void CompleteServerReloadIfReadyForTests()
        {
            CompleteServerReloadIfReady();
        }

        [ClientRpc]
        private void FireEffectsClientRpc(Vector3 spawnPosition, Vector3 direction)
        {
            if (IsOwner) return;

            Weapon weapon = GetCurrentWeapon();
            if (weapon == null) return;

            weapon.SpawnVisualBullet(spawnPosition, direction);
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
            float finalDamage = weaponData.damage * multiplier;

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
            float finalDamage = weaponData.damage * multiplier;
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

            serverState.EnsureInitialized(weapon.GetInstanceID(), weapon.Data);
            return weapon;
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

            serverState.CompleteReloadIfReady(weapon.Data, GetServerTime());
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

        private void SendHitConfirmedToAttacker(ulong attackerClientId, DamageInfo damageInfo)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            HitConfirmedClientRpc(
                damageInfo.hitZone,
                damageInfo.amount,
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
