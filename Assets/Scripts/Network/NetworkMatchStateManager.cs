using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum NetworkMatchState
    {
        Lobby,
        Loading,
        Warmup,
        Playing,
        GameOver
    }

    public class NetworkMatchStateManager : NetworkBehaviour
    {
        public static NetworkMatchStateManager Instance { get; private set; }

        private readonly NetworkVariable<NetworkMatchState> state = new(
            NetworkMatchState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> stateStartedServerTime = new(
            0.0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Dictionary<ulong, double> respawnDueTimes = new();
        private double localRespawnDueServerTime = -1.0;

        public event Action<NetworkMatchState, NetworkMatchState> OnStateChanged;

        public NetworkMatchState State => state.Value;
        public double StateStartedServerTime => stateStartedServerTime.Value;
        public float StateElapsedSeconds => Mathf.Max(0f, (float)(GetServerTime() - stateStartedServerTime.Value));
        public float WarmupRemainingSeconds => State == NetworkMatchState.Warmup
            ? Mathf.Max(0f, NetworkGameplayPolicy.WarmupSeconds - StateElapsedSeconds)
            : 0f;
        public float LocalRespawnRemainingSeconds => localRespawnDueServerTime > 0.0
            ? Mathf.Max(0f, (float)(localRespawnDueServerTime - GetServerTime()))
            : 0f;

        public static bool IsGameplayActive => Instance == null || Instance.State == NetworkMatchState.Playing;
        public static bool IsGameplayBlocked => !IsGameplayActive;
        public static bool HasInstance => Instance != null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            PlayerHealth.PlayerDiedServer += HandlePlayerDiedServer;
        }

        private void OnDisable()
        {
            PlayerHealth.PlayerDiedServer -= HandlePlayerDiedServer;
            if (Instance == this)
                InputManager.MatchInputBlocked = false;
        }

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += HandleStateChanged;
            ApplyLocalInputBlock();
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= HandleStateChanged;
            if (Instance == this)
                InputManager.MatchInputBlocked = false;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            ApplyLocalInputBlock();

            if (!CanRunServerLogic())
                return;

            if (state.Value == NetworkMatchState.Warmup
                && StateElapsedSeconds >= NetworkGameplayPolicy.WarmupSeconds)
            {
                EnterPlaying();
            }

            if (state.Value == NetworkMatchState.Playing)
                ProcessRespawns();
        }

        public void EnterLoading() => TryEnterState(NetworkMatchState.Loading);
        public void EnterWarmup() => TryEnterState(NetworkMatchState.Warmup);
        public void EnterPlaying() => TryEnterState(NetworkMatchState.Playing);
        public void EnterGameOver() => TryEnterState(NetworkMatchState.GameOver);

        public bool TryEnterState(NetworkMatchState nextState)
        {
            if (!CanRunServerLogic())
                return false;

            if (state.Value == nextState)
                return true;

            NetworkMatchState previous = state.Value;
            bool invokeManually = !IsSpawned;
            stateStartedServerTime.Value = GetServerTime();
            state.Value = nextState;
            if (invokeManually)
                HandleStateChanged(previous, nextState);
            return true;
        }

        public void SetStateForTests(NetworkMatchState nextState, double startedTime = 0.0)
        {
            // EditMode không gọi Awake cho MonoBehaviour thường, nên test seam phải tự claim singleton.
            Instance = this;
            NetworkMatchState previous = state.Value;
            stateStartedServerTime.Value = startedTime;
            state.Value = nextState;
            HandleStateChanged(previous, nextState);
        }

        public bool TryGetRespawnRemaining(ulong clientId, out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (!respawnDueTimes.TryGetValue(clientId, out double dueTime))
                return false;

            remainingSeconds = Mathf.Max(0f, (float)(dueTime - GetServerTime()));
            return true;
        }

        private void HandleStateChanged(NetworkMatchState oldState, NetworkMatchState newState)
        {
            ApplyLocalInputBlock();
            OnStateChanged?.Invoke(oldState, newState);
        }

        private void ApplyLocalInputBlock()
        {
            InputManager.MatchInputBlocked =
                state.Value == NetworkMatchState.Loading
                || state.Value == NetworkMatchState.Warmup
                || state.Value == NetworkMatchState.GameOver;
        }

        private void HandlePlayerDiedServer(PlayerHealth playerHealth, ulong clientId)
        {
            if (!CanRunServerLogic() || playerHealth == null)
                return;

            if (state.Value != NetworkMatchState.Playing)
                return;

            double dueTime = GetServerTime() + NetworkGameplayPolicy.RespawnSeconds;
            respawnDueTimes[clientId] = dueTime;
            SendRespawnScheduledClientRpc(dueTime, CreateSingleClientParams(clientId));
        }

        private void ProcessRespawns()
        {
            if (respawnDueTimes.Count == 0)
                return;

            double now = GetServerTime();
            List<ulong> completed = null;

            foreach (var entry in respawnDueTimes)
            {
                if (entry.Value > now)
                    continue;

                if (TryRespawnPlayer(entry.Key))
                {
                    completed ??= new List<ulong>();
                    completed.Add(entry.Key);
                }
            }

            if (completed == null)
                return;

            for (int i = 0; i < completed.Count; i++)
            {
                ulong clientId = completed[i];
                respawnDueTimes.Remove(clientId);
                SendRespawnCompletedClientRpc(CreateSingleClientParams(clientId));
            }
        }

        private bool TryRespawnPlayer(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return false;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
                return false;

            if (client.PlayerObject == null)
                return false;

            PlayerHealth playerHealth = client.PlayerObject.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                return false;

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            SpawnRequest request = new SpawnRequest(clientId);
            if (NetworkSpawnManager.Instance != null)
                NetworkSpawnManager.Instance.TryGetSpawnPose(out position, out rotation, request);

            playerHealth.Respawn(position, rotation);
            return true;
        }

        [ClientRpc]
        private void SendRespawnScheduledClientRpc(double dueServerTime, ClientRpcParams clientRpcParams = default)
        {
            localRespawnDueServerTime = dueServerTime;
        }

        [ClientRpc]
        private void SendRespawnCompletedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            localRespawnDueServerTime = -1.0;
        }

        private static ClientRpcParams CreateSingleClientParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }

        private static bool CanRunServerLogic()
        {
            return NetworkManager.Singleton == null
                || !NetworkManager.Singleton.IsListening
                || NetworkManager.Singleton.IsServer;
        }

        private static double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }
    }
}
