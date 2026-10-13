using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPS
{
    // Audit build only. Remove the copy in Assets before the clean release build.
    [DefaultExecutionOrder(10000)]
    public sealed class PerformanceAuditPlayer : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [DllImport("kernel32.dll")] static extern bool QueryPerformanceCounter(out long value);
        [DllImport("kernel32.dll")] static extern bool QueryPerformanceFrequency(out long value);
        static long Qpc()
        {
            if (!QueryPerformanceCounter(out long value)) throw new InvalidOperationException("QPC unavailable");
            return value;
        }
        readonly List<Sample> samples = new List<Sample>(65536);
        readonly List<EnemyAI> roster = new List<EnemyAI>(70);
        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly Dictionary<string, ProfilerRecorder> counters = new Dictionary<string, ProfilerRecorder>();
        string output, variant;
        string[] scenarios;
        int repeats, seed, seconds, warmup, renderedFrames, unfocusedFrames, rosterFailures;
        bool measuring, lockPose, visual;
        Vector3 fixturePosition, cameraOffset;
        Quaternion fixtureRotation, cameraRotation;
        float nextValidation;
        Camera worldCamera;
        PlayerHealth player;
        CampaignMissionController campaign;
#if DEVELOPMENT_BUILD
        bool profile;
        int profileFrames;
#endif

        struct Sample
        {
            public float ms;
            public double cpu, gpu;
            public long gc, draws, setPass, triangles, main, render;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            string folder = Arg(args, "--fps-audit-output", null);
            if (folder == null) return;
            var runner = new GameObject("Performance audit player").AddComponent<PerformanceAuditPlayer>();
            Object.DontDestroyOnLoad(runner);
            runner.output = folder;
            runner.variant = Arg(args, "--fps-audit-variant", "normal");
            runner.scenarios = Arg(args, "--fps-audit-scenarios", "factory3,factory43,lab27,lab70,asylum10").Split(',');
            runner.repeats = IntArg(args, "--fps-audit-repeats", 3, 1, 10);
            runner.seed = IntArg(args, "--fps-audit-seed", 1701, 0, int.MaxValue);
            runner.seconds = IntArg(args, "--fps-audit-seconds", 60, 10, 1800);
            runner.warmup = IntArg(args, "--fps-audit-warmup", 30, 2, 120);
            runner.samples.Capacity = Math.Max(65536, runner.seconds * 2000);
            runner.visual = Array.IndexOf(args, "--fps-audit-visual") >= 0;
#if DEVELOPMENT_BUILD
            runner.profile = Array.IndexOf(args, "--fps-audit-profile") >= 0;
#endif
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            runner.StartCoroutine(runner.Run());
        }

        static string Arg(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        static int IntArg(string[] args, string name, int fallback, int min, int max)
        {
            if (!int.TryParse(Arg(args, name, fallback.ToString()), out int value) || value < min || value > max)
                throw new ArgumentException("Invalid " + name);
            return value;
        }

        void OnEnable() { Camera.onPostRender += Rendered; }
        void OnDisable() { Camera.onPostRender -= Rendered; DisposeCounters(); }
        void Rendered(Camera camera) { if (measuring) renderedFrames++; }

        void LateUpdate()
        {
            if (lockPose)
            {
                player.transform.SetPositionAndRotation(fixturePosition, fixtureRotation);
                worldCamera.transform.SetPositionAndRotation(fixturePosition + cameraOffset, cameraRotation);
                worldCamera.fieldOfView = 60;
            }
            if (!measuring) return;
#if DEVELOPMENT_BUILD
            if (profileFrames > 0 && --profileFrames == 0)
            {
                Profiler.enabled = false;
                Profiler.enableBinaryLog = false;
            }
#endif
            double cpu = -1, gpu = -1;
            if (FrameTimingManager.IsFeatureEnabled())
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    cpu = timings[0].cpuFrameTime > 0 ? timings[0].cpuFrameTime : -1;
                    gpu = timings[0].gpuFrameTime > 0 ? timings[0].gpuFrameTime : -1;
                }
            }
            samples.Add(new Sample { ms = Time.unscaledDeltaTime * 1000, cpu = cpu, gpu = gpu,
                gc = Counter("GC Allocated In Frame"), draws = Counter("Draw Calls Count"),
                setPass = Counter("SetPass Calls Count"), triangles = Counter("Triangles Count"),
                main = Counter("Main Thread"), render = Counter("Render Thread") });
            if (!Application.isFocused) unfocusedFrames++;
            if (Time.unscaledTime >= nextValidation)
            {
                nextValidation = Time.unscaledTime + 1;
                for (int i = 0; i < roster.Count; i++)
                    if (roster[i] == null || !roster[i].isActiveAndEnabled) { rosterFailures++; break; }
            }
        }

        long Counter(string name)
        {
            return counters.TryGetValue(name, out var counter) && counter.Valid && counter.Count > 0
                ? counter.LastValue : -1;
        }

        void StartCounters()
        {
            DisposeCounters();
            foreach (var item in new[] { (ProfilerCategory.Memory, "GC Allocated In Frame"),
                (ProfilerCategory.Render, "Draw Calls Count"), (ProfilerCategory.Render, "SetPass Calls Count"),
                (ProfilerCategory.Render, "Triangles Count"), (ProfilerCategory.Internal, "Main Thread"),
                (ProfilerCategory.Internal, "Render Thread") })
                counters[item.Item2] = ProfilerRecorder.StartNew(item.Item1, item.Item2, 1);
        }

        void DisposeCounters()
        {
            foreach (var recorder in counters.Values) recorder.Dispose();
            counters.Clear();
        }

        IEnumerator Run()
        {
            yield return null;
            var network = NetworkManager.Singleton;
            if (network == null) { Fail("NetworkManager missing"); yield break; }
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            using (var socket = new System.Net.Sockets.UdpClient(0))
            {
                ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
                socket.Close();
                network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", port, "127.0.0.1");
            }
            if (!network.StartHost()) { Fail("Local host failed"); yield break; }
            network.SceneManager.OnLoadEventCompleted += SpawnPlayer;
            network.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            float deadline = Time.realtimeSinceStartup + 180;
            while ((CampaignMissionController.Instance == null || !CampaignMissionController.Instance.InsertionReady)
                && Time.realtimeSinceStartup < deadline) yield return null;
            campaign = CampaignMissionController.Instance;
            if (campaign == null || !campaign.InsertionReady) { Fail("Insertion timeout"); yield break; }
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Low"), true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            while (campaign.State.phase != CampaignPhase.Exploring && Time.realtimeSinceStartup < deadline) yield return null;
            if (campaign.State.phase != CampaignPhase.Exploring) { Fail("Exploring timeout"); yield break; }
            player = network.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            InputManager.GameplayInputBlocked = true;
            InputManager.Instance.ActionsAsset.FindActionMap("Gameplay", true).Disable();
            player.GetType().GetField("campaignProtectionUntil", Private).SetValue(player, campaign.Now + 100000);
            // Keep Director evaluation active while freezing autonomous spawn scheduling in this fixture.
            var director = AIDirector.Instance;
            typeof(AIDirector).GetField("baseSpawnInterval", Private).SetValue(director, 1e9f);
            typeof(AIDirector).GetField("adaptiveObserveOnly", Private).SetValue(director, true);
            var factoryPose = player.transform.position;
            var factoryRotation = player.transform.rotation;
            worldCamera = player.GetComponentsInChildren<Camera>(true).First(c => c.name == "Camera");
            File.WriteAllText(Path.Combine(output, "started.txt"), DateTime.UtcNow.ToString("O"));
            if (visual) yield return VisibilityTour();
            ApplyVariant();
            for (int repeat = 1; repeat <= repeats; repeat++)
            {
                foreach (string scenario in scenarios)
                {
                    int count;
                    CampaignChapter chapter;
                    switch (scenario)
                    {
                        case "factory3": count = 3; chapter = CampaignChapter.Factory; break;
                        case "factory43": count = 43; chapter = CampaignChapter.Factory; break;
                        case "lab27": count = 27; chapter = CampaignChapter.Laboratory; break;
                        case "lab70": count = 70; chapter = CampaignChapter.Laboratory; break;
                        case "asylum10": count = 10; chapter = CampaignChapter.Asylum; break;
                        default: Fail("Unknown scenario " + scenario); yield break;
                    }
                    campaign.GetType().GetMethod("ClearEnemies", Private).Invoke(campaign, null);
                    roster.Clear();
                    lockPose = false;
                    campaign.State.chapter = chapter;
                    campaign.ConfigureChapterServices();
                    var root = campaign.chapters.First(c => c.chapter == chapter);
                    var pose = root.recoveries[root.recoveries.Length / 2].transform;
                    fixturePosition = chapter == CampaignChapter.Factory ? factoryPose :
                        chapter == CampaignChapter.Laboratory ? new Vector3(243.55f, -17.92f, 270.17f) : pose.position;
                    fixtureRotation = chapter == CampaignChapter.Factory ? factoryRotation : pose.rotation;
                    player.RelocateCampaign(fixturePosition, fixtureRotation);
                    yield return new WaitForSecondsRealtime(3);
                    if (player.CampaignTransferPending) { Fail("Transfer pending " + scenario); yield break; }
                    player.transform.SetPositionAndRotation(fixturePosition, fixtureRotation);
                    cameraOffset = Vector3.up * 1.6f;
                    cameraRotation = fixtureRotation;
                    lockPose = true;
                    UnityEngine.Random.InitState(seed);
                    for (int i = 0; i < count; i++)
                    {
                        GameObject zombie = null;
                        for (int attempt = 0; attempt < 20 && zombie == null; attempt++)
                        {
                            float angle = (i + attempt * count) * 2.39996f;
                            var point = player.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (5 + (i + attempt) % 14);
                            if (NavMesh.SamplePosition(point, out var hit, 3, NavMesh.AllAreas)
                                && Mathf.Abs(hit.position.y - player.transform.position.y) < 1.5f)
                                zombie = ZombieFactory.Instance.SpawnZombie(hit.position, Quaternion.identity);
                        }
                        if (zombie == null) { Fail("Spawn failed " + scenario + " index " + i); yield break; }
                        var brain = zombie.GetComponent<EnemyAI>();
                        brain.BeginDirectorPursuit(player.transform);
                        roster.Add(brain);
                        yield return null;
                    }
                    yield return new WaitForSecondsRealtime(warmup);
                    if (Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Count(e => e.isActiveAndEnabled) != count)
                    { Fail("Roster drift " + scenario); yield break; }
                    yield return Measure(scenario + "-r" + repeat, count);
                    if (File.Exists(Path.Combine(output, "failed.txt"))) yield break;
                }
            }
            network.Shutdown();
            File.WriteAllText(Path.Combine(output, "complete.txt"), DateTime.UtcNow.ToString("O"));
            Application.Quit();
        }

        void ApplyVariant()
        {
            if (variant == "normal") return;
            if (variant == "no-occlusion") foreach (var c in Object.FindObjectsByType<Camera>()) c.useOcclusionCulling = false;
            else if (variant == "no-render") foreach (var c in Object.FindObjectsByType<Camera>()) c.cullingMask = 0;
            else if (variant == "no-lights") foreach (var l in Object.FindObjectsByType<Light>())
                { if (l.bakingOutput.lightmapBakeType != LightmapBakeType.Baked) l.enabled = false; }
            else if (variant == "no-probes") foreach (var r in Object.FindObjectsByType<Renderer>()) r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            else if (variant == "no-lod") foreach (var group in Object.FindObjectsByType<LODGroup>()) group.ForceLOD(0);
            else if (variant.StartsWith("texture-budget-") && int.TryParse(variant.Substring(15), out int budget))
            {
                QualitySettings.streamingMipmapsActive = true;
                QualitySettings.streamingMipmapsMemoryBudget = budget;
            }
            else throw new ArgumentException("Unknown variant " + variant);
        }

        IEnumerator VisibilityTour()
        {
            campaign.GetType().GetMethod("ClearEnemies", Private).Invoke(campaign, null);
            var poses = new List<string>();
            foreach (var chapter in campaign.chapters)
            {
                campaign.State.chapter = chapter.chapter;
                campaign.ConfigureChapterServices();
                var points = chapter.arrivals.Concat(chapter.recoveries.Select(r => r.transform)).ToArray();
                for (int point = 0; point < points.Length; point++)
                {
                    lockPose = false;
                    player.RelocateCampaign(points[point].position, points[point].rotation);
                    yield return new WaitForSecondsRealtime(.5f);
                    if (player.CampaignTransferPending) { Fail("Visibility tour transfer pending"); yield break; }
                    fixturePosition = points[point].position;
                    cameraOffset = Vector3.up * 1.6f;
                    lockPose = true;
                    Time.timeScale = 0;
                    for (int turn = 0; turn < 4; turn++)
                    {
                        fixtureRotation = points[point].rotation * Quaternion.Euler(0, turn * 90, 0);
                        cameraRotation = fixtureRotation;
                        worldCamera.fieldOfView = 60;
                        string name = $"{chapter.chapter}-{point}-{turn}";
                        poses.Add($"{name}: position={fixturePosition + cameraOffset}; rotation={cameraRotation.eulerAngles}; fov=60");
                        foreach (bool enabled in new[] { false, true })
                        {
                            worldCamera.useOcclusionCulling = enabled;
                            yield return null;
                            yield return new WaitForEndOfFrame();
                            if (Quaternion.Angle(worldCamera.transform.rotation, cameraRotation) > .1f)
                            { Fail("Visibility tour camera drift"); yield break; }
                            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + (enabled ? "-occlusion.png" : "-reference.png")));
                            yield return null;
                        }
                    }
                    Time.timeScale = 1;
                }
            }
            lockPose = false;
            File.WriteAllLines(Path.Combine(output, "visibility-poses.txt"), poses);
        }

        void SpawnPlayer(string name, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (name != "GameScene") return;
            var network = NetworkManager.Singleton;
            network.SceneManager.OnLoadEventCompleted -= SpawnPlayer;
            var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
            if (catalog == null || !catalog.TryGetPrefab(PlayerCharacterId.Brimstone, out var prefab))
            { Fail("Player prefab missing"); return; }
            NetworkSpawnManager.Instance.TryGetNextSpawnPose(out var position, out var rotation);
            var spawned = Object.Instantiate(prefab, position, rotation);
            spawned.GetComponent<PlayerHealth>().PrepareInitialSpawn(new SessionPlayerId(1));
            spawned.GetComponent<NetworkObject>().SpawnAsPlayerObject(network.LocalClientId);
            NetworkMatchStateManager.Instance.EnterWarmup();
        }

        IEnumerator Measure(string name, int expectedEnemies)
        {
            samples.Clear();
            renderedFrames = unfocusedFrames = rosterFailures = 0;
            nextValidation = Time.unscaledTime + 1;
            StartCounters();
#if DEVELOPMENT_BUILD
            if (profile)
            {
                Profiler.logFile = Path.Combine(output, name + ".raw");
                Profiler.enableBinaryLog = true;
                Profiler.enabled = true;
                profileFrames = 300;
            }
#endif
            if (!QueryPerformanceFrequency(out long qpcFrequency)) throw new InvalidOperationException("QPC frequency unavailable");
            long startQpc = Qpc();
            measuring = true;
            yield return new WaitForSecondsRealtime(seconds);
            measuring = false;
#if DEVELOPMENT_BUILD
            if (profile) { Profiler.enabled = false; Profiler.enableBinaryLog = false; }
#endif
            long endQpc = Qpc();
            DisposeCounters();
            yield return new WaitForEndOfFrame();
            int enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Count(e => e.isActiveAndEnabled);
            bool valid = samples.Count > 0 && unfocusedFrames == 0 && rosterFailures == 0 && enemies == expectedEnemies
                && Vector3.Distance(player.transform.position, fixturePosition) < .01f
                && Quaternion.Angle(worldCamera.transform.rotation, cameraRotation) < .1f
                && Screen.width == 1920 && Screen.height == 1080 && QualitySettings.names[QualitySettings.GetQualityLevel()] == "Low"
                && (variant == "no-render" || renderedFrames >= samples.Count);
            var sorted = samples.Select(s => s.ms).OrderBy(s => s).ToArray();
            var lines = new List<string> { "valid=" + valid, "variant=" + variant,
                $"qpcStart={startQpc}; qpcEnd={endQpc}; qpcFrequency={qpcFrequency}; nativeQpc=True",
                $"frames={samples.Count}; fps={1000 / sorted.Average():F3}; averageMs={sorted.Average():F3}; p95Ms={Percentile(sorted,.95):F3}; p99Ms={Percentile(sorted,.99):F3}; maxMs={sorted.Last():F3}",
                $"over33ms={sorted.Count(s=>s>33.333)}; over50ms={sorted.Count(s=>s>50)}; unfocusedFrames={unfocusedFrames}; rosterFailures={rosterFailures}; enemies={enemies}; renderedCameraFrames={renderedFrames}",
                $"resolution={Screen.width}x{Screen.height}; quality=Low; seed={seed}; warmup={warmup}; seconds={seconds}; chapter={campaign.State.chapter}; phase={campaign.State.phase}",
                $"playerPosition={player.transform.position}; playerRotation={player.transform.eulerAngles}; cameraPosition={worldCamera.transform.position}; cameraRotation={worldCamera.transform.eulerAngles}; fov={worldCamera.fieldOfView}",
                $"allocatedMB={Profiler.GetTotalAllocatedMemoryLong()/1048576.0:F1}; reservedMB={Profiler.GetTotalReservedMemoryLong()/1048576.0:F1}; monoUsedMB={Profiler.GetMonoUsedSizeLong()/1048576.0:F1}",
                $"textureCurrentMB={Texture.currentTextureMemory/1048576.0:F1}; textureDesiredMB={Texture.desiredTextureMemory/1048576.0:F1}; textureNonStreamingMB={Texture.nonStreamingTextureMemory/1048576.0:F1}; streamingActive={QualitySettings.streamingMipmapsActive}; streamingBudgetMB={QualitySettings.streamingMipmapsMemoryBudget}",
                $"cpu={SystemInfo.processorType}; gpu={SystemInfo.graphicsDeviceName}; graphicsAPI={SystemInfo.graphicsDeviceType}; vramMB={SystemInfo.graphicsMemorySize}; ramMB={SystemInfo.systemMemorySize}; frameTimingEnabled={FrameTimingManager.IsFeatureEnabled()}; developmentBuild={Debug.isDebugBuild}",
                "gpuMs=" + Average(samples.Select(s=>s.gpu)), "cpuTimingMs=" + Average(samples.Select(s=>s.cpu)),
                "mainThreadNs=" + Average(samples.Select(s=>(double)s.main)), "renderThreadNs=" + Average(samples.Select(s=>(double)s.render)),
                "gcBytes=" + Average(samples.Select(s=>(double)s.gc)), "draws=" + Average(samples.Select(s=>(double)s.draws)),
                "setPass=" + Average(samples.Select(s=>(double)s.setPass)), "triangles=" + Average(samples.Select(s=>(double)s.triangles)) };
            foreach (var camera in Object.FindObjectsByType<Camera>())
                lines.Add($"CAMERA {camera.name}: enabled={camera.enabled}; mask={camera.cullingMask}; occlusion={camera.useOcclusionCulling}");
            var animators = roster.SelectMany(e => e.GetComponentsInChildren<Animator>()).ToArray();
            lines.Add("ENEMY_ANIMATORS " + string.Join(",", animators.GroupBy(a => a.cullingMode).Select(g => g.Key + "=" + g.Count())));
            File.WriteAllLines(Path.Combine(output, name + ".txt"), lines);
            using (var csv = new StreamWriter(Path.Combine(output, name + ".csv")))
            {
                csv.WriteLine("frame,deltaMs,cpuTimingMs,gpuTimingMs,gcBytes,draws,setPass,triangles,mainThreadNs,renderThreadNs");
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    csv.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                        i, s.ms, s.cpu, s.gpu, s.gc, s.draws, s.setPass, s.triangles, s.main, s.render));
                }
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null;
            if (!valid) Fail("Invalid measurement " + name);
        }

        static float Percentile(float[] values, double p) { return values[(int)((values.Length - 1) * p)]; }
        static string Average(IEnumerable<double> values)
        {
            var valid = values.Where(v => v >= 0).ToArray();
            return valid.Length == 0 ? "unavailable" : valid.Average().ToString("F3", CultureInfo.InvariantCulture);
        }

        void Fail(string message)
        {
            measuring = false;
            File.WriteAllText(Path.Combine(output, "failed.txt"), message);
            Application.Quit(1);
        }
    }
}
