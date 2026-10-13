using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using UnityEngine.AI;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace FPS
{
    // Temporary measurement harness, copied into Assets only for the audit build.
    public sealed class PerformanceAuditPlayer : MonoBehaviour
    {
        readonly List<float> frames = new();
        readonly List<long> allocations = new();
        ProfilerRecorder gc;
        bool measuring;
        string output;
        int renderedFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--fps-audit-output");
            if (index < 0 || index + 1 >= args.Length) return;
            var runner = new GameObject("Performance audit player").AddComponent<PerformanceAuditPlayer>();
            Object.DontDestroyOnLoad(runner);
            runner.output = args[index + 1];
            Directory.CreateDirectory(runner.output);
            Application.runInBackground = true;
            runner.StartCoroutine(runner.Run());
        }

        void Update()
        {
            if (!measuring) return;
            frames.Add(Time.unscaledDeltaTime * 1000);
            allocations.Add(gc.Valid ? gc.LastValue : -1);
        }

        void OnEnable() => Camera.onPostRender += Rendered;
        void OnDisable() => Camera.onPostRender -= Rendered;
        void Rendered(Camera camera) { if (measuring) renderedFrames++; }

        IEnumerator Run()
        {
            yield return null;
            var network = NetworkManager.Singleton;
            if (network == null) { Fail("NetworkManager is missing"); yield break; }
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            using var socket = new System.Net.Sockets.UdpClient(0);
            ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            socket.Close();
            network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", port, "127.0.0.1");
            if (!network.StartHost()) { Fail("Local host failed"); yield break; }
            network.SceneManager.OnLoadEventCompleted += SpawnPlayer;
            network.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            float deadline = Time.realtimeSinceStartup + 180;
            while ((CampaignMissionController.Instance == null || !CampaignMissionController.Instance.IsSpawned
                || !CampaignMissionController.Instance.InsertionReady) && Time.realtimeSinceStartup < deadline) yield return null;
            if (CampaignMissionController.Instance == null || !CampaignMissionController.Instance.InsertionReady)
            { Fail("Insertion did not start"); yield break; }
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Low"), true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(3);
            yield return Measure("insertion", 10);
            while (CampaignMissionController.Instance.State.phase != CampaignPhase.Exploring && Time.realtimeSinceStartup < deadline) yield return null;
            if (CampaignMissionController.Instance.State.phase != CampaignPhase.Exploring)
            { Fail("Exploration did not start"); yield break; }
            yield return new WaitForSecondsRealtime(3);
            var player = network.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            // Keep the stationary test subject alive so later comparison windows stay in the same phase.
            player.GetType().GetField("campaignProtectionUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(player, CampaignMissionController.Instance.Now + 300);
            var rig = Object.FindAnyObjectByType<HelicopterInsertionRappel>(FindObjectsInactive.Include);
            var animationChecks = new List<string>();
            foreach (var template in rig.rappellers)
            {
                var probe = Object.Instantiate(template);
                probe.gameObject.SetActive(true);
                foreach (var renderer in probe.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
                probe.Pose(0, .55f, false, 0);
                Vector3 start = probe.leftHand.position;
                probe.Pose(0, 1.3f, false, 0);
                float travel = Vector3.Distance(start, probe.leftHand.position);
                animationChecks.Add($"{template.name}: animator={probe.GetComponent<Animator>() != null}; equipHandTravel={travel:F4}m");
                if (travel < .05f) { Fail("Equip clip did not move " + template.name); yield break; }
                Object.Destroy(probe.gameObject);
            }
            File.WriteAllLines(Path.Combine(output, "animation-checks.txt"), animationChecks);
            yield return Measure("exploring-1080p", 20);
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(2);
            yield return Measure("exploring-720p", 15);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(2);
            int spawnSuccesses = 0;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * 2.39996f;
                var position = player.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (8 + i % 12);
                if (NavMesh.SamplePosition(position, out var hit, 4, NavMesh.AllAreas))
                {
                    var zombie = ZombieFactory.Instance.SpawnZombie(hit.position, Quaternion.identity);
                    if (zombie != null)
                    {
                        spawnSuccesses++;
                        zombie.GetComponent<EnemyAI>().BeginDirectorPursuit(player.transform);
                    }
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(output, "stress-spawns.txt"), $"Requested 40 extra enemies; spawned={spawnSuccesses}");
            yield return new WaitForSecondsRealtime(2);
            yield return Measure("stress-1080p", 20);
            var cameras = Object.FindObjectsByType<Camera>();
            var masks = cameras.Select(c => c.cullingMask).ToArray();
            foreach (var camera in cameras) camera.cullingMask = 0;
            yield return new WaitForSecondsRealtime(2);
            yield return Measure("stress-no-world-render", 10);
            for (int i = 0; i < cameras.Length; i++) if (cameras[i] != null) cameras[i].cullingMask = masks[i];
            var campaign = CampaignMissionController.Instance;
            campaign.GetType().GetMethod("ClearEnemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(campaign, null);
            foreach (var chapter in campaign.chapters.Where(c => c.chapter != CampaignChapter.Factory))
            {
                // Controlled viewpoints audit rendering; this does not exercise natural mission progression.
                campaign.State.chapter = chapter.chapter;
                campaign.ConfigureChapterServices();
                var viewpoints = new[] { chapter.arrivals[0], chapter.recoveries[chapter.recoveries.Length / 2].transform };
                for (int view = 0; view < viewpoints.Length; view++)
                {
                    player.RelocateCampaign(viewpoints[view].position, viewpoints[view].rotation);
                    yield return new WaitForSecondsRealtime(5);
                    if (player.CampaignTransferPending) { Fail("Chapter viewpoint transfer failed"); yield break; }
                    yield return Measure($"{chapter.chapter.ToString().ToLowerInvariant()}-view{view + 1}-1080p", 15);
                }
            }
            network.Shutdown();
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Standalone measurements completed");
            Application.Quit();
        }

        void SpawnPlayer(string name, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (name != "GameScene") return;
            var network = NetworkManager.Singleton;
            network.SceneManager.OnLoadEventCompleted -= SpawnPlayer;
            var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
            if (catalog == null || !catalog.TryGetPrefab(PlayerCharacterId.Brimstone, out var prefab))
            { Fail("Review player missing"); return; }
            NetworkSpawnManager.Instance.TryGetNextSpawnPose(out var position, out var rotation);
            var player = Object.Instantiate(prefab, position, rotation);
            player.GetComponent<PlayerHealth>().PrepareInitialSpawn(new SessionPlayerId(1));
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(network.LocalClientId);
            NetworkMatchStateManager.Instance.EnterWarmup();
        }

        IEnumerator Measure(string name, int seconds)
        {
            frames.Clear(); allocations.Clear();
            renderedFrames = 0;
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            measuring = true;
            yield return new WaitForSecondsRealtime(seconds);
            measuring = false;
            gc.Dispose();
            yield return new WaitForEndOfFrame();
            if (frames.Count == 0) { Fail("No frames for " + name); yield break; }
            var sorted = frames.OrderBy(x => x).ToArray();
            var lines = new List<string> {
                $"frames={frames.Count}; fps={1000/frames.Average():F2}; averageMs={frames.Average():F3}; p50Ms={sorted[sorted.Length/2]:F3}; p95Ms={sorted[(int)((sorted.Length-1)*.95)]:F3}; p99Ms={sorted[(int)((sorted.Length-1)*.99)]:F3}; maxMs={sorted[sorted.Length-1]:F3}",
                $"over33ms={frames.Count(d=>d>33.333)}; over50ms={frames.Count(d=>d>50)}; gcCounterAvailable={allocations.Any(a=>a>=0)}; gcAverageBytes={allocations.Where(a=>a>=0).DefaultIfEmpty().Average():F0}; gcMaxBytes={allocations.Max()}",
                $"resolution={Screen.width}x{Screen.height}; phase={CampaignMissionController.Instance.State.phase}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; enemies={Object.FindObjectsByType<EnemyAI>().Count(e=>e.isActiveAndEnabled)}; renderedCameraFrames={renderedFrames}; focused={Application.isFocused}",
                $"allocatedMB={Profiler.GetTotalAllocatedMemoryLong()/1048576.0:F1}; reservedMB={Profiler.GetTotalReservedMemoryLong()/1048576.0:F1}; monoUsedMB={Profiler.GetMonoUsedSizeLong()/1048576.0:F1}",
                $"cpu={SystemInfo.processorType}; gpu={SystemInfo.graphicsDeviceName}; vramMB={SystemInfo.graphicsMemorySize}; ramMB={SystemInfo.systemMemorySize}"
            };
            lines.Add($"chapter={CampaignMissionController.Instance.State.chapter}; playerPosition={NetworkManager.Singleton.LocalClient.PlayerObject.transform.position}; transferPending={NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHealth>().CampaignTransferPending}");
            foreach (var camera in Object.FindObjectsByType<Camera>()) lines.Add($"CAMERA {camera.name}: enabled={camera.enabled}; mask={camera.cullingMask}");
            File.WriteAllLines(Path.Combine(output, name + ".txt"), lines);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null;
        }

        void Fail(string message)
        {
            File.WriteAllText(Path.Combine(output, "failed.txt"), message);
            Application.Quit(1);
        }
    }
}
