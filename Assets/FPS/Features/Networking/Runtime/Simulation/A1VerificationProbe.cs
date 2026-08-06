using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

namespace FPS.NetworkSimulation
{
    /// <summary>
    /// Development-only runtime oracle for the A1 standalone harness. It records facts observed
    /// by the process; the PowerShell summarizer is responsible for cross-peer assertions.
    /// </summary>
    public sealed class A1VerificationProbe : MonoBehaviour
    {
        [Serializable]
        private struct EventRecord
        {
            public string runId;
            public string peer;
            public string role;
            public string scenario;
            public string @event;
            public double serverTime;
            public ulong clientId;
            public ulong stablePlayerId;
            public ulong networkObjectId;
            public string scene;
            public string result;
            public string reason;
        }

        private readonly HashSet<ulong> observedObjects = new();
        private readonly HashSet<ulong> observedStablePlayers = new();
        private StreamWriter writer;
        private string runId;
        private string peer;
        private string role;
        private string scenario;
        private string artifactPath;
        private bool subscribed;
        private bool completed;
        private bool prefabValidated;
        private bool servicesReported;
        private double nextPoll;
        private double nextSnapshot;
        private int lastPlayerCount = -1;
        private bool expectedNormalShutdown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallFromCommandLine()
        {
            if (!IsEnabled())
                return;

            GameObject gameObject = new(nameof(A1VerificationProbe));
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<A1VerificationProbe>();
        }

        private void Awake()
        {
            runId = GetArgument("-a1RunId") ?? GetArgument("-a2RunId")
                ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            peer = GetArgument("-a1PeerId") ?? GetArgument("-a2PeerId")
                ?? GetArgument("-a1Role") ?? "unknown";
            role = GetArgument("-a1Role") ?? GetArgument("-a2Role") ?? "unknown";
            scenario = GetArgument("-a1Scenario") ?? GetArgument("-a2Scenario") ?? "baseline";
            string artifactDirectory = GetArgument("-a1ArtifactDir") ?? GetArgument("-a2ArtifactDir");
            if (string.IsNullOrWhiteSpace(artifactDirectory))
                artifactDirectory = Path.Combine(Application.persistentDataPath, "A1", runId);

            try
            {
                Directory.CreateDirectory(artifactDirectory);
                artifactPath = Path.Combine(artifactDirectory, $"{peer}.events.jsonl");
                writer = new StreamWriter(artifactPath, append: false,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
            }
            catch (Exception exception)
            {
                Debug.LogError($"[A1Verification] artifact_open_failed:{exception.Message}");
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            Application.logMessageReceived += HandleLogMessage;
            NetworkDiagnostics.EventEmitted += HandleNetworkDiagnostic;
            AdaptiveDirectorDiagnostics.EventEmitted += HandleAdaptiveDiagnostic;
            Emit("probe_started", "pass", artifactPath);
        }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (NetworkGameManager.Instance == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            NetworkGameManager networkGameManager = NetworkGameManager.Instance;
            if (networkGameManager == null)
            {
                Emit("probe_ready", "fail", "NetworkGameManager not found");
                yield break;
            }

            networkGameManager.OnHostStarted += HandleHostStarted;
            networkGameManager.OnClientConnected += HandleClientConnected;
            networkGameManager.OnClientDisconnected += HandleClientDisconnected;
            networkGameManager.OnConnectionFailed += HandleConnectionFailed;
            networkGameManager.OnSessionStateChanged += HandleSessionStateChanged;
            PlayerHealth.ReconnectRestoreAcknowledged += HandleReconnectRestoreAcknowledged;
            subscribed = true;
            Emit("probe_ready", "pass", "callbacks_registered");
        }

        private void Update()
        {
            if (completed || Time.realtimeSinceStartupAsDouble < nextPoll)
                return;

            nextPoll = Time.realtimeSinceStartupAsDouble + 0.5;
            NetworkManager manager = NetworkManager.Singleton;
            if (!servicesReported && NetworkGameManager.Instance != null
                && NetworkGameManager.Instance.IsServicesInitialized)
            {
                servicesReported = true;
                Emit("services_ready", "pass", "");
            }

            if (manager == null || !manager.IsListening)
                return;

            if (!prefabValidated)
            {
                prefabValidated = true;
                bool valid = manager.NetworkConfig != null
                    && manager.NetworkConfig.PlayerPrefab != null
                    && manager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>() != null
                    && manager.NetworkConfig.Prefabs != null
                    && manager.NetworkConfig.Prefabs.Contains(manager.NetworkConfig.PlayerPrefab)
                    && manager.NetworkConfig.ConnectionApproval;
                uint prefabHash = 0;
                if (manager.NetworkConfig != null && manager.NetworkConfig.Prefabs != null
                    && manager.NetworkConfig.PlayerPrefab != null)
                {
                    foreach (NetworkPrefabsList prefabList in manager.NetworkConfig.Prefabs.NetworkPrefabsLists)
                    {
                        if (prefabList == null)
                            continue;

                        foreach (NetworkPrefab prefab in prefabList.PrefabList)
                        {
                            if (prefab != null && prefab.Override == NetworkPrefabOverride.None
                                && prefab.Prefab == manager.NetworkConfig.PlayerPrefab)
                            {
                                prefabHash = prefab.SourcePrefabGlobalObjectIdHash;
                                break;
                            }
                        }

                        if (prefabHash != 0)
                            break;
                    }
                }
                Emit("prefab_registration", valid ? "pass" : "fail",
                    valid
                        ? $"runtime_player_prefab_registered_and_approval_enabled;globalObjectIdHash={prefabHash}"
                        : "runtime_network_config_invalid");
            }

            int playerCount = manager.ConnectedClientsList.Count;
            if (playerCount != lastPlayerCount)
            {
                lastPlayerCount = playerCount;
                Emit("connected_player_count", playerCount == 4 ? "pass" : "info", playerCount.ToString(CultureInfo.InvariantCulture));
            }

            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null)
                    continue;

                PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
                ulong stablePlayerId = health != null ? health.StablePlayerId.Value : 0;
                if (observedObjects.Add(playerObject.NetworkObjectId))
                    Emit("player_spawned", "pass", "", stablePlayerId, playerObject.NetworkObjectId, client.ClientId);
                if (stablePlayerId != 0)
                    observedStablePlayers.Add(stablePlayerId);

                if (client.ClientId == manager.LocalClientId && health != null && health.IsInputReady)
                    Emit("input_ready", "pass", "", stablePlayerId, playerObject.NetworkObjectId, client.ClientId);
            }

            if (Time.realtimeSinceStartupAsDouble >= nextSnapshot)
            {
                nextSnapshot = Time.realtimeSinceStartupAsDouble + 5.0;
                Emit("player_snapshot", playerCount == 4 ? "pass" : "info",
                    $"players={playerCount};stableIds={observedStablePlayers.Count}");
                AIDirector director = FindAnyObjectByType<AIDirector>();
                if (director != null)
                {
                    Emit("enemy_state", "info", $"alive={director.ZombiesAlive};phase={director.CurrentPhase}");
                    if (director.AdaptiveDirectorEnabled)
                    {
                        Emit("adaptive_state", "info",
                            $"phase={director.AdaptivePhase};multiplier={director.AdaptiveDifficultyMultiplier:F4};"
                            + $"observeOnly={director.AdaptiveObserveOnly}");
                    }
                }
            }
        }

        public void Complete(string reason = "run_completed")
        {
            if (completed)
                return;
            completed = true;
            Emit(reason, "pass", "");
        }

        public void Record(string eventName, string result = "info", string reason = "")
        {
            Emit(eventName, result, reason);
        }

        public void MarkExpectedShutdown()
        {
            expectedNormalShutdown = true;
        }

        private void HandleReconnectRestoreAcknowledged(SessionPlayerId playerId)
        {
            Emit("reconnect_restore_ack", "pass", "", playerId.Value);
        }

        private void HandleHostStarted() => Emit("host_started", "pass", "");
        private void HandleClientConnected() => Emit("client_connected", "pass", "");
        private void HandleClientDisconnected() => Emit("client_disconnected", "info", "");
        private void HandleConnectionFailed(string reason)
        {
            bool hostLoss = reason != null && reason.IndexOf("Host", StringComparison.OrdinalIgnoreCase) >= 0;
            bool expectedShutdown = expectedNormalShutdown
                && reason != null
                && reason.IndexOf("ClosedByRemote", StringComparison.OrdinalIgnoreCase) >= 0;
            bool expectedReconnectExpiry = string.Equals(scenario, "reconnect", StringComparison.OrdinalIgnoreCase)
                && reason != null
                && (reason.IndexOf("ReconnectExpired", StringComparison.OrdinalIgnoreCase) >= 0
                    || reason.IndexOf("Reconnect reservation expired", StringComparison.OrdinalIgnoreCase) >= 0);
            Emit("connection_failed", hostLoss || expectedShutdown || expectedReconnectExpiry ? "info" : "fail", reason);
            if (hostLoss)
                Emit("host_loss_cleanup", "pass", reason);
        }

        private void HandleSessionStateChanged(SessionState previous, SessionState current)
        {
            Emit("session_state", "info", $"{previous}->{current}");
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.Equals(scene.name, "GameScene", StringComparison.Ordinal))
                EnsureVerificationNavMesh();

            string eventName = string.Equals(scene.name, "LobbyScene", StringComparison.Ordinal)
                ? "lobby_ready"
                : string.Equals(scene.name, "GameScene", StringComparison.Ordinal)
                    ? "game_scene_ready"
                    : "scene_loaded";
            Emit(eventName, "pass", scene.name);
        }

        private void EnsureVerificationNavMesh()
        {
            // The checked-in GameScene references a legacy NavMeshData GUID that
            // is absent in this checkout. Build a verification-only surface from
            // physics colliders so the standalone harness can exercise real AI.
            // Production scenes are not modified by this fallback.
            try
            {
                NavMeshSurface surface = FindAnyObjectByType<NavMeshSurface>();
                if (surface == null)
                {
                    GameObject root = new GameObject("A1VerificationNavMesh");
                    DontDestroyOnLoad(root);
                    surface = root.AddComponent<NavMeshSurface>();
                }

                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                Emit("navmesh_ready", "pass", "verification_runtime_surface_built");
            }
            catch (Exception exception)
            {
                Emit("navmesh_ready", "fail", exception.Message);
            }
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (IsAllowlistedVerificationWarning(condition))
                return;

            if (type == LogType.Error || type == LogType.Exception)
                Emit("network_error", "fail", condition);
            else if (type == LogType.Warning)
                Emit("network_warning", "fail", condition);
        }

        private static bool IsAllowlistedVerificationWarning(string condition)
        {
            // Unity Services Multiplayer emits this benign lobby patch message when
            // a lobby has already converged. It is not a transport/protocol warning.
            return condition != null
                && (condition.IndexOf("Attempting to apply patches to lobby, but there were no patches to apply.",
                        StringComparison.Ordinal) >= 0
                    || condition.IndexOf("GameObjectsNetcodeNetworkHandler.StopAsync: Failed to stop session: session was never started.",
                        StringComparison.Ordinal) >= 0
                    || condition.IndexOf("[Session] Leave failed: lobby not found", StringComparison.Ordinal) >= 0
                    || (condition.IndexOf("LobbyServiceException: lobby not found", StringComparison.Ordinal) >= 0
                        && condition.IndexOf("LobbyChannel", StringComparison.Ordinal) >= 0));
        }

        private void HandleNetworkDiagnostic(string eventName, SessionState state, string reason, SessionPlayerId playerId)
        {
            string verificationEvent = eventName switch
            {
                "pickup_transaction" => "pickup_result",
                "fire_reject" => "fire_result",
                "fire_result" => "fire_result",
                _ => null
            };
            if (!string.IsNullOrEmpty(verificationEvent))
                Emit(verificationEvent, "info", reason, playerId.Value);
        }

        private void HandleAdaptiveDiagnostic(string eventName, string reason)
        {
            Emit(eventName, "info", reason);
        }

        private void Emit(string eventName, string result, string reason,
            ulong stablePlayerId = 0, ulong networkObjectId = 0, ulong clientId = 0)
        {
            if (writer == null)
                return;

            NetworkManager manager = NetworkManager.Singleton;
            var record = new EventRecord
            {
                runId = runId,
                peer = peer,
                role = role,
                scenario = scenario,
                @event = eventName ?? string.Empty,
                serverTime = manager != null && manager.IsListening
                    ? manager.ServerTime.Time
                    : Time.realtimeSinceStartupAsDouble,
                clientId = clientId != 0 || manager == null ? clientId : manager.LocalClientId,
                stablePlayerId = stablePlayerId,
                networkObjectId = networkObjectId,
                scene = SceneManager.GetActiveScene().name,
                result = result ?? "info",
                reason = reason ?? string.Empty
            };

            lock (writer)
                writer.WriteLine(JsonUtility.ToJson(record));
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Application.logMessageReceived -= HandleLogMessage;
            NetworkDiagnostics.EventEmitted -= HandleNetworkDiagnostic;
            AdaptiveDirectorDiagnostics.EventEmitted -= HandleAdaptiveDiagnostic;
            if (subscribed && NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnHostStarted -= HandleHostStarted;
                NetworkGameManager.Instance.OnClientConnected -= HandleClientConnected;
                NetworkGameManager.Instance.OnClientDisconnected -= HandleClientDisconnected;
                NetworkGameManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
                NetworkGameManager.Instance.OnSessionStateChanged -= HandleSessionStateChanged;
            }
            PlayerHealth.ReconnectRestoreAcknowledged -= HandleReconnectRestoreAcknowledged;

            lock (this)
                writer?.Dispose();
            writer = null;
        }

        private static bool IsEnabled()
        {
            string value = GetArgument("-a1Verify") ?? GetArgument("-a2Verify");
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }
    }
}
