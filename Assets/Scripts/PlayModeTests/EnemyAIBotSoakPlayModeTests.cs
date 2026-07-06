using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class EnemyAIBotSoakPlayModeTests
    {
        private readonly List<Object> objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (objectsToDestroy[i] != null)
                    Object.Destroy(objectsToDestroy[i]);
            }

            objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator PandemoniumBotSoak_HordeCoordinatesPressureWithoutDogpilingPlayerCenter()
        {
            yield return LoadEmptySoakScene();

            GameObject difficultyGo = CreateGameObject("DifficultyManager");
            DifficultyManager difficulty = difficultyGo.AddComponent<DifficultyManager>();
            ForceDifficulty(difficulty, DifficultyLevel.Pandemonium);
            Assert.AreEqual(6, difficulty.GetCurrentStats().maxConcurrentAttackers,
                "Soak setup should run against Pandemonium attacker cap.");

            GameObject profilerGo = CreateGameObject("PlayerProfiler");
            profilerGo.AddComponent<PlayerProfiler>();

            GameObject slotManagerGo = CreateGameObject("AttackSlotManager");
            AttackSlotManager slotManager = slotManagerGo.AddComponent<AttackSlotManager>();

            GameObject player = CreateGameObject("BotPlayer");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
            player.AddComponent<NetworkObject>();
            player.AddComponent<PlayerHealth>();

            GameObject influenceGo = CreateGameObject("InfluenceMapManager");
            InfluenceMapManager influenceMap = influenceGo.AddComponent<InfluenceMapManager>();

            yield return null;
            yield return null;

            Assert.IsFalse(influenceMap.IsFairSpawnPoint(new Vector3(0f, 0f, 10f)),
                "Fair spawn policy must reject near player spawns during soak.");
            Assert.IsFalse(influenceMap.IsFairSpawnPoint(new Vector3(0f, 0f, 35f)),
                "Fair spawn policy must reject visible pressure spawns inside the near view cone.");
            Assert.IsTrue(influenceMap.IsFairSpawnPoint(new Vector3(0f, 0f, -35f)),
                "Fair spawn policy should still allow far off-screen pressure.");

            var enemies = new List<EnemyAI>();
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 14f, 0f, Mathf.Sin(angle) * 14f);
                GameObject enemyGo = CreateGameObject($"SoakEnemy_{i:00}");
                enemyGo.transform.position = position;
                enemies.Add(enemyGo.AddComponent<EnemyAI>());
            }

            float endTime = Time.time + 3f;
            while (Time.time < endTime)
            {
                player.transform.position = new Vector3(Mathf.Sin(Time.time * 0.8f) * 0.75f, 0f, 0f);
                yield return null;
            }

            int brainTicking = 0;
            int acquiredTargets = 0;
            int attackerCount = 0;
            int coordinatedAssignments = 0;
            int offCenterDestinations = 0;
            int flankOrReservePressure = 0;

            foreach (EnemyAI enemy in enemies)
            {
                EnemyAI.TestSnapshot snapshot = enemy.CaptureTestSnapshot();

                if (snapshot.brainTickCount > 0)
                    brainTicking++;

                if (snapshot.currentTarget == player.transform)
                    acquiredTargets++;

                if (slotManager.HasAssignment(enemy))
                    coordinatedAssignments++;

                if (slotManager.IsAttacker(enemy))
                    attackerCount++;
                else if (slotManager.HasAssignment(enemy))
                    flankOrReservePressure++;

                if (snapshot.lastDestinationRequestTime > 0f &&
                    Vector3.Distance(snapshot.lastDesiredDestination, player.transform.position) > 0.75f)
                {
                    offCenterDestinations++;
                }
            }

            Assert.AreEqual(enemies.Count, brainTicking,
                "Every spawned enemy should keep running its runtime brain during the soak.");
            Assert.AreEqual(enemies.Count, acquiredTargets,
                "Every spawned enemy should acquire the bot player as pressure target.");
            Assert.AreEqual(enemies.Count, coordinatedAssignments,
                "Every spawned enemy should ask AttackSlotManager for a coordinated assignment.");
            Assert.AreEqual(6, attackerCount,
                "Pandemonium should use exactly the configured 6 active attackers before overflowing to pressure modes.");
            Assert.GreaterOrEqual(flankOrReservePressure, 4,
                "Overflow enemies should become flank/pressure/reserve instead of piling into attacker slots.");
            Assert.AreEqual(enemies.Count, offCenterDestinations,
                "All enemies should request non-center pressure destinations instead of dogpiling the player center.");
            Assert.AreEqual(enemies.Count, slotManager.GetZombiesTargeting(0),
                "All soak enemies should coordinate against the same profiled player without losing assignments.");
        }

        [UnityTest]
        public IEnumerator ZombieFactoryBotSoak_UnfairPreferredSpawnFallsBackToFairPressurePoint()
        {
            yield return LoadEmptySoakScene();

            GameObject profilerGo = CreateGameObject("PlayerProfiler");
            profilerGo.AddComponent<PlayerProfiler>();

            GameObject player = CreateGameObject("BotPlayer");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
            player.AddComponent<NetworkObject>();
            player.AddComponent<PlayerHealth>();

            GameObject influenceGo = CreateGameObject("InfluenceMapManager");
            InfluenceMapManager influenceMap = influenceGo.AddComponent<InfluenceMapManager>();

            GameObject registryGo = CreateGameObject("ZombieRegistry");
            ZombieRegistry registry = registryGo.AddComponent<ZombieRegistry>();

            GameObject factoryGo = CreateGameObject("ZombieFactory");
            ZombieFactory factory = factoryGo.AddComponent<ZombieFactory>();

            GameObject prefab = CreateGameObject("RuntimeZombiePrefab");
            prefab.AddComponent<EnemyAI>();
            registry.AddZombieType(new ZombieData { displayName = "Runtime Test Zombie", prefab = prefab, spawnWeight = 1 });

            Transform fairSpawnPoint = CreateGameObject("FairPressureSpawn").transform;
            fairSpawnPoint.position = new Vector3(38f, 0f, -38f);
            AddRegistrySpawnPoint(registry, fairSpawnPoint);

            yield return null;
            yield return null;

            Vector3 unfairPreferredPoint = new Vector3(0f, 0f, 8f);
            Assert.IsFalse(influenceMap.IsFairSpawnPoint(unfairPreferredPoint),
                "Soak setup should use an unfair preferred point to prove the factory refuses it.");

            GameObject spawned = factory.SpawnZombieAtFairPressurePosition(unfairPreferredPoint, Quaternion.identity);

            Assert.NotNull(spawned, "Factory should still spawn pressure when a fair fallback point exists.");
            Assert.AreNotEqual(unfairPreferredPoint, spawned.transform.position,
                "Factory must not use the raw unfair preferred point.");
            Assert.AreNotEqual(Vector3.zero, spawned.transform.position,
                "Factory must not fall back to zero while profiled players exist.");
            Assert.IsTrue(influenceMap.IsFairSpawnPoint(spawned.transform.position),
                "Factory fallback spawn must satisfy the runtime fair spawn validator.");
            Assert.Greater(Vector3.Distance(player.transform.position, spawned.transform.position), 28f,
                "Factory fallback spawn should remain outside the minimum player distance.");
        }

        [UnityTest]
        [Category("LongSoak")]
        [Explicit("Long soak is opt-in/nightly; run explicitly when validating five-minute horde behavior.")]
        [Timeout(390000)]
        public IEnumerator PandemoniumBotSoak_FiveMinutes_ReportsSpawnSlotsFlankRubberAndDogpileMetrics()
        {
            yield return LoadEmptySoakScene();

            const int enemyCount = 14;
            const float soakSeconds = 300f;
            const float sampleInterval = 1f;

            GameObject difficultyGo = CreateGameObject("DifficultyManager");
            DifficultyManager difficulty = difficultyGo.AddComponent<DifficultyManager>();
            ForceDifficulty(difficulty, DifficultyLevel.Pandemonium);

            GameObject profilerGo = CreateGameObject("PlayerProfiler");
            profilerGo.AddComponent<PlayerProfiler>();

            GameObject slotManagerGo = CreateGameObject("AttackSlotManager");
            AttackSlotManager slotManager = slotManagerGo.AddComponent<AttackSlotManager>();

            GameObject rubberGo = CreateGameObject("RubberBandingSystem");
            RubberBandingSystem rubberBanding = rubberGo.AddComponent<RubberBandingSystem>();

            GameObject player = CreateGameObject("FiveMinuteBotPlayer");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
            player.AddComponent<NetworkObject>();
            player.AddComponent<PlayerHealth>();

            GameObject influenceGo = CreateGameObject("InfluenceMapManager");
            InfluenceMapManager influenceMap = influenceGo.AddComponent<InfluenceMapManager>();

            var enemies = new List<EnemyAI>();
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = i * Mathf.PI * 2f / enemyCount;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 16f, 0f, Mathf.Sin(angle) * 16f);
                GameObject enemyGo = CreateGameObject($"FiveMinuteSoakEnemy_{i:00}");
                enemyGo.transform.position = position;
                enemies.Add(enemyGo.AddComponent<EnemyAI>());
            }

            yield return WarmupForSeconds(2f, player);

            var modeObservations = new Dictionary<EnemyAssignmentMode, int>
            {
                [EnemyAssignmentMode.Attacker] = 0,
                [EnemyAssignmentMode.Flanker] = 0,
                [EnemyAssignmentMode.Pressure] = 0,
                [EnemyAssignmentMode.Reserve] = 0
            };

            int samples = 0;
            int minAttackerCount = int.MaxValue;
            int maxAttackerCount = 0;
            int maxLostAssignments = 0;
            int maxLostTargets = 0;
            int maxCenterDogpiles = 0;
            int maxOffCenterDestinations = 0;
            int fairNearRejectSamples = 0;
            int fairVisibleRejectSamples = 0;
            int fairOffscreenAcceptSamples = 0;

            float nextSampleTime = Time.unscaledTime;
            float endTime = Time.unscaledTime + soakSeconds;

            while (Time.unscaledTime < endTime)
            {
                DriveBotPlayer(player);

                if (Time.unscaledTime >= nextSampleTime)
                {
                    samples++;
                    nextSampleTime += sampleInterval;

                    int attackerCount = 0;
                    int lostAssignments = 0;
                    int lostTargets = 0;
                    int centerDogpiles = 0;
                    int offCenterDestinations = 0;

                    foreach (EnemyAI enemy in enemies)
                    {
                        EnemyAI.TestSnapshot snapshot = enemy.CaptureTestSnapshot();

                        if (snapshot.currentTarget != player.transform)
                            lostTargets++;

                        if (slotManager.TryGetAssignmentMode(enemy, out EnemyAssignmentMode mode))
                        {
                            modeObservations[mode]++;
                            if (mode == EnemyAssignmentMode.Attacker)
                                attackerCount++;
                        }
                        else
                        {
                            lostAssignments++;
                        }

                        if (snapshot.lastDestinationRequestTime > 0f)
                        {
                            float distanceToPlayerCenter = Vector3.Distance(
                                snapshot.lastDesiredDestination,
                                player.transform.position);

                            if (distanceToPlayerCenter <= 0.75f)
                                centerDogpiles++;
                            else
                                offCenterDestinations++;
                        }
                    }

                    if (!influenceMap.IsFairSpawnPoint(player.transform.position + player.transform.forward * 10f))
                        fairNearRejectSamples++;

                    if (!influenceMap.IsFairSpawnPoint(player.transform.position + player.transform.forward * 35f))
                        fairVisibleRejectSamples++;

                    if (influenceMap.IsFairSpawnPoint(player.transform.position - player.transform.forward * 35f))
                        fairOffscreenAcceptSamples++;

                    minAttackerCount = Mathf.Min(minAttackerCount, attackerCount);
                    maxAttackerCount = Mathf.Max(maxAttackerCount, attackerCount);
                    maxLostAssignments = Mathf.Max(maxLostAssignments, lostAssignments);
                    maxLostTargets = Mathf.Max(maxLostTargets, lostTargets);
                    maxCenterDogpiles = Mathf.Max(maxCenterDogpiles, centerDogpiles);
                    maxOffCenterDestinations = Mathf.Max(maxOffCenterDestinations, offCenterDestinations);
                }

                yield return null;
            }

            int brainTicking = 0;
            foreach (EnemyAI enemy in enemies)
            {
                if (enemy.CaptureTestSnapshot().brainTickCount > 0)
                    brainTicking++;
            }

            string report = BuildFiveMinuteSoakReport(
                samples,
                enemyCount,
                brainTicking,
                minAttackerCount,
                maxAttackerCount,
                maxLostAssignments,
                maxLostTargets,
                maxCenterDogpiles,
                maxOffCenterDestinations,
                modeObservations,
                fairNearRejectSamples,
                fairVisibleRejectSamples,
                fairOffscreenAcceptSamples,
                rubberBanding.CaptureTestSnapshot().trackedZombieCount);

            WriteFiveMinuteSoakReport(report);
            TestContext.Out.WriteLine(report);

            Assert.GreaterOrEqual(samples, 295, "Five-minute soak should collect roughly one sample per second.");
            Assert.AreEqual(enemyCount, brainTicking, "Every enemy should keep running its brain for the long soak.");
            Assert.AreEqual(6, minAttackerCount, "Pandemonium should not dip below its configured attacker pressure after warmup.");
            Assert.AreEqual(6, maxAttackerCount, "Pandemonium should cap active dogpile attackers at exactly 6.");
            Assert.AreEqual(0, maxLostAssignments, "No enemy should lose AttackSlotManager assignment during the long soak.");
            Assert.AreEqual(0, maxLostTargets, "No enemy should lose the bot player target during the long soak.");
            Assert.AreEqual(0, maxCenterDogpiles, "No enemy should request the player center as its destination.");
            Assert.AreEqual(enemyCount, maxOffCenterDestinations, "Every enemy should keep requesting non-center pressure destinations.");
            Assert.Greater(modeObservations[EnemyAssignmentMode.Flanker], 0, "Long soak should exercise flanker assignments.");
            Assert.Greater(modeObservations[EnemyAssignmentMode.Pressure], 0, "Long soak should exercise pressure assignments.");
            Assert.Greater(modeObservations[EnemyAssignmentMode.Reserve], 0, "Long soak should exercise reserve assignments.");
            Assert.AreEqual(samples, fairNearRejectSamples, "Fair spawn policy should reject close spawns throughout the soak.");
            Assert.AreEqual(samples, fairVisibleRejectSamples, "Fair spawn policy should reject visible forward spawns throughout the soak.");
            Assert.AreEqual(samples, fairOffscreenAcceptSamples, "Fair spawn policy should keep accepting off-screen pressure throughout the soak.");
            Assert.AreEqual(enemyCount, rubberBanding.CaptureTestSnapshot().trackedZombieCount, "RubberBandingSystem should track every runtime enemy.");
        }

        private IEnumerator LoadEmptySoakScene()
        {
            Scene scene = SceneManager.CreateScene($"EnemyAIBotSoakScene_{Time.frameCount}");
            Assert.IsTrue(SceneManager.SetActiveScene(scene));
            yield return null;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }

        private static void ForceDifficulty(DifficultyManager difficulty, DifficultyLevel level)
        {
            typeof(DifficultyManager)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(difficulty, null);

            typeof(NetworkVariable<DifficultyLevel>)
                .GetField("m_InternalValue", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(difficulty.CurrentDifficulty, level);
        }

        private static void AddRegistrySpawnPoint(ZombieRegistry registry, Transform spawnPoint)
        {
            var spawnPoints = (List<Transform>)typeof(ZombieRegistry)
                .GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(registry);

            spawnPoints.Add(spawnPoint);
        }

        private static IEnumerator WarmupForSeconds(float seconds, GameObject player)
        {
            float endTime = Time.unscaledTime + seconds;
            while (Time.unscaledTime < endTime)
            {
                DriveBotPlayer(player);
                yield return null;
            }
        }

        private static void DriveBotPlayer(GameObject player)
        {
            float t = Time.unscaledTime;
            player.transform.position = new Vector3(
                Mathf.Sin(t * 0.45f) * 1.25f,
                0f,
                Mathf.Cos(t * 0.35f) * 1.25f);
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.25f) * 50f, 0f);
        }

        private static string BuildFiveMinuteSoakReport(
            int samples,
            int enemyCount,
            int brainTicking,
            int minAttackerCount,
            int maxAttackerCount,
            int maxLostAssignments,
            int maxLostTargets,
            int maxCenterDogpiles,
            int maxOffCenterDestinations,
            Dictionary<EnemyAssignmentMode, int> modeObservations,
            int fairNearRejectSamples,
            int fairVisibleRejectSamples,
            int fairOffscreenAcceptSamples,
            int rubberTrackedCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Codex AI Bot Soak Report");
            sb.AppendLine($"DurationSeconds: 300");
            sb.AppendLine($"Samples: {samples}");
            sb.AppendLine($"Enemies: {enemyCount}");
            sb.AppendLine($"BrainTickingEnemies: {brainTicking}");
            sb.AppendLine($"AttackerCountMinMax: {minAttackerCount}-{maxAttackerCount}");
            sb.AppendLine($"ModeObservations.Attacker: {modeObservations[EnemyAssignmentMode.Attacker]}");
            sb.AppendLine($"ModeObservations.Flanker: {modeObservations[EnemyAssignmentMode.Flanker]}");
            sb.AppendLine($"ModeObservations.Pressure: {modeObservations[EnemyAssignmentMode.Pressure]}");
            sb.AppendLine($"ModeObservations.Reserve: {modeObservations[EnemyAssignmentMode.Reserve]}");
            sb.AppendLine($"MaxLostAssignments: {maxLostAssignments}");
            sb.AppendLine($"MaxLostTargets: {maxLostTargets}");
            sb.AppendLine($"MaxCenterDogpiles: {maxCenterDogpiles}");
            sb.AppendLine($"MaxOffCenterDestinations: {maxOffCenterDestinations}");
            sb.AppendLine($"FairNearRejectSamples: {fairNearRejectSamples}");
            sb.AppendLine($"FairVisibleRejectSamples: {fairVisibleRejectSamples}");
            sb.AppendLine($"FairOffscreenAcceptSamples: {fairOffscreenAcceptSamples}");
            sb.AppendLine($"RubberTrackedEnemies: {rubberTrackedCount}");
            return sb.ToString();
        }

        private static void WriteFiveMinuteSoakReport(string report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string logDir = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, "CodexAIBotSoakFiveMinuteReport.txt"), report);
        }
    }
}
