using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS
{
    /// <summary>
    /// Persistent composition root and backwards-compatible UI facade for the session layer.
    /// SessionCoordinator owns operation/FSM state, PlayerSessionRegistry owns stable identity,
    /// and NetworkMatchStateManager owns replicated gameplay state.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        private const string ReconnectGrantMessage = "FPS/A1/ReconnectGrant";
        private const string ReconnectPrepareDisconnectMessage = "FPS/A1/PrepareDisconnect";
        private const float VerificationDisconnectDrainSeconds = 0.25f;

        private sealed class PendingApproval
        {
            public PlayerSessionRecord Record;
            public SessionCredentials Credentials;
            public bool IsReconnect;
            public string PlayerName;
        }

        private sealed class AttemptWindow
        {
            public double StartedAt;
            public int Count;
        }

        public static NetworkGameManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string lobbyScene = "LobbyScene";
        [SerializeField] private string gameScene = "GameScene";

        [Header("A1 Network Policy")]
        [SerializeField] private NetworkHardeningConfig hardeningConfig;
        [SerializeField] private LayerMask reconnectHazardLayers;

        [Header("Character Prefabs")]
        [SerializeField] private PlayerPrefabCatalog playerPrefabCatalog;

        public event Action OnHostStarted;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;
        public event Action<string> OnConnectionFailed;
        public event Action<ulong> OnPlayerJoinedLobby;
        public event Action<ulong> OnPlayerLeftLobby;
        public event Action<SessionState, SessionState> OnSessionStateChanged;

        public DifficultyLevel SelectedDifficulty { get; set; } = DifficultyLevel.Medium;
        public SessionState State => sessionCoordinator?.State ?? SessionState.Offline;
        public string LastDisconnectMessage { get; private set; } = string.Empty;
        public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        public bool IsHosting => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public int ConnectedPlayerCount => NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;
        public string CurrentJoinCode { get; private set; } = string.Empty;
        public bool IsServicesInitialized { get; private set; }
        public bool IsInLobby { get; private set; }
        public PickupTransactionService PickupTransactions { get; private set; }
        public ServerTelemetryAggregator Telemetry { get; private set; }
        public AdaptiveDifficultyMetricsCollector AdaptiveMetrics { get; private set; }
        public NetworkHardeningSettings Settings => settings;

        private readonly Dictionary<ulong, PendingApproval> pendingApprovals = new();
        private readonly Dictionary<ulong, string> connectionPlayerNames = new();
        private readonly Dictionary<string, AttemptWindow> reconnectAttemptWindows = new(StringComparer.Ordinal);
        private readonly HashSet<ulong> capturedDisconnects = new();

        private SessionCoordinator sessionCoordinator;
        private PlayerSessionRegistry playerRegistry;
        private NetworkHardeningSettings settings;
        private ISession currentSession;
        private SessionPlayerId localStablePlayerId;
        private string localReconnectToken = string.Empty;
        private bool callbacksRegistered;
        private bool customMessageRegistered;
        private bool matchStarted;
        private bool intentionalShutdown;
        private bool suppressDisconnectHandling;
        private bool holdReconnectForVerification;
        private bool verificationTransportShutdown;
        private Task verificationLeaveTask;
        private double verificationDisconnectAt = -1d;
        private bool verificationDisconnectStarted;
        private bool terminateOnHostLossForVerification;
        private Coroutine sceneLoadTimeoutRoutine;
        private SessionOperation matchLoadOperation;
        private CancellationTokenSource lifetimeCancellation;
        private Task cleanupTask;

        private async void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            settings = hardeningConfig != null ? hardeningConfig.ToSettings() : NetworkHardeningSettings.Default;
            NetworkHardeningRuntime.Apply(settings);
            sessionCoordinator = new SessionCoordinator();
            sessionCoordinator.StateChanged += HandleSessionStateChanged;
            playerRegistry = new PlayerSessionRegistry(settings.MaxPlayers);
            PickupTransactions = new PickupTransactionService(settings.PickupRequestsPerSecond);
            Telemetry = new ServerTelemetryAggregator();
            AdaptiveMetrics = new AdaptiveDifficultyMetricsCollector();
            lifetimeCancellation = new CancellationTokenSource();
            await InitializeUnityServicesAsync(lifetimeCancellation.Token);
        }

        private void LateUpdate()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer || Telemetry == null)
                return;

            Telemetry.SealBefore(manager.ServerTime.Tick, ApplyTelemetrySnapshot);
        }

        private void ApplyTelemetrySnapshot(ServerTelemetrySnapshot snapshot)
        {
            if (!playerRegistry.TryGetByStableId(snapshot.PlayerId, out PlayerSessionRecord record))
                return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null
                || !manager.ConnectedClients.TryGetValue(record.ClientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return;
            }

            client.PlayerObject.GetComponent<PlayerCombatTelemetry>()?.ApplyAggregateSnapshot(snapshot, GetServerTime());
            AdaptiveMetrics?.Record(snapshot);
        }

        private async Task InitializeUnityServicesAsync(CancellationToken cancellationToken)
        {
            try
            {
                InitializationOptions initializationOptions = CreateVerificationInitializationOptions();
                Task initializationTask = initializationOptions == null
                    ? UnityServices.InitializeAsync()
                    : UnityServices.InitializeAsync(initializationOptions);
                await AwaitWithCancellation(initializationTask, cancellationToken);
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    if (initializationOptions == null)
                    {
                        string fallbackName = $"Player{UnityEngine.Random.Range(1000, 9999)}";
                        string playerName = PlayerPrefs.GetString("PlayerName", fallbackName).Trim();
                        if (string.IsNullOrEmpty(playerName))
                            playerName = fallbackName;

                        string profile = playerName.Split(' ')[0];
                        profile = profile.Substring(0, Mathf.Min(profile.Length, 30));
                        if (!string.Equals(AuthenticationService.Instance.Profile, profile, StringComparison.Ordinal))
                            AuthenticationService.Instance.SwitchProfile(profile);
                    }

                    await AwaitWithCancellation(AuthenticationService.Instance.SignInAnonymouslyAsync(), cancellationToken);
                }

                IsServicesInitialized = true;
                NetworkDiagnostics.Emit("services_ready", State);
            }
            catch (OperationCanceledException)
            {
                // Object lifetime ended while services were initializing.
            }
            catch (Exception exception)
            {
                IsServicesInitialized = false;
                sessionCoordinator.Transition(SessionState.Failed);
                GameLog.Error($"[Services] Initialization failed: {exception.Message}");
                OnConnectionFailed?.Invoke("Online services could not be initialized.");
            }
        }

        private static InitializationOptions CreateVerificationInitializationOptions()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string requestedProfile = GetCommandLineArgument("-a1ServicesProfile")
                ?? GetCommandLineArgument("-a2ServicesProfile");
            if (!string.IsNullOrWhiteSpace(requestedProfile))
                return new InitializationOptions().SetProfile(requestedProfile);
#endif
            return null;
        }

        private static string GetCommandLineArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }

            return null;
        }

        public void StartHostGame() => _ = StartHostGameAsync();

        public async Task<SessionOperationResult> StartHostGameAsync(CancellationToken cancellationToken = default)
        {
            if (!IsServicesInitialized)
                return FailImmediately(SessionFailureReason.ServicesUnavailable, "Services are not ready yet.");

            if (!sessionCoordinator.TryBegin(
                    SessionState.StartingHost,
                    TimeSpan.FromSeconds(settings.OperationTimeoutSeconds),
                    out SessionOperation operation))
            {
                return FailImmediately(SessionFailureReason.Busy, "Another session operation is already running.");
            }

            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, cancellationToken);
            try
            {
                NetworkDiagnostics.BeginSession();
                PrepareNetworkManager(ConnectionIntent.NewPlayer);
                RegisterNetworkCallbacks();
                playerRegistry.Clear();
                matchStarted = false;
                intentionalShutdown = false;
                capturedDisconnects.Clear();

                var options = new SessionOptions { MaxPlayers = settings.MaxPlayers }.WithRelayNetwork();
                currentSession = await AwaitWithCancellation(
                    MultiplayerService.Instance.CreateSessionAsync(options), linked.Token);
                CurrentJoinCode = currentSession.Code;

                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsListening || !manager.IsHost)
                    throw new InvalidOperationException("Relay session was created but NGO host did not start.");

                RegisterCustomMessageHandler();
                IsInLobby = true;
                SceneEventProgressStatus loadStatus = manager.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
                if (loadStatus != SceneEventProgressStatus.Started)
                    throw new InvalidOperationException($"Lobby scene load was rejected: {loadStatus}.");

                sessionCoordinator.Complete(operation, SessionState.Lobby);
                NetworkDiagnostics.Emit("host_started", State);
                OnHostStarted?.Invoke();
                return SessionOperationResult.Success();
            }
            catch (OperationCanceledException)
            {
                SessionFailureReason reason = cancellationToken.IsCancellationRequested
                    ? SessionFailureReason.Cancelled
                    : SessionFailureReason.OperationTimedOut;
                return await FailOperationAsync(operation, reason,
                    reason == SessionFailureReason.Cancelled ? "Host startup was cancelled." : "Host startup timed out.");
            }
            catch (Exception exception)
            {
                GameLog.Error($"[Session] Host startup failed: {exception}");
                return await FailOperationAsync(operation, SessionFailureReason.TransportStartFailed, "Could not start the host session.");
            }
        }

        public void JoinGame(string joinCode) => _ = JoinGameAsync(joinCode);

        public async Task<SessionOperationResult> JoinGameAsync(string joinCode, CancellationToken cancellationToken = default)
        {
            if (!IsServicesInitialized)
                return FailImmediately(SessionFailureReason.ServicesUnavailable, "Services are not ready yet.");
            if (string.IsNullOrWhiteSpace(joinCode))
                return FailImmediately(SessionFailureReason.InvalidJoinCode, "Join code is empty.");

            if (!sessionCoordinator.TryBegin(
                    SessionState.Joining,
                    TimeSpan.FromSeconds(settings.OperationTimeoutSeconds),
                    out SessionOperation operation))
            {
                return FailImmediately(SessionFailureReason.Busy, "Another session operation is already running.");
            }

            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, cancellationToken);
            try
            {
                NetworkDiagnostics.BeginSession();
                CurrentJoinCode = joinCode.Trim();
                intentionalShutdown = false;
                PrepareNetworkManager(ConnectionIntent.NewPlayer);
                RegisterNetworkCallbacks();
                RegisterCustomMessageHandler();
                currentSession = await AwaitWithCancellation(
                    MultiplayerService.Instance.JoinSessionByCodeAsync(CurrentJoinCode), linked.Token);
                RegisterCustomMessageHandler();

                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsListening || !manager.IsConnectedClient)
                    throw new InvalidOperationException("Relay join completed without an NGO client connection.");

                IsInLobby = true;
                sessionCoordinator.Complete(operation, SessionState.Lobby);
                NetworkDiagnostics.Emit("client_joined", State);
                return SessionOperationResult.Success();
            }
            catch (OperationCanceledException)
            {
                SessionFailureReason reason = cancellationToken.IsCancellationRequested
                    ? SessionFailureReason.Cancelled
                    : SessionFailureReason.OperationTimedOut;
                return await FailOperationAsync(operation, reason,
                    reason == SessionFailureReason.Cancelled ? "Joining was cancelled." : "Joining the session timed out.");
            }
            catch (Exception exception)
            {
                GameLog.Warning(() => $"[Session] Join failed: {exception.Message}");
                return await FailOperationAsync(operation, SessionFailureReason.InvalidJoinCode, "The join code is invalid or the session is unavailable.");
            }
        }

        public void Reconnect() => _ = ReconnectAsync();

        public async Task<SessionOperationResult> ReconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!localStablePlayerId.IsValid || string.IsNullOrEmpty(localReconnectToken) || string.IsNullOrEmpty(CurrentJoinCode))
                return FailImmediately(SessionFailureReason.InvalidReconnectToken, "No reconnect reservation is available.");

            if (!sessionCoordinator.TryBegin(
                    SessionState.Reconnecting,
                    TimeSpan.FromSeconds(settings.ReconnectGraceSeconds + 5f),
                    out SessionOperation operation))
            {
                return SessionOperationResult.Failure(SessionFailureReason.Busy, "Reconnect is already running.");
            }

            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, cancellationToken);

            // A verification disconnect carries the exact local disconnect timestamp. If the
            // reservation is already outside its grace window, do not start another NGO/Relay
            // session just to discover expiry. Starting a failed session and then asking the
            // Multiplayer package to stop it produces the package's "session was never started"
            // warning because its network handler never reached Started.
            if (verificationDisconnectAt >= 0d
                && Time.realtimeSinceStartupAsDouble - verificationDisconnectAt >= settings.ReconnectGraceSeconds)
            {
                if (verificationTransportShutdown && verificationLeaveTask != null)
                    await verificationLeaveTask;

                verificationTransportShutdown = false;
                verificationLeaveTask = null;
                return await CompleteReconnectExpiredAsync(operation);
            }

            double startedAt = Time.realtimeSinceStartupAsDouble;
            Exception lastException = null;
            while (Time.realtimeSinceStartupAsDouble - startedAt < settings.ReconnectGraceSeconds)
            {
                try
                {
                    bool transportWasShutdownByVerification = verificationTransportShutdown;
                    verificationTransportShutdown = false;
                    suppressDisconnectHandling = true;
                    UnregisterCustomMessageHandler();
                    UnregisterNetworkCallbacks();
                    if (transportWasShutdownByVerification)
                    {
                        // DisconnectTransportForVerification starts LeaveAsync before NGO stops.
                        // Await that single lifecycle operation; do not leave or shutdown again.
                        if (verificationLeaveTask != null)
                            await verificationLeaveTask;
                    }
                    else
                    {
                        await LeaveCurrentSessionBestEffortAsync();
                    }
                    verificationLeaveTask = null;
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                        NetworkManager.Singleton.Shutdown(discardMessageQueue: true);
                    suppressDisconnectHandling = false;

                    PrepareNetworkManager(ConnectionIntent.Reconnect);
                    RegisterNetworkCallbacks();
                    RegisterCustomMessageHandler();
                    currentSession = await AwaitWithCancellation(
                        MultiplayerService.Instance.JoinSessionByCodeAsync(CurrentJoinCode), linked.Token);
                    RegisterCustomMessageHandler();

                    NetworkManager manager = NetworkManager.Singleton;
                    if (manager != null && manager.IsListening && manager.IsConnectedClient)
                    {
                        IsInLobby = false;
                        verificationDisconnectAt = -1d;
                        verificationDisconnectStarted = false;
                        sessionCoordinator.Complete(operation, SessionState.InMatch);
                        NetworkDiagnostics.Emit("reconnect_transport_ready", State, playerId: localStablePlayerId);
                        return SessionOperationResult.Success();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SessionException exception) when (IsTerminalHostLoss(exception.Error))
                {
                    lastException = exception;
                    return await FailReconnectAsHostUnavailableAsync(operation);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    GameLog.Info(() => $"[Reconnect] Attempt failed: {exception.Message}");
                }
                finally
                {
                    suppressDisconnectHandling = false;
                }

                try
                {
                    await DelayWithCancellation(TimeSpan.FromSeconds(4), linked.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                sessionCoordinator.Complete(operation, SessionState.Failed);
                await CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
                return SessionOperationResult.Failure(SessionFailureReason.Cancelled, "Reconnect was cancelled.");
            }

            GameLog.Info(() => $"[Reconnect] Grace period expired. Last error: {lastException?.Message}");
            return await CompleteReconnectExpiredAsync(operation);
        }

        private async Task<SessionOperationResult> CompleteReconnectExpiredAsync(SessionOperation operation)
        {
            sessionCoordinator.Complete(operation, SessionState.Failed);
            LastDisconnectMessage = "Reconnect reservation expired.";
            OnConnectionFailed?.Invoke(LastDisconnectMessage);
            await CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
            return SessionOperationResult.Failure(SessionFailureReason.ReconnectExpired, LastDisconnectMessage);
        }

        private async Task<SessionOperationResult> FailReconnectAsHostUnavailableAsync(SessionOperation operation)
        {
            sessionCoordinator.Complete(operation, SessionState.Failed);
            LastDisconnectMessage = SessionDisconnectReason.HostUnavailable.ToString();
            OnConnectionFailed?.Invoke(LastDisconnectMessage);
            await CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
            return SessionOperationResult.Failure(SessionFailureReason.HostUnavailable, LastDisconnectMessage);
        }

        private static bool IsTerminalHostLoss(SessionError error)
        {
            return error == SessionError.SessionNotFound
                || error == SessionError.SessionDeleted
                || error == SessionError.AllocationNotFound;
        }

        public void Disconnect() => _ = DisconnectAsync();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void MarkExpectedShutdownForVerification()
        {
            intentionalShutdown = true;
            holdReconnectForVerification = false;
        }

        public void HoldReconnectForVerification(bool hold)
        {
            holdReconnectForVerification = hold;
        }

        public void TerminateOnHostLossForVerification(bool enabled)
        {
            terminateOnHostLossForVerification = enabled;
        }

        public void DisconnectTransportForVerification()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || verificationDisconnectStarted)
                return;

            verificationDisconnectStarted = true;
            verificationDisconnectAt = Time.realtimeSinceStartupAsDouble;
            verificationTransportShutdown = true;

            // Stop movement snapshots before NGO broadcasts the disconnect/despawn. The
            // short drain lets unreliable snapshots already in flight arrive before the
            // reliable despawn message reaches the other peers.
            if (manager.IsClient && !manager.IsServer && manager.CustomMessagingManager != null)
            {
                using var writer = new FastBufferWriter(1, Allocator.Temp);
                manager.CustomMessagingManager.SendNamedMessage(
                    ReconnectPrepareDisconnectMessage,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }

            StartCoroutine(ShutdownVerificationTransportAfterDrain());
        }

        private IEnumerator ShutdownVerificationTransportAfterDrain()
        {
            yield return new WaitForSecondsRealtime(VerificationDisconnectDrainSeconds);

            // ISession.LeaveAsync owns the Multiplayer session leave. NGO is then shut down
            // with its local queues discarded so no old RPC can cross the reconnect boundary.
            verificationLeaveTask = LeaveCurrentSessionBestEffortAsync();
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
                manager.Shutdown(discardMessageQueue: true);
        }

        public Task<SessionOperationResult> ReconnectForVerificationAsync(
            CancellationToken cancellationToken = default)
        {
            holdReconnectForVerification = false;
            return ReconnectAsync(cancellationToken);
        }
#endif

        public async Task DisconnectAsync()
        {
            if (State == SessionState.ShuttingDown)
                return;

            intentionalShutdown = true;
            sessionCoordinator.CancelActive(SessionState.ShuttingDown);
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && manager.IsServer)
            {
                var clients = new List<ulong>(manager.ConnectedClientsIds);
                for (int i = 0; i < clients.Count; i++)
                {
                    ulong clientId = clients[i];
                    if (clientId != manager.LocalClientId)
                        manager.DisconnectClient(clientId, SessionDisconnectReason.HostEndedSession.ToString());
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            LastDisconnectMessage = SessionDisconnectReason.UserLeft.ToString();
            await CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
        }

        public void StartMatch() => _ = StartMatchAsync();

        public async Task<SessionOperationResult> StartMatchAsync(CancellationToken cancellationToken = default)
        {
            if (!IsHosting || !IsInLobby)
                return SessionOperationResult.Failure(SessionFailureReason.Unknown, "Only the lobby host can start the match.");

            if (!sessionCoordinator.TryBegin(
                    SessionState.LoadingMatch,
                    TimeSpan.FromSeconds(settings.SceneLoadTimeoutSeconds),
                    out matchLoadOperation))
            {
                return SessionOperationResult.Failure(SessionFailureReason.Busy, "Another session operation is already running.");
            }

            if (cancellationToken.IsCancellationRequested)
                return await FailOperationAsync(matchLoadOperation, SessionFailureReason.Cancelled, "Match loading was cancelled.");
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.SceneManager == null)
                return await FailOperationAsync(matchLoadOperation, SessionFailureReason.SceneLoadFailed, "Network scene manager is unavailable.");

            // The MPS session deliberately remains unlocked. NGO approval rejects fresh players after
            // this point while still allowing a reserved player to reacquire the Relay allocation.
            SyncLobbyCharacterSelectionsToRegistry();
            matchStarted = true;
            IsInLobby = false;
            manager.SceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;
            manager.SceneManager.OnLoadEventCompleted += HandleGameSceneLoaded;
            SceneEventProgressStatus status = manager.SceneManager.LoadScene(gameScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                manager.SceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;
                matchStarted = false;
                return await FailOperationAsync(matchLoadOperation, SessionFailureReason.SceneLoadFailed, $"Game scene load was rejected: {status}.");
            }

            NetworkMatchStateManager.Instance?.EnterLoading();
            if (sceneLoadTimeoutRoutine != null)
                StopCoroutine(sceneLoadTimeoutRoutine);
            sceneLoadTimeoutRoutine = StartCoroutine(SceneLoadTimeoutRoutine());
            return SessionOperationResult.Success();
        }

        private void PrepareNetworkManager(ConnectionIntent intent)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
                throw new InvalidOperationException("NetworkManager is missing.");

            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = ApproveConnection;
            manager.NetworkConfig.ConnectionData = ConnectionPayload.Encode(new ConnectionPayload
            {
                protocolVersion = NetworkProtocol.Version,
                buildVersion = Application.version,
                unityPlayerId = GetLocalUnityPlayerId(),
                intent = intent,
                sessionPlayerId = localStablePlayerId.Value,
                reconnectToken = intent == ConnectionIntent.Reconnect ? localReconnectToken : string.Empty,
                playerName = GetLocalPlayerName()
            });
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Approved = false;

            if (!ConnectionPayload.TryDecode(request.Payload, out ConnectionPayload payload))
            {
                Reject(response, SessionFailureReason.ProtocolMismatch);
                return;
            }
            if (payload.protocolVersion != NetworkProtocol.Version)
            {
                Reject(response, SessionFailureReason.ProtocolMismatch);
                return;
            }
            if (!string.Equals(payload.buildVersion, Application.version, StringComparison.Ordinal))
            {
                Reject(response, SessionFailureReason.BuildMismatch);
                return;
            }

            double now = GetServerTime();
            if (matchStarted)
            {
                if (payload.intent != ConnectionIntent.Reconnect)
                {
                    Reject(response, SessionFailureReason.MatchAlreadyStarted);
                    return;
                }
                if (!CanAttemptReconnect(payload.unityPlayerId, now))
                {
                    Reject(response, SessionFailureReason.InvalidReconnectToken);
                    return;
                }

                if (!playerRegistry.TryReconnect(
                        payload.unityPlayerId,
                        new SessionPlayerId(payload.sessionPlayerId),
                        payload.reconnectToken,
                        request.ClientNetworkId,
                        now,
                        out PlayerSessionRecord reconnected,
                        out SessionFailureReason reconnectFailure))
                {
                    Reject(response, reconnectFailure);
                    return;
                }

                pendingApprovals[request.ClientNetworkId] = new PendingApproval
                {
                    Record = reconnected,
                    IsReconnect = true,
                    PlayerName = payload.playerName
                };
                connectionPlayerNames[request.ClientNetworkId] = payload.playerName;
                response.Approved = true;
                NetworkDiagnostics.Emit("approval_reconnect", State, playerId: reconnected.PlayerId);
                return;
            }

            if (payload.intent != ConnectionIntent.NewPlayer)
            {
                Reject(response, SessionFailureReason.InvalidReconnectToken);
                return;
            }

            if (!playerRegistry.TryRegisterNew(
                    payload.unityPlayerId,
                    request.ClientNetworkId,
                    now,
                    out PlayerSessionRecord record,
                    out SessionCredentials credentials,
                    out SessionFailureReason failure))
            {
                // NGO always approves the host. Reuse its record if approval was invoked twice.
                if (request.ClientNetworkId == NetworkManager.ServerClientId
                    && playerRegistry.TryGetByClientId(request.ClientNetworkId, out record))
                {
                    response.Approved = true;
                    connectionPlayerNames[request.ClientNetworkId] = payload.playerName;
                    return;
                }

                Reject(response, failure);
                return;
            }

            pendingApprovals[request.ClientNetworkId] = new PendingApproval
            {
                Record = record,
                Credentials = credentials,
                IsReconnect = false,
                PlayerName = payload.playerName
            };
            connectionPlayerNames[request.ClientNetworkId] = payload.playerName;
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                localStablePlayerId = credentials.PlayerId;
                localReconnectToken = credentials.ReconnectToken;
            }

            response.Approved = true;
            NetworkDiagnostics.Emit("approval_new", State, playerId: record.PlayerId);
        }

        private void Reject(NetworkManager.ConnectionApprovalResponse response, SessionFailureReason reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = reason.ToString();
            NetworkDiagnostics.Emit("approval_rejected", State, reason.ToString());
        }

        private void RegisterNetworkCallbacks()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || callbacksRegistered)
                return;

            manager.OnClientConnectedCallback += HandleClientConnected;
            manager.OnClientDisconnectCallback += HandleClientDisconnected;
            callbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !callbacksRegistered)
                return;

            manager.OnClientConnectedCallback -= HandleClientConnected;
            manager.OnClientDisconnectCallback -= HandleClientDisconnected;
            callbacksRegistered = false;
        }

        private void RegisterCustomMessageHandler()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null || customMessageRegistered)
                return;

            manager.CustomMessagingManager.RegisterNamedMessageHandler(ReconnectGrantMessage, HandleReconnectGrantMessage);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(
                ReconnectPrepareDisconnectMessage,
                HandleReconnectPrepareDisconnectMessage);
            customMessageRegistered = true;
        }

        private void UnregisterCustomMessageHandler()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.CustomMessagingManager == null || !customMessageRegistered)
                return;

            manager.CustomMessagingManager.UnregisterNamedMessageHandler(ReconnectGrantMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(ReconnectPrepareDisconnectMessage);
            customMessageRegistered = false;
        }

        private void HandleClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
                return;

            if (manager.IsServer && pendingApprovals.TryGetValue(clientId, out PendingApproval approval))
            {
                if (!approval.IsReconnect)
                {
                    SendReconnectGrant(clientId, approval.Credentials);
                    StartCoroutine(ResendReconnectGrantAfterSpawn(clientId, approval.Credentials));
                }

                if (matchStarted)
                    SpawnPlayerForClient(clientId, approval.Record, approval.IsReconnect);
                pendingApprovals.Remove(clientId);
            }

            if (clientId == manager.LocalClientId)
                OnClientConnected?.Invoke();
            if (IsInLobby)
            {
                if (connectionPlayerNames.TryGetValue(clientId, out string playerName))
                    WaitingRoomManager.Instance?.ApplyApprovedPlayerName(clientId, playerName);
                OnPlayerJoinedLobby?.Invoke(clientId);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || suppressDisconnectHandling)
                return;

            if (manager.IsServer && clientId != manager.LocalClientId)
            {
                if (IsInLobby)
                {
                    playerRegistry.Remove(clientId);
                    OnPlayerLeftLobby?.Invoke(clientId);
                }
                else if (matchStarted && !capturedDisconnects.Contains(clientId))
                {
                    StopServerReplicationForDisconnectedPlayer(clientId);
                    ReserveDisconnectedPlayer(clientId, null);
                }
                pendingApprovals.Remove(clientId);
                connectionPlayerNames.Remove(clientId);
            }

            if (clientId != manager.LocalClientId)
                return;

            OnClientDisconnected?.Invoke();
            if (intentionalShutdown)
                return;

            string transportReason = manager.DisconnectReason;
            LastDisconnectMessage = string.IsNullOrEmpty(transportReason)
                ? SessionDisconnectReason.HostUnavailable.ToString()
                : transportReason;

            bool hostExplicitlyEnded = string.Equals(
                    transportReason,
                    SessionDisconnectReason.HostEndedSession.ToString(),
                    StringComparison.Ordinal)
                || string.Equals(
                    transportReason,
                    SessionDisconnectReason.HostUnavailable.ToString(),
                    StringComparison.Ordinal);

            if (hostExplicitlyEnded)
            {
                sessionCoordinator.CancelActive(SessionState.Failed);
                OnConnectionFailed?.Invoke(LastDisconnectMessage);
                _ = CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
                return;
            }

            if (holdReconnectForVerification)
                return;

            if (terminateOnHostLossForVerification)
            {
                sessionCoordinator.CancelActive(SessionState.Failed);
                OnConnectionFailed?.Invoke(SessionDisconnectReason.HostUnavailable.ToString());
                _ = CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
                return;
            }

            if ((State == SessionState.InMatch || State == SessionState.Reconnecting)
                && localStablePlayerId.IsValid
                && !string.IsNullOrEmpty(localReconnectToken))
            {
                _ = ReconnectAsync();
                return;
            }

            sessionCoordinator.CancelActive(SessionState.Failed);
            OnConnectionFailed?.Invoke(LastDisconnectMessage);
            _ = CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
        }

        private static void StopServerReplicationForDisconnectedPlayer(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.SpawnManager?.SpawnedObjects == null)
                return;

            foreach (NetworkObject networkObject in manager.SpawnManager.SpawnedObjects.Values)
            {
                if (networkObject == null || networkObject.OwnerClientId != clientId)
                    continue;

                networkObject.GetComponent<PlayerMovement>()?.StopServerReplicationForDisconnect();
                break;
            }
        }

        private void SendReconnectGrant(ulong clientId, SessionCredentials credentials)
        {
            if (!credentials.PlayerId.IsValid || string.IsNullOrEmpty(credentials.ReconnectToken))
                return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
                return;

            if (clientId == manager.LocalClientId)
            {
                localStablePlayerId = credentials.PlayerId;
                localReconnectToken = credentials.ReconnectToken;
                return;
            }

            using var writer = new FastBufferWriter(128, Allocator.Temp);
            ulong playerId = credentials.PlayerId.Value;
            var token = new FixedString64Bytes(credentials.ReconnectToken);
            writer.WriteValueSafe(playerId);
            writer.WriteValueSafe(token);
            manager.CustomMessagingManager.SendNamedMessage(
                ReconnectGrantMessage,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private IEnumerator ResendReconnectGrantAfterSpawn(ulong clientId, SessionCredentials credentials)
        {
            // The first callback can occur before the remote custom-message handler is ready.
            // Retry only in memory and only while the same NGO connection is still alive.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return null;
                yield return new WaitForSecondsRealtime(0.5f);

                NetworkManager manager = NetworkManager.Singleton;
                if (manager == null || !manager.IsServer || !manager.ConnectedClients.ContainsKey(clientId))
                    yield break;

                SendReconnectGrant(clientId, credentials);
            }
        }

        private void HandleReconnectGrantMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong playerId);
            reader.ReadValueSafe(out FixedString64Bytes token);
            localStablePlayerId = new SessionPlayerId(playerId);
            localReconnectToken = token.ToString();
            NetworkDiagnostics.Emit("reconnect_credentials_received", State, playerId: localStablePlayerId);
        }

        private void HandleReconnectPrepareDisconnectMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || senderClientId == manager.LocalClientId)
                return;

            StopServerReplicationForDisconnectedPlayer(senderClientId);
            NetworkDiagnostics.Emit("reconnect_disconnect_prepared", State);
        }

        private void HandleGameSceneLoaded(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.SceneManager != null)
                manager.SceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;
            if (sceneLoadTimeoutRoutine != null)
            {
                StopCoroutine(sceneLoadTimeoutRoutine);
                sceneLoadTimeoutRoutine = null;
            }

            if (manager == null || !manager.IsServer || !string.Equals(sceneName, gameScene, StringComparison.Ordinal))
                return;
            if (clientsTimedOut != null && clientsTimedOut.Count > 0)
            {
                _ = HandleSceneLoadFailureAsync(SessionFailureReason.SceneLoadTimedOut, clientsTimedOut);
                return;
            }

            var completed = clientsCompleted != null
                ? new HashSet<ulong>(clientsCompleted)
                : new HashSet<ulong>();
            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (!completed.Contains(clientId))
                {
                    _ = HandleSceneLoadFailureAsync(
                        SessionFailureReason.SceneLoadTimedOut,
                        new List<ulong> { clientId });
                    return;
                }
            }

            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (!playerRegistry.TryGetByClientId(clientId, out PlayerSessionRecord record))
                    continue;
                SpawnPlayerForClient(clientId, record, isReconnect: false);
            }

            sessionCoordinator.Complete(matchLoadOperation, SessionState.InMatch);
            NetworkMatchStateManager.Instance?.EnterWarmup();
            NetworkDiagnostics.Emit("match_ready", State);
        }

        private IEnumerator SceneLoadTimeoutRoutine()
        {
            yield return new WaitForSecondsRealtime(settings.SceneLoadTimeoutSeconds);
            sceneLoadTimeoutRoutine = null;
            if (State == SessionState.LoadingMatch)
                _ = HandleSceneLoadFailureAsync(SessionFailureReason.SceneLoadTimedOut, null);
        }

        private async Task HandleSceneLoadFailureAsync(SessionFailureReason reason, List<ulong> timedOutClients)
        {
            if (timedOutClients != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                for (int i = 0; i < timedOutClients.Count; i++)
                    NetworkManager.Singleton.DisconnectClient(timedOutClients[i], reason.ToString());
            }

            sessionCoordinator.CancelActive(SessionState.Failed);
            LastDisconnectMessage = reason.ToString();
            OnConnectionFailed?.Invoke("The match scene could not be synchronized for every player.");
            await CleanupAndReturnToMenuAsync(clearReconnectCredentials: true);
        }

        private void SpawnPlayerForClient(ulong clientId, PlayerSessionRecord record, bool isReconnect)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || record == null)
                return;
            if (manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null)
                return;

            GetFallbackSpawnPose(clientId, out Vector3 fallbackPosition, out Quaternion fallbackRotation);
            Vector3 position = fallbackPosition;
            Quaternion rotation = fallbackRotation;
            if (isReconnect)
            {
                position = ResolveSafeReconnectPosition(record.Snapshot.position, fallbackPosition);
                rotation = record.Snapshot.rotation;
            }

            if (!TryGetPlayerPrefab(record.CharacterId, out GameObject playerPrefab))
            {
                manager.DisconnectClient(clientId, "PlayerCharacterUnavailable");
                return;
            }

            GameObject playerObject = Instantiate(playerPrefab, position, rotation);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
            if (networkObject == null || health == null)
            {
                Destroy(playerObject);
                manager.DisconnectClient(clientId, "PlayerPrefabInvalid");
                return;
            }

            if (isReconnect)
            {
                PlayerRuntimeSnapshot snapshot = record.Snapshot;
                snapshot.position = position;
                snapshot.rotation = rotation;
                health.PrepareReconnect(snapshot);
            }
            else
            {
                health.PrepareInitialSpawn(record.PlayerId);
            }

            capturedDisconnects.Remove(clientId);
            networkObject.SpawnAsPlayerObject(clientId, true);
        }

        internal bool SetPlayerCharacter(ulong clientId, PlayerCharacterId characterId)
        {
            if (!Enum.IsDefined(typeof(PlayerCharacterId), characterId)
                || playerRegistry == null
                || !playerRegistry.TryGetByClientId(clientId, out PlayerSessionRecord record))
            {
                return false;
            }

            if (!TryGetPlayerPrefab(characterId, out _))
                return false;

            record.CharacterId = characterId;
            SyncLobbyCharacterSelectionsToRegistry();
            return true;
        }

        internal bool TryGetPlayerCharacter(ulong clientId, out PlayerCharacterId characterId)
        {
            if (playerRegistry != null && playerRegistry.TryGetByClientId(clientId, out PlayerSessionRecord record))
            {
                characterId = record.CharacterId;
                return true;
            }

            characterId = PlayerCharacterId.Clove;
            return false;
        }

        private bool TryGetPlayerPrefab(PlayerCharacterId characterId, out GameObject prefab)
        {
            PlayerPrefabCatalog catalog = playerPrefabCatalog != null
                ? playerPrefabCatalog
                : Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
            if (catalog != null && catalog.TryGetPrefab(characterId, out prefab))
                return true;

            // NetworkConfig.PlayerPrefab remains a Clove fallback for old scenes,
            // but it is never used for a non-Clove character.
            NetworkManager manager = NetworkManager.Singleton;
            prefab = characterId == PlayerCharacterId.Clove ? manager?.NetworkConfig.PlayerPrefab : null;
            return prefab != null;
        }

        private void SyncLobbyCharacterSelectionsToRegistry()
        {
            if (!IsHosting || WaitingRoomManager.Instance == null || playerRegistry == null)
                return;

            foreach (PlayerLobbyData player in WaitingRoomManager.Instance.Players)
            {
                if (!playerRegistry.TryGetByClientId(player.clientId, out PlayerSessionRecord record))
                    continue;

                if (Enum.IsDefined(typeof(PlayerCharacterId), player.characterId))
                    record.CharacterId = (PlayerCharacterId)player.characterId;
            }
        }

        private void GetFallbackSpawnPose(ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            NetworkSpawnManager.Instance?.TryGetSpawnPose(
                out position,
                out rotation,
                new SpawnRequest(clientId));
        }

        private Vector3 ResolveSafeReconnectPosition(Vector3 requested, Vector3 fallback)
        {
            if (IsSafePlayerPosition(requested))
                return requested;

            for (int radius = 1; radius <= 3; radius++)
            {
                for (int directionIndex = 0; directionIndex < 8; directionIndex++)
                {
                    float angle = directionIndex * 45f * Mathf.Deg2Rad;
                    Vector3 candidate = requested + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsSafePlayerPosition(candidate))
                        return candidate;
                }
            }

            return fallback;
        }

        private bool IsSafePlayerPosition(Vector3 position)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                return false;

            Vector3 lower = position + Vector3.up * 0.45f;
            Vector3 upper = position + Vector3.up * 1.45f;
            if (Physics.CheckCapsule(lower, upper, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (reconnectHazardLayers.value != 0
                && Physics.CheckCapsule(lower, upper, 0.3f, reconnectHazardLayers, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            return Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public void CaptureDisconnectedPlayer(PlayerHealth playerHealth)
        {
            if (!matchStarted || playerHealth == null || !playerHealth.StablePlayerId.IsValid)
                return;

            PlayerRuntimeSnapshot snapshot = playerHealth.CaptureRuntimeSnapshot();
            if (capturedDisconnects.Contains(playerHealth.OwnerClientId))
            {
                playerRegistry.UpdateReservedSnapshot(
                    playerHealth.StablePlayerId,
                    snapshot,
                    GetServerTime() + settings.ReconnectReservationSeconds);
                return;
            }

            ReserveDisconnectedPlayer(playerHealth.OwnerClientId, snapshot);
        }

        private void ReserveDisconnectedPlayer(ulong clientId, PlayerRuntimeSnapshot? snapshot)
        {
            if (capturedDisconnects.Contains(clientId))
                return;
            if (!playerRegistry.TryGetByClientId(clientId, out PlayerSessionRecord record))
                return;

            PlayerRuntimeSnapshot value = snapshot ?? record.Snapshot;
            if (!value.sessionPlayerId.IsValid)
                value.sessionPlayerId = record.PlayerId;
            playerRegistry.Reserve(clientId, value, GetServerTime() + settings.ReconnectReservationSeconds);
            capturedDisconnects.Add(clientId);
            NetworkDiagnostics.Emit("player_reserved", State, playerId: record.PlayerId);
        }

        private bool CanAttemptReconnect(string unityPlayerId, double now)
        {
            if (string.IsNullOrWhiteSpace(unityPlayerId))
                return false;

            if (!reconnectAttemptWindows.TryGetValue(unityPlayerId, out AttemptWindow window)
                || now - window.StartedAt >= settings.ReconnectAttemptWindowSeconds)
            {
                reconnectAttemptWindows[unityPlayerId] = new AttemptWindow { StartedAt = now, Count = 1 };
                return true;
            }

            window.Count++;
            return window.Count <= settings.ReconnectAttemptsPerWindow;
        }

        private SessionOperationResult FailImmediately(SessionFailureReason reason, string message)
        {
            OnConnectionFailed?.Invoke(message);
            return SessionOperationResult.Failure(reason, message);
        }

        private async Task<SessionOperationResult> FailOperationAsync(
            SessionOperation operation,
            SessionFailureReason reason,
            string message)
        {
            sessionCoordinator.Complete(operation, SessionState.Failed);
            LastDisconnectMessage = message;
            OnConnectionFailed?.Invoke(message);
            await CleanupTransportAsync(clearReconnectCredentials: reason != SessionFailureReason.OperationTimedOut);
            return SessionOperationResult.Failure(reason, message);
        }

        private async Task CleanupAndReturnToMenuAsync(bool clearReconnectCredentials)
        {
            await CleanupTransportAsync(clearReconnectCredentials);
            sessionCoordinator.CancelActive(SessionState.Offline);
            if (!string.IsNullOrEmpty(mainMenuScene) && SceneManager.GetActiveScene().name != mainMenuScene)
                SceneManager.LoadScene(mainMenuScene);
        }

        private async Task CleanupTransportAsync(bool clearReconnectCredentials)
        {
            if (cleanupTask != null)
            {
                await cleanupTask;
                return;
            }

            cleanupTask = CleanupTransportCoreAsync(clearReconnectCredentials);
            try
            {
                await cleanupTask;
            }
            finally
            {
                cleanupTask = null;
            }
        }

        private async Task CleanupTransportCoreAsync(bool clearReconnectCredentials)
        {
            suppressDisconnectHandling = true;
            if (sceneLoadTimeoutRoutine != null)
            {
                StopCoroutine(sceneLoadTimeoutRoutine);
                sceneLoadTimeoutRoutine = null;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager?.SceneManager != null)
                manager.SceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;

            UnregisterCustomMessageHandler();
            UnregisterNetworkCallbacks();
            await LeaveCurrentSessionBestEffortAsync();
            if (manager != null && manager.IsListening)
                manager.Shutdown(discardMessageQueue: false);
            currentSession = null;
            IsInLobby = false;
            matchStarted = false;
            CurrentJoinCode = string.Empty;
            pendingApprovals.Clear();
            connectionPlayerNames.Clear();
            reconnectAttemptWindows.Clear();
            capturedDisconnects.Clear();
            verificationDisconnectAt = -1d;
            verificationDisconnectStarted = false;
            playerRegistry.Clear();
            PickupTransactions?.Clear();
            Telemetry?.Clear();
            if (clearReconnectCredentials)
            {
                localStablePlayerId = default;
                localReconnectToken = string.Empty;
            }
            suppressDisconnectHandling = false;
            NetworkDiagnostics.EndSession();
        }

        private async Task LeaveCurrentSessionBestEffortAsync()
        {
            if (currentSession == null)
                return;

            try
            {
                await currentSession.LeaveAsync();
            }
            catch (OperationCanceledException)
            {
                // Cancellation during teardown is expected.
            }
            catch (ObjectDisposedException)
            {
                // The Services SDK may dispose a session while a leave request is
                // in flight. It is already in the desired terminal state.
            }
            catch (Exception exception) when (exception.Message.IndexOf("already left", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Idempotent leave: the remote service already removed us.
            }
            catch (Exception exception)
            {
                GameLog.Warning(() => $"[Session] Unexpected leave failure: {exception.Message}");
            }
            finally
            {
                currentSession = null;
            }
        }

        private void HandleSessionStateChanged(SessionState previous, SessionState current)
        {
            NetworkDiagnostics.Emit("session_state", current, $"{previous}->{current}");
            OnSessionStateChanged?.Invoke(previous, current);
        }

        private static string GetLocalUnityPlayerId()
        {
            try
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                return string.IsNullOrWhiteSpace(playerId) ? $"local-{SystemInfo.deviceUniqueIdentifier}" : playerId;
            }
            catch (Exception)
            {
                return $"local-{SystemInfo.deviceUniqueIdentifier}";
            }
        }

        private static string GetLocalPlayerName()
        {
            string fallback = $"Player{UnityEngine.Random.Range(1000, 9999)}";
            string value = PlayerPrefs.GetString("PlayerName", fallback).Trim();
            if (string.IsNullOrEmpty(value))
                value = fallback;

            while (value.Length > 1 && System.Text.Encoding.UTF8.GetByteCount(value) > 63)
                value = value.Substring(0, value.Length - 1);
            return value;
        }

        internal bool TryGetApprovedPlayerName(ulong clientId, out string playerName)
        {
            return connectionPlayerNames.TryGetValue(clientId, out playerName)
                && !string.IsNullOrWhiteSpace(playerName);
        }

        private static double GetServerTime()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening ? manager.ServerTime.Time : Time.timeAsDouble;
        }

        private static async Task AwaitWithCancellation(Task task, CancellationToken cancellationToken)
        {
            Task cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(task, cancelled) != task)
                throw new OperationCanceledException(cancellationToken);
            await task;
        }

        private static async Task<T> AwaitWithCancellation<T>(Task<T> task, CancellationToken cancellationToken)
        {
            Task cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(task, cancelled) != task)
                throw new OperationCanceledException(cancellationToken);
            return await task;
        }

        private static async Task DelayWithCancellation(TimeSpan delay, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            lifetimeCancellation?.Cancel();
            UnregisterCustomMessageHandler();
            UnregisterNetworkCallbacks();
            Task cleanup = CleanupTransportAsync(clearReconnectCredentials: true);
            _ = DisposeAfterCleanupAsync(cleanup);
            Instance = null;
            NetworkHardeningRuntime.Reset();
            NetworkDiagnostics.EndSession();
        }

        private async Task DisposeAfterCleanupAsync(Task cleanup)
        {
            try
            {
                await cleanup;
            }
            catch (Exception exception) when (exception is OperationCanceledException || exception is ObjectDisposedException)
            {
                // Expected during application shutdown.
            }
            finally
            {
                lifetimeCancellation?.Dispose();
                lifetimeCancellation = null;
                if (sessionCoordinator != null)
                {
                    sessionCoordinator.StateChanged -= HandleSessionStateChanged;
                    sessionCoordinator.Dispose();
                    sessionCoordinator = null;
                }
            }
        }

        public static bool HasInstance => Instance != null;
    }
}
