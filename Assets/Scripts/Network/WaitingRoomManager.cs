using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Data cho mỗi player trong lobby. Sync qua NetworkList.
    /// </summary>
    public struct PlayerLobbyData : INetworkSerializable, IEquatable<PlayerLobbyData>
    {
        public ulong clientId;
        public FixedString64Bytes playerName;
        public bool isReady;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref playerName);
            serializer.SerializeValue(ref isReady);
        }

        public bool Equals(PlayerLobbyData other)
        {
            return clientId == other.clientId
                && playerName.Equals(other.playerName)
                && isReady == other.isReady;
        }

        public override int GetHashCode()
        {
            return clientId.GetHashCode();
        }
    }

    /// <summary>
    /// Server-authoritative lobby manager. Đặt trong LobbyScene.
    /// Quản lý player list, ready state, và start match.
    /// </summary>
    public class WaitingRoomManager : NetworkBehaviour
    {
        public static WaitingRoomManager Instance { get; private set; }

        public NetworkList<PlayerLobbyData> Players { get; private set; }

        public event Action OnPlayerListChanged;
        public event Action<bool> OnAllReadyChanged;

        private bool cachedAllReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Players = new NetworkList<PlayerLobbyData>();
        }

        public override void OnNetworkSpawn()
        {
            Players.OnListChanged += HandlePlayerListChanged;

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // Host tự thêm chính mình
                AddPlayer(NetworkManager.Singleton.LocalClientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            Players.OnListChanged -= HandlePlayerListChanged;

            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==========================================
        // SERVER — Player join/leave
        // ==========================================

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            // Host đã tự thêm trong OnNetworkSpawn, bỏ qua
            if (clientId == NetworkManager.Singleton.LocalClientId) return;

            AddPlayer(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId == clientId)
                {
                    Players.RemoveAt(i);
                    break;
                }
            }
        }

        private void AddPlayer(ulong clientId)
        {
            // Tránh duplicate
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].clientId == clientId) return;

            Players.Add(new PlayerLobbyData
            {
                clientId = clientId,
                playerName = "Player",
                isReady = false
            });

            // Yêu cầu client gửi tên lên
            RequestPlayerNameClientRpc(clientId);
        }

        // ==========================================
        // NAME SYNC — Client gửi tên từ PlayerPrefs lên server
        // ==========================================

        [ClientRpc]
        private void RequestPlayerNameClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            string name = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
            SubmitPlayerNameServerRpc(new FixedString64Bytes(name));
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitPlayerNameServerRpc(FixedString64Bytes name, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId == senderId)
                {
                    var data = Players[i];
                    data.playerName = name;
                    Players[i] = data;
                    break;
                }
            }
        }

        // ==========================================
        // READY SYSTEM
        // ==========================================

        [ServerRpc(RequireOwnership = false)]
        public void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId == senderId)
                {
                    var data = Players[i];
                    data.isReady = !data.isReady;
                    Players[i] = data;
                    break;
                }
            }
        }

        public bool AreAllPlayersReady()
        {
            if (Players.Count < 1) return false;

            for (int i = 0; i < Players.Count; i++)
                if (!Players[i].isReady) return false;

            return true;
        }

        // ==========================================
        // START MATCH — Host only
        // ==========================================

        public void StartMatch()
        {
            if (!IsServer) return;
            if (!AreAllPlayersReady()) return;

            NetworkGameManager.Instance?.StartMatch();
        }

        // ==========================================
        // UI EVENTS
        // ==========================================

        private void HandlePlayerListChanged(NetworkListEvent<PlayerLobbyData> changeEvent)
        {
            OnPlayerListChanged?.Invoke();

            bool allReady = AreAllPlayersReady();
            if (allReady != cachedAllReady)
            {
                cachedAllReady = allReady;
                OnAllReadyChanged?.Invoke(allReady);
            }
        }
    }
}
