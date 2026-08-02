using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS
{
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        public static event System.Action<PlayerHealth, ulong> PlayerDiedServer;
        public static event System.Action<PlayerHealth> PlayerSpawnedServer;
        public static event System.Action<PlayerHealth> PlayerDespawnedServer;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        private NetworkVariable<float> networkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> networkIsDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<SessionPlayerId> networkSessionPlayerId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<PlayerLifeState> networkLifeState = new(
            PlayerLifeState.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> networkLifeStateDeadline = new(
            0.0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> networkInputReady = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool hasPreparedSnapshot;
        private bool preparedAsReconnect;
        private PlayerRuntimeSnapshot preparedSnapshot;

        public float CurrentHealth => networkHealth.Value;
        public float MaxHealth => maxHealth;
        public bool IsDead => networkIsDead.Value;
        public bool IsInputReady => networkInputReady.Value;
        public PlayerLifeState LifeState => networkLifeState.Value;
        public double LifeStateDeadline => networkLifeStateDeadline.Value;
        public SessionPlayerId StablePlayerId => networkSessionPlayerId.Value;

        public delegate void OnHealthChanged(float current, float max);
        public event OnHealthChanged HealthChangedEvent;

        public delegate void OnPlayerDeath();
        public event OnPlayerDeath PlayerDeathEvent;
        public static event System.Action<SessionPlayerId> ReconnectRestoreAcknowledged;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (hasPreparedSnapshot && preparedAsReconnect)
                    ApplyPreparedSnapshotServer();
                else
                    ApplyDefaultSpawnStateServer();

                PlayerSpawnedServer?.Invoke(this);
            }

            // Subscribe to network variable changes for UI updates
            networkHealth.OnValueChanged += OnHealthValueChanged;
            networkIsDead.OnValueChanged += OnDeadValueChanged;
            networkLifeState.OnValueChanged += OnLifeStateChanged;

            // Initial UI update
            HealthChangedEvent?.Invoke(networkHealth.Value, maxHealth);

            if (IsServer && preparedAsReconnect)
                BeginReconnectRestoreClientRpc(preparedSnapshot, CreateOwnerRpcParams());
        }

        public override void OnNetworkDespawn()
        {
            networkHealth.OnValueChanged -= OnHealthValueChanged;
            networkIsDead.OnValueChanged -= OnDeadValueChanged;
            networkLifeState.OnValueChanged -= OnLifeStateChanged;

            if (IsServer)
            {
                NetworkGameManager.Instance?.CaptureDisconnectedPlayer(this);
                PlayerDespawnedServer?.Invoke(this);
            }
        }

        private void OnHealthValueChanged(float oldValue, float newValue)
        {
            HealthChangedEvent?.Invoke(newValue, maxHealth);
        }

        private void OnDeadValueChanged(bool oldValue, bool newValue)
        {
            if (newValue && !oldValue)
            {
                PlayerDeathEvent?.Invoke();
            }
        }

        private void OnLifeStateChanged(PlayerLifeState oldState, PlayerLifeState newState)
        {
            if (!IsServer || newState != PlayerLifeState.Downed || oldState == PlayerLifeState.Downed)
                return;

            NetworkGameManager.Instance?.Telemetry?.RecordDowned(
                StablePlayerId,
                NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Tick : 0);
        }

        public void TakeDamage(float damage)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;

            networkHealth.Value = Mathf.Max(0, networkHealth.Value - Mathf.Max(0f, damage));

            NetworkGameManager.Instance?.Telemetry?.RecordHealth(
                StablePlayerId,
                NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Tick : 0,
                networkHealth.Value,
                damage);

            GameLog.Info(() => $"Player took {damage} damage. HP: {networkHealth.Value}/{maxHealth}");

            if (networkHealth.Value <= 0)
                Die();
        }

        private void Die()
        {
            if (networkIsDead.Value) return;

            networkIsDead.Value = true;
            networkLifeState.Value = PlayerLifeState.Dead;
            GameLog.Info(() => $"Player {OwnerClientId} died!");
            PlayerDiedServer?.Invoke(this, OwnerClientId);

            // Notify all clients
            OnPlayerDiedClientRpc();
        }

        [ClientRpc]
        private void OnPlayerDiedClientRpc()
        {
            PlayerDeathEvent?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;

            networkHealth.Value = Mathf.Min(networkHealth.Value + amount, maxHealth);
        }

        public void ResetHealth()
        {
            if (!IsServer) return;
            networkIsDead.Value = false;
            networkLifeState.Value = PlayerLifeState.Alive;
            networkLifeStateDeadline.Value = 0.0;
            networkHealth.Value = maxHealth;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;

            ApplyRespawnPose(position, rotation);
            networkIsDead.Value = false;
            networkLifeState.Value = PlayerLifeState.Alive;
            networkLifeStateDeadline.Value = 0.0;
            networkHealth.Value = maxHealth;
            RespawnClientRpc(position, rotation);
        }

        public void PrepareInitialSpawn(SessionPlayerId playerId)
        {
            preparedSnapshot = PlayerRuntimeSnapshot.CreateDefault(playerId, transform.position, transform.rotation);
            hasPreparedSnapshot = true;
            preparedAsReconnect = false;
        }

        public void PrepareReconnect(PlayerRuntimeSnapshot snapshot)
        {
            preparedSnapshot = snapshot;
            hasPreparedSnapshot = true;
            preparedAsReconnect = true;
        }

        public PlayerRuntimeSnapshot CaptureRuntimeSnapshot()
        {
            int serverTick = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Tick
                : 0;
            var snapshot = new PlayerRuntimeSnapshot
            {
                schemaVersion = NetworkProtocol.SnapshotSchemaVersion,
                sessionPlayerId = StablePlayerId,
                sceneName = new FixedString64Bytes(SceneManager.GetActiveScene().name),
                serverTick = serverTick,
                position = transform.position,
                rotation = transform.rotation,
                health = networkHealth.Value,
                lifeState = networkLifeState.Value,
                lifeStateDeadline = networkLifeStateDeadline.Value,
                inventorySchemaVersion = 1
            };

            WeaponManager manager = GetComponent<WeaponManager>();
            snapshot.equippedWeaponSlot = (byte)Mathf.Clamp(manager != null ? manager.CurrentWeaponIndex : 0, 0, byte.MaxValue);
            WeaponFireHandler fireHandler = GetComponent<WeaponFireHandler>();
            if (fireHandler != null)
            {
                snapshot.weaponSlot0 = fireHandler.CaptureWeaponSnapshot(0);
                snapshot.weaponSlot1 = fireHandler.CaptureWeaponSnapshot(1);
            }

            return snapshot;
        }

        private void ApplyDefaultSpawnStateServer()
        {
            networkSessionPlayerId.Value = hasPreparedSnapshot
                ? preparedSnapshot.sessionPlayerId
                : default;
            networkHealth.Value = maxHealth;
            networkIsDead.Value = false;
            networkLifeState.Value = PlayerLifeState.Alive;
            networkLifeStateDeadline.Value = 0.0;
            networkInputReady.Value = true;
        }

        private void ApplyPreparedSnapshotServer()
        {
            networkSessionPlayerId.Value = preparedSnapshot.sessionPlayerId;
            networkHealth.Value = Mathf.Clamp(preparedSnapshot.health, 0f, maxHealth);
            networkLifeState.Value = ResolveExpiredLifeState(preparedSnapshot.lifeState, preparedSnapshot.lifeStateDeadline);
            networkIsDead.Value = networkLifeState.Value == PlayerLifeState.Dead
                || networkLifeState.Value == PlayerLifeState.Spectating;
            networkLifeStateDeadline.Value = networkLifeState.Value == PlayerLifeState.Downed
                ? preparedSnapshot.lifeStateDeadline
                : 0.0;
            networkInputReady.Value = !preparedAsReconnect;
            ApplyRespawnPose(preparedSnapshot.position, preparedSnapshot.rotation);

            WeaponFireHandler fireHandler = GetComponent<WeaponFireHandler>();
            if (fireHandler != null)
            {
                fireHandler.RestoreServerSnapshot(preparedSnapshot);
                preparedSnapshot.weaponSlot0 = fireHandler.CaptureWeaponSnapshot(0);
                preparedSnapshot.weaponSlot1 = fireHandler.CaptureWeaponSnapshot(1);
            }
            GetComponent<WeaponManager>()?.SetEquippedWeaponServer(preparedSnapshot.equippedWeaponSlot);
        }

        private PlayerLifeState ResolveExpiredLifeState(PlayerLifeState state, double deadline)
        {
            if (state != PlayerLifeState.Downed || deadline <= 0.0)
                return state;

            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            return now >= deadline ? PlayerLifeState.Dead : state;
        }

        [ClientRpc]
        private void BeginReconnectRestoreClientRpc(PlayerRuntimeSnapshot snapshot, ClientRpcParams rpcParams = default)
        {
            ApplyRespawnPose(snapshot.position, snapshot.rotation);
            WeaponRuntimeSnapshot weaponSnapshot = snapshot.equippedWeaponSlot == 1
                ? snapshot.weaponSlot1
                : snapshot.weaponSlot0;
            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            GetComponent<WeaponManager>()?.ApplyAuthoritativeWeaponState(new WeaponOwnerState
            {
                slotIndex = snapshot.equippedWeaponSlot,
                magazineAmmo = weaponSnapshot.magazineAmmo,
                reserveAmmo = weaponSnapshot.reserveAmmo,
                isReloading = weaponSnapshot.reloadCompleteTime >= 0.0
                    && now < weaponSnapshot.reloadCompleteTime,
                acknowledgedFireSequence = weaponSnapshot.lastAcceptedFireSequence,
                lastFireResult = FireRejectReason.None,
                authoritativeShotTick = snapshot.serverTick
            });
            AcknowledgeReconnectRestoreServerRpc(snapshot.revision);
        }

        [ServerRpc]
        private void AcknowledgeReconnectRestoreServerRpc(uint revision, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;
            if (!preparedAsReconnect || revision != preparedSnapshot.revision)
                return;

            networkInputReady.Value = true;
            preparedAsReconnect = false;
            ReconnectRestoreAcknowledged?.Invoke(StablePlayerId);
            NetworkDiagnostics.Emit("reconnect_restore_ack", NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.State
                : SessionState.InMatch, playerId: StablePlayerId);
        }

        private ClientRpcParams CreateOwnerRpcParams()
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };
        }

        [ClientRpc]
        private void RespawnClientRpc(Vector3 position, Quaternion rotation)
        {
            ApplyRespawnPose(position, rotation);
        }

        private void ApplyRespawnPose(Vector3 position, Quaternion rotation)
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.TeleportForRespawn(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
