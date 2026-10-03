#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace FPS
{
    /// <summary>Scene-specific authoring, not a runtime procedural room system. Original meshes remain intact.</summary>
    public static class CampaignMorgueAuthoring
    {
        const string Basement = "CampaignWorld/Asylum/Asylum/Basement/";
        const string Folder = "Assets/FPS/Features/World/Content/Campaign/MorgueArena";

        static CampaignMissionController Controller(bool clean = false)
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null
                || c.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity" || clean && c.gameObject.scene.isDirty)
                throw new InvalidOperationException("Open GameScene in Edit Mode; authoring requires a clean scene.");
            return c;
        }

        [MenuItem("Tools/FPS/Campaign/Morgue/Audit")]
        public static void Audit()
        {
            Controller();
            Physics.SyncTransforms();
            foreach (var hit in Physics.OverlapBox(new Vector3(156.6f, -2, 240), new Vector3(.15f, 1.7f, 5), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                Debug.Log($"[MorgueAudit] Expansion boundary: {AnimationUtility.CalculateTransformPath(hit.transform, null)} / {hit.bounds}");
            var spawns = Object.FindFirstObjectByType<DirectorSpawnService>();
            var targets = new[] { new Vector3(152, -3.84f, 239.8f), new Vector3(146.3f, -3.84f, 238.79f) };
            foreach (var target in targets)
            {
                int arenas = 0, routes = 0, hidden = 0;
                string examples = "";
                for (float x = 151; x <= 165; x += 1)
                    for (float z = 237; z <= 243; z += 1)
                    {
                        var p = new Vector3(x, -3.84f, z);
                        if (!DirectorSpawnService.HasTankArenaClearance(p)) continue;
                        arenas++;
                        Debug.Log($"[MorguePath] {p} -> {target}: {RouteObstruction(p, target)}");
                        if (!spawns.HasCampaignSpecialRoute(p, target, true)) continue;
                        routes++;
                        if (Vector3.Distance(p, target) < 10 || !Physics.Linecast(target + Vector3.up * 1.55f,
                            p + Vector3.up * .9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        hidden++;
                        if (hidden <= 5) examples += p + "; ";
                    }
                Debug.Log($"[MorgueAudit] Target {target}: arena={arenas}, route={routes}, hidden>=10m={hidden}: {examples}");
            }
        }

        static string RouteObstruction(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var start, .5f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(to, out var end, .5f, NavMesh.AllAreas)) return "missing floor";
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return path.status.ToString();
            for (int i = 1; i < path.corners.Length; i++)
            {
                var a = path.corners[i - 1]; var b = path.corners[i];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .45f));
                for (int j = 0; j <= steps; j++)
                {
                    var p = Vector3.Lerp(a, b, (float)j / steps);
                    if (Vector3.Distance(p, to) < 2.4f) continue;
                    var hits = Physics.OverlapCapsule(p + Vector3.up * .98f, p + Vector3.up * 1.98f,
                        .9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (hits.Length > 0) return p + " blocked by " + string.Join(", ", hits.Select(h => h.name));
                }
            }
            return "clear";
        }

        [MenuItem("Tools/FPS/Campaign/Morgue/Expand Room")]
        public static void Expand()
        {
            var c = Controller(true);
            if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Morgue variants already exist; refusing to overwrite them.");
            var morgue = GameObject.Find(Basement + "Morgue").transform;
            var hall = GameObject.Find(Basement + "HallUnderground").transform;
            var targets = new[] { morgue, morgue.Find("Morgue_facing"), morgue.Find("Morgue_floor"), morgue.Find("Morgue_roof"), hall };
            if (targets.Any(t => t == null || t.GetComponent<MeshFilter>()?.sharedMesh == null || t.GetComponent<MeshCollider>()?.sharedMesh == null))
                throw new InvalidOperationException("Missing structural source mesh.");
            string[] props = { "DoorD_V1 (1)", "MorgueWheelBed", "MorgueTable", "NeonLamp (68)", "NeonLamp (69)" };
            if (props.Any(name => morgue.Find(name) == null)) throw new InvalidOperationException("Morgue props have changed; audit first.");
            AssetDatabase.CreateFolder("Assets/FPS/Features/World/Content/Campaign", "MorgueArena");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Expand morgue Tank arena");
            int undo = Undo.GetCurrentGroup();
            try
            {
                foreach (var target in targets)
                {
                    var filter = target.GetComponent<MeshFilter>();
                    var collider = target.GetComponent<MeshCollider>();
                    var renderMesh = Variant(filter.sharedMesh, target, target != hall, target.name + "_Arena", true);
                    var collisionMesh = Variant(collider.sharedMesh, target, target != hall, target.name + "_ArenaCollider", false);
                    Undo.RecordObjects(new Object[] { filter, collider }, "Assign arena mesh variants");
                    filter.sharedMesh = renderMesh;
                    collider.sharedMesh = collisionMesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
                var door = morgue.Find(props[0]).gameObject;
                Undo.RecordObject(door, "Retain original morgue doorway disabled");
                door.SetActive(false);
                Move(morgue.Find(props[1]), new Vector3(163, -3.84f, 242));
                Move(morgue.Find(props[2]), new Vector3(164.4f, -3.84f, 236.6f));
                foreach (string name in props.Skip(3))
                {
                    var source = morgue.Find(name);
                    var copy = Object.Instantiate(source.gameObject, morgue);
                    copy.name = name + " Arena";
                    copy.transform.position = source.position + Vector3.right * 9;
                    Undo.RegisterCreatedObjectUndo(copy, "Extend morgue lights");
                }
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Physics.SyncTransforms();
                Undo.CollapseUndoOperations(undo);
                Debug.Log("[MorgueAudit] Room extended 10m east; doorway 4m wide / 3.34m high. Original assets retained. Scene NOT saved; bake and verify next.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw; // Generated assets remain recoverable; no unrelated scene or asset is deleted.
            }
        }

        static void Move(Transform target, Vector3 position)
        {
            Undo.RecordObject(target, "Clear morgue route");
            target.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static Mesh Variant(Mesh source, Transform owner, bool expandEast, string name, bool render)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = name;
            var original = mesh.vertices;
            var vertices = (Vector3[])original.Clone();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = owner.TransformPoint(vertices[i]);
                if (expandEast && p.x > 156) p.x += 10;
                if (p.x > 149.45f && p.x < 149.7f)
                {
                    if (p.z > 236.65f && p.z < 236.85f) p.z = 236.1f;
                    if (p.z > 239 && p.z < 239.25f) p.z = 240.1f;
                    if (p.y > -1.75f && p.y < -1.55f) p.y = -.5f;
                }
                vertices[i] = owner.InverseTransformPoint(p);
            }
            // Extend the existing planar UVs instead of stretching the old tiles over a larger room.
            var uv = mesh.uv;
            var mapped = (Vector2[])uv.Clone();
            var triangles = mesh.triangles;
            var visited = new bool[vertices.Length];
            if (uv.Length == vertices.Length)
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Vector3 u = original[b] - original[a], v = original[c] - original[a];
                    float uu = Vector3.Dot(u, u), uvDot = Vector3.Dot(u, v), vv = Vector3.Dot(v, v), det = uu * vv - uvDot * uvDot;
                    if (Mathf.Abs(det) < .000001f) continue;
                    foreach (int vertex in new[] { a, b, c })
                    {
                        if (visited[vertex]) continue;
                        Vector3 delta = vertices[vertex] - original[a];
                        float du = Vector3.Dot(delta, u), dv = Vector3.Dot(delta, v);
                        mapped[vertex] = uv[a] + (uv[b] - uv[a]) * ((du * vv - dv * uvDot) / det)
                            + (uv[c] - uv[a]) * ((dv * uu - du * uvDot) / det);
                        visited[vertex] = true;
                    }
                }
            mesh.vertices = vertices;
            if (uv.Length == vertices.Length) mesh.uv = mapped;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            if (render) { mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh); }
            AssetDatabase.CreateAsset(mesh, Folder + "/" + name + ".asset");
            return mesh;
        }

        [MenuItem("Tools/FPS/Campaign/Morgue/Bake Asylum Only")]
        public static void Bake()
        {
            var c = Controller();
            if (!AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Expand the room first.");
            var surface = GameObject.Find("CampaignSystems/AsylumNavigation").GetComponent("NavMeshSurface");
            if (surface == null) throw new InvalidOperationException("Asylum NavMeshSurface is missing.");
            var surfaceType = surface.GetType();
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/AsylumNavigation.asset");
            Undo.RecordObject(surface, "Bake expanded Asylum");
            // A volume avoids rebaking the Factory/Lab into the Asylum's surface.
            surfaceType.GetProperty("collectObjects")?.SetValue(surface, Enum.Parse(surfaceType.Assembly.GetType("Unity.AI.Navigation.CollectObjects"), "Volume"));
            surfaceType.GetProperty("center")?.SetValue(surface, new Vector3(130, 2.5f, 234));
            surfaceType.GetProperty("size")?.SetValue(surface, new Vector3(79, 19.1f, 52));
            Physics.SyncTransforms();
            surfaceType.GetMethod("BuildNavMesh")?.Invoke(surface, null);
            var navData = surfaceType.GetProperty("navMeshData")?.GetValue(surface) as NavMeshData;
            if (navData == null) throw new InvalidOperationException("Asylum bake produced no NavMesh data.");
            AssetDatabase.CreateAsset(navData, path);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            Debug.Log("[MorgueAudit] Baked Asylum only to " + path + ". Scene NOT saved; audit paths first.");
        }

        [MenuItem("Tools/FPS/Campaign/Morgue/Configure Concealed Staging")]
        public static void ConfigureStaging()
        {
            var c = Controller();
            var morgue = GameObject.Find(Basement + "Morgue").transform;
            if (!AssetDatabase.GetAssetPath(morgue.GetComponent<MeshFilter>().sharedMesh).StartsWith(Folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Expand the morgue before staging the Tank.");
            var surface = GameObject.Find("CampaignSystems/AsylumNavigation").GetComponent("NavMeshSurface");
            var volumeType = surface.GetType().Assembly.GetType("Unity.AI.Navigation.NavMeshModifierVolume", true);
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Configure concealed morgue Tank staging");
            int group = Undo.GetCurrentGroup();
            try
            {
                // Discard only the redundant shell generated during this authoring pass; the mesh variants already enclose the room.
                foreach (string suffix in new[] { "Floor", "North", "South", "East", "Roof" })
                {
                    var duplicate = morgue.Find("TankArena_Extension" + suffix);
                    if (duplicate != null) Undo.DestroyObjectImmediate(duplicate.gameObject);
                }
                Move(morgue.Find("MorgueWheelBed"), new Vector3(161.5f, -3.84f, 244));
                Move(morgue.Find("MorgueTable"), new Vector3(162, -3.84f, 235.8f));
                Move(GameObject.Find("CampaignWorld/Asylum/CampaignObjectives/AsylumTransfer").transform,
                    new Vector3(151.6f, -3.79f, 241.5f));
                if (morgue.Find("TankArena_ReceivingPartition") == null)
                    CreateShell("TankArena_ReceivingPartition", new Vector3(159, -2.14f, 237.45f),
                        new Vector3(.25f, 3.4f, 5f), morgue.GetComponent<Renderer>().sharedMaterial, morgue);
                if (morgue.Find("TankArena_PartitionTiles") == null)
                {
                    CreateShell("TankArena_PartitionTiles", new Vector3(159, -3.04f, 237.45f),
                        new Vector3(.27f, 1.6f, 5.02f), morgue.Find("Morgue_facing").GetComponent<Renderer>().sharedMaterial, morgue);
                    morgue.Find("TankArena_PartitionTiles").GetComponent<Collider>().enabled = false;
                }
                var clearance = morgue.Find("TankArena_PartitionClearance");
                if (clearance == null)
                {
                    var root = new GameObject("TankArena_PartitionClearance");
                    Undo.RegisterCreatedObjectUndo(root, "Buffer Tank turn around partition");
                    root.transform.SetParent(morgue, false);
                    root.transform.position = new Vector3(159, -2.14f, 237.45f);
                    var volume = root.AddComponent(volumeType);
                    // The shared NavMesh has a smaller agent radius. Reserve extra space only around this staging turn.
                    volumeType.GetProperty("size").SetValue(volume, new Vector3(2.3f, 3.6f, 7.3f));
                    volumeType.GetProperty("area").SetValue(volume, 1); // Unity's built-in Not Walkable area.
                }
                else
                {
                    var volume = clearance.GetComponent(volumeType);
                    Undo.RecordObject(volume, "Reserve full Tank clearance around turn");
                    volumeType.GetProperty("size").SetValue(volume, new Vector3(2.3f, 3.6f, 7.3f));
                }
                foreach (var corner in new[] { ("North", 235.1f, 4f), ("South", 243.1f, 8f) })
                {
                    if (morgue.Find("TankArena_DoorBuffer" + corner.Item1) != null) continue;
                    var root = new GameObject("TankArena_DoorBuffer" + corner.Item1);
                    Undo.RegisterCreatedObjectUndo(root, "Reserve Tank doorway clearance");
                    root.transform.SetParent(morgue, false);
                    root.transform.position = new Vector3(149.6f, -2.14f, corner.Item2);
                    var volume = root.AddComponent(volumeType);
                    volumeType.GetProperty("size").SetValue(volume, new Vector3(2.3f, 3.6f, corner.Item3));
                    volumeType.GetProperty("area").SetValue(volume, 1);
                }
                foreach (string name in new[] { "TankArena_PartitionClearance", "TankArena_DoorBufferNorth", "TankArena_DoorBufferSouth" })
                {
                    var volume = morgue.Find(name).GetComponent(volumeType);
                    Undo.RecordObject(volume, "Align navigation buffer to floor");
                    volumeType.GetProperty("center").SetValue(volume, Vector3.zero);
                }
                Move(morgue.Find("Tank_MorgueArena_1"), new Vector3(164, -3.79f, 238));
                Move(morgue.Find("Tank_MorgueArena_2"), new Vector3(163, -3.79f, 240));
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Physics.SyncTransforms();
                Undo.CollapseUndoOperations(group);
                Debug.Log("[MorgueAudit] Concealed receiving bay configured; redundant temporary shell removed with Undo support. Re-bake and audit before saving.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        [MenuItem("Tools/FPS/Campaign/Morgue/Add Tank Arena Zone")]
        public static void AddTankArenaZone()
        {
            var c = Controller();
            var morgue = GameObject.Find(Basement + "Morgue").transform;
            if (morgue.Find("TankArena_MorgueZone") != null)
                throw new InvalidOperationException("Morgue Tank zone already exists; refusing to duplicate it.");
            if (!AssetDatabase.GetAssetPath(morgue.GetComponent<MeshFilter>().sharedMesh).StartsWith(Folder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("Expand the room before adding its zone.");
            var chapter = c.chapters[(int)CampaignChapter.Asylum];
            var zoneRoot = new GameObject("TankArena_MorgueZone");
            Undo.RegisterCreatedObjectUndo(zoneRoot, "Add morgue Tank zone");
            zoneRoot.transform.SetParent(morgue, false);
            zoneRoot.transform.position = new Vector3(161.7f, -2.0f, 240.4f);
            var box = zoneRoot.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 4f, 11f);
            var zone = zoneRoot.AddComponent<DirectorZone>();
            zone.Configure("Zone_MorgueTankArena", DirectorZoneKind.Playable, box.size);
            Undo.RecordObject(chapter, "Register morgue Tank anchors");
            var anchors = chapter.anchors.ToList();
            foreach (var data in new[] { ("Tank_MorgueArena_1", new Vector3(164, -3.79f, 238)), ("Tank_MorgueArena_2", new Vector3(163, -3.79f, 240)) })
            {
                var root = new GameObject(data.Item1);
                Undo.RegisterCreatedObjectUndo(root, "Add morgue Tank anchor");
                root.transform.SetParent(morgue, false);
                root.transform.position = data.Item2;
                var anchor = root.AddComponent<DirectorSpawnAnchor>();
                anchor.Configure(DirectorSpawnAnchorType.Special, zone);
                anchors.Add(anchor);
            }
            chapter.anchors = anchors.ToArray();
            EditorUtility.SetDirty(chapter);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            Physics.SyncTransforms();
            Debug.Log("[MorgueAudit] Added playable morgue Tank zone and two anchors. Scene NOT saved; rebake and audit routes.");
        }

        static void CreateShell(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = name;
            shell.transform.SetParent(parent, true);
            shell.transform.position = position;
            shell.transform.localScale = scale;
            shell.isStatic = true;
            var renderer = shell.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(shell, "Add morgue arena shell");
        }

        [MenuItem("Tools/FPS/Campaign/Morgue/Finish Partition Materials")]
        public static void FinishPartitionMaterials()
        {
            var c = Controller(true);
            var morgue = GameObject.Find(Basement + "Morgue").transform;
            const string albedoPath = "Assets/FPS/Features/World/Content/AsylumFacility/Textures/Tiled/Walls/Wall_4_Albedo.png";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedo == null) throw new InvalidOperationException("Existing wall albedo is missing.");
            foreach (var name in new[] { "TankArena_ReceivingPartition", "TankArena_PartitionTiles" })
            {
                var renderer = morgue.Find(name)?.GetComponent<Renderer>();
                if (renderer == null) throw new InvalidOperationException("Configure staging before finishing the partition.");
                string path = Folder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(renderer.sharedMaterial) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }
                Undo.RecordObject(material, "Finish partition material variant");
                bool wall = name == "TankArena_ReceivingPartition";
                if (wall) material.mainTexture = albedo;
                material.mainTextureScale = new Vector2(2.5f, wall ? 1.7f : .8f);
                material.color = new Color(.65f, .65f, .65f, 1);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                Undo.RecordObject(renderer, "Assign partition material variant");
                renderer.sharedMaterial = material;
            }
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            Debug.Log("[MorgueAudit] Partition uses local material variants and existing albedo; originals unchanged. Scene NOT saved.");
        }
    }
}
#endif
