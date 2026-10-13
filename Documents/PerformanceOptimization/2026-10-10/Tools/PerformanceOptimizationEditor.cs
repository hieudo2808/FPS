using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityEditorInternal;
using System.Globalization;

public static class PerformanceOptimizationEditor
{
    public const string Output = "Documents/PerformanceOptimization/2026-10-10";
    [Serializable] public class Entry
    {
        public string id, path, mesh, meshName;
        public int flags, triangles, lightmap;
        public Vector3 center, size;
        public string[] materials;
        public bool active, opaque, geometry;
    }
    [Serializable] public class InventoryData
    {
        public string scene, time;
        public int occlusionBytes, lodGroups;
        public Entry[] renderers;
    }
    static string PathOf(Transform t) { return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name; }
    static bool Opaque(Renderer r)
    {
        return r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null && m.renderQueue <= 2000
            && (!m.HasProperty("_Mode") || m.GetFloat("_Mode") == 0));
    }
    [MenuItem("Tools/FPS/Performance Optimization/Save")]
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
    static void RequireScene()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/FPS/Scenes/GameScene.unity")
            throw new InvalidOperationException("Requires authored GameScene in Edit Mode.");
    }
    [MenuItem("Tools/FPS/Performance Optimization/Quit For Benchmark")]
    public static void QuitForBenchmark()
    {
        RequireScene(); Save();
        if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Benchmark shutdown requires one clean authored scene.");
        EditorApplication.Exit(0);
    }
    [MenuItem("Tools/FPS/Performance Optimization/Inventory")]
    public static void Inventory()
    {
        RequireScene();
        Directory.CreateDirectory(Output);
        var entries = new List<Entry>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var filter = r.GetComponent<MeshFilter>();
            if (filter == null) continue;
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            int triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
            string path = PathOf(r.transform);
            entries.Add(new Entry { id = GlobalObjectId.GetGlobalObjectIdSlow(r.gameObject).ToString(), path = path,
                flags = (int)GameObjectUtility.GetStaticEditorFlags(r.gameObject), mesh = AssetDatabase.GetAssetPath(mesh),
                meshName = mesh.name, triangles = triangles, center = r.bounds.center, size = r.bounds.size,
                materials = r.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray(), lightmap = r.lightmapIndex,
                active = r.gameObject.activeInHierarchy && r.enabled, opaque = Opaque(r), geometry = path.Contains("/Geometry/") });
        }
        var data = new InventoryData { scene = SceneManager.GetActiveScene().path, time = DateTime.UtcNow.ToString("O"),
            occlusionBytes = StaticOcclusionCulling.umbraDataSize,
            lodGroups = Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            renderers = entries.ToArray() };
        File.WriteAllText(Output + "/inventory.json", JsonUtility.ToJson(data, true));
        Debug.Log($"Performance inventory: {entries.Count} meshes; occlusion={data.occlusionBytes} bytes; LOD={data.lodGroups}");
    }
    [MenuItem("Tools/FPS/Performance Optimization/Prepare Occlusion")]
    public static void PrepareOcclusion()
    {
        RequireScene(); Save(); Inventory();
        if (!File.Exists(Output + "/occlusion-original-flags.json"))
            File.Copy(Output + "/inventory.json", Output + "/occlusion-original-flags.json");
        var recoveryCameras = Object.FindObjectsByType<CampaignChapterRoot>(FindObjectsSortMode.None)
            .SelectMany(c => c.arrivals.Concat(c.recoveries.Select(p => p.transform)))
            .Select(p => p.position + Vector3.up * 1.6f).ToArray();
        int changed = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string path = PathOf(r.transform);
            if (!path.StartsWith("CampaignWorld/", StringComparison.Ordinal)) continue;
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            var filter = r.GetComponent<MeshFilter>();
            string meshName = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : r.name;
            string label = meshName + "/" + r.name;
            bool structure = path.Contains("/Geometry/") || path.Contains("/Architecture/")
                || path.Contains("/FactoryRepair_Architecture/") || path.Contains("/04_Ground_Foundations/")
                || new[] { "wall", "floor", "ceiling", "roof", "stairs", "concrete_fence", "ExteriorFaces",
                    "Hangar_", "Road_set_", "Cargo_container_v1_LD2close", "LabPartition", "PitEnd" }
                .Any(word => label.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            bool doorLeaf = (label.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("_gate", StringComparison.OrdinalIgnoreCase) >= 0)
                && !new[] { "arch", "frame", "cut" }.Any(word => label.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            bool moving = r.GetComponentInParent<Animator>() != null || r.GetComponentInParent<CampaignDoor>() != null
                || r.GetComponentInParent<Rigidbody>() != null || doorLeaf;
            bool large = new[] { r.bounds.size.x, r.bounds.size.y, r.bounds.size.z }.Count(v => v >= 1) >= 2;
            // Hollow building shells around recovery cameras produced false visibility in both trial bakes.
            var cameraBounds = r.bounds;
            cameraBounds.Expand(1);
            bool surroundsCamera = recoveryCameras.Any(cameraBounds.Contains);
            bool occluder = structure && !moving && Opaque(r) && large && !surroundsCamera;
            var next = occluder ? flags | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                : flags & ~StaticEditorFlags.OccluderStatic;
            if (moving) next &= ~StaticEditorFlags.OccludeeStatic;
            if (next == flags) continue;
            Undo.RecordObject(r.gameObject, "Prepare performance occlusion");
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, next);
            changed++;
        }
        var roots = Object.FindObjectsByType<CampaignChapterRoot>(FindObjectsSortMode.None).Select(c => c.transform).ToList();
        var connector = GameObject.Find("CampaignWorld/FactoryAsylumConnection");
        if (connector != null) roots.Add(connector.transform);
        foreach (var root in roots)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) continue;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            var existing = root.Find("PerformanceOcclusion");
            var go = existing != null ? existing.gameObject : new GameObject("PerformanceOcclusion");
            if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Add chapter occlusion area");
            go.transform.SetParent(root, true);
            go.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var area = go.GetComponent<OcclusionArea>();
            if (area == null) area = go.AddComponent<OcclusionArea>();
            if (area == null) throw new InvalidOperationException("OcclusionArea could not be added.");
            area.center = Vector3.zero;
            area.size = bounds.size + new Vector3(8, 12, 8);
            var serialized = new SerializedObject(area);
            var viewVolume = serialized.FindProperty("m_IsViewVolume");
            if (viewVolume != null) viewVolume.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Save();
        File.WriteAllText(Output + "/occlusion-prepared.txt", $"changedFlags={changed}; areas={roots.Count}");
        Debug.Log("Prepared occlusion, modified static flags: " + changed);
    }
    [MenuItem("Tools/FPS/Performance Optimization/Bake Occlusion 5m")]
    public static void Bake5() { Bake(5); }
    [MenuItem("Tools/FPS/Performance Optimization/Bake Occlusion 2m")]
    public static void Bake2() { Bake(2); }
    static void Bake(float smallest)
    {
        RequireScene(); Save();
        StaticOcclusionCulling.smallestOccluder = smallest;
        StaticOcclusionCulling.smallestHole = .25f;
        StaticOcclusionCulling.backfaceThreshold = 100;
        if (File.Exists(Output + "/bake-complete.txt")) File.Delete(Output + "/bake-complete.txt");
        if (File.Exists(Output + "/bake-failed.txt")) File.Delete(Output + "/bake-failed.txt");
        bakeStartedAt = EditorApplication.timeSinceStartup;
        if (!StaticOcclusionCulling.GenerateInBackground()) throw new InvalidOperationException("Occlusion bake could not start.");
        EditorApplication.update -= FinishBake;
        EditorApplication.update += FinishBake;
        File.WriteAllText(Output + "/bake-running.txt", "smallest=" + smallest);
    }
    static double bakeStartedAt;
    [MenuItem("Tools/FPS/Performance Optimization/Verify Occlusion")]
    public static void VerifyOcclusion()
    {
        RequireScene(); Save();
        if (StaticOcclusionCulling.umbraDataSize <= 0)
            throw new InvalidOperationException("Render the Scene View before verifying loaded occlusion data.");
        var areas = Object.FindObjectsByType<OcclusionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (areas.Count(a => a.name == "PerformanceOcclusion") != 4)
            throw new InvalidOperationException("Expected four authored view volumes.");
        File.WriteAllText(Output + "/bake-complete.txt", "bytes=" + StaticOcclusionCulling.umbraDataSize);
        if (File.Exists(Output + "/bake-failed.txt")) File.Delete(Output + "/bake-failed.txt");
        Inventory();
    }
    static void FinishBake()
    {
        if (StaticOcclusionCulling.isRunning) return;
        if (EditorApplication.timeSinceStartup - bakeStartedAt < 2) return;
        // The native size is stale until a camera has rendered this scene.
        if (StaticOcclusionCulling.umbraDataSize == 0) { SceneView.RepaintAll(); return; }
        EditorApplication.update -= FinishBake;
        Save();
        File.WriteAllText(Output + "/bake-complete.txt", "bytes=" + StaticOcclusionCulling.umbraDataSize);
        if (File.Exists(Output + "/bake-running.txt")) File.Delete(Output + "/bake-running.txt");
        Inventory();
    }
    [MenuItem("Tools/FPS/Performance Optimization/Rollback Occlusion")]
    public static void RollbackOcclusion()
    {
        RequireScene(); Save();
        var original = JsonUtility.FromJson<InventoryData>(File.ReadAllText(Output + "/occlusion-original-flags.json"));
        foreach (var entry in original.renderers)
            if (GlobalObjectId.TryParse(entry.id, out var id) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is GameObject go)
            {
                const StaticEditorFlags mask = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                GameObjectUtility.SetStaticEditorFlags(go, (GameObjectUtility.GetStaticEditorFlags(go) & ~mask)
                    | ((StaticEditorFlags)entry.flags & mask));
            }
        foreach (var chapter in Object.FindObjectsByType<CampaignChapterRoot>(FindObjectsSortMode.None))
        {
            var area = chapter.transform.Find("PerformanceOcclusion");
            if (area != null) Object.DestroyImmediate(area.gameObject);
        }
        var connector = GameObject.Find("CampaignWorld/FactoryAsylumConnection/PerformanceOcclusion");
        if (connector != null) Object.DestroyImmediate(connector);
        StaticOcclusionCulling.Clear();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Save();
    }

    [MenuItem("Tools/FPS/Performance Optimization/Read Profiles")]
    public static void ReadProfiles()
    {
        RequireScene(); Save();
        foreach (string file in Directory.GetFiles(Output + "/Development", "*.raw", SearchOption.AllDirectories))
        {
            if (!ProfilerDriver.LoadProfile(file, false)) throw new InvalidOperationException("Could not load " + file);
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            if (first < 0 || last < first) throw new InvalidOperationException("No profile frames: " + file);
            var markers = new Dictionary<string, double[]>();
            int count = last - first + 1;
            for (int frame = first; frame <= last; frame++)
                for (int thread = 0; thread < 256; thread++)
                {
                    using var view = ProfilerDriver.GetRawFrameDataView(frame, thread);
                    if (!view.valid) break;
                    string prefix = thread + ":" + view.threadGroupName + "/" + view.threadName + ",";
                    for (int sample = 0; sample < view.sampleCount; sample++)
                    {
                        string name = prefix + view.GetSampleName(sample);
                        if (!markers.TryGetValue(name, out var values)) markers[name] = values = new double[count];
                        values[frame - first] += view.GetSampleTimeMs(sample);
                    }
                }
            using var writer = new StreamWriter(file + "-markers.csv");
            writer.WriteLine("thread,marker,meanInclusiveMs,p95InclusiveMs,p99InclusiveMs,maxInclusiveMs,frames");
            foreach (var entry in markers.OrderByDescending(p => p.Value.Average()))
            {
                var values = entry.Value.OrderBy(v => v).ToArray();
                if (values.Last() < .05) continue;
                string[] names = entry.Key.Split(new[] { ',' }, 2);
                writer.WriteLine(string.Join(",", names.Select(n => "\"" + n.Replace("\"", "\"\"") + "\"")) + ","
                    + string.Join(",", new[] { values.Average(), values[(int)((count - 1) * .95)],
                        values[(int)((count - 1) * .99)], values.Last() }.Select(v => v.ToString("F4", CultureInfo.InvariantCulture)))
                    + "," + count);
            }
        }
        Save();
    }
}
