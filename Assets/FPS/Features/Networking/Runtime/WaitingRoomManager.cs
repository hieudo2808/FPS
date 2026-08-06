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
        // Keep this as a byte on the wire so enum additions remain explicit and bounded.
        public byte characterId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref playerName);
            serializer.SerializeValue(ref isReady);
            serializer.SerializeValue(ref characterId);
        }

        public bool Equals(PlayerLobbyData other)
        {
            return clientId == other.clientId
                && playerName.Equals(other.playerName)
                && isReady == other.isReady
                && characterId == other.characterId;
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

        [Header("Character Selection")]
        [SerializeField] private PlayerPrefabCatalog playerPrefabCatalog;

        public NetworkList<PlayerLobbyData> Players { get; private set; }
        public NetworkVariable<DifficultyLevel> LobbyDifficulty { get; private set; } = new NetworkVariable<DifficultyLevel>(DifficultyLevel.Medium);

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
                ApplyApprovedNames();
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

        public override void OnDestroy()
        {
            base.OnDestroy();
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
            ApplyApprovedName(clientId);
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

            PlayerCharacterId selectedCharacter = PlayerCharacterId.Clove;
            NetworkGameManager.Instance?.TryGetPlayerCharacter(clientId, out selectedCharacter);

            Players.Add(new PlayerLobbyData
            {
                clientId = clientId,
                playerName = "Player",
                isReady = false,
                characterId = (byte)selectedCharacter
            });

            ApplyApprovedName(clientId);
        }

        // ==========================================
        // NAME SYNC — Server applies the name carried in connection approval.
        // ==========================================

        private void ApplyApprovedNames()
        {
            if (!IsServer || NetworkManager.Singleton == null)
                return;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                ApplyApprovedName(clientId);
        }

        private void ApplyApprovedName(ulong clientId)
        {
            if (!IsServer || NetworkGameManager.Instance == null
                || !NetworkGameManager.Instance.TryGetApprovedPlayerName(clientId, out string playerName))
                return;

            ApplyApprovedPlayerName(clientId, playerName);
        }

        public void ApplyApprovedPlayerName(ulong clientId, string name)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId == clientId)
                {
                    var data = Players[i];
                    data.playerName = new FixedString64Bytes(name ?? "Player");
                    Players[i] = data;
                    break;
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetCharacterServerRpc(PlayerCharacterId characterId, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!Enum.IsDefined(typeof(PlayerCharacterId), characterId))
                return;

            PlayerPrefabCatalog catalog = GetCatalog();
            if (catalog == null || !catalog.TryGetPrefab(characterId, out _))
                return;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId != senderId)
                    continue;

                PlayerLobbyData data = Players[i];
                data.characterId = (byte)characterId;
                // A character change invalidates a previous ready vote.
                data.isReady = false;
                Players[i] = data;
                NetworkGameManager.Instance?.SetPlayerCharacter(senderId, characterId);
                return;
            }
        }

        public bool TryGetCharacter(ulong clientId, out PlayerCharacterId characterId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId == clientId
                    && Enum.IsDefined(typeof(PlayerCharacterId), Players[i].characterId))
                {
                    characterId = (PlayerCharacterId)Players[i].characterId;
                    return true;
                }
            }

            characterId = PlayerCharacterId.Clove;
            return false;
        }

        public string GetCharacterDisplayName(PlayerCharacterId id)
        {
            return GetCatalog()?.GetDisplayName(id) ?? id.ToString();
        }

        private PlayerPrefabCatalog GetCatalog()
        {
            return playerPrefabCatalog != null
                ? playerPrefabCatalog
                : Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
        }

        // ==========================================
        // READY SYSTEM
        // ==========================================

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleReadyServerRpc(RpcParams rpcParams = default)
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
        // SET DIFFICULTY — Host only
        // ==========================================

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetDifficultyServerRpc(DifficultyLevel level, RpcParams rpcParams = default)
        {
            // Only host can change difficulty (Assuming Host is LocalClientId 0 or just check if sender is Host)
            if (rpcParams.Receive.SenderClientId != NetworkManager.Singleton.LocalClientId) return; 
            LobbyDifficulty.Value = level;
            
            if (NetworkGameManager.Instance != null)
                NetworkGameManager.Instance.SelectedDifficulty = level;
        }

        // ==========================================
        // START MATCH — Host only
        // ==========================================

        public void StartMatch()
        {
            if (!IsServer) return;
            if (!AreAllPlayersReady()) return;

            PlayerPrefabCatalog catalog = GetCatalog();
            string catalogError = catalog == null ? "Catalog asset is missing." : string.Empty;
            if (catalog == null || !catalog.IsComplete(out catalogError))
            {
                GameLog.Warning(() => $"Cannot start match: character catalog is invalid. {catalogError}");
                return;
            }

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.SelectedDifficulty = LobbyDifficulty.Value;
                NetworkGameManager.Instance.StartMatch();
            }
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
