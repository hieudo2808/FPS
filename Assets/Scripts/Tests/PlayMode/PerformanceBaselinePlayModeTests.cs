using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class PerformanceBaselinePlayModeTests
    {
        private readonly List<Object> objectsToDestroy = new List<Object>();
        private readonly List<NavMeshDataInstance> navMeshInstances = new List<NavMeshDataInstance>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (objectsToDestroy[i] != null)
                    Object.Destroy(objectsToDestroy[i]);
            }

            objectsToDestroy.Clear();

            for (int i = navMeshInstances.Count - 1; i >= 0; i--)
                navMeshInstances[i].Remove();

            navMeshInstances.Clear();
        }

        [UnityTest]
        [Category("LongSoak")]
        [Explicit("Opt-in performance baseline. Writes Logs/Performance/CodexPerformanceBaselineReport.txt.")]
        [Timeout(90000)]
        public IEnumerator HordePerformanceBaseline_RecordsSixtySecondRuntimeMetrics()
        {
            Scene scene = SceneManager.CreateScene($"PerformanceBaseline_{Time.frameCount}");
            Assert.IsTrue(SceneManager.SetActiveScene(scene));

            GameObject slotManagerGo = CreateGameObject("AttackSlotManager");
            slotManagerGo.AddComponent<AttackSlotManager>();

            GameObject floor = CreatePrimitive("PerfNavMeshFloor", PrimitiveType.Plane);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            BuildTestNavMesh();

            GameObject player = CreateGameObject("PerfPlayer");
            player.tag = "Player";
            player.transform.position = Vector3.zero;
            player.AddComponent<TestDamageableTarget>();

            var enemies = new List<EnemyAI>();
            const int enemyCount = 16;
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = i * Mathf.PI * 2f / enemyCount;
                GameObject enemyGo = CreateGameObject($"PerfEnemy_{i:00}");
                enemyGo.transform.position = new Vector3(Mathf.Cos(angle) * 18f, 0f, Mathf.Sin(angle) * 18f);
                NavMeshAgent agent = enemyGo.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.speed = 5f;
                agent.stoppingDistance = 0.5f;
                agent.updateRotation = false;
                agent.Warp(enemyGo.transform.position);
                EnemyAI enemy = enemyGo.AddComponent<EnemyAI>();
                enemy.DebugForceTargetForTests(player.transform, i);
                enemies.Add(enemy);
            }

            yield return null;
            yield return null;

            using ProfilerRecorder mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 256);
            using ProfilerRecorder gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 256);
            using ProfilerRecorder drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 256);
            using ProfilerRecorder batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 256);
            using ProfilerRecorder setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 256);

            int frames = 0;
            float endTime = Time.unscaledTime + 60f;
            while (Time.unscaledTime < endTime)
            {
                float t = Time.unscaledTime;
                player.transform.position = new Vector3(Mathf.Sin(t * 0.35f), 0f, Mathf.Cos(t * 0.25f));
                player.transform.rotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.2f) * 45f, 0f);
                frames++;
                yield return null;
            }

            int brainTicks = 0;
            int intentDestinationRequests = 0;
            int agentDestinationRequests = 0;
            foreach (EnemyAI enemy in enemies)
            {
                EnemyAI.TestSnapshot snapshot = enemy.CaptureTestSnapshot();
                brainTicks += snapshot.brainTickCount;
                intentDestinationRequests += snapshot.intentDestinationRequestCount;
                agentDestinationRequests += snapshot.agentDestinationRequestCount;
            }

            string report = BuildReport(
                frames,
                enemyCount,
                brainTicks,
                intentDestinationRequests,
                agentDestinationRequests,
                mainThreadTime,
                gcAllocated,
                drawCalls,
                batches,
                setPassCalls);

            WriteReport(report);
            TestContext.Out.WriteLine(report);

            Assert.Greater(frames, 0, "Baseline should sample runtime frames.");
            Assert.Greater(brainTicks, 0, "Baseline should exercise enemy runtime brains.");
            Assert.Greater(intentDestinationRequests, 0, "Baseline should exercise chase destination intent updates.");
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }

        private GameObject CreatePrimitive(string name, PrimitiveType primitiveType)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            objectsToDestroy.Add(go);
            return go;
        }

        private void BuildTestNavMesh()
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            var bounds = new Bounds(Vector3.zero, new Vector3(90f, 20f, 90f));
            NavMeshBuilder.CollectSources(
                bounds,
                ~0,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                markups,
                sources);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(data, "Baseline should create a runtime NavMesh for agent path requests.");
            navMeshInstances.Add(NavMesh.AddNavMeshData(data));
        }

        private static string BuildReport(
            int frames,
            int enemyCount,
            int brainTicks,
            int intentDestinationRequests,
            int agentDestinationRequests,
            ProfilerRecorder mainThreadTime,
            ProfilerRecorder gcAllocated,
            ProfilerRecorder drawCalls,
            ProfilerRecorder batches,
            ProfilerRecorder setPassCalls)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Codex Performance Baseline Report");
            sb.AppendLine("DurationSeconds: 60");
            sb.AppendLine($"Frames: {frames}");
            sb.AppendLine($"Enemies: {enemyCount}");
            sb.AppendLine($"EnemyBrainTicks: {brainTicks}");
            sb.AppendLine($"EnemyIntentDestinationRequests: {intentDestinationRequests}");
            sb.AppendLine($"EnemyAgentSetDestinationRequests: {agentDestinationRequests}");
            sb.AppendLine($"MainThreadMs.Avg: {Average(mainThreadTime) / 1000000.0:F3}");
            sb.AppendLine($"GcAllocatedBytes.Avg: {Average(gcAllocated):F0}");
            sb.AppendLine($"DrawCalls.Avg: {Average(drawCalls):F1}");
            sb.AppendLine($"Batches.Avg: {Average(batches):F1}");
            sb.AppendLine($"SetPassCalls.Avg: {Average(setPassCalls):F1}");
            return sb.ToString();
        }

        private static double Average(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0)
                return 0.0;

            double total = 0.0;
            for (int i = 0; i < recorder.Count; i++)
                total += recorder.GetSample(i).Value;

            return total / recorder.Count;
        }

        private static void WriteReport(string report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string logDir = Path.Combine(projectRoot, "Logs", "Performance");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "CodexPerformanceBaselineReport.txt"), report);
        }
    }
}
