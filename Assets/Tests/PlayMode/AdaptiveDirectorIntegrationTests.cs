#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class DirectorThreatFixtureEnemy : EnemyAI
    {
        protected override void Update() { }
    }
    public sealed class DirectorSpecialFixtureEnemy : SpecialInfectedBase
    {
        protected override void Update() { }
        public override void UseAbility() { }
    }
    /// <summary>Isolated server/bridge integration with the real stable registry.
    /// Fixture composition avoids contacting UGS; two-Player login is a separate gate.</summary>
    public sealed class AdaptiveDirectorIntegrationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        NetworkManager host;
        NetworkGameManager sessions;
        PlayerSessionRegistry registry;
        PlayerHealth actor;
        AIDirector director;
        Scene scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(NetworkGameManager.Instance, Is.Null);
            scene = SceneManager.CreateScene("AdaptiveIntegration");
            SceneManager.SetActiveScene(scene);
            var serverRoot = new GameObject("Adaptive server");
            host = serverRoot.AddComponent<NetworkManager>();
            var transport = serverRoot.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 17843, "127.0.0.1");
            host.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = false };
            host.SetSingleton();
            Assert.That(host.StartHost(), Is.True);
            var sessionRoot = new GameObject("Inactive service composition");
            sessionRoot.SetActive(false);
            sessions = sessionRoot.AddComponent<NetworkGameManager>();
            registry = new PlayerSessionRegistry(4);
            Set(sessions, "playerRegistry", registry);
            typeof(NetworkGameManager).GetProperty("AdaptiveMetrics").SetValue(sessions, new AdaptiveDifficultyMetricsCollector());
            typeof(NetworkGameManager).GetProperty("Telemetry").SetValue(sessions, new ServerTelemetryAggregator());
            typeof(NetworkGameManager).GetProperty("Instance").SetValue(null, sessions);
            Assert.That(registry.TryRegisterNew("integration-host", host.LocalClientId, 0, out var record, out _, out _), Is.True);
            var player = new GameObject("Registered actor", typeof(NetworkObject));
            actor = player.AddComponent<PlayerHealth>();
            player.AddComponent<PlayerCombatTelemetry>();
            actor.PrepareInitialSpawn(record.PlayerId);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(host.LocalClientId);
            var root = new GameObject("Adaptive Director", typeof(NetworkObject));
            director = root.AddComponent<AIDirector>();
            Set(director, "enableAdaptiveDirector", true);
            Set(director, "adaptiveObserveOnly", false);
            director.enabled = false;
            root.GetComponent<NetworkObject>().Spawn();
            yield return null;
            TickDirector();
            Assert.That(sessions.TryGetRegisteredActor(actor.StablePlayerId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(actor));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            typeof(NetworkGameManager).GetProperty("Instance").SetValue(null, null);
            if (host != null) host.Shutdown();
            yield return null;
            if (host != null) Object.Destroy(host.gameObject);
            if (sessions != null) Object.Destroy(sessions.gameObject);
            if (director != null) Object.Destroy(director.gameObject);
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageFlowsThroughRegistryAndSeal_AndEvaluationIsPublishedBeforeReset()
        {
            int evaluations = 0;
            director.EvaluationCompleted += r => {
                evaluations++;
                Assert.That(sessions.AdaptiveMetrics.PlayerCount, Is.EqualTo(1), "Publish while source evidence still exists.");
                Assert.That(r.Samples[0].CumulativeDamageMaxHealth, Is.EqualTo(.4f).Within(.001));
            };
            actor.TakeDamage(40);
            TickDirector();
            Assert.That(director.AdaptivePhase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(director.AdaptiveSpawningBlocked, Is.True);
            int boundary = sessions.AdaptiveMetrics.PendingBoundaryTick.Value;
            while (host.ServerTime.Tick <= boundary) yield return null;
            Seal();
            Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(director.LastEvaluation.BoundaryTick, Is.EqualTo(boundary));
            Assert.That(director.LastEvaluation.Samples[0].StablePlayerId, Is.EqualTo(actor.StablePlayerId.Value));
            Seal(); TickDirector();
            Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(actor.CurrentHealth, Is.EqualTo(60), "Protection never heals.");
        }

        [UnityTest]
        public IEnumerator OverkillRecordsActualHP_AndWeaponOnlySnapshotDoesNotKillActor()
        {
            sessions.Telemetry.RecordWeapon(actor.StablePlayerId, host.ServerTime.Tick, false, 10, 30);
            int tick = host.ServerTime.Tick;
            while (host.ServerTime.Tick <= tick) yield return null;
            Seal();
            Assert.That(actor.CurrentHealth, Is.EqualTo(100));
            actor.TakeDamage(30);
            actor.TakeDamage(500);
            tick = host.ServerTime.Tick;
            while (host.ServerTime.Tick <= tick) yield return null;
            Seal();
            Assert.That(sessions.AdaptiveMetrics.BuildSamples()[0].CumulativeDamageMaxHealth, Is.EqualTo(1).Within(.001));
        }

        [UnityTest]
        public IEnumerator WrongActorWithSameStableIdCannotProduceSafetyTelemetry()
        {
            var impostor = new GameObject("Unregistered actor", typeof(NetworkObject)).AddComponent<PlayerHealth>();
            impostor.PrepareInitialSpawn(actor.StablePlayerId);
            impostor.NetworkObject.Spawn();
            sessions.NotifyDirectorDamage(impostor, 90);
            TickDirector();
            Assert.That(director.Intensity, Is.Zero);
            Object.Destroy(impostor.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EndAndRetryCancelPendingBoundaryAndKeepCommittedMultiplier()
        {
            actor.TakeDamage(40); TickDirector();
            Assert.That(sessions.AdaptiveMetrics.PendingBoundaryTick.HasValue, Is.True);
            director.EndCampaignEncounter();
            Assert.That(sessions.AdaptiveMetrics.PendingBoundaryTick, Is.Null);
            Assert.That(director.AdaptivePhase, Is.EqualTo(DirectorPhase.Relax));
            director.BeginAdaptiveAttempt();
            TickDirector();
            Assert.That(director.AdaptivePhase, Is.EqualTo(DirectorPhase.Calm));
            Assert.That(director.AdaptiveDifficultyMultiplier, Is.EqualTo(1));
            Assert.That(director.LastEvaluation, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ObserveOnlyDoesNotWriteLegacyPhaseMultiplierTimerOrRNG()
        {
            Set(director, "adaptiveObserveOnly", true);
            Set(director, "spawnTimer", .37f);
            Set(director, "localPhase", GamePhase.BUILD);
            var phase = director.CurrentPhase;
            UnityEngine.Random.InitState(818);
            var seed = UnityEngine.Random.state;
            actor.TakeDamage(40);
            Invoke(director, "SampleActorPressure", host.ServerTime.Time, true);
            Invoke(director, "UpdateAdaptiveDirector");
            Assert.That(director.CurrentPhase, Is.EqualTo(phase));
            Assert.That(director.AdaptiveDifficultyMultiplier, Is.EqualTo(1));
            Assert.That((float)typeof(AIDirector).GetField("spawnTimer", Private).GetValue(director), Is.EqualTo(.37f));
            float observed = UnityEngine.Random.value;
            UnityEngine.Random.state = seed;
            Assert.That(UnityEngine.Random.value, Is.EqualTo(observed));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ObserveOnlyPreservesTheAppliedPacingAndActualSpawnSequence()
        {
            var registryRoot = new GameObject("Sequence zombie registry");
            var zombies = registryRoot.AddComponent<ZombieRegistry>();
            var point = new GameObject("Sequence spawn point");
            point.transform.position = Vector3.forward * 25;
            Set(zombies, "spawnPoints", new List<Transform> { point.transform });
            var template = new GameObject("Sequence enemy", typeof(NetworkObject), typeof(EnemyHealth));
            zombies.AddZombieType(new ZombieData { prefab = template });
            var factoryRoot = new GameObject("Sequence factory");
            var factory = factoryRoot.AddComponent<ZombieFactory>();
            Set(factory, "usePooling", false);
            Set(director, "useSmartSpawning", false);
            Set(director, "enableSpecialInfected", false);
            Set(director, "baseSpawnInterval", .04f);
            Set(director, "spawnIntervalMin", .001f);
            Set(director, "buildDuration", .3f);
            Set(director, "peakDuration", .3f);
            Set(director, "relaxDuration", .3f);
            Set(director, "maxZombiesAlive", 1000);
            var spawned = new List<GameObject>();
            var trace = new List<string>();
            factory.OnZombieSpawned += enemy => {
                spawned.Add(enemy);
                trace.Add($"spawn:{director.CurrentPhase}:{enemy.transform.position:R}:{enemy.GetComponent<EnemyHealth>().MaxHealth:R}");
            };
            var originalRandom = UnityEngine.Random.state;
            string[] baseline = null;
            float baselineRandom = 0;
            try
            {
                // Both replays run within the same frame, giving every applied
                // Update the same delta and server clock, with the same seed.
                for (int pass = 0; pass < 2; pass++)
                {
                    Set(director, "enableAdaptiveDirector", pass == 1);
                    Set(director, "adaptiveObserveOnly", true);
                    director.EndCampaignEncounter();
                    Invoke(director, "TransitionTo", GamePhase.BUILD);
                    Invoke(director, "SetZombiesAlive", 0);
                    UnityEngine.Random.InitState(818);
                    trace.Clear();
                    for (int step = 0; step < 300; step++)
                    {
                        if (step == 60) director.RecordDirectorDamage(actor.StablePlayerId.Value, host.ServerTime.Time, 90, 100);
                        TickDirector();
                        trace.Add($"step:{director.CurrentPhase}:{director.AdaptiveDifficultyMultiplier:R}:{director.Intensity:R}:"
                            + $"{typeof(AIDirector).GetField("spawnTimer", Private).GetValue(director)}:"
                            + $"{typeof(AIDirector).GetField("phaseTimer", Private).GetValue(director)}");
                    }
                    float nextRandom = UnityEngine.Random.value;
                    Assert.That(spawned.Count, Is.GreaterThan(0), "An empty action sequence cannot prove spawn equivalence.");
                    if (pass == 0) { baseline = trace.ToArray(); baselineRandom = nextRandom; }
                    else
                    {
                        Assert.That(trace, Is.EqualTo(baseline), "Shadow proposals must preserve every applied step and actual spawn.");
                        Assert.That(nextRandom, Is.EqualTo(baselineRandom), "The observer must not consume gameplay RNG.");
                    }
                    foreach (var enemy in spawned) if (enemy != null) Object.DestroyImmediate(enemy);
                    spawned.Clear();
                }
            }
            finally
            {
                UnityEngine.Random.state = originalRandom;
                foreach (var enemy in spawned) if (enemy != null) Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(factoryRoot);
                Object.DestroyImmediate(point);
                Object.DestroyImmediate(registryRoot);
            }
            yield return null;
        }

        [Serializable] private sealed class TracePlayer { public float hp, maxHp; public string lifeState; }
        [Serializable] private sealed class TraceRow
        {
            public int schemaVersion = 2, serverTick, livingEnemyCount, sampleCount, eligibleCount;
            public string recordType = "sample", runState, decisionReason, adaptivePhase;
            public double serverTime;
            public float pressure, multiplier, protectionLatencyMs;
            public bool collectorPresent = true;
            public TracePlayer[] players;
        }
        [UnityTest]
        public IEnumerator RuntimeTrace_DistressCommitsOnce_ExistingEnemyStatsStayUnchanged_ThenDeathStopsDirector()
        {
            var enemyRoot = new GameObject("Controlled threatening enemy", typeof(NetworkObject));
            var enemyHealth = enemyRoot.AddComponent<EnemyHealth>();
            var ai = enemyRoot.AddComponent<DirectorThreatFixtureEnemy>();
            ai.enabled = false;
            enemyRoot.transform.position = actor.transform.position + Vector3.forward * 2;
            enemyRoot.GetComponent<NetworkObject>().Spawn();
            Set(ai, "player", actor.transform);
            var state = typeof(EnemyAI).GetField("currentState", Private);
            state.SetValue(ai, Enum.Parse(state.FieldType, "Attack"));
            // Disabled brain is a controlled threat-state fixture, so keep its roster
            // registration explicit. It never supplies damage directly to metrics.
            EnemyAI.PressureRoster.Add(ai);
            ai.enabled = true;
            // Prevent automatic Update while retaining the active threat observation.
            var rootHealth = enemyHealth.MaxHealth;
            float originalDamage = ai.AttackDamage;
            Directory.CreateDirectory("Documents/AI-Director-Defense/Captures");
            string path = "Documents/AI-Director-Defense/Captures/registered-runtime-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl";
            int evaluations = 0;
            director.EvaluationCompleted += _ => evaluations++;
            using (var writer = new StreamWriter(path))
            {
                double start = host.ServerTime.Time;
                double nextLog = start;
                bool burst = false, continuedDamage = false, lethal = false;
                while (host.ServerTime.Time - start < 8.5)
                {
                    double elapsed = host.ServerTime.Time - start;
                    // This fixture drives the same attack/damage entry point as gameplay;
                    // AI path/state is controlled so the test does not require a NavMesh.
                    Set(ai, "player", actor.transform);
                    state.SetValue(ai, Enum.Parse(state.FieldType, "Attack"));
                    if (elapsed >= 6 && !burst) { actor.TakeDamage(40); burst = true; }
                    if (elapsed >= 6.5 && !continuedDamage) { actor.TakeDamage(5); continuedDamage = true; }
                    if (elapsed >= 8 && !lethal) { actor.TakeDamage(1000); lethal = true; }
                    TickDirector(); Seal();
                    // Advance pacing at the graphics frame rate, while capture stays
                    // at 10 Hz. Waiting .1 s here incorrectly slows Time.deltaTime's
                    // pacing clock relative to the server clock.
                    if (host.ServerTime.Time < nextLog) { yield return null; continue; }
                    nextLog = host.ServerTime.Time + .1;
                    var samples = sessions.AdaptiveMetrics.BuildSamples();
                    int eligible = 0;
                    foreach (var sample in samples) if (sample.HasEvidence(DynamicDifficultyPolicy.Default) || sample.HasDistress) eligible++;
                    writer.WriteLine(JsonUtility.ToJson(new TraceRow { serverTime = host.ServerTime.Time,
                        serverTick = host.ServerTime.Tick, pressure = director.Intensity, multiplier = director.AdaptiveDifficultyMultiplier,
                        adaptivePhase = director.AdaptivePhase.ToString(), runState = director.RunState.ToString(),
                        decisionReason = director.DecisionReason.ToString(), livingEnemyCount = enemyHealth.IsDead ? 0 : 1,
                        sampleCount = samples.Count, eligibleCount = eligible, protectionLatencyMs = director.ProtectionLatencyMilliseconds,
                        players = new[] { new TracePlayer { hp = actor.CurrentHealth, maxHp = actor.MaxHealth, lifeState = actor.LifeState.ToString() } } }));
                    yield return null;
                }
            }
            Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(director.AdaptiveDifficultyMultiplier, Is.EqualTo(.95f).Within(.00001), "Six seconds under threat + actual damage is low-kill distress evidence.");
            Assert.That(enemyHealth.MaxHealth, Is.EqualTo(rootHealth));
            Assert.That(ai.AttackDamage, Is.EqualTo(originalDamage));
            Assert.That(director.AdaptiveSpawningBlocked, Is.True);
            Assert.That(director.RunState, Is.EqualTo(DirectorRunState.Suspended));
            Assert.That(actor.CurrentHealth, Is.Zero);
            Object.Destroy(enemyRoot);
        }

        [UnityTest]
        public IEnumerator GameplayShotAndKillReachCollectorThroughRegisteredActor()
        {
            actor.NetworkObject.Despawn();
            yield return null;
            Assert.That(registry.TryGetByClientId(host.LocalClientId, out var record), Is.True);
            const string prefabPath = "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab";
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Vector3.up * 5, Quaternion.identity);
            player.GetComponent<PlayerMovement>().enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            actor = player.GetComponent<PlayerHealth>();
            actor.PrepareInitialSpawn(record.PlayerId);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(host.LocalClientId);
            var profiler = new GameObject("Registered gameplay profiler").AddComponent<PlayerProfiler>();
            var target = new GameObject("Gameplay kill target", typeof(NetworkObject), typeof(BoxCollider));
            target.transform.position = player.transform.position + Vector3.forward * 5;
            var health = target.AddComponent<EnemyHealth>();
            target.GetComponent<NetworkObject>().Spawn();
            health.SetMaxHealth(1);
            yield return null;
            Physics.SyncTransforms();
            var fire = player.GetComponent<WeaponFireHandler>();
            double readyDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while ((player.GetComponent<WeaponManager>().CurrentWeapon == null || fire.IsServerEquipping)
                && Time.realtimeSinceStartupAsDouble < readyDeadline) yield return null;
            Assert.That(actor.CanUseCombat, Is.True, "The registered actor must finish readiness before firing.");
            Assert.That(fire.IsServerEquipping, Is.False, "Do not mistake a valid equip lock for broken telemetry.");
            bool accepted = (bool)typeof(WeaponFireHandler).GetMethod("TryProcessFireServer", Private).Invoke(fire,
                new object[] { player.transform.position, Vector3.forward, host.ServerTime.Tick,
                    host.ServerTime.Time, (ushort)1, false, host.LocalClientId, true, true, false,
                    new WeaponStabilityContext { grounded = true }, false });
            Assert.That(accepted, Is.True, "Use the gameplay fire/damage path, not direct collector injection.");
            int tick = host.ServerTime.Tick;
            while (host.ServerTime.Tick <= tick) yield return null;
            Seal();
            var samples = sessions.AdaptiveMetrics.BuildSamples();
            Assert.That(samples.Count, Is.EqualTo(1));
            Assert.That(samples[0].ShotsFired, Is.EqualTo(1));
            Assert.That(samples[0].Kills, Is.EqualTo(1));
            Object.Destroy(profiler.gameObject); Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ThreatObservationExcludesFarAndIdle_AndPoolRegistrationIsIdempotent()
        {
            var root = new GameObject("Pressure lifecycle fixture", typeof(NetworkObject), typeof(EnemyHealth));
            var ai = root.AddComponent<DirectorThreatFixtureEnemy>();
            root.GetComponent<NetworkObject>().Spawn();
            Set(ai, "player", actor.transform);
            var state = typeof(EnemyAI).GetField("currentState", Private);
            state.SetValue(ai, Enum.Parse(state.FieldType, "Attack"));
            Assert.That(ai.TryGetPressureThreat(out var target), Is.True, "A stationary Attack needs no NavMesh path.");
            Assert.That(target, Is.EqualTo(actor.transform));
            root.transform.position = actor.transform.position + Vector3.forward * 60;
            Invoke(director, "SampleActorPressure", host.ServerTime.Time, true);
            Assert.That(director.ActorPressure[0].ThreatCount, Is.Zero, "Far threats do not contribute proximity pressure.");
            root.transform.position = actor.transform.position + Vector3.forward * 2;
            state.SetValue(ai, Enum.Parse(state.FieldType, "Idle"));
            Invoke(director, "SampleActorPressure", host.ServerTime.Time, true);
            Assert.That(director.ActorPressure[0].ThreatCount, Is.Zero);
            for (int i = 0; i < 3; i++) { root.SetActive(false); root.SetActive(true); }
            Assert.That(EnemyAI.PressureRoster.Contains(ai), Is.True);
            state.SetValue(ai, Enum.Parse(state.FieldType, "Attack"));
            Invoke(director, "SampleActorPressure", host.ServerTime.Time, true);
            Assert.That(director.ActorPressure[0].ThreatCount, Is.EqualTo(1), "Pool reuse cannot duplicate a roster entry.");
            root.SetActive(false);
            Assert.That(EnemyAI.PressureRoster.Contains(ai), Is.False);
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpecialSpawnRequiresActualPeakGate_AndProtectionClosesIt()
        {
            var registryRoot = new GameObject("Special acceptance registry");
            var specialRegistry = registryRoot.AddComponent<SpecialInfectedRegistry>();
            specialRegistry.enabled = false;
            var template = new GameObject("Special fixture template", typeof(NetworkObject), typeof(EnemyHealth));
            template.AddComponent<DirectorSpecialFixtureEnemy>();
            Set(specialRegistry, "specialTypes", new System.Collections.Generic.List<SpecialInfectedData> {
                new SpecialInfectedData { type = SpecialType.Screamer, prefab = template, allowedInSolo = true,
                    implementationState = SpecialImplementationState.Playable, cooldown = 0, spawnWeight = 1 } });
            Set(specialRegistry, "lastSpecialSpawnTime", -999f);
            int events = 0;
            specialRegistry.OnSpecialSpawned += _ => events++;
            var fsm = (DirectorStateMachine)typeof(AIDirector).GetField("adaptiveStateMachine", Private).GetValue(director);
            fsm.RequestCrescendo(host.ServerTime.Time + 30);
            var input = new DirectorInput(1, 0, 0, 999, 0, 0);
            var step = fsm.Advance(0, input, serverTime: host.ServerTime.Time);
            Set(director, "adaptiveDecision", step.Decision);
            Assert.That(specialRegistry.SpawnSpecial(Vector3.zero), Is.Null);
            step = fsm.Advance(5, input, serverTime: host.ServerTime.Time + 5);
            Set(director, "adaptiveDecision", step.Decision);
            var spawned = specialRegistry.SpawnSpecial(Vector3.zero);
            Assert.That(spawned, Is.Not.Null, "An open gate must reach an actual registry spawn event.");
            Assert.That(events, Is.EqualTo(1));
            actor.TakeDamage(40); TickDirector();
            Assert.That(director.AdaptiveSpecialGateOpen, Is.False);
            Assert.That(specialRegistry.SpawnSpecial(Vector3.zero), Is.Null);
            Assert.That(events, Is.EqualTo(1));
            Object.Destroy(spawned); Object.Destroy(template); Object.Destroy(registryRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpecialPressureReadsItsOwnTarget_AndScreamerDoesNotReportHPThreat()
        {
            var root = new GameObject("Infector pressure target", typeof(NetworkObject), typeof(EnemyHealth));
            var infector = root.AddComponent<SI_Infector>();
            Set(infector, "currentTargetTransform", actor.transform);
            Set(infector, "currentInfectorState", InfectorState.Implant);
            Assert.That(infector.TryGetPressureThreat(out var target), Is.True);
            Assert.That(target, Is.EqualTo(actor.transform));
            Set(infector, "currentInfectorState", InfectorState.Search);
            Assert.That(infector.TryGetPressureThreat(out _), Is.False);
            var screamerRoot = new GameObject("Screamer pressure", typeof(NetworkObject));
            var screamer = screamerRoot.AddComponent<SI_Screamer>();
            Assert.That(screamer.TryGetPressureThreat(out _), Is.False);
            Object.Destroy(root); Object.Destroy(screamerRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpecialScalingCapturesCommittedDDA_AndDoesNotChangeLivingEnemy()
        {
            Invoke(director, "SetAdaptiveState", DirectorPhase.Calm, .9f);
            var first = new GameObject("First special", typeof(NetworkObject), typeof(EnemyHealth));
            var brain = first.AddComponent<DirectorSpecialFixtureEnemy>();
            Invoke(brain, "ApplySpecialScaling");
            float hp = first.GetComponent<EnemyHealth>().MaxHealth;
            float damage = (float)InvokeResult(brain, "ScaleDamage", 10f);
            Assert.That(hp, Is.EqualTo(135).Within(.001), "Authored 100 HP x special 1.5 x committed DDA .9.");
            Assert.That(damage, Is.EqualTo(9).Within(.001));
            Invoke(director, "SetAdaptiveState", DirectorPhase.Calm, .95f);
            Assert.That(first.GetComponent<EnemyHealth>().MaxHealth, Is.EqualTo(hp));
            Assert.That((float)InvokeResult(brain, "ScaleDamage", 10f), Is.EqualTo(damage));
            var next = new GameObject("Next special", typeof(NetworkObject), typeof(EnemyHealth));
            var nextBrain = next.AddComponent<DirectorSpecialFixtureEnemy>();
            Invoke(nextBrain, "ApplySpecialScaling");
            Assert.That(next.GetComponent<EnemyHealth>().MaxHealth, Is.EqualTo(142.5f).Within(.001));
            Object.Destroy(first); Object.Destroy(next);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectedPursuitRetainsDistantReachableTargetWithoutChangingAmbientDetection()
        {
            var root = new GameObject("Directed pursuit", typeof(NetworkObject), typeof(EnemyHealth));
            var ai = root.AddComponent<DirectorThreatFixtureEnemy>();
            root.GetComponent<NetworkObject>().Spawn();
            root.transform.position = actor.transform.position + Vector3.forward * 80;
            float range = (float)typeof(EnemyAI).GetField("detectionRange", Private).GetValue(ai);
            ai.BeginDirectorPursuit(actor.transform);
            Assert.That(typeof(EnemyAI).GetField("player", Private).GetValue(ai), Is.EqualTo(actor.transform));
            Assert.That(typeof(EnemyAI).GetField("currentState", Private).GetValue(ai).ToString(), Is.EqualTo("Chase"));
            Assert.That((float)typeof(EnemyAI).GetField("detectionRange", Private).GetValue(ai), Is.EqualTo(range));
            Object.Destroy(root);
            yield return null;
        }

        static object InvokeResult(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);
        void TickDirector() => Invoke(director, "Update");
        void Seal() => Invoke(sessions, "LateUpdate");
        static void Set(object target, string field, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var member = type.GetField(field, Private);
                if (member == null) continue;
                member.SetValue(target, value); return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }
        static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    }
}
#endif
