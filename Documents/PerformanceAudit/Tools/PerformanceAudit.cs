using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Object = UnityEngine.Object;

public static class PerformanceAudit
{
    const string Output = "Documents/PerformanceAudit";
    static double end;
    static int previousFrame;
    static bool previousProfiler;
    static readonly List<float> deltas = new List<float>(4096);
    static readonly List<long> allocations = new List<long>(4096);
    static ProfilerRecorder gc;
    static bool lightCapture;

    [MenuItem("Tools/FPS/Performance Audit/Save Authored Assets")]
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

    [MenuItem("Tools/FPS/Performance Audit/Release Unused Editor Memory")]
    public static void ReleaseMemory()
    {
        Save();
        ProfilerDriver.ClearAllFrames();
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }

    [MenuItem("Tools/FPS/Performance Audit/Inventory")]
    public static void Inventory()
    {
        Directory.CreateDirectory(Output);
        var lines = new List<string> {
            $"playing={EditorApplication.isPlaying}; scene={SceneManager.GetActiveScene().path}",
            $"resolution={Screen.width}x{Screen.height}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; target={Application.targetFrameRate}; vsync={QualitySettings.vSyncCount}; fixedDelta={Time.fixedDeltaTime}",
            $"allocatedMB={Profiler.GetTotalAllocatedMemoryLong()/1048576.0:F1}; reservedMB={Profiler.GetTotalReservedMemoryLong()/1048576.0:F1}; monoUsedMB={Profiler.GetMonoUsedSizeLong()/1048576.0:F1}",
        };
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            lines.Add($"ROOT {root.name}: active={root.activeInHierarchy}; objects={root.GetComponentsInChildren<Transform>(true).Length}");
        foreach (var group in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include).GroupBy(b => b.GetType().FullName).OrderByDescending(g => g.Count()).Take(45))
            lines.Add($"BEHAVIOUR {group.Key}: total={group.Count()}; activeEnabled={group.Count(b => b.isActiveAndEnabled)}");
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (var group in lights.GroupBy(l => $"{l.type}/{l.lightmapBakeType}/{l.shadows}"))
            lines.Add($"LIGHT {group.Key}: total={group.Count()}; active={group.Count(l => l.isActiveAndEnabled)}");
        foreach (var camera in Object.FindObjectsByType<Camera>())
            lines.Add($"CAMERA {camera.name}: enabled={camera.enabled}; mask={camera.cullingMask}; path={camera.renderingPath}; actual={camera.actualRenderingPath}; occlusion={camera.useOcclusionCulling}; far={camera.farClipPlane}; hdr={camera.allowHDR}");
        foreach (var group in Object.FindObjectsByType<Renderer>().GroupBy(r => r.GetType().Name))
            lines.Add($"RENDERER {group.Key}: active={group.Count()}; enabled={group.Count(r=>r.enabled)}; static={group.Count(r=>r.gameObject.isStatic)}");
        var campaign = FPS.CampaignMissionController.Instance;
        if (campaign != null) lines.Add($"CAMPAIGN phase={campaign.State.phase}; chapter={campaign.State.chapter}");
        foreach (var chapter in Object.FindObjectsByType<FPS.CampaignChapterRoot>())
            lines.Add($"CHAPTER {chapter.chapter}: renderers={chapter.GetComponentsInChildren<Renderer>().Length}; position={chapter.transform.position}");
        foreach (var group in Object.FindObjectsByType<Renderer>().GroupBy(r=>LayerMask.LayerToName(r.gameObject.layer)))
            lines.Add($"LAYER {group.Key}: renderers={group.Count()}; enabled={group.Count(r=>r.enabled)}");
        File.WriteAllLines(Output + "/inventory-" + (EditorApplication.isPlaying ? "play" : "edit") + ".txt", lines);
    }

    [MenuItem("Tools/FPS/Performance Audit/Capture 20 Seconds")]
    public static void Capture()
    {
        lightCapture = false;
        BeginCapture();
    }

    [MenuItem("Tools/FPS/Performance Audit/Capture Lightweight")]
    public static void CaptureLightweight()
    {
        lightCapture = true;
        BeginCapture();
    }

    static void BeginCapture()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || end != 0)
            throw new InvalidOperationException("Requires unpaused Play Mode without an active capture.");
        Save();
        deltas.Clear(); allocations.Clear();
        previousFrame = Time.frameCount;
        previousProfiler = Profiler.enabled;
        if (!lightCapture) ProfilerDriver.ClearAllFrames();
        Profiler.enabled = !lightCapture;
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        end = EditorApplication.timeSinceStartup + 20;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }
        if (Time.frameCount != previousFrame)
        {
            previousFrame = Time.frameCount;
            deltas.Add(Time.unscaledDeltaTime * 1000);
            allocations.Add(gc.Valid ? gc.LastValue : -1);
        }
        if (EditorApplication.timeSinceStartup >= end) Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        end = 0;
        gc.Dispose();
        Profiler.enabled = false;
        try
        {
            Directory.CreateDirectory(Output);
            var stamps = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var totals = new Dictionary<string, double[]>();
            int validFrames = 0;
            int first = Math.Max(ProfilerDriver.firstFrameIndex, ProfilerDriver.lastFrameIndex - 299);
            for (int frame = first; !lightCapture && frame <= ProfilerDriver.lastFrameIndex; frame++)
            {
                using var data = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (!data.valid) continue;
                validFrames++;
                var self = new double[data.sampleCount];
                var parents = new Stack<int>();
                var remaining = new int[data.sampleCount];
                for (int i = 0; i < data.sampleCount; i++)
                {
                    while (parents.Count > 0 && remaining[parents.Peek()] == 0) parents.Pop();
                    double ms = data.GetSampleTimeMs(i);
                    self[i] = ms;
                    if (parents.Count > 0) { int p = parents.Peek(); self[p] -= ms; remaining[p]--; }
                    remaining[i] = data.GetSampleChildrenCount(i);
                    if (remaining[i] > 0) parents.Push(i);
                }
                for (int i = 0; i < data.sampleCount; i++)
                {
                    string name = data.GetSampleName(i);
                    if (!totals.TryGetValue(name, out var total)) totals[name] = total = new double[4];
                    total[0] += Math.Max(0, self[i]); total[1] += data.GetSampleTimeMs(i); total[2]++;
                    total[3] = Math.Max(total[3], data.GetSampleTimeMs(i));
                }
            }
            if (deltas.Count == 0 || !lightCapture && validFrames == 0) throw new InvalidOperationException("No measured frames.");
            var sorted = deltas.OrderBy(x => x).ToArray();
            var lines = new List<string> {
                $"frames={deltas.Count}; profilerFrames={validFrames}; fps={1000/deltas.Average():F2}; averageMs={deltas.Average():F3}; p50Ms={sorted[sorted.Length/2]:F3}; p95Ms={sorted[(int)((sorted.Length-1)*.95)]:F3}; p99Ms={sorted[(int)((sorted.Length-1)*.99)]:F3}; maxMs={sorted[sorted.Length-1]:F3}",
                $"over33ms={deltas.Count(d=>d>33.333)}; over50ms={deltas.Count(d=>d>50)}; gcAverageBytes={allocations.Where(a=>a>=0).DefaultIfEmpty().Average():F0}; gcMaxBytes={allocations.Max()}",
                "CPU Main Thread: marker | exclusive average ms/frame | inclusive average ms/frame | calls/frame | max individual ms (inclusive rows overlap)"
            };
            foreach (var pair in totals.OrderByDescending(p => p.Value[0]).Take(100))
                lines.Add($"{pair.Key} | {pair.Value[0]/validFrames:F4} | {pair.Value[1]/validFrames:F4} | {pair.Value[2]/validFrames:F2} | {pair.Value[3]:F4}");
            lines.Add("Gameplay and engine markers by inclusive cost:");
            foreach (var pair in totals.Where(p=>!p.Key.StartsWith("Editor") && !p.Key.StartsWith("GUI") && !p.Key.StartsWith("IMGUI")).OrderByDescending(p=>p.Value[1]).Take(100))
                lines.Add($"{pair.Key} | {pair.Value[0]/validFrames:F4} | {pair.Value[1]/validFrames:F4} | {pair.Value[2]/validFrames:F2} | {pair.Value[3]:F4}");
            File.WriteAllLines(Output + (lightCapture ? "/lightweight-" : "/capture-") + stamps + ".txt", lines);
            if (!lightCapture) ProfilerDriver.SaveProfile(Output + "/capture-" + stamps + ".data");
            Inventory();
            Save();
        }
        finally { Profiler.enabled = previousProfiler; }
    }
}
