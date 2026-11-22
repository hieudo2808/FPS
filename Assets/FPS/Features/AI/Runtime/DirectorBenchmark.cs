#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace FPS
{
    // Fixed population is a performance fixture; it is not a gameplay/balance playtest.
    public sealed class DirectorBenchmark : MonoBehaviour
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private string folder, role;
        private AIDirector director;
        private ProfilerRecorder directorCpu, pressureCpu, gcBytes;
        private readonly Frame[] frames = new Frame[120000];
        private int frameCount, population, expectedParty = 4;
        private bool measuring;
        private string runtimeError;
        private Transform[] actorTransforms;
        private Vector3[] fixedPositions;
        private Quaternion[] fixedRotations;
        private float warmup = 30, duration = 60;
        private struct Frame
        {
            public double time;
            public float frameMs, actorDrift;
            public long directorNs, pressureNs, gcBytes;
            public int alive, actors, threats, phase, state;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-directorBenchmark") < 0) return;
            var runner = new GameObject("Director performance fixture").AddComponent<DirectorBenchmark>();
            DontDestroyOnLoad(runner);
            runner.role = Argument(args, "-directorRole") ?? "host";
            runner.folder = Path.GetFullPath(Argument(args, "-directorRun"));
            if (int.TryParse(Argument(args, "-directorParty"), out int party)) runner.expectedParty = party;
            if (float.TryParse(Argument(args, "-benchmarkWarmup"), NumberStyles.Float, Invariant, out float w)) runner.warmup = w;
            if (float.TryParse(Argument(args, "-benchmarkDuration"), NumberStyles.Float, Invariant, out float d)) runner.duration = d;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }

        private static string Argument(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            double deadline = Time.realtimeSinceStartupAsDouble + 240;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                var campaign = CampaignMissionController.Instance;
                if (campaign != null && campaign.IsSpawned)
                {
                    campaign.RequestReady(true);
                    if (campaign.State.phase == CampaignPhase.Exploring && NetworkManager.Singleton.LocalClient.PlayerObject != null
                        && NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHealth>().CanUseCombat) break;
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
            if (CampaignMissionController.Instance == null || CampaignMissionController.Instance.State.phase != CampaignPhase.Exploring)
            { Fail("Production session did not reach Exploring within 240s."); yield break; }
            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
            // Keep network ticks/registered health active while excluding player locomotion from this fixed workload.
            var ownController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<CharacterController>();
            if (ownController != null) ownController.enabled = false;
            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                var device = InputSystem.devices[i];
                InputSystem.ResetDevice(device);
                InputSystem.DisableDevice(device);
            }
            yield return null;
            foreach (var capture in FindObjectsByType<DirectorAcceptanceHarness>()) capture.enabled = false;
            Application.logMessageReceived += TrackRuntimeError;
            File.WriteAllText(Path.Combine(folder, role + "-benchmark-ready.txt"), "ready");
            if (role != "host")
            {
                while (!File.Exists(Path.Combine(folder, "finished.txt")) && !File.Exists(Path.Combine(folder, "failed.txt")))
                {
                    if (runtimeError != null) { Fail("Runtime error: " + runtimeError); yield break; }
                    yield return new WaitForSecondsRealtime(1);
                }
                Application.Quit(); yield break;
            }
            director = AIDirector.Instance;
            if (director == null || !director.IsServer) { Fail("Authoritative Director missing."); yield break; }
            Set(director, "enableAdaptiveDirector", true);
            Set(director, "adaptiveObserveOnly", false);
            Set(director, "maxZombiesAlive", 0); // Freeze population without disabling Director/pressure execution.
            Set(director, "showDebugLogs", false);
            deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (Directory.GetFiles(folder, "*-benchmark-ready.txt").Length < expectedParty
                && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (Directory.GetFiles(folder, "*-benchmark-ready.txt").Length != expectedParty
                || NetworkManager.Singleton.ConnectedClientsList.Count != expectedParty)
            { Fail("Expected party did not become ready."); yield break; }
            actorTransforms = new Transform[expectedParty];
            fixedPositions = new Vector3[expectedParty];
            fixedRotations = new Quaternion[expectedParty];
            var authoredSpawns = (Transform[])typeof(NetworkSpawnManager).GetField("spawnPoints", Private).GetValue(NetworkSpawnManager.Instance);
            if (authoredSpawns == null || authoredSpawns.Length < expectedParty)
            { Fail("Authored player spawn poses missing."); yield break; }
            for (int i = 0; i < expectedParty; i++)
            {
                actorTransforms[i] = NetworkManager.Singleton.ConnectedClientsList[i].PlayerObject.transform;
                var controller = actorTransforms[i].GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                fixedPositions[i] = authoredSpawns[i].position;
                fixedRotations[i] = authoredSpawns[i].rotation;
            }
            File.WriteAllText(Path.Combine(folder, "machine.txt"),
                $"unity={Application.unityVersion}\nplatform={Application.platform}\ndevelopment={UnityEngine.Debug.isDebugBuild}\n"
                + $"cpu={SystemInfo.processorType}\ncores={SystemInfo.processorCount}\nramMB={SystemInfo.systemMemorySize}\n"
                + $"gpu={SystemInfo.graphicsDeviceName}\ngpuMB={SystemInfo.graphicsMemorySize}\napi={SystemInfo.graphicsDeviceType}\n"
                + $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}\nresolution={Screen.width}x{Screen.height}\n"
                + $"targetFPS={Application.targetFrameRate}\nvSync={QualitySettings.vSyncCount}\nparty={NetworkManager.Singleton.ConnectedClientsList.Count}\n"
                + $"difficulty={NetworkGameManager.Instance.SelectedDifficulty}\n"
                + "fixtureVersion=2\nexternalInput=disabled\nplayerLocomotion=disabled\nactorHorizontalDriftLimitMetres=0.05\n"
                + $"adaptive={director.AdaptiveDirectorEnabled}\nobserveOnly={director.AdaptiveObserveOnly}\nwarmup={warmup}\nduration={duration}\n");
            // Remove any initial ambient spawn before establishing the fixed workload.
            var initial = new System.Collections.Generic.List<EnemyAI>(EnemyAI.PressureRoster);
            foreach (var enemy in initial)
                if (enemy != null && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned) enemy.NetworkObject.Despawn(true);
            yield return null;
            director.ReconcileActivePopulation();
            yield return Calibrate("empty");
            if (File.Exists(Path.Combine(folder, "failed.txt"))) yield break;
            directorCpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FPS.Director.Update", 1);
            pressureCpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FPS.Director.Pressure", 1);
            gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            if (!directorCpu.Valid || !pressureCpu.Valid || !gcBytes.Valid)
            { Fail("Required profiler markers unavailable."); yield break; }
            foreach (int count in new[] { 10, 30, 70 })
            {
                director.BeginAdaptiveAttempt();
                yield return null;
                for (int i = 0; i < expectedParty; i++)
                    actorTransforms[i].GetComponent<PlayerHealth>().RelocateCampaign(fixedPositions[i], fixedRotations[i]);
                deadline = Time.realtimeSinceStartupAsDouble + 30;
                bool allReady;
                do
                {
                    yield return null;
                    allReady = director.RunState == DirectorRunState.Active && director.ActorPressure.Count == expectedParty;
                    for (int i = 0; i < expectedParty; i++) allReady &= actorTransforms[i].GetComponent<PlayerHealth>().IsInputReady;
                } while (!allReady && Time.realtimeSinceStartupAsDouble < deadline);
                if (!allReady) { Fail("Player relocation did not settle."); yield break; }
                UnityEngine.Random.InitState(20261120 + count);
                var clients = NetworkManager.Singleton.ConnectedClientsList;
                int existing = EnemyAI.PressureRoster.Count;
                for (int i = existing; i < count; i++)
                {
                    var target = clients[i % clients.Count].PlayerObject.transform;
                    float angle = i * 2.399963f;
                    Vector3 desired = target.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (4 + i % 5);
                    if (!NavMesh.SamplePosition(desired, out var hit, 15, NavMesh.AllAreas))
                    { Fail("No NavMesh for fixed population " + count); yield break; }
                    var go = ZombieFactory.Instance.SpawnZombie(hit.position, Quaternion.identity, 1, 1, 0);
                    if (go == null) { Fail("Factory rejected fixed population spawn " + i); yield break; }
                    go.GetComponent<EnemyAI>().BeginDirectorPursuit(target);
                }
                population = count;
                yield return Calibrate("population-" + count);
                if (File.Exists(Path.Combine(folder, "failed.txt"))) yield break;
                using (var setup = new StreamWriter(Path.Combine(folder, "setup-" + count + ".txt")))
                {
                    for (int i = 0; i < clients.Count; i++) setup.WriteLine($"actor={clients[i].ClientId} position={clients[i].PlayerObject.transform.position:F4} expected={fixedPositions[i]:F4}");
                    foreach (var enemy in EnemyAI.PressureRoster) setup.WriteLine($"enemy={enemy.name} position={enemy.transform.position:F4} damage={enemy.AttackDamage}");
                }
                File.WriteAllText(Path.Combine(folder, "progress.txt"), "warmup " + count);
                yield return new WaitForSecondsRealtime(warmup);
                frameCount = 0;
                measuring = true;
                File.WriteAllText(Path.Combine(folder, "progress.txt"), "measure " + count);
                yield return new WaitForSecondsRealtime(duration);
                measuring = false;
                WriteFrames();
                if (File.Exists(Path.Combine(folder, "failed.txt"))) yield break;
            }
            directorCpu.Dispose(); pressureCpu.Dispose(); gcBytes.Dispose();
            yield return MeasureProtection();
            if (runtimeError != null) { Fail("Runtime error: " + runtimeError); yield break; }
            if (!File.Exists(Path.Combine(folder, "failed.txt"))) File.WriteAllText(Path.Combine(folder, "finished.txt"), "complete");
            Application.Quit();
        }

        private void LateUpdate()
        {
            if (!measuring) return;
            if (frameCount == frames.Length) { measuring = false; Fail("Frame buffer exhausted."); return; }
            int alive = 0;
            foreach (EnemyAI enemy in EnemyAI.PressureRoster) if (enemy != null && enemy.IsAliveForDirector) alive++;
            int threats = 0;
            float drift = 0;
            for (int i = 0; i < actorTransforms.Length; i++)
            {
                Vector3 delta = actorTransforms[i].position - fixedPositions[i];
                delta.y = 0;
                drift = Mathf.Max(drift, delta.magnitude);
            }
            var actors = director.ActorPressure;
            for (int i = 0; i < actors.Count; i++) threats += actors[i].ThreatCount;
            frames[frameCount++] = new Frame { time = Time.realtimeSinceStartupAsDouble, frameMs = Time.unscaledDeltaTime * 1000,
                directorNs = directorCpu.LastValue, pressureNs = pressureCpu.LastValue, gcBytes = gcBytes.LastValue,
                alive = alive, actors = actors.Count, threats = threats, actorDrift = drift,
                phase = (int)director.AdaptivePhase, state = (int)director.RunState };
        }

        private void WriteFrames()
        {
            using var writer = new StreamWriter(Path.Combine(folder, "population-" + population + ".csv"));
            writer.WriteLine("time,frameMs,directorNs,pressureNs,gcBytes,livingEnemies,actors,threats,actorDriftMetres,phase,runState");
            bool valid = frameCount > 0;
            for (int i = 0; i < frameCount; i++)
            {
                var f = frames[i];
                writer.WriteLine(FormattableString.Invariant($"{f.time:F6},{f.frameMs:F6},{f.directorNs},{f.pressureNs},{f.gcBytes},{f.alive},{f.actors},{f.threats},{f.actorDrift:F6},{f.phase},{f.state}"));
                valid &= f.alive == population && f.actors == expectedParty
                    && f.state == (int)DirectorRunState.Active && f.actorDrift <= .05f;
            }
            valid &= NetworkManager.Singleton.ConnectedClientsList.Count == expectedParty && runtimeError == null;
            if (!valid) Fail("Population/actor/active/position invariant failed for workload " + population);
        }

        private IEnumerator Calibrate(string label)
        {
            // Mono's GC.GetAllocatedBytesForCurrentThread can return zero; validate the Unity allocation marker instead.
            var options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 4096, options);
            if (!allocations.Valid) { Fail("GC.Alloc marker unavailable."); yield break; }
            allocations.Stop();
            allocations.Reset(); allocations.Start();
            for (int i = 0; i < 32; i++) GC.KeepAlive(new byte[1024]);
            allocations.Stop();
            long deliberate = AllocationCount(allocations);
            var sample = (Action<double, bool>)Delegate.CreateDelegate(typeof(Action<double, bool>), director,
                typeof(AIDirector).GetMethod("SampleActorPressure", Private));
            double now = NetworkManager.Singleton.ServerTime.Time;
            for (int i = 0; i < 256; i++) sample(now, false);
            allocations.Reset(); allocations.Start();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 256; i++) sample(now, false);
            long elapsed = Stopwatch.GetTimestamp() - start;
            allocations.Stop();
            long sampleAllocations = AllocationCount(allocations);
            File.WriteAllText(Path.Combine(folder, "allocation-calibration-" + label + ".txt"),
                $"intentionalAllocationEvents={deliberate}\nintentionalArrays=32\narrayPayloadBytes=1024\n"
                + $"pressureCalls=256\npressureAllocationEvents={sampleAllocations}\npressureBatchMicroseconds={elapsed * 1000000.0 / Stopwatch.Frequency:F3}\n"
                + "gcMarkerValueIsBytes=false\n");
            if (deliberate < 32) Fail("GC allocation calibration did not detect intentional allocations.");
            yield return null;
        }

        private static long AllocationCount(ProfilerRecorder recorder)
        {
            long count = 0;
            for (int i = 0; i < recorder.Count; i++) count += recorder.GetSample(i).Count;
            return count;
        }

        private IEnumerator MeasureProtection()
        {
            var health = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            using var writer = new StreamWriter(Path.Combine(folder, "protection.csv"));
            writer.WriteLine("trial,acceptedDamage,observedLatencyMs,directorLatencyMs,relax,protectionReason,spawnBlocked,survivingEnemies");
            for (int trial = 0; trial < 20; trial++)
            {
                director.BeginAdaptiveAttempt();
                yield return null;
                if (director.AdaptivePhase == DirectorPhase.Relax) { Fail("Latency trial did not start outside Relax."); yield break; }
                // Reset health only between synthetic latency trials; damage enters through PlayerHealth.TakeDamage.
                var hp = (NetworkVariable<float>)typeof(PlayerHealth).GetField("networkHealth", Private).GetValue(health);
                hp.Value = health.MaxHealth;
                Set(health, "campaignProtectionUntil", 0.0);
                float previous = health.CurrentHealth;
                double start = Time.realtimeSinceStartupAsDouble;
                health.TakeDamage(40);
                bool accepted = health.CurrentHealth < previous;
                double deadline = start + 1;
                while (director.AdaptivePhase != DirectorPhase.Relax && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                double ms = (Time.realtimeSinceStartupAsDouble - start) * 1000;
                bool relax = director.AdaptivePhase == DirectorPhase.Relax;
                bool protection = director.DecisionReason == DirectorDecisionReason.Protection;
                writer.WriteLine(FormattableString.Invariant($"{trial},{accepted},{ms:F6},{director.ProtectionLatencyMilliseconds:F6},{relax},{protection},{director.AdaptiveSpawningBlocked},{EnemyAI.PressureRoster.Count}"));
                if (!accepted || !relax || !protection || !director.AdaptiveSpawningBlocked || ms > 300)
                { Fail("Damage/protection latency invariant failed."); yield break; }
            }
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Private);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        private void TrackRuntimeError(string message, string stack, LogType type)
        {
            if (runtimeError == null && type is LogType.Error or LogType.Exception or LogType.Assert
                && !File.Exists(Path.Combine(folder, "finished.txt")) && !File.Exists(Path.Combine(folder, "failed.txt")))
                runtimeError = message;
        }
        private void OnDestroy() => Application.logMessageReceived -= TrackRuntimeError;
        private void Fail(string message)
        {
            measuring = false;
            File.WriteAllText(Path.Combine(folder, "failed.txt"), message);
            UnityEngine.Debug.LogError("[Director benchmark] " + message);
            Application.Quit(2);
        }
    }
}
#endif
