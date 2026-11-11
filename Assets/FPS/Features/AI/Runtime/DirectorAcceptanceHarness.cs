#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace FPS
{
    /// <summary>Opt-in Development Player harness. Uses production authentication/session
    /// entry points; commands use gameplay/RPC paths, never insert collector evidence.</summary>
    public sealed class DirectorAcceptanceHarness : MonoBehaviour
    {
        public static string AuthenticationProfile { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProfile() => AuthenticationProfile = null;
        private string role, folder, previousName;
        private StreamWriter writer;
        private double nextSample, started;
        private AIDirector subscribed;
        private ZombieFactory factory;
        private SpecialInfectedRegistry specials;
        private NetworkGameManager sessionsObserved;
        private string lastSessionStatus;
        private string overlay;
        private GUIStyle overlayStyle;
        [Serializable] private sealed class Row
        {
            public int schemaVersion = 2, serverTick, livingEnemyCount, sampleCount, eligibleCount;
            public string recordType = "sample", utc, role, stage, message, runState, decisionReason, phase, entityId, campaignPhase, scene, sessionState, disconnectReason;
            public double serverTime, realtime;
            public float multiplier, pressure, hp, maxHp, damage, speed, frameMs, protectionLatencyMs = -1;
            public bool collectorPresent;
            public Actor[] actors;
            public Enemy[] enemies;
            public string runId, reason, holdReason;
            public int attemptId, encounterId, boundaryTick;
            public float score, target, multiplierBefore, multiplierAfter;
            public bool applied, hasEvidence, hasPerformanceScore, combatStatsAuthoritative;
            public Metric[] metrics;
        }
        [Serializable] private sealed class Actor
        {
            public string stableId, entityId, lifeState;
            public string fireResult;
            public int fireSequence, shotTick, ammo;
            public float hp, maxHp;
            public Vector3 position, forward;
            public bool ready;
        }
        [Serializable] private sealed class Enemy
        {
            public string entityId;
            public float hp, maxHp, damage, speed;
        }
        [Serializable] private sealed class Metric
        {
            public string playerId, evidence;
            public int shots, kills, downs;
            public float combatExposureSeconds, cumulativeDamageMaxHealth;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = Value(args, "-directorRole");
            if (role != "host" && role != "client") return;
            var root = new GameObject("Director acceptance harness");
            DontDestroyOnLoad(root);
            var harness = root.AddComponent<DirectorAcceptanceHarness>();
            harness.role = role;
            harness.folder = Path.GetFullPath(Value(args, "-directorRun") ?? "Documents/AI-Director-Defense/Captures/two-player");
            Directory.CreateDirectory(harness.folder);
            harness.previousName = PlayerPrefs.GetString("PlayerName", "Player");
            // In-memory preference only; restore on exit. Distinct first tokens select
            // distinct Unity Authentication profiles in the production bootstrap.
            string profile = "Dir" + (role == "host" ? "Host" : "Client") + Guid.NewGuid().ToString("N").Substring(0, 12);
            AuthenticationProfile = profile;
            PlayerPrefs.SetString("PlayerName", profile);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            harness.writer = new StreamWriter(Path.Combine(harness.folder, role + ".jsonl"), false) { AutoFlush = true };
            harness.started = Time.realtimeSinceStartupAsDouble;
            harness.Event("profile-selected", profile);
            harness.StartCoroutine(harness.Connect());
        }
        private static string Value(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        private IEnumerator Connect()
        {
            Event("bootstrap", "Waiting for production Services initialization.");
            double deadline = Time.realtimeSinceStartupAsDouble + 75;
            while ((NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsServicesInitialized)
                && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            var sessions = NetworkGameManager.Instance;
            if (sessions == null || !sessions.IsServicesInitialized)
            { Event("blocked", "Services initialization did not become ready within 75 seconds."); yield break; }
            sessionsObserved = sessions;
            sessions.OnConnectionFailed += ConnectionFailed;
            Event("authenticated", AuthenticationService.Instance.Profile + ":" + AuthenticationService.Instance.PlayerId);
            sessions.SelectedDifficulty = DifficultyLevel.Medium;
            if (role == "host")
            {
                Task<SessionOperationResult> host = sessions.StartHostGameAsync();
                yield return Await(host, "host");
                if (!host.IsCompletedSuccessfully || !host.Result.Succeeded) yield break;
                // Publish only a closed complete file. A client can observe Exists
                // between Create and WriteAllText's close otherwise.
                string pendingCode = Path.Combine(folder, "join-code.pending");
                File.WriteAllText(pendingCode, sessions.CurrentJoinCode);
                File.Move(pendingCode, Path.Combine(folder, "join-code.txt"));
                deadline = Time.realtimeSinceStartupAsDouble + 75;
                while ((!LobbyReady() || !File.Exists(Path.Combine(folder, "client-lobby-ready.txt")))
                    && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!LobbyReady() || !File.Exists(Path.Combine(folder, "client-lobby-ready.txt")))
                { Event("blocked", "Both Players did not complete production Lobby synchronization within 75 seconds."); yield break; }
                Event("lobby-ready", "Both peers have a spawned WaitingRoom with two registered lobby entries.");
                yield return Await(sessions.StartMatchAsync(), "start-match");
            }
            else
            {
                string code = Path.Combine(folder, "join-code.txt");
                deadline = Time.realtimeSinceStartupAsDouble + 75;
                while (!File.Exists(code) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!File.Exists(code)) { Event("blocked", "No host join code; production host unavailable."); yield break; }
                var join = sessions.JoinGameAsync(File.ReadAllText(code).Trim());
                yield return Await(join, "join");
                if (!join.IsCompletedSuccessfully || !join.Result.Succeeded) yield break;
                deadline = Time.realtimeSinceStartupAsDouble + 75;
                while (!LobbyReady() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!LobbyReady()) { Event("blocked", "Client Lobby synchronization timed out."); yield break; }
                string pending = Path.Combine(folder, "client-lobby-ready.pending");
                File.WriteAllText(pending, "ready");
                File.Move(pending, Path.Combine(folder, "client-lobby-ready.txt"));
                Event("lobby-ready", "Client WaitingRoom spawned and contains both players.");
            }
        }
        private static bool LobbyReady()
        {
            var room = WaitingRoomManager.Instance;
            return SceneManager.GetActiveScene().name == "LobbyScene"
                && room != null && room.IsSpawned && room.Players != null && room.Players.Count >= 2;
        }
        private void ConnectionFailed(string message) => Event("connection-failed", message);
        private IEnumerator Await(Task<SessionOperationResult> task, string stage)
        {
            while (!task.IsCompleted) yield return null;
            Event(task.IsCompletedSuccessfully && task.Result.Succeeded ? stage + "-ok" : "blocked",
                task.IsFaulted ? task.Exception.GetBaseException().Message
                    : task.IsCanceled ? "Operation cancelled" : task.Result.FailureReason + ": " + task.Result.Message);
        }
        private void Event(string stage, string message)
            => Write(new Row { recordType = "event", stage = stage, message = message });
        private void Write(Row row)
        {
            if (writer == null) return;
            var network = NetworkManager.Singleton;
            row.utc = DateTime.UtcNow.ToString("O"); row.role = role;
            row.scene = SceneManager.GetActiveScene().name;
            var sessions = NetworkGameManager.Instance;
            row.sessionState = sessions != null ? sessions.State.ToString() : "none";
            row.disconnectReason = sessions != null ? sessions.LastDisconnectMessage : "";
            var director = AIDirector.Instance;
            if (row.recordType != "sample" && director != null)
            {
                row.phase = director.AdaptivePhase.ToString();
                row.multiplier = director.AdaptiveDifficultyMultiplier;
                row.pressure = director.Intensity;
                row.runState = network != null && network.IsServer ? director.RunState.ToString() : "server-only";
            }
            row.realtime = Time.realtimeSinceStartupAsDouble - started;
            row.serverTick = network != null && network.IsListening ? network.ServerTime.Tick : -1;
            row.serverTime = network != null && network.IsListening ? network.ServerTime.Time : Time.timeAsDouble;
            writer.WriteLine(JsonUtility.ToJson(row));
        }
        private void Update()
        {
            if (writer == null) return;
            var sessions = NetworkGameManager.Instance;
            string status = SceneManager.GetActiveScene().name + "/" + (sessions != null ? sessions.State.ToString() : "none");
            if (status != lastSessionStatus) { lastSessionStatus = status; Event("session-state", status); }
            var director = AIDirector.Instance;
            if (director != subscribed)
            {
                if (subscribed != null) subscribed.EvaluationCompleted -= Evaluation;
                subscribed = director;
                if (subscribed != null) subscribed.EvaluationCompleted += Evaluation;
            }
            var currentFactory = ZombieFactory.HasInstance ? ZombieFactory.Instance : null;
            if (factory != currentFactory)
            {
                if (factory != null) factory.OnZombieSpawned -= CommonSpawn;
                factory = currentFactory;
                if (factory != null) factory.OnZombieSpawned += CommonSpawn;
            }
            if (specials != SpecialInfectedRegistry.Instance)
            {
                if (specials != null) specials.OnSpecialSpawned -= SpecialSpawn;
                specials = SpecialInfectedRegistry.Instance;
                if (specials != null) specials.OnSpecialSpawned += SpecialSpawn;
            }
            if (Time.realtimeSinceStartupAsDouble < nextSample) return;
            nextSample = Time.realtimeSinceStartupAsDouble + .1;
            var row = new Row { frameMs = Time.unscaledDeltaTime * 1000 };
            row.campaignPhase = CampaignMissionController.Instance != null ? CampaignMissionController.Instance.State.phase.ToString() : "none";
            var network = NetworkManager.Singleton;
            bool authority = network != null && network.IsServer;
            row.combatStatsAuthoritative = authority;
            if (director != null)
            {
                row.phase = director.AdaptivePhase.ToString(); row.multiplier = director.AdaptiveDifficultyMultiplier;
                row.pressure = director.Intensity; row.runState = director.RunState.ToString(); row.decisionReason = director.DecisionReason.ToString();
                row.protectionLatencyMs = authority ? director.ProtectionLatencyMilliseconds : -1;
                if (!authority) { row.runState = "server-only"; row.decisionReason = "server-only"; }
            }
            var metrics = NetworkGameManager.Instance?.AdaptiveMetrics;
            row.collectorPresent = metrics != null;
            if (metrics != null)
            {
                var samples = metrics.BuildSamples(); row.sampleCount = samples.Count;
                row.metrics = new Metric[samples.Count];
                for (int i = 0; i < samples.Count; i++) row.metrics[i] = ToMetric(samples[i], DynamicDifficultyPolicy.Default);
                foreach (var s in samples) if (s.HasEvidence(DynamicDifficultyPolicy.Default) || s.HasDistress) row.eligibleCount++;
            }
            var actors = new List<Actor>();
            var enemies = new List<Enemy>();
            if (network != null && network.IsListening)
                foreach (var obj in network.SpawnManager.SpawnedObjectsList)
                {
                    if (obj.IsPlayerObject && obj.TryGetComponent<PlayerHealth>(out var h))
                    {
                        var fire = obj.GetComponent<WeaponFireHandler>();
                        bool canReadFire = fire != null && (authority || fire.IsOwner);
                        var fireState = canReadFire ? fire.DiagnosticOwnerState : default;
                        actors.Add(new Actor { stableId = h.StablePlayerId.Value.ToString(), entityId = obj.NetworkObjectId.ToString(),
                            hp = h.CurrentHealth, maxHp = h.MaxHealth, lifeState = h.LifeState.ToString(),
                            ready = h.IsInputReady, position = h.transform.position, forward = h.transform.forward,
                            fireResult = canReadFire ? fireState.lastFireResult.ToString() : "owner-only",
                            fireSequence = fireState.acknowledgedFireSequence, shotTick = fireState.authoritativeShotTick,
                            ammo = canReadFire ? fireState.magazineAmmo : -1 });
                    }
                    if (obj.TryGetComponent<EnemyHealth>(out var e) && !e.IsDead)
                    {
                        row.livingEnemyCount++;
                        var ai = obj.GetComponent<EnemyAI>();
                        var agent = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        // HP is replicated. Client prefab damage/speed are not live server stats.
                        enemies.Add(new Enemy { entityId = obj.NetworkObjectId.ToString(), hp = e.CurrentHealth,
                            maxHp = e.MaxHealth, damage = authority && ai != null ? ai.AttackDamage : -1,
                            speed = authority && agent != null ? agent.speed : -1 });
                    }
                }
            row.actors = actors.ToArray();
            row.enemies = enemies.ToArray();
            Write(row);
            overlay = $"DIRECTOR VERIFICATION / {role}\nServer t={row.serverTime:F1}s  tick={row.serverTick}\n"
                + $"{row.phase} / {row.runState}\nPressure {row.pressure:F1}    DDA x{row.multiplier:F2}\n"
                + $"Actors {actors.Count}    Living enemies {row.livingEnemyCount}\n{row.decisionReason}";
            string commandPath = Path.Combine(folder, role + "-command.txt");
            if (TryTakeCommand(commandPath, out string command))
            {
                StartCoroutine(Command(command));
            }
        }
        private void OnGUI()
        {
            if (overlay == null) return;
            if (overlayStyle == null) overlayStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 18, normal = { textColor = Color.white } };
            var bounds = new Rect(Screen.width - 405, 25, 380, 175);
            GUI.Box(bounds, GUIContent.none);
            GUI.Label(new Rect(bounds.x + 12, bounds.y + 8, bounds.width - 24, bounds.height - 16), overlay, overlayStyle);
        }
        private static bool TryTakeCommand(string path, out string command)
        {
            command = null;
            if (!File.Exists(path)) return false;
            try
            {
                command = File.ReadAllText(path).Trim();
                if (command.Length == 0) return false;
                File.Delete(path);
                return true;
            }
            catch (IOException) { return false; } // Writer still owns it; retry at next sample.
        }
        private IEnumerator Command(string command)
        {
            Event("command", command);
            var network = NetworkManager.Singleton;
            if (command == "quit") { Application.Quit(); yield break; }
            if (command == "screenshot")
            {
                string path = Path.Combine(folder, role + "-" + DateTime.UtcNow.ToString("HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Event("screenshot-requested", path);
            }
            if (command == "fire")
            {
                if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left, true));
                yield return new WaitForSeconds(.25f);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            }
            else if (command.StartsWith("move-", StringComparison.Ordinal))
            {
                if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
                Key key = command == "move-left" ? Key.A : command == "move-right" ? Key.D : command == "move-back" ? Key.S : Key.W;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
                yield return new WaitForSeconds(.5f);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }
            else if (command == "reconnect") yield return Await(NetworkGameManager.Instance.ReconnectAsync(), "reconnect");
            else if (command == "retry") CampaignMissionController.Instance?.RequestRetry();
            else if (command == "ready") CampaignMissionController.Instance?.RequestReady(true);
            else if (command == "revive")
            {
                var campaign = CampaignMissionController.Instance;
                if (campaign == null) yield break;
                PlayerHealth target = null;
                foreach (var person in campaign.Players)
                    if (person.LifeState == PlayerLifeState.Downed) { target = person; break; }
                if (target == null) { Event("command-rejected", "No downed target."); yield break; }
                double until = Time.realtimeSinceStartupAsDouble + campaign.settings.reviveSeconds + 1;
                while (target != null && target.LifeState == PlayerLifeState.Downed && Time.realtimeSinceStartupAsDouble < until)
                {
                    campaign.RequestAid(target.NetworkObjectId, false, true);
                    yield return new WaitForSeconds(.2f);
                }
                if (target != null) campaign.RequestAid(target.NetworkObjectId, false, false);
            }
            else if ((command == "damage" || command == "chip") && network != null && network.IsServer)
            {
                foreach (var client in network.ConnectedClientsList)
                    if (client.PlayerObject != null && client.ClientId != network.LocalClientId)
                    { client.PlayerObject.GetComponent<PlayerHealth>()?.TakeDamage(command == "chip" ? 1 : 40); break; }
            }
            else if (command == "wipe" && network != null && network.IsServer)
                foreach (var client in network.ConnectedClientsList)
                    client.PlayerObject?.GetComponent<PlayerHealth>()?.TakeDamage(10000);
        }
        private void CommonSpawn(GameObject enemy) => Spawn(enemy, "common-spawn");
        private void SpecialSpawn(GameObject enemy) => Spawn(enemy, "special-spawn");
        private void Spawn(GameObject enemy, string type)
        {
            var health = enemy.GetComponent<EnemyHealth>(); var ai = enemy.GetComponent<EnemyAI>();
            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>(); var obj = enemy.GetComponent<NetworkObject>();
            Write(new Row { recordType = type, entityId = obj != null ? obj.NetworkObjectId.ToString() : "",
                hp = health != null ? health.MaxHealth : -1, damage = ai != null ? ai.AttackDamage : -1,
                speed = agent != null ? agent.speed : -1, multiplier = AIDirector.Instance.AdaptiveDifficultyMultiplier });
        }
        private void Evaluation(DirectorEvaluationRecord record)
        {
            var metrics = new Metric[record.Samples.Count];
            for (int i = 0; i < metrics.Length; i++)
            {
                var s = record.Samples[i];
                metrics[i] = ToMetric(s, record.Policy);
            }
            Write(new Row { recordType = "evaluation", runId = record.RunId, attemptId = record.AttemptId,
                encounterId = record.EncounterId, boundaryTick = record.BoundaryTick, reason = record.Reason.ToString(),
                holdReason = record.HoldReason, score = record.Evaluation.TeamPerformanceScore, target = record.Evaluation.TargetMultiplier,
                multiplierBefore = record.MultiplierBefore, multiplierAfter = record.Evaluation.Multiplier,
                applied = record.Applied, hasEvidence = record.Evaluation.HasEvidence, hasPerformanceScore = record.Evaluation.HasPerformanceScore,
                metrics = metrics });
        }
        private static Metric ToMetric(PlayerPerformanceSample s, DynamicDifficultyPolicy policy)
            => new Metric { playerId = s.StablePlayerId.ToString(), evidence = s.Classify(policy).ToString(),
                shots = s.ShotsFired, kills = s.Kills, downs = s.DownedCount,
                combatExposureSeconds = s.CombatExposureSeconds, cumulativeDamageMaxHealth = s.CumulativeDamageMaxHealth };
        private void OnApplicationQuit() => PlayerPrefs.SetString("PlayerName", previousName);
        private void OnDestroy()
        {
            if (sessionsObserved != null) sessionsObserved.OnConnectionFailed -= ConnectionFailed;
            if (subscribed != null) subscribed.EvaluationCompleted -= Evaluation;
            if (factory != null) factory.OnZombieSpawned -= CommonSpawn;
            if (specials != null) specials.OnSpecialSpawned -= SpecialSpawn;
            writer?.Dispose(); writer = null;
        }
    }
}
#endif
