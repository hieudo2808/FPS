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
        [SerializeField] private string gameScene = "GameScene";

        public event Action OnHostStarted;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;
        public event Action<string> OnConnectionFailed;

        public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        public bool IsHosting => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public int ConnectedPlayerCount => NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;

        public string CurrentJoinCode { get; private set; } = "";
        public bool IsServicesInitialized { get; private set; } = false;

        private ISession currentSession;

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
                    Debug.Log($"[Services] Signed in anonymously as {AuthenticationService.Instance.PlayerId}");
                }
                
                IsServicesInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Services] Error initializing Unity Services: {e}");
            }
        }

        public async void StartHostGame()
        {
            if (!IsServicesInitialized)
            {
                OnConnectionFailed?.Invoke("Services not initialized yet. Please wait.");
                return;
            }

            try 
            {
                // Đăng ký callback TRƯỚC khi tạo session để không bỏ lỡ event
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // WithRelayNetwork() để MPS tự cấu hình Relay + StartHost()
                var options = new SessionOptions
                {
                    MaxPlayers = 4
                }.WithRelayNetwork();

                currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                CurrentJoinCode = currentSession.Code;

                Debug.Log($"[Multiplayer] Session created. Join Code: {CurrentJoinCode}");

                OnHostStarted?.Invoke();

                // MPS đã tự gọi StartHost(), chỉ cần load scene
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene(gameScene, LoadSceneMode.Single);
                }
            } 
            catch(Exception e) 
            {
                Debug.LogError(e);
                OnConnectionFailed?.Invoke(e.Message);
            }
        }

        public async void JoinGame(string joinCode)
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

            try 
            {
                // Đăng ký callback TRƯỚC khi join session
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // MPS tự cấu hình Relay transport + StartClient() khi join
                currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                CurrentJoinCode = joinCode;

                Debug.Log("[Multiplayer] Joined session successfully.");
            } 
            catch(Exception e) 
            {
                Debug.LogError(e);
                OnConnectionFailed?.Invoke("Invalid Join Code or Room Full: " + e.Message);
            }
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

            NetworkManager.Singleton.Shutdown();
            currentSession = null;
            CurrentJoinCode = "";
            Debug.Log("[NetworkGameManager] Disconnected");

            SceneManager.LoadScene(mainMenuScene);
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[NetworkGameManager] Client connected: {clientId}");

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                OnClientConnected?.Invoke();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[NetworkGameManager] Client disconnected: {clientId}");

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                OnClientDisconnected?.Invoke();
                currentSession = null;
                CurrentJoinCode = "";
                SceneManager.LoadScene(mainMenuScene);
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
