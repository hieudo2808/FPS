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
        private bool combatAvailabilityInitialized;
        private bool lastCombatAvailability;
        private double campaignProtectionUntil;
        private readonly NetworkVariable<uint> damageRevision = new();
        public uint DamageRevision => damageRevision.Value;
        private uint campaignTransferSerial;
        private bool campaignTransferPending;
        private Vector3 campaignTransferPosition;
        private Quaternion campaignTransferRotation;
        private double nextCampaignTransferRetry;
        public bool CampaignTransferPending => campaignTransferPending;
        private double CampaignNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

        private void Update()
        {
            if (IsServer && CampaignMissionController.Instance != null && LifeState == PlayerLifeState.Downed && CampaignNow >= LifeStateDeadline)
                Die();
            if (IsServer && campaignTransferPending && CampaignNow >= nextCampaignTransferRetry)
                SendCampaignTransfer();
        }

        public void RelocateCampaign(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            campaignTransferSerial++;
            campaignTransferPending = true;
            campaignTransferPosition = position;
            campaignTransferRotation = rotation;
            networkInputReady.Value = false;
            ApplyRespawnPose(position, rotation);
            SendCampaignTransfer();
        }
        private void SendCampaignTransfer()
        {
            nextCampaignTransferRetry = CampaignNow + 1;
            CampaignTransferClientRpc(campaignTransferPosition, campaignTransferRotation, campaignTransferSerial);
        }
        [ClientRpc]
        private void CampaignTransferClientRpc(Vector3 position, Quaternion rotation, uint serial)
        {
            ApplyRespawnPose(position, rotation);
            if (IsOwner) AcknowledgeCampaignTransferServerRpc(serial);
        }
        [ServerRpc]
        private void AcknowledgeCampaignTransferServerRpc(uint serial)
        {
            if (!campaignTransferPending || serial != campaignTransferSerial) return;
            campaignTransferPending = false;
            networkInputReady.Value = true;
        }
        public void SetCampaignSpectating()
        {
            if (!IsServer) return;
            networkLifeState.Value = PlayerLifeState.Spectating;
            networkIsDead.Value = true;
            networkLifeStateDeadline.Value = 0;
        }
        public void ReviveCampaign(float health)
        {
            if (!IsServer) return;
            networkIsDead.Value = false;
            networkLifeState.Value = PlayerLifeState.Alive;
            networkLifeStateDeadline.Value = 0;
            networkHealth.Value = Mathf.Clamp(health, 1, maxHealth);
            campaignProtectionUntil = CampaignNow + (CampaignMissionController.Instance?.settings.reviveProtectionSeconds ?? 2);
        }
        public void RestoreCampaignSnapshot(PlayerRuntimeSnapshot snapshot)
        {
            if (!IsServer) return;
            campaignTransferPending = false;
            preparedSnapshot = snapshot;
            preparedAsReconnect = true;
            ApplyPreparedSnapshotServer();
            BeginReconnectRestoreClientRpc(snapshot, CreateOwnerRpcParams());
        }

        public float CurrentHealth => networkHealth.Value;
        public float MaxHealth => maxHealth;
        public bool IsDead => networkIsDead.Value;
        public bool IsInputReady => networkInputReady.Value;
        public PlayerLifeState LifeState => networkLifeState.Value;
        public bool CanUseCombat => LifeState == PlayerLifeState.Alive && IsInputReady && !IsDead;
        public double LifeStateDeadline => networkLifeStateDeadline.Value;
        public SessionPlayerId StablePlayerId => networkSessionPlayerId.Value;

        public delegate void OnHealthChanged(float current, float max);
        public event OnHealthChanged HealthChangedEvent;

        public delegate void OnPlayerDeath();
        public event OnPlayerDeath PlayerDeathEvent;
        public event System.Action<bool> CombatAvailabilityChanged;

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
            networkInputReady.OnValueChanged += OnInputReadyChanged;
            RefreshCombatAvailability(raiseInitialEvent: false);

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
            networkInputReady.OnValueChanged -= OnInputReadyChanged;
            combatAvailabilityInitialized = false;

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
            RefreshCombatAvailability();
            if (newValue && !oldValue)
            {
                PlayerDeathEvent?.Invoke();
            }
        }

        private void OnLifeStateChanged(PlayerLifeState oldState, PlayerLifeState newState)
        {
            RefreshCombatAvailability();
            if (!IsServer || newState != PlayerLifeState.Downed || oldState == PlayerLifeState.Downed)
                return;

            NetworkGameManager.Instance?.Telemetry?.RecordDowned(
                StablePlayerId,
                NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Tick : 0);
        }

        private void OnInputReadyChanged(bool oldValue, bool newValue)
        {
            RefreshCombatAvailability();
        }

        private void RefreshCombatAvailability(bool raiseInitialEvent = true)
        {
            bool canUseCombat = CanUseCombat;
            if (!combatAvailabilityInitialized)
            {
                combatAvailabilityInitialized = true;
                lastCombatAvailability = canUseCombat;
                if (raiseInitialEvent)
                    CombatAvailabilityChanged?.Invoke(canUseCombat);
                return;
            }

            if (lastCombatAvailability == canUseCombat)
                return;

            lastCombatAvailability = canUseCombat;
            CombatAvailabilityChanged?.Invoke(canUseCombat);
        }

        public void TakeDamage(float damage) => ApplyDamage(damage, interruptTreatment: true);

        // Sepsis drains health but must not make the five-second antidote impossible to finish.
        public void TakeInfectionDamage(float damage) => ApplyDamage(damage, interruptTreatment: false);

        private void ApplyDamage(float damage, bool interruptTreatment)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;
            var campaign = CampaignMissionController.Instance;
            if (campaign != null && (campaign.BlocksInput || CampaignNow < campaignProtectionUntil)) return;
            if (!float.IsFinite(damage) || damage <= 0) return;
            if (interruptTreatment) damageRevision.Value++;
            if (campaign != null && LifeState == PlayerLifeState.Downed)
            {
                networkLifeStateDeadline.Value -= damage * campaign.settings.bleedoutDamageSeconds;
                if (CampaignNow >= LifeStateDeadline) Die();
                return;
            }

            networkHealth.Value = Mathf.Max(0, networkHealth.Value - Mathf.Max(0f, damage));

            NetworkGameManager.Instance?.Telemetry?.RecordHealth(
                StablePlayerId,
                NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Tick : 0,
                networkHealth.Value,
                damage);

            GameLog.Info(() => $"Player took {damage} damage. HP: {networkHealth.Value}/{maxHealth}");

            if (networkHealth.Value <= 0)
            {
                if (campaign != null && campaign.ActivePlayerCount > 1)
                {
                    networkLifeState.Value = PlayerLifeState.Downed;
                    networkLifeStateDeadline.Value = CampaignNow + campaign.settings.bleedoutSeconds;
                }
                else Die();
            }
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
            if (networkIsDead.Value || !float.IsFinite(amount) || amount <= 0f) return;

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

            GetComponent<SurvivalInventory>()?.CancelUseServer();
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
                infection = GetComponent<PlayerInfectionController>()?.CurrentInfection ?? 0f,
                lifeState = networkLifeState.Value,
                lifeStateDeadline = networkLifeStateDeadline.Value,
                inventorySchemaVersion = 5,
                medicineCount = GetComponent<SurvivalInventory>()?.MedkitCount.Value ?? 0,
                campaignChapter = CampaignMissionController.Instance != null ? (byte)((int)CampaignMissionController.Instance.SnapshotChapter + 1) : (byte)0
            };

            WeaponManager manager = GetComponent<WeaponManager>();
            snapshot.equippedWeaponSlot = (byte)Mathf.Clamp(manager != null ? manager.CurrentWeaponIndex : 0, 0, byte.MaxValue);
            snapshot.primaryWeaponId = manager != null ? manager.ActivePrimaryWeaponId : PrimaryWeaponId.Vandal;
            WeaponFireHandler fireHandler = GetComponent<WeaponFireHandler>();
            if (fireHandler != null)
            {
                snapshot.weaponSlot0 = fireHandler.CaptureWeaponSnapshot(0);
                snapshot.weaponSlot1 = fireHandler.CaptureWeaponSnapshot(1);
            }

            GetComponent<SurvivalInventory>()?.Capture(ref snapshot);
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
            GetComponent<PlayerInfectionController>()?.SetInfectionServer(0f);
        }

        private void ApplyPreparedSnapshotServer()
        {
            GetComponent<SurvivalInventory>()?.RestoreServer(preparedSnapshot);
            networkSessionPlayerId.Value = preparedSnapshot.sessionPlayerId;
            networkHealth.Value = Mathf.Clamp(preparedSnapshot.health, 0f, maxHealth);
            networkLifeState.Value = ResolveExpiredLifeState(preparedSnapshot.lifeState, preparedSnapshot.lifeStateDeadline);
            networkIsDead.Value = networkLifeState.Value == PlayerLifeState.Dead
                || networkLifeState.Value == PlayerLifeState.Spectating;
            networkLifeStateDeadline.Value = networkLifeState.Value == PlayerLifeState.Downed
                ? preparedSnapshot.lifeStateDeadline
                : 0.0;
            networkInputReady.Value = !preparedAsReconnect;
            GetComponent<PlayerInfectionController>()?.SetInfectionServer(preparedSnapshot.infection);
            ApplyRespawnPose(preparedSnapshot.position, preparedSnapshot.rotation);

            WeaponManager manager = GetComponent<WeaponManager>();
            manager?.RestorePrimaryWeaponServer(preparedSnapshot.primaryWeaponId);

            WeaponFireHandler fireHandler = GetComponent<WeaponFireHandler>();
            if (fireHandler != null)
            {
                fireHandler.RestoreServerSnapshot(preparedSnapshot);
                preparedSnapshot.weaponSlot0 = fireHandler.CaptureWeaponSnapshot(0);
                preparedSnapshot.weaponSlot1 = fireHandler.CaptureWeaponSnapshot(1);
            }
            manager?.RestoreEquippedWeaponServer(preparedSnapshot.equippedWeaponSlot);
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
                equipCompleteTime = weaponSnapshot.equipCompleteTime,
                acknowledgedFireSequence = weaponSnapshot.lastAcceptedFireSequence,
                lastFireResult = FireRejectReason.None,
                authoritativeShotTick = snapshot.serverTick
            });
            if (IsSpawned && NetworkManager != null && NetworkManager.IsListening)
            {
                AcknowledgeReconnectRestoreServerRpc(snapshot.revision);
            }
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
