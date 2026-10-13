using System;
using System.IO;
using System.Reflection;
using FPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit controlled interventions. Never runs automatically or changes authored settings.
public static class DirectorLiveProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly DirectorInput Healthy = new DirectorInput(1, 0, 0, 999, 0, 0);

    [Serializable] public sealed class SmartEnemy
    {
        public string entityId, name, type, state, target;
        public bool spawned, onNavMesh, hasPath;
        public Vector3 position, velocity;
        public float hp, damage, speed;
    }
    [Serializable] public sealed class SmartReport
    {
        public string utc, formation;
        public double gameSeconds;
        public int profiledPlayers, trackedZombies;
        public bool smartSpawning, rubberBanding, canSpawnSpecial;
        public Vector3 playerPosition, playerForward;
        public SmartEnemy[] enemies;
    }

    [MenuItem("Tools/AI Director/Export Smart Snapshot")]
    public static void ExportSmart()
    {
        var director = Live();
        var brains = UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        var rows = new System.Collections.Generic.List<SmartEnemy>();
        foreach (var brain in brains)
        {
            var health = brain.GetComponent<EnemyHealth>();
            if (health == null || health.IsDead || !brain.isActiveAndEnabled) continue;
            var agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var target = typeof(EnemyAI).GetField("player", Private).GetValue(brain) as Transform;
            rows.Add(new SmartEnemy {
                entityId = brain.GetEntityId().ToString(), name = brain.name, type = brain.GetType().Name,
                state = typeof(EnemyAI).GetField("currentState", Private).GetValue(brain).ToString(),
                target = target != null ? target.name : "none", spawned = brain.IsSpawned,
                position = brain.transform.position, hp = health.MaxHealth, damage = brain.AttackDamage,
                onNavMesh = agent != null && agent.enabled && agent.isOnNavMesh,
                hasPath = agent != null && agent.enabled && agent.hasPath,
                velocity = agent != null && agent.enabled ? agent.velocity : Vector3.zero,
                speed = agent != null ? agent.speed : -1
            });
        }
        var tracked = RubberBandingSystem.Instance != null
            ? Get(RubberBandingSystem.Instance, "trackedZombies") as System.Collections.IDictionary : null;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        var report = new SmartReport {
            utc = DateTime.UtcNow.ToString("O"), gameSeconds = Time.timeAsDouble,
            formation = TeamAnalyzer.Instance != null ? TeamAnalyzer.Instance.Formation.ToString() : "none",
            profiledPlayers = PlayerProfiler.Instance != null ? PlayerProfiler.Instance.PlayerCount : 0,
            trackedZombies = tracked != null ? tracked.Count : 0,
            smartSpawning = (bool)Get(director, "useSmartSpawning"),
            rubberBanding = RubberBandingSystem.Instance != null && RubberBandingSystem.Instance.isEnabled,
            canSpawnSpecial = SpecialInfectedRegistry.Instance != null
                && SpecialInfectedRegistry.Instance.CanSpawnSpecial(director.CurrentPhase),
            playerPosition = player != null ? player.transform.position : Vector3.zero,
            playerForward = player != null ? player.transform.forward : Vector3.zero,
            enemies = rows.ToArray()
        };
        if (report.profiledPlayers == 0) throw new InvalidOperationException("No live player profiles to verify smart AI.");
        string path = Path.GetFullPath("Documents/AI-Director-Defense/Captures/smart-"
            + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("Smart AI snapshot: " + path);
    }

    [MenuItem("Tools/AI Director/Save Authored Assets")]
    public static void Save()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!string.IsNullOrEmpty(scene.path) && !scene.name.StartsWith("Untitled")
                && !scene.path.StartsWith("Assets/InitTestScene", StringComparison.Ordinal)
                && !EditorSceneManager.IsPreviewScene(scene) && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }

    static AIDirector Live()
    {
        var director = AIDirector.Instance;
        if (!Application.isPlaying || director == null || !NetworkMatchStateManager.IsGameplayActive)
            throw new InvalidOperationException("Requires an active disposable Play Mode gameplay session.");
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening
            && !Unity.Netcode.NetworkManager.Singleton.IsServer)
            throw new InvalidOperationException("Requires the authoritative server.");
        return director;
    }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static object Call(AIDirector director, string name, params object[] args) => typeof(AIDirector).GetMethod(name, Private).Invoke(director, args);
    static void Mark(string intervention)
    {
        string directory = Path.GetFullPath("Documents/AI-Director-Defense/Captures");
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "interventions.txt"), DateTime.UtcNow.ToString("O") + " | gameSeconds=" + Time.timeAsDouble.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " | " + intervention + Environment.NewLine);
        Debug.Log("[CONTROLLED AI PROBE] " + intervention);
    }
    static void Combat(AIDirector director)
    {
        var machine = new DirectorStateMachine(DirectorPolicy.Default);
        machine.Advance(3, Healthy);
        machine.Advance(15, Healthy);
        Set(director, "adaptiveStateMachine", machine);
        Set(director, "adaptiveDecision", machine.GetDecision());
        Call(director, "SetPhase", GamePhase.BUILD);
        Call(director, "SetAdaptiveState", DirectorPhase.Combat, (float)Get(director, "adaptiveDifficultyMultiplier"));
        Set(director, "spawnTimer", 0f);
    }

    [MenuItem("Tools/AI Director/Controlled/Activate Combat")]
    public static void ActivateCombat()
    {
        var director = Live();
        Set(director, "enableAdaptiveDirector", true);
        Set(director, "adaptiveObserveOnly", false);
        Combat(director);
        Mark("Runtime flags adaptive=true, observeOnly=false; fresh default FSM advanced with healthy fixture to Combat. DDA evaluator preserved. No authored settings saved.");
    }

    [MenuItem("Tools/AI Director/Controlled/Good Telemetry Then Relax")]
    public static void GoodRelax() => EvaluateRelax(false);
    [MenuItem("Tools/AI Director/Controlled/Poor Telemetry Then Relax")]
    public static void PoorRelax() => EvaluateRelax(true);
    static void EvaluateRelax(bool poor)
    {
        var director = Live();
        if (!director.AdaptiveDirectorEnabled || director.AdaptiveObserveOnly)
            throw new InvalidOperationException("Activate Combat first.");
        var metrics = NetworkGameManager.Instance?.AdaptiveMetrics;
        if (metrics == null) throw new InvalidOperationException("AdaptiveMetrics is missing.");
        metrics.ResetEncounter();
        metrics.Record(new ServerTelemetrySnapshot(0, new SessionPlayerId(1), true, false, 20, 30,
            100, poor ? 800 : 0, 0, poor ? 8 : 10, poor ? 0 : 10,
            poor ? 80 : 20, poor ? 8 : 20, poor ? 0 : 20, poor ? 3 : 0));
        var peak = new DirectorStateMachine(DirectorPolicy.Default);
        peak.Advance(3, Healthy); peak.Advance(15, Healthy); peak.Advance(60, Healthy);
        // The next actual UpdateAdaptiveDirector call crosses Peak -> Relax using the frame delta.
        Set(peak, "phaseElapsed", 15f);
        Set(director, "adaptiveStateMachine", peak);
        float before = director.AdaptiveDifficultyMultiplier;
        Call(director, "UpdateAdaptiveDirector");
        Mark((poor ? "Poor" : "Good") + " synthetic server snapshot injected directly into collector; live UpdateAdaptiveDirector triggered Relax boundary; multiplier " + before + " -> " + director.AdaptiveDifficultyMultiplier + ". This bypasses gameplay event/registry acquisition and does not represent a human performance trial.");
        if (director.AdaptivePhase != DirectorPhase.Relax || metrics.PlayerCount != 0
            || Math.Abs(director.AdaptiveDifficultyMultiplier - before) > .10001f)
            throw new InvalidOperationException("Relax transition/encounter reset failed.");
    }

    [MenuItem("Tools/AI Director/Controlled/Resume Combat")]
    public static void ResumeCombat() { var director = Live(); Combat(director); Mark("Controlled FSM reset to Combat; current evaluated multiplier preserved."); }

    [MenuItem("Tools/AI Director/Controlled/Move Player To Valid Map Position")]
    public static void MovePlayer()
    {
        Live();
        var campaign = CampaignMissionController.Instance;
        var service = DirectorSpawnService.Instance;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (campaign?.Current == null || service == null || player == null || campaign.SuppressSpawns)
            throw new InvalidOperationException("Requires active campaign exploration, player and spawn service.");
        Vector3 original = player.transform.position;
        Vector3 chosen = original;
        DirectorSpawnAnchor valid = null;
        // Use the map's recovery markers first; preserve all real spawn visibility/path constraints.
        try
        {
            foreach (var marker in campaign.Current.recoveries)
            {
                if (marker == null || !UnityEngine.AI.NavMesh.SamplePosition(marker.transform.position, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                player.transform.position = hit.position;
                Physics.SyncTransforms();
                foreach (var anchor in campaign.Current.anchors)
                    if (service.IsAnchorValid(anchor)) { chosen = hit.position; valid = anchor; break; }
                if (valid != null) break;
            }
        }
        finally { player.transform.position = original; Physics.SyncTransforms(); }
        if (valid == null) throw new InvalidOperationException("No recovery marker permits a fair spawn; nothing relocated.");
        var direction = valid.SpawnPosition - chosen; direction.y = 0;
        player.RelocateCampaign(chosen, Quaternion.LookRotation(direction));
        Mark("Controlled relocation to authored recovery marker at " + chosen + "; validated actual anchor " + valid.name + ". Visibility, ground, distance, capsule and NavMesh guards preserved.");
    }

    [MenuItem("Tools/AI Director/Controlled/Skip Campaign Intro")]
    public static void SkipIntro()
    {
        Live();
        var campaign = CampaignMissionController.Instance;
        if (campaign == null || !campaign.InsertionReady)
            throw new InvalidOperationException("Wait for the campaign insertion roster to become ready.");
        typeof(CampaignMissionController).GetMethod("SetPhase", Private).Invoke(campaign, new object[] { CampaignPhase.Exploring });
        campaign.ConfigureChapterServices();
        Mark("Controlled skip of cinematic insertion to Exploring through campaign phase handler. Campaign spawn guards otherwise unchanged.");
    }

    [MenuItem("Tools/AI Director/Controlled/Move Player Near Live Enemy")]
    public static void MoveNearEnemy()
    {
        Live();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null) throw new InvalidOperationException("No live player.");
        foreach (var brain in UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            var health = brain.GetComponent<EnemyHealth>();
            if (!brain.IsSpawned || health == null || health.IsDead) continue;
            var desired = brain.transform.position + Vector3.forward * 10f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(desired, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(brain.transform.position, hit.position,
                UnityEngine.AI.NavMesh.AllAreas, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
            if (Vector3.Distance(brain.transform.position, hit.position) < 5f) continue;
            player.RelocateCampaign(hit.position, Quaternion.identity);
            Mark("Controlled relocation near living enemy " + brain.GetEntityId()
                + " at " + hit.position + "; complete NavMesh path confirmed. Faces away to exercise catch-up; AI state/damage unchanged.");
            return;
        }
        throw new InvalidOperationException("No live enemy has a reachable nearby position; nothing relocated.");
    }

    [MenuItem("Tools/AI Director/Controlled/Attempt One Common Spawn")]
    public static void SpawnOnce()
    {
        var director = Live();
        Set(director, "spawnTimer", 100f);
        int before = director.ZombiesAlive;
        Call(director, "UpdateAdaptiveSpawning");
        Mark("Forced timer expiry; actual UpdateAdaptiveSpawning attempt in " + director.AdaptivePhase + "; alive " + before + " -> " + director.ZombiesAlive + ". Failed anchors/cap may reject the request; common-spawn events prove success.");
    }
}
