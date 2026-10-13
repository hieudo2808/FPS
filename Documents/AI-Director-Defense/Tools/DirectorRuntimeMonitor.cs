#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using FPS;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Install under an Editor folder. Polls existing state; never advances or changes gameplay.
public sealed class DirectorRuntimeMonitor : EditorWindow
{
    const double SamplePeriod = 0.5;
    const int HistoryLimit = 240;
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo DecisionField = typeof(AIDirector).GetField("adaptiveDecision", PrivateInstance);
    static readonly FieldInfo StateField = typeof(AIDirector).GetField("adaptiveStateMachine", PrivateInstance);
    static readonly FieldInfo EvaluatorField = typeof(AIDirector).GetField("adaptiveDifficultyEvaluator", PrivateInstance);
    static readonly FieldInfo PolicyField = typeof(DynamicDifficultyEvaluator).GetField("policy", PrivateInstance);
    static readonly Color[] PhaseColors = { new Color(.25f,.55f,.75f), new Color(.8f,.7f,.2f), new Color(.9f,.45f,.15f), new Color(.85f,.2f,.2f), new Color(.25f,.7f,.5f) };
    readonly List<Snapshot> history = new List<Snapshot>();
    StreamWriter writer;
    Snapshot current;
    string capturePath = "", status = "Read-only polling: no gameplay changes.";
    int writtenSamples;
    double nextSample;
    Vector2 scroll;
    ZombieFactory observedFactory;
    SpecialInfectedRegistry observedSpecialRegistry;
    static Text runtimeHud;

    [Serializable] public sealed class PlayerRow
    {
        public string objectName, clientId, lifeState;
        public float hp, maxHp;
    }
    [Serializable] public sealed class MetricRow
    {
        public string playerId;
        public int shots, kills, downs;
        public float headshotRatio, damageTakenNorm, downedCountNorm, ammoEfficiency;
        public bool eligibilityKnown, eligible;
    }
    [Serializable] public sealed class Snapshot
    {
        public string recordType = "sample", utc, scene, role, mode, legacyPhase, adaptivePhase;
        public double gameSeconds;
        public int frame, directorAlive, observedLivingEnemyHealth, totalKills, adaptivePhaseIndex;
        public bool playing, paused, directorFound, directorActive, gameplayActive, adaptiveEnabled, observeOnly, adaptiveAffectsSpawning;
        public bool internalStateAvailable, specialGateProposal, protectWeakestProposal, policyAvailable, metricsAvailable;
        public float multiplier = 1f, intensity, phaseElapsed, phasePressure, evaluatorMultiplier = 1f;
        public int minimumShots, minimumKills;
        public PlayerRow[] players = Array.Empty<PlayerRow>();
        public MetricRow[] metrics = Array.Empty<MetricRow>();
    }
    [Serializable] sealed class CaptureHeader
    {
        public string recordType = "header", utc, unityVersion;
        public string label = "natural runtime observation; no injected inputs";
        public string method = "Editor polling every 0.5s wall time; not an event-complete trace or spawn-request log";
        public double samplePeriodSeconds = SamplePeriod;
    }
    [Serializable] sealed class CaptureFooter
    {
        public string recordType = "footer", utc;
        public int samples;
    }
    [Serializable] sealed class SpawnRecord
    {
        public string recordType = "common-spawn", utc, name, phase, adaptivePhase;
        public double gameSeconds;
        public float multiplier, hp, damage, speed, nearestPlayerDistance = -1;
        public Vector3 position;
        public bool adaptiveEnabled, observeOnly;
    }

    [MenuItem("Tools/AI Director/Runtime Monitor")]
    public static void Open() => GetWindow<DirectorRuntimeMonitor>("Director Monitor").Show();

    [MenuItem("Tools/AI Director/Start Natural Capture")]
    public static void CaptureStart()
        => BeginCapture("natural runtime observation; no injected inputs");

    [MenuItem("Tools/AI Director/Start Controlled Capture")]
    public static void CaptureControlled()
        => BeginCapture("controlled runtime verification; inputs/configuration may be injected; see report for each intervention");

    static void BeginCapture(string label)
    {
        var window = GetWindow<DirectorRuntimeMonitor>("Director Monitor");
        if (window.writer != null) return;
        if (!EditorApplication.isPlaying) { window.status = "Enter Play Mode before starting capture."; return; }
        try
        {
            window.capturePath = NewPath(label.StartsWith("controlled", StringComparison.Ordinal) ? "controlled" : "natural", ".jsonl");
            window.writer = new StreamWriter(new FileStream(window.capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            window.writer.WriteLine(JsonUtility.ToJson(new CaptureHeader { utc = UtcNow(), unityVersion = Application.unityVersion, label = label }));
            window.writtenSamples = 0;
            window.history.Clear();
            window.nextSample = 0;
            window.status = label + ". Capture stops when this window closes or Play Mode ends.";
            Debug.Log("Director capture: " + window.capturePath);
        }
        catch (Exception error) { window.Fail(error); }
    }

    [MenuItem("Tools/AI Director/Stop Capture")]
    public static void CaptureStop()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<DirectorRuntimeMonitor>()) window.StopCapture();
    }

    [MenuItem("Tools/AI Director/Show Runtime HUD")]
    public static void ShowHud()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Runtime HUD requires Play Mode.");
        if (runtimeHud != null) return;
        Open();
        var canvasObject = new GameObject("Director verification HUD", typeof(Canvas));
        canvasObject.hideFlags = HideFlags.DontSave;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var panel = new GameObject("Read-only panel", typeof(Image));
        panel.hideFlags = HideFlags.DontSave;
        panel.transform.SetParent(canvasObject.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -70);
        rect.sizeDelta = new Vector2(530, 310);
        panel.GetComponent<Image>().color = new Color(.025f, .04f, .065f, .94f);
        panel.GetComponent<Image>().raycastTarget = false;
        var textObject = new GameObject("State", typeof(Text));
        textObject.hideFlags = HideFlags.DontSave;
        textObject.transform.SetParent(panel.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(15, 10); textRect.offsetMax = new Vector2(-15, -12);
        runtimeHud = textObject.GetComponent<Text>();
        runtimeHud.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        runtimeHud.fontSize = 18; runtimeHud.color = Color.white; runtimeHud.raycastTarget = false;
        runtimeHud.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    [MenuItem("Tools/AI Director/Hide Runtime HUD")]
    public static void HideHud()
    {
        if (runtimeHud != null) UnityEngine.Object.Destroy(runtimeHud.transform.root.gameObject);
        runtimeHud = null;
    }

    [MenuItem("Tools/AI Director/Export Current Snapshot")]
    public static void ExportCurrent()
    {
        var window = GetWindow<DirectorRuntimeMonitor>("Director Monitor");
        try
        {
            string path = NewPath("snapshot", ".json");
            File.WriteAllText(path, JsonUtility.ToJson(ReadSnapshot(), true), new UTF8Encoding(false));
            window.status = "Snapshot exported: " + path;
            Debug.Log(window.status);
        }
        catch (Exception error) { window.Fail(error); }
    }

    static string UtcNow() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    static string NewPath(string prefix, string extension)
    {
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Documents", "AI-Director-Defense", "Captures");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, prefix + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);
    }

    public static Snapshot ReadSnapshot()
    {
        var s = new Snapshot { utc = UtcNow(), scene = SceneManager.GetActiveScene().path, playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused, gameSeconds = Time.timeAsDouble, frame = Time.frameCount };
        if (!s.playing) { s.mode = "Edit Mode: no runtime observation"; return s; }
        var network = NetworkManager.Singleton;
        bool authority = network == null || !network.IsListening || network.IsServer;
        s.role = network == null || !network.IsListening ? "Local/offline" : network.IsHost ? "Host" : network.IsServer ? "Server" : "Client (server internals unavailable)";
        s.gameplayActive = NetworkMatchStateManager.IsGameplayActive;
        var director = AIDirector.Instance;
        s.directorFound = director != null;
        if (director != null)
        {
            s.directorActive = director.isActiveAndEnabled;
            s.legacyPhase = director.CurrentPhase.ToString();
            s.adaptivePhase = director.AdaptivePhase.ToString();
            s.adaptivePhaseIndex = (int)director.AdaptivePhase;
            s.adaptiveEnabled = director.AdaptiveDirectorEnabled;
            s.observeOnly = director.AdaptiveObserveOnly;
            s.adaptiveAffectsSpawning = s.adaptiveEnabled && !s.observeOnly && authority && s.directorActive && s.gameplayActive;
            s.multiplier = director.AdaptiveDifficultyMultiplier;
            s.intensity = director.Intensity;
            s.directorAlive = director.ZombiesAlive;
            s.totalKills = director.TotalKills;
            s.mode = !s.adaptiveEnabled ? "LEGACY: adaptive disabled" : s.observeOnly ? "OBSERVE ONLY: legacy spawning remains active" : s.adaptiveAffectsSpawning ? "ADAPTIVE: controls local authoritative spawning" : "Adaptive configured; waiting for server/gameplay";
            var state = authority ? StateField?.GetValue(director) as DirectorStateMachine : null;
            s.internalStateAvailable = state != null && DecisionField != null;
            if (s.internalStateAvailable)
            {
                var decision = (DirectorDecision)DecisionField.GetValue(director);
                s.phaseElapsed = state.PhaseElapsedSeconds;
                s.phasePressure = decision.SpawnRateMultiplier;
                s.specialGateProposal = decision.SpecialGateOpen;
                s.protectWeakestProposal = decision.ProtectWeakestPlayer;
            }
            var evaluator = authority ? EvaluatorField?.GetValue(director) as DynamicDifficultyEvaluator : null;
            if (evaluator != null) s.evaluatorMultiplier = evaluator.CurrentMultiplier;
            s.policyAvailable = evaluator != null && PolicyField != null;
            if (s.policyAvailable)
            {
                var policy = (DynamicDifficultyPolicy)PolicyField.GetValue(evaluator);
                s.minimumShots = policy.MinimumShots;
                s.minimumKills = policy.MinimumKills;
            }
        }
        else s.mode = "No live AIDirector instance";
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (enemy.gameObject.activeInHierarchy && !enemy.IsDead) s.observedLivingEnemyHealth++;
        var players = new List<PlayerRow>();
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            players.Add(new PlayerRow { objectName = player.name, clientId = player.IsSpawned ? player.OwnerClientId.ToString(CultureInfo.InvariantCulture) : "unspawned", lifeState = player.LifeState.ToString(), hp = player.CurrentHealth, maxHp = player.MaxHealth });
        s.players = players.ToArray();
        var metrics = authority ? NetworkGameManager.Instance?.AdaptiveMetrics : null;
        s.metricsAvailable = metrics != null;
        var rows = new List<MetricRow>();
        if (metrics != null)
            foreach (var sample in metrics.BuildSamples())
                rows.Add(new MetricRow { playerId = sample.StablePlayerId.ToString(CultureInfo.InvariantCulture), shots = sample.ShotsFired, kills = sample.Kills, downs = sample.DownedCount, headshotRatio = sample.HeadshotRatio, damageTakenNorm = sample.DamageTakenNorm, downedCountNorm = sample.DownedCountNorm, ammoEfficiency = sample.AmmoEfficiency, eligibilityKnown = s.policyAvailable, eligible = s.policyAvailable && sample.ShotsFired >= s.minimumShots && sample.Kills >= s.minimumKills });
        s.metrics = rows.ToArray();
        return s;
    }

    void OnEnable()
    {
        minSize = new Vector2(520, 530);
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += StopCapture;
    }
    void OnDisable()
    {
        EditorApplication.update -= Poll;
        AssemblyReloadEvents.beforeAssemblyReload -= StopCapture;
        StopCapture();
        if (observedFactory != null) observedFactory.OnZombieSpawned -= OnSpawn;
        if (observedSpecialRegistry != null) observedSpecialRegistry.OnSpecialSpawned -= OnSpecialSpawn;
    }
    void OnSpecialSpawn(GameObject enemy) => WriteSpawn(enemy, "special-spawn");
    void OnSpawn(GameObject enemy) => WriteSpawn(enemy, "common-spawn");
    void WriteSpawn(GameObject enemy, string recordType)
    {
        if (writer == null || enemy == null) return;
        var director = AIDirector.Instance;
        var health = enemy.GetComponent<EnemyHealth>();
        var brain = enemy.GetComponent<EnemyAI>();
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var row = new SpawnRecord { recordType = recordType, utc = UtcNow(), name = enemy.name, gameSeconds = Time.timeAsDouble,
            position = enemy.transform.position, hp = health != null ? health.MaxHealth : -1,
            damage = brain != null ? brain.AttackDamage : -1, speed = agent != null ? agent.speed : -1,
            phase = director != null ? director.CurrentPhase.ToString() : "none",
            adaptivePhase = director != null ? director.AdaptivePhase.ToString() : "none",
            multiplier = director != null ? director.AdaptiveDifficultyMultiplier : 1,
            adaptiveEnabled = director != null && director.AdaptiveDirectorEnabled,
            observeOnly = director != null && director.AdaptiveObserveOnly };
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(player.transform.position, row.position);
            if (row.nearestPlayerDistance < 0 || distance < row.nearestPlayerDistance) row.nearestPlayerDistance = distance;
        }
        writer.WriteLine(JsonUtility.ToJson(row));
    }
    void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + SamplePeriod;
        try
        {
            current = ReadSnapshot();
            if (observedFactory != ZombieFactory.Instance)
            {
                if (observedFactory != null) observedFactory.OnZombieSpawned -= OnSpawn;
                observedFactory = ZombieFactory.Instance;
                if (observedFactory != null) observedFactory.OnZombieSpawned += OnSpawn;
            }
            if (observedSpecialRegistry != SpecialInfectedRegistry.Instance)
            {
                if (observedSpecialRegistry != null) observedSpecialRegistry.OnSpecialSpawned -= OnSpecialSpawn;
                observedSpecialRegistry = SpecialInfectedRegistry.Instance;
                if (observedSpecialRegistry != null) observedSpecialRegistry.OnSpecialSpawned += OnSpecialSpawn;
            }
            if (runtimeHud != null)
            {
                string hp = current.players.Length > 0 ? current.players[0].hp.ToString("F0") + "/" + current.players[0].maxHp.ToString("F0") : "no player";
                runtimeHud.text = "AI DIRECTOR | LIVE VERIFICATION\n" + current.mode + "\n"
                    + "Legacy / adaptive: " + current.legacyPhase + " / " + current.adaptivePhase + "\n"
                    + "DDA multiplier: " + current.multiplier.ToString("F4") + "\n"
                    + "Adaptive pressure: " + (current.adaptiveEnabled ? current.phasePressure.ToString("F2") : "inactive") + " | elapsed " + current.phaseElapsed.ToString("F1") + "s\n"
                    + "Living enemies: " + current.observedLivingEnemyHealth + " | Director count: " + current.directorAlive + "\n"
                    + "Player HP: " + hp + " | metric rows: " + current.metrics.Length + "\n"
                    + "Special gate proposal: " + current.specialGateProposal + "\n"
                    + "Read-only HUD. Probe inputs are documented separately.";
            }
            if (!current.playing) StopCapture();
            else
            {
                history.Add(current);
                if (history.Count > HistoryLimit) history.RemoveAt(0);
                if (writer != null) { writer.WriteLine(JsonUtility.ToJson(current)); writtenSamples++; }
            }
            Repaint();
        }
        catch (Exception error) { Fail(error); }
    }
    void StopCapture()
    {
        if (writer == null) return;
        var closing = writer;
        writer = null;
        try
        {
            using (closing) closing.WriteLine(JsonUtility.ToJson(new CaptureFooter { utc = UtcNow(), samples = writtenSamples }));
            status = "Saved " + writtenSamples + " samples: " + capturePath;
        }
        catch (Exception error) { status = "Capture close failed: " + error.Message; Debug.LogError(status); }
    }
    void Fail(Exception error)
    {
        StopCapture();
        status = "Monitor error: " + error.Message;
        Debug.LogError(status);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("AI DIRECTOR · LIVE OBSERVATION", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(current?.mode ?? "Waiting for first sample…", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || writer != null)) if (GUILayout.Button("Start capture")) CaptureStart();
        using (new EditorGUI.DisabledScope(writer == null)) if (GUILayout.Button("Stop capture")) StopCapture();
        if (GUILayout.Button("Export snapshot")) ExportCurrent();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        if (current != null && current.playing)
        {
            var s = current;
            EditorGUILayout.LabelField("Authority / time", s.role + " | " + s.gameSeconds.ToString("F1") + "s" + (s.paused ? " (PAUSED)" : ""));
            EditorGUILayout.LabelField("Legacy / adaptive phase", s.legacyPhase + " / " + s.adaptivePhase);
            EditorGUILayout.LabelField("Multiplier / intensity", s.multiplier.ToString("F4") + " / " + s.intensity.ToString("F1"));
            EditorGUILayout.LabelField("Director count / scene living EnemyHealth", s.directorAlive + " / " + s.observedLivingEnemyHealth + "   Kills: " + s.totalKills);
            EditorGUILayout.LabelField("Adaptive phase proposal", !s.adaptiveEnabled ? "Inactive: adaptive disabled" : s.internalStateAvailable ? "Elapsed " + s.phaseElapsed.ToString("F1") + "s; pressure " + s.phasePressure.ToString("F2") + "; special gate " + s.specialGateProposal : "Unavailable");
            DrawHistory();
            GUILayout.Label("PLAYER HEALTH (live components)", EditorStyles.boldLabel);
            foreach (var player in s.players) EditorGUILayout.LabelField(player.objectName, player.hp.ToString("F0") + "/" + player.maxHp.ToString("F0") + " — " + player.lifeState);
            GUILayout.Label("CURRENT ENCOUNTER METRICS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(s.metricsAvailable ? "Reset by gameplay at Relax; these are not the last completed evaluation." : "Authoritative metrics unavailable.", EditorStyles.wordWrappedLabel);
            foreach (var row in s.metrics) EditorGUILayout.LabelField("Player " + row.playerId, "shots " + row.shots + ", kills " + row.kills + ", downs " + row.downs + ", eligible " + (row.eligibilityKnown ? row.eligible.ToString() : "unknown"));
            EditorGUILayout.HelpBox("0.5s polling may miss brief transitions. Alive counts are observed state, not successful-spawn events or spawn requests. Special gate is a proposal, not proof that a special spawned. Client metrics cannot verify server decisions.", MessageType.None);
        }
        EditorGUILayout.EndScrollView();
    }
    void DrawHistory()
    {
        Rect rect = GUILayoutUtility.GetRect(300, 135, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.10f, .12f, .15f));
        if (history.Count < 2) return;
        float maxAlive = 1f;
        foreach (var s in history) maxAlive = Mathf.Max(maxAlive, s.observedLivingEnemyHealth);
        Handles.BeginGUI();
        for (int i = 1; i < history.Count; i++)
        {
            var a = history[i - 1]; var b = history[i];
            float x0 = rect.x + (i - 1f) / (history.Count - 1) * rect.width;
            float x1 = rect.x + i / (float)(history.Count - 1) * rect.width;
            EditorGUI.DrawRect(new Rect(x0, rect.y, Mathf.Max(1f, x1 - x0), 12), b.adaptiveEnabled ? PhaseColors[Mathf.Clamp(b.adaptivePhaseIndex, 0, 4)] : Color.gray);
            Handles.color = Color.cyan;
            Handles.DrawLine(new Vector3(x0, rect.yMax - Mathf.Clamp01(a.multiplier / 1.5f) * 110), new Vector3(x1, rect.yMax - Mathf.Clamp01(b.multiplier / 1.5f) * 110));
            Handles.color = new Color(1f,.75f,.25f);
            Handles.DrawLine(new Vector3(x0, rect.yMax - a.observedLivingEnemyHealth / maxAlive * 110), new Vector3(x1, rect.yMax - b.observedLivingEnemyHealth / maxAlive * 110));
        }
        Handles.EndGUI();
        EditorGUILayout.LabelField("Cyan: multiplier 0–1.5 | Gold: living enemies 0–" + maxAlive + " | last " + ((history.Count - 1) * SamplePeriod).ToString("F0") + " wall seconds", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Phase strip: Calm blue · BuildUp yellow · Combat orange · Peak red · Relax green · disabled gray", EditorStyles.miniLabel);
    }
}
#endif
