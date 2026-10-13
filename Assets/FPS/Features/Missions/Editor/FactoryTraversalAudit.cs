#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    /// <summary>Read-only Factory geometry measurements. Explicit menus only; never saves scenes.</summary>
    public static class FactoryTraversalAudit
    {
        static string outputSuffix = "";
        static string Output => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Documents/FactoryTraversalAudit", outputSuffix));
        const float Spacing = .5f;
        static string scanPrefix = "";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static readonly RaycastHit[] Hits = new RaycastHit[256];
        static readonly Collider[] Overlaps = new Collider[256];
        static Dictionary<Collider, int> colliderIds;
        static StreamWriter samples;
        static Metadata metadata;
        static int row, sampleCount;
        static double started;
        static PhysicsScene snapshotPhysics;

        [Serializable] public sealed class Profile
        {
            public string prefab;
            public float radius, height, step, slope, skin, jump, gravity, speed, walkMultiplier;
            public Vector3 center, scale;
            public Vector3 groundCheck;
            public int layer;
        }
        [Serializable] public sealed class Geometry
        {
            public int id, layer;
            public string path, type, mesh;
            public bool active, enabled, trigger;
            public Vector3 position, center, size;
        }
        [Serializable] public sealed class Marker
        {
            public string path, type;
            public Vector3 position;
        }
        [Serializable] public sealed class Metadata
        {
            public string generatedUtc, scene, unityVersion, sceneLastWriteUtc, sceneSha256;
            public bool capturedInPlayMode;
            public float spacing, minX, minZ, castTop, castBottom, radius, height, slope;
            public int width, depth;
            public Profile[] players;
            public Geometry[] colliders;
            public Marker[] markers;
        }

        static GameObject Factory()
        {
            var root = GameObject.Find("CampaignWorld/Factory");
            if (root == null || root.scene.path != "Assets/FPS/Scenes/GameScene.unity"
                || EditorApplication.isPlaying || root.scene.isDirty)
                throw new InvalidOperationException("Load a saved GameScene; the audit never saves unsaved work.");
            return root;
        }

        static string Path(Transform transform) => AnimationUtility.CalculateTransformPath(transform, null);
        static void Save(string name, object value) => File.WriteAllText(System.IO.Path.Combine(Output, name), JsonUtility.ToJson(value, true));

        [MenuItem("Tools/FPS/Factory Traversal Audit/Export Inventory")]
        public static void ExportInventory()
        {
            var root = Factory();
            Physics.SyncTransforms();
            Directory.CreateDirectory(Output);
            var profiles = new List<Profile>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FPS/Features/Characters/Content/Players" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var movement = prefab.GetComponent<PlayerMovement>();
                var controller = prefab.GetComponent<CharacterController>();
                if (movement == null || controller == null) continue;
                var serialized = new SerializedObject(movement);
                float Value(string name) => serialized.FindProperty(name).floatValue;
                profiles.Add(new Profile { prefab = path, radius = controller.radius, height = controller.height,
                    center = controller.center, scale = controller.transform.lossyScale,
                    step = controller.stepOffset, slope = controller.slopeLimit, skin = controller.skinWidth,
                    jump = Value("jumpHeight"), gravity = Value("gravityScale"), speed = Value("speed"),
                    walkMultiplier = Value("walkMultiplier"), layer = prefab.layer,
                    groundCheck = serialized.FindProperty("groundCheck").objectReferenceValue is Transform ground
                        ? prefab.transform.InverseTransformPoint(ground.position) : Vector3.zero });
            }
            if (profiles.Count == 0) throw new InvalidOperationException("No authored player controllers were found.");
            var all = root.GetComponentsInChildren<Collider>(true).ToList();
            var connection = GameObject.Find("CampaignWorld/FactoryAsylumConnection");
            if (connection != null) all.AddRange(connection.GetComponentsInChildren<Collider>(true));
            colliderIds = new Dictionary<Collider, int>();
            var geometry = new List<Geometry>();
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var collider in all)
            {
                int id = geometry.Count;
                colliderIds.Add(collider, id);
                bool active = collider.gameObject.activeInHierarchy && collider.enabled && !collider.isTrigger;
                var renderer = collider.GetComponent<Renderer>();
                Bounds shape = !active && renderer != null ? renderer.bounds : collider.bounds;
                geometry.Add(new Geometry { id = id, path = Path(collider.transform), type = collider.GetType().Name,
                    mesh = collider is MeshCollider mesh && mesh.sharedMesh != null ? mesh.sharedMesh.name : "",
                    active = collider.gameObject.activeInHierarchy, enabled = collider.enabled, trigger = collider.isTrigger,
                    layer = collider.gameObject.layer, position = collider.transform.position,
                    center = shape.center, size = shape.size });
                if (!active) continue;
                if (!hasBounds) { bounds = shape; hasBounds = true; } else bounds.Encapsulate(shape);
            }
            if (!hasBounds) throw new InvalidOperationException("Factory has no active solid colliders.");
            metadata = new Metadata { generatedUtc = DateTime.UtcNow.ToString("O"), scene = root.scene.path,
                capturedInPlayMode = EditorApplication.isPlaying,
                sceneLastWriteUtc = File.GetLastWriteTimeUtc(root.scene.path).ToString("O"), unityVersion = Application.unityVersion,
                sceneSha256 = BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(root.scene.path))).Replace("-", ""),
                spacing = Spacing, minX = Mathf.Floor(bounds.min.x / Spacing) * Spacing,
                minZ = Mathf.Floor(bounds.min.z / Spacing) * Spacing,
                castTop = bounds.max.y + 1, castBottom = bounds.min.y - 1,
                radius = profiles.Max(p => p.radius * Mathf.Max(p.scale.x, p.scale.z)),
                height = profiles.Max(p => p.height * p.scale.y), slope = profiles.Min(p => p.slope),
                players = profiles.ToArray(), colliders = geometry.ToArray(),
                markers = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.GetComponent<CampaignInteractable>() != null || t.GetComponent<MapRecoveryPoint>() != null
                        || t.name.Contains("Arrival") || t.name.Contains("Ramp") || t.name.Contains("Steps"))
                    .Select(t => new Marker { path = Path(t), position = t.position,
                        type = t.GetComponent<CampaignInteractable>() != null ? "objective" : t.GetComponent<MapRecoveryPoint>() != null ? "recovery" : "access" }).ToArray() };
            metadata.width = Mathf.CeilToInt((bounds.max.x - metadata.minX) / Spacing) + 1;
            metadata.depth = Mathf.CeilToInt((bounds.max.z - metadata.minZ) / Spacing) + 1;
            Save("inventory.json", metadata);
            Debug.Log($"[FactoryTraversalAudit] Inventory exported: {all.Count} colliders, {profiles.Count} player prefabs; grid {metadata.width} x {metadata.depth}.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Scan Standing Surfaces")]
        public static void Scan()
        {
            if (samples != null) throw new InvalidOperationException("A Factory scan is already running.");
            scanPrefix = "";
            ExportInventory();
            // The shared Editor is now idle: query its saved geometry directly in Edit Mode.
            snapshotPhysics = Physics.defaultPhysicsScene;
            samples = new StreamWriter(System.IO.Path.Combine(Output, "surfaces.csv"));
            samples.WriteLine("gx,gz,y,collider,nx,ny,nz,nav");
            row = sampleCount = 0;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += ScanUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            Progress("running");
        }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Refine Logistics South Pit")]
        public static void RefinePit()
        {
            if (samples != null) throw new InvalidOperationException("Wait for the surface scan.");
            ExportInventory();
            scanPrefix = "refinement-";
            metadata.spacing = .125f; metadata.minX = 7; metadata.minZ = -106;
            metadata.width = 209; metadata.depth = 113;
            Save(scanPrefix + "inventory.json", metadata);
            snapshotPhysics = Physics.defaultPhysicsScene;
            samples = new StreamWriter(System.IO.Path.Combine(Output, scanPrefix + "surfaces.csv"));
            samples.WriteLine("gx,gz,y,collider,nx,ny,nz,nav");
            row = sampleCount = 0; started = EditorApplication.timeSinceStartup;
            EditorApplication.update += ScanUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            Progress("running");
        }

        [Serializable] sealed class ScanProgress
        {
            public string status, error;
            public int completedRows, totalRows, samples;
            public double seconds;
        }
        static void Progress(string status, string error = null) => Save(scanPrefix + "progress.json", new ScanProgress
        { status = status, error = error, completedRows = row, totalRows = metadata.depth,
            samples = sampleCount, seconds = EditorApplication.timeSinceStartup - started });

        static void ScanUpdate()
        {
            try
            {
                Factory(); // Abort if the source scene changes or Play Mode starts.
                if (!snapshotPhysics.IsValid()) throw new InvalidOperationException("Invalid physics scene.");
                double until = EditorApplication.timeSinceStartup + .08;
                while (row < metadata.depth && EditorApplication.timeSinceStartup < until)
                {
                    float z = metadata.minZ + row * metadata.spacing;
                    for (int x = 0; x < metadata.width; x++) Sample(x, row, metadata.minX + x * metadata.spacing, z);
                    row++;
                }
                samples.Flush();
                if (row < metadata.depth) { Progress("running"); return; }
                Close();
                Progress("complete");
                Debug.Log($"[FactoryTraversalAudit] Complete: {sampleCount} standing surfaces, {row} rows. Scene was not saved or modified.");
            }
            catch (Exception exception)
            {
                Close();
                Progress("failed", exception.ToString());
                Debug.LogException(exception);
            }
        }

        static void Sample(int gx, int gz, float x, float z)
        {
            int count = snapshotPhysics.Raycast(new Vector3(x, metadata.castTop, z), Vector3.down, Hits,
                metadata.castTop - metadata.castBottom, ~0, QueryTriggerInteraction.Ignore);
            if (count == Hits.Length) throw new InvalidOperationException("Raycast buffer overflow; coverage would be incomplete.");
            Array.Sort(Hits, 0, count, HitHeightComparer.Instance);
            float previous = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (!colliderIds.TryGetValue(hit.collider, out int id)
                    || hit.normal.y < Mathf.Cos(metadata.slope * Mathf.Deg2Rad) || previous - hit.point.y < .08f) continue;
                previous = hit.point.y;
                float radius = metadata.radius - .02f;
                int overlaps = snapshotPhysics.OverlapCapsule(hit.point + Vector3.up * (radius + .025f),
                    hit.point + Vector3.up * (metadata.height - radius + .025f), radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
                if (overlaps == Overlaps.Length) throw new InvalidOperationException("Overlap buffer overflow; coverage would be incomplete.");
                if (overlaps != 0) continue;
                bool nav = NavMesh.SamplePosition(hit.point, out var navHit, .2f, NavMesh.AllAreas)
                    && Mathf.Abs(navHit.position.y - hit.point.y) <= .12f;
                samples.WriteLine(string.Join(",", gx, gz, hit.point.y.ToString("R", Invariant), id,
                    hit.normal.x.ToString("R", Invariant), hit.normal.y.ToString("R", Invariant),
                    hit.normal.z.ToString("R", Invariant), nav ? 1 : 0));
                sampleCount++;
            }
        }

        sealed class HitHeightComparer : IComparer<RaycastHit>
        {
            public static readonly HitHeightComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => b.point.y.CompareTo(a.point.y);
        }
        static void Close()
        {
            EditorApplication.update -= ScanUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            samples?.Dispose();
            samples = null;
        }
        [MenuItem("Tools/FPS/Factory Traversal Audit/Cancel Scan")]
        public static void Cancel()
        {
            if (samples == null) return;
            Close();
            Progress("cancelled");
        }

        [Serializable] public sealed class Probe
        {
            public string name;
            public Vector3[] route;
            public bool jump, singleJump, navWaypoints, walk;
            public int maxTicks;
        }
        [Serializable] public sealed class ProbeBatch { public Probe[] probes; }
        [Serializable] public sealed class ProbeResult
        {
            public string name;
            public bool reached, validStart;
            public int reachedWaypoint, ticks;
            public Vector3 start, settledStart, end, target;
            public float maximumY, minimumY, maximumTickRise;
        }
        [Serializable] public sealed class ProbeResults { public ProbeResult[] results; }

        [Serializable] public sealed class LandingPath
        {
            public string name, status;
            public Vector3 start;
            public Vector3[] corners;
        }
        [Serializable] public sealed class LandingPaths { public LandingPath[] paths; }
        [Serializable] public sealed class PlacementCheck
        {
            public string name;
            public bool floorValid, bodyClear;
            public Vector3 requested, floor;
            public string[] connectedRecoveries;
        }
        [Serializable] public sealed class PlacementChecks { public PlacementCheck[] checks; }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Check Landing Paths and Recovery")]
        public static void CheckLandingPaths()
        {
            var root = Factory();
            Physics.SyncTransforms();
            var paths = new List<LandingPath>();
            foreach (string filename in new[] { "boundary-results.json", "remaining-results.json" })
                foreach (var result in JsonUtility.FromJson<ProbeResults>(File.ReadAllText(System.IO.Path.Combine(Output, filename))).results)
                {
                    if (!result.reached) continue;
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(result.target, new Vector3(100.304f, 2.11f, -78.54f), NavMesh.AllAreas, path);
                    paths.Add(new LandingPath { name = result.name, start = result.target,
                        status = path.status.ToString(), corners = path.corners });
                }
            Save("landing-paths.json", new LandingPaths { paths = paths.ToArray() });
            var chapter = root.GetComponent<CampaignChapterRoot>();
            if (chapter == null) throw new InvalidOperationException("Factory chapter root is missing.");
            var checks = new List<PlacementCheck>();
            foreach (var requested in new[] { new Vector3(15, .08f, -98), new Vector3(15, .12f, -98) })
            {
                bool valid = CampaignPlacement.TryFloor(chapter, requested, out var floor);
                var connected = new List<string>();
                foreach (var recovery in root.GetComponentsInChildren<MapRecoveryPoint>())
                    if (valid && CampaignPlacement.TryFloor(chapter, recovery.transform.position, out var destination)
                        && CampaignPlacement.CompletePath(floor, destination, out _)) connected.Add(Path(recovery.transform));
                checks.Add(new PlacementCheck { name = "L30", requested = requested, floor = floor,
                    floorValid = valid, bodyClear = valid && CampaignPlacement.Clear(floor), connectedRecoveries = connected.ToArray() });
            }
            Save("placement-checks.json", new PlacementChecks { checks = checks.ToArray() });
            Debug.Log($"[FactoryTraversalAudit] Landing paths checked: {paths.Count}; L30 recovery placement checked.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Run Controller Probes")]
        public static void RunProbes()
        {
            if (samples != null) throw new InvalidOperationException("Wait for the surface scan.");
            Factory();
            ExportInventory();
            var batch = JsonUtility.FromJson<ProbeBatch>(File.ReadAllText(System.IO.Path.Combine(Output, "probes.json")));
            var results = new List<ProbeResult>();
            var profile = metadata.players[0]; // All four authored movement/controller profiles match.
            var go = new GameObject("FactoryAudit_ControllerProbe") { hideFlags = HideFlags.HideAndDontSave, layer = profile.layer };
            try
            {
                var controller = go.AddComponent<CharacterController>();
                controller.height = profile.height; controller.radius = profile.radius; controller.center = profile.center;
                controller.stepOffset = profile.step; controller.slopeLimit = profile.slope; controller.skinWidth = profile.skin;
                var movement = go.AddComponent<PlayerMovement>();
                var ground = new GameObject("GroundCheck") { hideFlags = HideFlags.HideAndDontSave };
                ground.transform.SetParent(go.transform, false); ground.transform.localPosition = profile.groundCheck;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Set(string field, object value) => typeof(PlayerMovement).GetField(field, flags).SetValue(movement, value);
                Set("controller", controller); Set("groundCheck", ground.transform);
                Set("jumpHeight", profile.jump); Set("gravityScale", profile.gravity);
                Set("speed", profile.speed); Set("walkMultiplier", profile.walkMultiplier);
                var tick = typeof(PlayerMovement).GetMethod("SimulateTick", flags);
                foreach (var probe in batch.probes)
                {
                    if (probe.route == null || probe.route.Length < 2) throw new InvalidOperationException("Each probe requires a start and target.");
                    controller.enabled = false;
                    go.transform.position = probe.route[0] + Vector3.up * .001f;
                    controller.enabled = true;
                    Physics.SyncTransforms(); Set("verticalVelocity", 0f);
                    var result = new ProbeResult { name = probe.name, start = probe.route[0], target = probe.route[^1], maximumY = go.transform.position.y, minimumY = go.transform.position.y };
                    for (int settle = 0; settle < 60; settle++)
                        tick.Invoke(movement, new object[] { new PlayerInputPayload(), 1f / PlayerMovement.SimulationHz });
                    result.settledStart = go.transform.position;
                    result.validStart = Mathf.Abs(go.transform.position.y - probe.route[0].y) < .2f;
                    if (!result.validStart) { result.end = go.transform.position; results.Add(result); continue; }
                    int waypoint = 1, stuck = 0;
                    Vector3 previous = go.transform.position;
                    for (int t = 0; t < (probe.maxTicks > 0 ? probe.maxTicks : 6000) && waypoint < probe.route.Length; t++)
                    {
                        Vector3 destination = probe.route[waypoint];
                        Vector3 delta = destination - go.transform.position;
                        // NavMesh corners can sit below a sloped collider plus controller skin.
                        // Keep the final landing strict so stacked-floor endpoints cannot pass.
                        float heightTolerance = probe.navWaypoints && waypoint < probe.route.Length - 1 ? .55f : .3f;
                        bool near = new Vector2(delta.x, delta.z).magnitude < .24f && Mathf.Abs(delta.y) < heightTolerance;
                        if (near) { waypoint++; stuck = 0; continue; }
                        var input = new PlayerInputPayload { move = Vector2.ClampMagnitude(new Vector2(delta.x, delta.z) * 6f, 1f),
                            sprint = !probe.walk, jumpPressed = probe.singleJump ? t == 0 : probe.jump && t % 12 == 0 && (delta.y > profile.step || stuck > 12 || t == 0 && probe.name.Contains("hop")), yaw = 0f };
                        tick.Invoke(movement, new object[] { input, 1f / PlayerMovement.SimulationHz });
                        result.ticks++; result.maximumY = Mathf.Max(result.maximumY, go.transform.position.y);
                        result.minimumY = Mathf.Min(result.minimumY, go.transform.position.y);
                        result.maximumTickRise = Mathf.Max(result.maximumTickRise, go.transform.position.y - previous.y);
                        stuck = Vector3.Distance(previous, go.transform.position) < .002f ? stuck + 1 : 0;
                        previous = go.transform.position;
                        if (stuck > 120 || go.transform.position.y < -10) break;
                    }
                    if (waypoint == probe.route.Length)
                        for (int settle = 0; settle < 30; settle++)
                            tick.Invoke(movement, new object[] { new PlayerInputPayload(), 1f / PlayerMovement.SimulationHz });
                    result.reached = waypoint == probe.route.Length && Mathf.Abs(go.transform.position.y - result.target.y) < .3f;
                    result.reachedWaypoint = waypoint - 1;
                    result.end = go.transform.position; results.Add(result);
                }
                Save("controller-probes.json", new ProbeResults { results = results.ToArray() });
                Debug.Log($"[FactoryTraversalAudit] Controller probes: {results.Count(r => r.reached)}/{results.Count} reached targets.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Physics.SyncTransforms(); }
        }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Run Fix Controller Probes")]
        public static void RunFixProbes()
        {
            outputSuffix = "FixEvidence";
            try { RunProbes(); }
            finally { outputSuffix = ""; }
        }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Run Fill Controller Probes")]
        public static void RunFillProbes()
        {
            outputSuffix = "FillEvidence";
            try { RunProbes(); }
            finally { outputSuffix = ""; }
        }

        [MenuItem("Tools/FPS/Factory Traversal Audit/Check Fix Landing Paths")]
        public static void CheckFixLandingPaths()
        {
            var root = Factory();
            var chapter = root.GetComponent<CampaignChapterRoot>();
            string baseline = Output;
            var landings = new[] { "boundary-results.json", "remaining-results.json" }
                .SelectMany(f => JsonUtility.FromJson<ProbeResults>(File.ReadAllText(System.IO.Path.Combine(baseline, f))).results)
                .Where(r => r.reached).ToArray();
            outputSuffix = "FixEvidence";
            try
            {
                var paths = new List<LandingPath>();
                var probes = new List<Probe>();
                var rows = new List<string> { "name,floor,clear,safe,status,x,y,z" };
                Physics.SyncTransforms();
                double start = EditorApplication.timeSinceStartup;
                foreach (var result in landings)
                {
                    Vector3 requested = result.target;
                    // A former pit landing under the new ramp now lands on its upper face.
                    if (requested.x >= 12 && requested.x <= 15 && requested.z >= -101.5f && requested.z <= -94.5f
                        && Physics.Raycast(new Vector3(requested.x, 3, requested.z), Vector3.down, out var hit, 4, ~0, QueryTriggerInteraction.Ignore))
                        requested.y = hit.point.y + .08f;
                    bool valid = CampaignPlacement.TryFloor(chapter, requested, out var floor);
                    bool clear = valid && CampaignPlacement.Clear(floor);
                    bool safe = CampaignPlacement.TrySafeFloor(chapter, requested, 0, out _);
                    var path = new NavMeshPath();
                    if (valid) NavMesh.CalculatePath(floor, new Vector3(100.304f, 2.11f, -78.54f), NavMesh.AllAreas, path);
                    paths.Add(new LandingPath { name = result.name, start = requested, status = valid ? path.status.ToString() : "NoFloor", corners = path.corners });
                    rows.Add(FormattableString.Invariant($"{result.name},{valid},{clear},{safe},{(valid ? path.status.ToString() : "NoFloor")},{requested.x},{requested.y},{requested.z}"));
                    if (valid && path.status == NavMeshPathStatus.PathComplete)
                    {
                        var route = new List<Vector3> { requested }; route.AddRange(path.corners);
                        route.Add(new Vector3(100.304f, 2.11f, -78.54f));
                        probes.Add(new Probe { name = "new_nav_" + result.name, route = route.ToArray(), navWaypoints = true, maxTicks = 15000 });
                    }
                }
                Save("landing-paths.json", new LandingPaths { paths = paths.ToArray() });
                Save("nav-probes.json", new ProbeBatch { probes = probes.ToArray() });
                File.WriteAllLines(System.IO.Path.Combine(Output, "landing-placement.csv"), rows);
                File.WriteAllText(System.IO.Path.Combine(Output, "placement-timing.txt"), $"{landings.Length} floor, clear, safe+path queries: {EditorApplication.timeSinceStartup - start:F3} seconds. Includes JSON/report allocation.\n");
                Debug.Log($"[FactoryTraversalAudit] Fix paths: {probes.Count}/{paths.Count} complete; see landing-placement.csv.");
            }
            finally { outputSuffix = ""; }
        }
    }
}
#endif
