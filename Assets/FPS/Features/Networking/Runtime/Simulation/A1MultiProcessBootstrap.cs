using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace FPS.NetworkSimulation
{
    /// <summary>
    /// Standalone-only entry point used by the A1 one-host/three-client harness.
    /// It never persists reconnect tokens; the shared file contains only the Relay join code.
    /// </summary>
    public sealed class A1MultiProcessBootstrap : MonoBehaviour
    {
        private enum PeerRole { None, Host, Client }

        private PeerRole role;
        private string joinFile;
        private string explicitJoinCode;
        private string profile;
        private float runDurationSeconds;
        private float burstAtSeconds;
        private float disconnectAtSeconds;
        private float reconnectAtSeconds;
        private float hostLossAtSeconds;
        private string scenario;
        private string controlFile;
        private int enemyCount;
        private int verificationSeed;
        private bool adaptiveEnabled;
        private bool adaptiveObserveOnly;
        private NetworkSimulator simulator;
        private string lastControlCommand;
        private Task controlledShutdownTask;
        private bool controlledShutdownTriggered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallFromCommandLine()
        {
            string requestedRole = GetArgument("-a1Role") ?? GetArgument("-a2Role");
            if (!string.Equals(requestedRole, "host", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(requestedRole, "client", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var gameObject = new GameObject(nameof(A1MultiProcessBootstrap));
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<A1MultiProcessBootstrap>();
        }

        private void Awake()
        {
            ConfigureVerificationLogging();

            // Give every standalone peer a distinct Unity Services authentication profile.
            // This stays in the process-local PlayerPrefs store and no credential/token is written
            // to the shared harness control files.
            string verificationPeer = GetArgument("-a1PeerId") ?? GetArgument("-a2PeerId") ?? "peer";
            string verificationRun = GetArgument("-a1RunId") ?? GetArgument("-a2RunId") ?? "run";
            PlayerPrefs.SetString("PlayerName", $"A1-{verificationRun}-{verificationPeer}");

            role = string.Equals(GetArgument("-a1Role") ?? GetArgument("-a2Role"), "host", StringComparison.OrdinalIgnoreCase)
                ? PeerRole.Host
                : PeerRole.Client;
            joinFile = GetArgument("-a1JoinFile") ?? GetArgument("-a2JoinFile");
            explicitJoinCode = GetArgument("-a1JoinCode") ?? GetArgument("-a2JoinCode");
            profile = GetArgument("-a1Profile") ?? GetArgument("-a2Profile") ?? "normal";
            runDurationSeconds = ParsePositiveFloat(GetArgument("-a1Duration") ?? GetArgument("-a2Duration"), 1800f);
            burstAtSeconds = ParsePositiveFloat(GetArgument("-a1BurstAt") ?? GetArgument("-a2BurstAt"), -1f);
            disconnectAtSeconds = ParsePositiveFloat(GetArgument("-a1DisconnectAt") ?? GetArgument("-a2DisconnectAt"), -1f);
            reconnectAtSeconds = ParsePositiveFloat(GetArgument("-a1ReconnectAt") ?? GetArgument("-a2ReconnectAt"), -1f);
            hostLossAtSeconds = ParsePositiveFloat(GetArgument("-a1HostLossAt") ?? GetArgument("-a2HostLossAt"), -1f);
            scenario = GetArgument("-a1Scenario") ?? GetArgument("-a2Scenario") ?? "baseline";
            controlFile = GetArgument("-a1ControlFile") ?? GetArgument("-a2ControlFile");
            enemyCount = Mathf.Max(1, ParseInt(GetArgument("-a1EnemyCount") ?? GetArgument("-a2EnemyCount"), 30));
            verificationSeed = ParseInt(GetArgument("-a1Seed") ?? GetArgument("-a2Seed"), 20260801);
            UnityEngine.Random.InitState(verificationSeed + (int)role);
            adaptiveEnabled = ParseBool(GetArgument("-a2EnableAdaptive"), false);
            adaptiveObserveOnly = ParseBool(GetArgument("-a2ObserveOnly"), true);

            simulator = gameObject.AddComponent<NetworkSimulator>();
            simulator.ConnectionPreset = CreatePreset(profile);
            gameObject.AddComponent<A1NetworkMetricsRecorder>();
            StartCoroutine(RunPeer());
        }

        private static void ConfigureVerificationLogging()
        {
            // The probe receives the message and stack trace independently through
            // Application.logMessageReceivedThreaded. Full per-line Unity stack traces
            // make long verification artifacts disproportionately large and slow down
            // post-run parsing. Keep script-only traces for actionable diagnostics while
            // preserving the structured JSONL evidence.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
        }

        private IEnumerator RunPeer()
        {
            float startupDeadline = Time.realtimeSinceStartup + 90f;
            while ((NetworkGameManager.Instance == null
                    || !NetworkGameManager.Instance.IsServicesInitialized)
                && Time.realtimeSinceStartup < startupDeadline)
            {
                yield return null;
            }

            if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsServicesInitialized)
            {
                Fail("services_start_timeout", 10);
                yield break;
            }

            Task<SessionOperationResult> operation;
            if (role == PeerRole.Host)
            {
                operation = NetworkGameManager.Instance.StartHostGameAsync();
            }
            else
            {
                string joinCode = explicitJoinCode;
                while (string.IsNullOrWhiteSpace(joinCode) && Time.realtimeSinceStartup < startupDeadline)
                {
                    if (!string.IsNullOrWhiteSpace(joinFile) && File.Exists(joinFile))
                    {
                        try
                        {
                            // The host publishes the code while clients may observe the
                            // file at the same time. Treat a transient Windows sharing
                            // violation like "not published yet" and retry next tick.
                            string candidate = File.ReadAllText(joinFile).Trim();
                            if (!string.IsNullOrWhiteSpace(candidate))
                                joinCode = candidate;
                        }
                        catch (IOException)
                        {
                            // Retry without turning a file publication race into a
                            // protocol failure.
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Retry while the host replaces the publication file.
                        }
                    }
                    if (string.IsNullOrWhiteSpace(joinCode))
                        yield return new WaitForSecondsRealtime(0.25f);
                }

                if (string.IsNullOrWhiteSpace(joinCode))
                {
                    Fail("join_code_timeout", 11);
                    yield break;
                }

                operation = NetworkGameManager.Instance.JoinGameAsync(joinCode);
            }

            while (!operation.IsCompleted)
                yield return null;

            if (operation.IsFaulted)
            {
                Fail("session_operation_faulted", 12);
                yield break;
            }

            SessionOperationResult result = operation.Result;
            if (!result.Succeeded)
            {
                Fail($"session_operation_failed:{result.FailureReason}", 13);
                yield break;
            }

            if (role == PeerRole.Host && !string.IsNullOrWhiteSpace(joinFile))
            {
                string directory = Path.GetDirectoryName(joinFile);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(joinFile, NetworkGameManager.Instance.CurrentJoinCode);
            }

            Debug.Log($"[A1Harness] ready role={role} profile={profile}");

            float peerDeadline = Time.realtimeSinceStartup + 90f;
            float readySince = -1f;
            const float readyStabilitySeconds = 2f;
            while (Time.realtimeSinceStartup < peerDeadline)
            {
                bool peersConnected = NetworkGameManager.Instance.ConnectedPlayerCount >= 4;
                bool lobbyStable = NetworkGameManager.Instance.State == SessionState.Lobby
                    && NetworkGameManager.Instance.IsInLobby;

                if (peersConnected && lobbyStable)
                {
                    if (readySince < 0f)
                        readySince = Time.realtimeSinceStartup;

                    if (Time.realtimeSinceStartup - readySince >= readyStabilitySeconds)
                        break;
                }
                else
                {
                    readySince = -1f;
                }

                yield return null;
            }

            if (NetworkGameManager.Instance.ConnectedPlayerCount < 4)
            {
                Fail("four_peer_ready_timeout", 20);
                yield break;
            }

            // The normal lobby flow starts this from the waiting-room button. The standalone
            // harness has no UI, so the host must advance the synchronized session explicitly.
            if (role == PeerRole.Host)
            {
                Task<SessionOperationResult> startMatchOperation = NetworkGameManager.Instance.StartMatchAsync();
                while (!startMatchOperation.IsCompleted)
                    yield return null;

                if (startMatchOperation.IsFaulted || !startMatchOperation.Result.Succeeded)
                {
                    Fail($"start_match_failed:{(startMatchOperation.IsFaulted ? "faulted" : startMatchOperation.Result.FailureReason.ToString())}", 21);
                    yield break;
                }
            }

            if (scenario.Equals("reconnect", StringComparison.OrdinalIgnoreCase))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                NetworkGameManager.Instance.HoldReconnectForVerification(true);
#endif
            }
            else if (scenario.Equals("host-loss", StringComparison.OrdinalIgnoreCase))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                NetworkGameManager.Instance.TerminateOnHostLossForVerification(true);
#endif
            }

            A1VerificationProbe probe = FindAnyObjectByType<A1VerificationProbe>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (role == PeerRole.Host && enemyCount > 0)
            {
                float directorDeadline = Time.realtimeSinceStartup + 30f;
                AIDirector director = FindAnyObjectByType<AIDirector>();
                while (director == null && Time.realtimeSinceStartup < directorDeadline)
                {
                    yield return null;
                    director = FindAnyObjectByType<AIDirector>();
                }

                if (director != null)
                {
                    director.SetVerificationMaxZombiesAlive(enemyCount);
                    if (adaptiveEnabled)
                        director.ConfigureAdaptiveForVerification(true, adaptiveObserveOnly);
                }
                else if (adaptiveEnabled)
                {
                    probe?.Record("network_error", "fail", "adaptive_director_not_found_after_game_scene_ready");
                }
            }
#endif

            float readyAt = Time.realtimeSinceStartup;
            bool burstTriggered = false;
            bool transportDisconnected = false;
            bool reconnectTriggered = false;
            bool hostLossTriggered = false;
            float nextActivityAt = 1f;
            int activityStep = 0;
            Task<SessionOperationResult> reconnectOperation = null;
            while (Time.realtimeSinceStartup - readyAt < runDurationSeconds)
            {
                float elapsed = Time.realtimeSinceStartup - readyAt;
                string command = ReadControlCommand();
                if (!string.IsNullOrEmpty(command) && command != lastControlCommand)
                {
                    lastControlCommand = command;
                    if (command.Equals("disconnect", StringComparison.OrdinalIgnoreCase))
                        disconnectAtSeconds = elapsed;
                    else if (command.Equals("reconnect", StringComparison.OrdinalIgnoreCase))
                        reconnectAtSeconds = elapsed;
                    else if (command.Equals("host-loss", StringComparison.OrdinalIgnoreCase))
                        hostLossAtSeconds = elapsed;
                    else if (command.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                    {
                        NetworkGameManager.Instance?.MarkExpectedShutdownForVerification();
                        probe?.MarkExpectedShutdown();
                        if (role == PeerRole.Client && !controlledShutdownTriggered)
                        {
                            controlledShutdownTriggered = true;
                            controlledShutdownTask = NetworkGameManager.Instance.DisconnectAsync();
                        }
                    }

                }

                if (controlledShutdownTriggered && controlledShutdownTask != null
                    && controlledShutdownTask.IsCompleted)
                {
                    probe?.Complete();
                    Application.Quit(0);
                    yield break;
                }

                if (!burstTriggered && burstAtSeconds >= 0f && elapsed >= burstAtSeconds)
                {
                    burstTriggered = true;
                    simulator.TriggerLagSpike(TimeSpan.FromSeconds(3));
                    Debug.Log("[A1Harness] triggered 3-second packet outage");
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (elapsed >= nextActivityAt)
                {
                    nextActivityAt = elapsed + 2f;
                    NetworkManager networkManager = NetworkManager.Singleton;
                    NetworkObject localPlayer = networkManager != null
                        && networkManager.IsListening
                        && networkManager.SpawnManager != null
                        ? networkManager.SpawnManager.GetLocalPlayerObject()
                        : null;
                    if (localPlayer != null)
                    {
                        PlayerMovement movement = localPlayer.GetComponent<PlayerMovement>();
                        movement?.SetVerificationInput(activityStep % 2 == 0
                            ? new Vector2(0.65f, 0.35f)
                            : new Vector2(-0.35f, 0.65f));
                        localPlayer.GetComponent<WeaponFireHandler>()?.RequestVerificationFire();
                        localPlayer.GetComponent<InteractionManager>()?.RequestVerificationPickup();
                        activityStep++;
                    }
                }
#endif

                if (!transportDisconnected && disconnectAtSeconds >= 0f && elapsed >= disconnectAtSeconds)
                {
                    transportDisconnected = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    NetworkGameManager.Instance.DisconnectTransportForVerification();
#else
                    Fail("verification_requires_development_build", 10);
                    yield break;
#endif
                    Debug.Log("[A1Harness] disconnected transport for verification");
                }

                if (!reconnectTriggered && transportDisconnected && reconnectAtSeconds >= 0f
                    && elapsed >= reconnectAtSeconds)
                {
                    reconnectTriggered = true;
                    probe?.Record("reconnect_started", "info", $"elapsed={elapsed:F3}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    reconnectOperation = NetworkGameManager.Instance.ReconnectForVerificationAsync();
#else
                    Fail("verification_requires_development_build", 10);
                    yield break;
#endif
                    Debug.Log("[A1Harness] reconnect started");
                }

                if (role == PeerRole.Host && !hostLossTriggered && hostLossAtSeconds >= 0f
                    && elapsed >= hostLossAtSeconds)
                {
                    hostLossTriggered = true;
                    Task shutdown = NetworkGameManager.Instance.DisconnectAsync();
                    while (!shutdown.IsCompleted)
                        yield return null;
                    probe?.Complete("host_shutdown");
                    Application.Quit(0);
                    yield break;
                }

                if (scenario.Equals("host-loss", StringComparison.OrdinalIgnoreCase)
                    && role == PeerRole.Client
                    && NetworkGameManager.Instance.State == SessionState.Offline)
                {
                    probe?.Complete("host_loss_cleanup");
                    Application.Quit(0);
                    yield break;
                }

                if (reconnectOperation != null && reconnectOperation.IsCompleted)
                {
                    if (reconnectOperation.IsFaulted || reconnectOperation.IsCanceled)
                    {
                        Fail("reconnect_operation_faulted_or_cancelled", 30);
                        yield break;
                    }
                    SessionOperationResult reconnectResult = reconnectOperation.Result;
                    bool expectedExpiry = reconnectAtSeconds >= disconnectAtSeconds
                        && reconnectAtSeconds - disconnectAtSeconds >= 60f;
                    if (expectedExpiry && reconnectResult.FailureReason != SessionFailureReason.ReconnectExpired)
                    {
                        Fail($"unexpected_reconnect_result:{reconnectResult.FailureReason}", 30);
                        yield break;
                    }
                    if (!expectedExpiry && !reconnectResult.Succeeded)
                    {
                        Fail($"reconnect_failed:{reconnectResult.FailureReason}", 30);
                        yield break;
                    }
                    if (reconnectResult.Succeeded)
                        probe?.Record("reconnect_completed", "pass", "");
                    reconnectOperation = null;
                }

                yield return null;
            }

            if (role == PeerRole.Host && !string.IsNullOrWhiteSpace(controlFile))
            {
                try
                {
                    File.WriteAllText(controlFile, "shutdown");
                }
                catch (IOException)
                {
                    // The process can still shut down safely if the shared control file is unavailable.
                }

                // Give clients time to consume shutdown and leave the Multiplayer session
                // while their NetworkManagerSession is still listening.
                yield return new WaitForSecondsRealtime(2f);
            }

            NetworkGameManager.Instance.MarkExpectedShutdownForVerification();
            probe?.MarkExpectedShutdown();
            Task disconnect = NetworkGameManager.Instance.DisconnectAsync();
            while (!disconnect.IsCompleted)
                yield return null;
            probe?.Complete();
            Application.Quit(0);
        }

        private static NetworkSimulatorPreset CreatePreset(string name)
        {
            return name?.ToLowerInvariant() switch
            {
                "weak" => NetworkSimulatorPreset.Create(
                    "A1 Weak", "RTT ~200ms, jitter +/-40ms, loss 5%", 100, 40, 0, 5),
                "severe" => NetworkSimulatorPreset.Create(
                    "A1 Severe", "RTT ~350ms, jitter +/-80ms, loss 10%", 175, 80, 0, 10),
                _ => NetworkSimulatorPreset.Create(
                    "A1 Normal", "RTT below 40ms, no loss", 15, 5, 0, 0)
            };
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

        private static float ParsePositiveFloat(string value, float fallback)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                && parsed >= 0f
                ? parsed
                : fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out int parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            return bool.TryParse(value, out bool parsed) ? parsed : fallback;
        }

        private string ReadControlCommand()
        {
            if (string.IsNullOrWhiteSpace(controlFile) || !File.Exists(controlFile))
                return null;

            try
            {
                return File.ReadAllText(controlFile).Trim();
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static void Fail(string reason, int exitCode)
        {
            Debug.LogError($"[A1Harness] {reason}");
            Application.Quit(exitCode);
        }
    }
}
