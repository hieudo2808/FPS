using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

namespace FPS
{
    public class NetworkGameManager : MonoBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string lobbyScene = "LobbyScene";
        [SerializeField] private string gameScene = "GameScene";

        public event Action OnHostStarted;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;
        public event Action<string> OnConnectionFailed;
        public event Action<ulong> OnPlayerJoinedLobby;
        public event Action<ulong> OnPlayerLeftLobby;

        public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        public bool IsHosting => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public int ConnectedPlayerCount => NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;

        public string CurrentJoinCode { get; private set; } = "";
        public bool IsServicesInitialized { get; private set; } = false;
        public bool IsInLobby { get; private set; }

        private ISession currentSession;
        private GameObject playerPrefabCache;

        private async void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                await InitializeUnityServicesAsync();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async Task InitializeUnityServicesAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    // If a player name is saved in settings, set it to the profile
                    string playerName = PlayerPrefs.GetString("PlayerName", "Player" + UnityEngine.Random.Range(1000, 9999));
                    if (AuthenticationService.Instance.Profile != playerName)
                    {
                        var parts = playerName.Split(' ');
                        AuthenticationService.Instance.SwitchProfile(parts[0].Substring(0, Mathf.Min(parts[0].Length, 30)));
                    }

                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                
                IsServicesInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Services] Error initializing Unity Services: {e}");
            }
        }

        public void StartHostGame() => _ = StartHostGameAsync();

        private async Task StartHostGameAsync()
        {
            if (!IsServicesInitialized)
            {
                OnConnectionFailed?.Invoke("Services not initialized yet. Please wait.");
                return;
            }

            bool callbacksRegistered = false;
            try 
            {
                // Đăng ký callback TRƯỚC khi tạo session để không bỏ lỡ event
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
                callbacksRegistered = true;

                // Tắt auto-spawn player — chỉ spawn khi vào GameScene
                SetupConnectionApproval();

                // WithRelayNetwork() để MPS tự cấu hình Relay + StartHost()
                var options = new SessionOptions
                {
                    MaxPlayers = 4
                }.WithRelayNetwork();

                currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                CurrentJoinCode = currentSession.Code;

                OnHostStarted?.Invoke();

                // Load LobbyScene (phòng chờ) thay vì GameScene
                if (NetworkManager.Singleton.IsServer)
                {
                    IsInLobby = true;
                    NetworkManager.Singleton.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
                }
            } 
            catch(Exception e) 
            {
                if (callbacksRegistered)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                }
                Debug.LogError(e);
                OnConnectionFailed?.Invoke(e.Message);
            }
        }

        public void JoinGame(string joinCode) => _ = JoinGameAsync(joinCode);

        private async Task JoinGameAsync(string joinCode)
        {
            if (!IsServicesInitialized)
            {
                OnConnectionFailed?.Invoke("Services not initialized yet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnConnectionFailed?.Invoke("Join Code is empty!");
                return;
            }

            bool callbacksRegistered = false;
            try 
            {
                // Tắt auto-spawn player cho client
                SetupConnectionApproval();

                // Đăng ký callback TRƯỚC khi join session
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
                callbacksRegistered = true;

                // MPS tự cấu hình Relay transport + StartClient() khi join
                currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                CurrentJoinCode = joinCode;
            } 
            catch(Exception e) 
            {
                if (callbacksRegistered)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                }
                Debug.LogError(e);
                OnConnectionFailed?.Invoke("Invalid Join Code or Room Full: " + e.Message);
            }
        }

        public void Disconnect() => _ = DisconnectAsync();

        private async Task DisconnectAsync()
        {
            if (currentSession != null)
            {
                try { await currentSession.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"[Session] Leave failed: {e.Message}"); }
                currentSession = null;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                NetworkManager.Singleton.Shutdown();
            }

            CurrentJoinCode = "";
            IsInLobby = false;
            SceneManager.LoadScene(mainMenuScene);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
                OnClientConnected?.Invoke();

            if (IsInLobby)
                OnPlayerJoinedLobby?.Invoke(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (IsInLobby)
                OnPlayerLeftLobby?.Invoke(clientId);

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                OnClientDisconnected?.Invoke();
                currentSession = null;
                CurrentJoinCode = "";
                IsInLobby = false;
                SceneManager.LoadScene(mainMenuScene);
            }
        }

        /// <summary>
        /// Host gọi khi tất cả player ready. Lock session + load GameScene.
        /// </summary>
        public void StartMatch() => _ = StartMatchAsync();

        private async Task StartMatchAsync()
        {
            if (!IsHosting) return;
            if (!IsInLobby) return;

            try
            {
                // Lock session — không ai join thêm được
                if (currentSession != null)
                {
                    var hostSession = currentSession.AsHost();
                    hostSession.IsLocked = true;
                    await hostSession.SavePropertiesAsync();
                }

                IsInLobby = false;

                // Đăng ký callback spawn player khi GameScene load xong
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleGameSceneLoaded;
                NetworkManager.Singleton.SceneManager.LoadScene(gameScene, LoadSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkGameManager] StartMatch failed: {e.Message}");
            }
        }

        // ==========================================
        // CONNECTION APPROVAL — Chặn auto-spawn player
        // ==========================================

        private void SetupConnectionApproval()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            // Cache player prefab để spawn thủ công sau
            if (nm.NetworkConfig.PlayerPrefab != null && playerPrefabCache == null)
                playerPrefabCache = nm.NetworkConfig.PlayerPrefab;

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = ApproveConnection;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = false; // KHÔNG spawn player tự động
        }

        // ==========================================
        // SPAWN PLAYERS — Khi GameScene load xong
        // ==========================================

        private void HandleGameSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode,
            System.Collections.Generic.List<ulong> clientsCompleted,
            System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;

            if (!NetworkManager.Singleton.IsServer) return;
            if (playerPrefabCache == null) return;

            // Spawn player cho tất cả connected clients
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var playerObj = Instantiate(playerPrefabCache);
                var netObj = playerObj.GetComponent<NetworkObject>();
                netObj.SpawnAsPlayerObject(clientId, true);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        public static bool HasInstance => Instance != null;
    }
}
