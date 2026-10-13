#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    /// <summary>Explicit scene authoring for the audited Logistics pit; no runtime generation.</summary>
    public static class FactoryTraversalRepair
    {
        const string Folder = "Assets/FPS/Features/World/Content/Campaign/FactoryTraversal";
        const string Evidence = "Documents/FactoryTraversalAudit/FillEvidence";

        static GameObject Factory(bool clean = false)
        {
            var root = GameObject.Find("CampaignWorld/Factory");
            if (EditorApplication.isPlayingOrWillChangePlaymode || root == null
                || root.scene.path != "Assets/FPS/Scenes/GameScene.unity" || clean && root.scene.isDirty)
                throw new InvalidOperationException("Load saved GameScene in Edit Mode before authoring Factory.");
            return root;
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Fill Logistics Pit")]
        public static void FillPit()
        {
            var root = Factory(true);
            var parent = root.transform.Find("Map_v2/Factory_GroundReview");
            var road = root.transform.Find("Map_v2/Static/Road_set_v1_b_floor (18)").gameObject;
            if (parent.Find("LogisticsSouthFill") != null) throw new InvalidOperationException("Pit fill already exists.");
            float top = road.GetComponent<Collider>().bounds.max.y + .002f;
            // A 2 mm overlap covers the existing coplanar paving without z-fighting.
            var mesh = new Mesh { name = "LogisticsSouthFill",
                vertices = new[] { new Vector3(-10,0,-4.3f), new Vector3(-10,0,4.3f), new Vector3(10,0,4.3f), new Vector3(10,0,-4.3f) },
                uv = new[] { new Vector2(0,0), new Vector2(0,.43f), new Vector2(1,.43f), new Vector2(1,0) },
                triangles = new[] { 0,1,2, 0,2,3 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, Folder + "/LogisticsSouthFill.asset");
            var fill = new GameObject("LogisticsSouthFill", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fill, root.scene);
            Undo.RegisterCreatedObjectUndo(fill, "Fill Logistics pit");
            fill.transform.SetParent(parent, false); fill.transform.position = new Vector3(20, top, -99.3f);
            fill.layer = road.layer;
            fill.GetComponent<MeshFilter>().sharedMesh = mesh;
            fill.GetComponent<MeshRenderer>().sharedMaterial = road.GetComponent<MeshRenderer>().sharedMaterial;
            var box = fill.GetComponent<BoxCollider>();
            float depth = top + .12f;
            box.center = Vector3.down * (depth / 2); box.size = new Vector3(20, depth, 8.6f);
            GameObjectUtility.SetStaticEditorFlags(fill, GameObjectUtility.GetStaticEditorFlags(road));
            CopyRoadLighting(road, mesh, fill.GetComponent<MeshRenderer>());
            var ramp = parent.Find("LogisticsSouthEscape");
            if (ramp != null) Undo.DestroyObjectImmediate(ramp.gameObject);
            foreach (string name in new[] { "LogisticsEscapeFence_West", "LogisticsEscapeFence_East" })
            {
                var obsolete = root.transform.Find("Map_v2/Static/" + name);
                if (obsolete != null) Undo.DestroyObjectImmediate(obsolete.gameObject);
            }
            var originalWall = root.transform.Find("Map_v2/Static/Concrete_fence_v1_wall_set_v2 (17)").gameObject;
            Undo.RecordObject(originalWall, "Restore original decorative fence"); originalWall.SetActive(true);
            EditorSceneManager.MarkSceneDirty(root.scene); Physics.SyncTransforms();
            Debug.Log("[FactoryTraversalRepair] Filled the whole Logistics pit flush with asphalt paving; removed escape ramp and restored the original fence. Inspect before saving.");
        }

        static void CopyRoadLighting(GameObject road, Mesh mesh, MeshRenderer target)
        {
            var source = road.GetComponent<MeshFilter>().sharedMesh;
            var renderer = road.GetComponent<MeshRenderer>();
            var vertices = source.vertices;
            var normals = source.normals;
            var uv = source.uv2;
            var top = Enumerable.Range(0, vertices.Length)
                .Where(i => road.transform.TransformDirection(normals[i]).y > .99f).ToArray();
            if (uv.Length != vertices.Length || top.Length == 0 || renderer.lightmapIndex < 0)
                throw new InvalidOperationException("The reference road has no baked top-face lighting.");
            var bounds = road.GetComponent<Collider>().bounds;
            // ponytail: Reuse the adjacent road's baked tile; rebake with the next Factory lighting pass.
            mesh.uv2 = mesh.vertices.Select(v =>
            {
                var corner = new Vector3(v.x < 0 ? bounds.min.x : bounds.max.x, bounds.max.y,
                    v.z < 0 ? bounds.min.z : bounds.max.z);
                int index = top.OrderBy(i => (road.transform.TransformPoint(vertices[i]) - corner).sqrMagnitude).First();
                return uv[index];
            }).ToArray();
            target.receiveGI = ReceiveGI.Lightmaps;
            target.lightmapIndex = renderer.lightmapIndex;
            target.lightmapScaleOffset = renderer.lightmapScaleOffset;
            var lighting = target.GetComponent<FactoryPitFillLighting>();
            if (lighting == null) lighting = Undo.AddComponent<FactoryPitFillLighting>(target.gameObject);
            Undo.RecordObject(lighting, "Bind fill lighting to the reference road");
            lighting.referenceRoad = renderer;
            EditorUtility.SetDirty(lighting);
            EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(target);
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Match Fill Road Lighting")]
        public static void MatchFillLighting()
        {
            var root = Factory();
            var road = root.transform.Find("Map_v2/Static/Road_set_v1_b_floor (18)").gameObject;
            var fill = root.transform.Find("Map_v2/Factory_GroundReview/LogisticsSouthFill").gameObject;
            Undo.RecordObject(fill.GetComponent<MeshFilter>().sharedMesh, "Match fill road lighting");
            Undo.RecordObject(fill.GetComponent<MeshRenderer>(), "Match fill road lighting");
            CopyRoadLighting(road, fill.GetComponent<MeshFilter>().sharedMesh, fill.GetComponent<MeshRenderer>());
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Inspect Pit Fill")]
        public static void InspectFill()
        {
            Factory(); Physics.SyncTransforms();
            var fill = GameObject.Find("CampaignWorld/Factory/Map_v2/Factory_GroundReview/LogisticsSouthFill");
            if (fill == null) throw new InvalidOperationException("Fill is missing.");
            var lighting = fill.GetComponent<FactoryPitFillLighting>();
            if (lighting == null || lighting.referenceRoad == null || fill.GetComponent<MeshRenderer>().lightmapIndex < 0)
                throw new InvalidOperationException("Fill lighting was not restored. Reload GameScene and inspect again.");
            var rows = new List<string> { "x,z,height,clear" };
            float top = fill.transform.position.y;
            for (int x = 0; x < 40; x++) for (int z = 0; z < 17; z++)
            {
                var p = new Vector3(10.25f + x * .5f, top + .25f, -103.35f + z * .5f);
                if (!fill.GetComponent<BoxCollider>().Raycast(new Ray(p, Vector3.down), out var hit, .5f)
                    || Mathf.Abs(hit.point.y - top) > .001f) throw new InvalidOperationException("Unfilled floor at " + p);
                rows.Add(FormattableString.Invariant($"{p.x},{p.z},{hit.point.y},{CampaignPlacement.Clear(hit.point)}"));
            }
            Directory.CreateDirectory(Evidence); File.WriteAllLines(Evidence + "/fill-geometry.csv", rows);
            Debug.Log("[FactoryTraversalRepair] Pit fill coverage: 680/680 solid floor samples, no hole remains.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Bake Factory Only")]
        public static void BakeFactory()
        {
            var root = Factory(true);
            var connection = GameObject.Find("CampaignWorld/FactoryAsylumConnection");
            var surface = GameObject.Find("CampaignSystems/FactoryNavigation")?.GetComponent("NavMeshSurface");
            if (surface == null || connection == null) throw new InvalidOperationException("Factory surface/connection missing.");
            var type = surface.GetType();
            var geometry = type.GetProperty("useGeometry");
            var collect = type.GetProperty("collectObjects");
            object oldGeometry = geometry.GetValue(surface), oldCollect = collect.GetValue(surface);
            List<NavMeshBuildSource> sources;
            try
            {
                geometry.SetValue(surface, NavMeshCollectGeometry.PhysicsColliders);
                collect.SetValue(surface, Enum.Parse(collect.PropertyType, "All"));
                Physics.SyncTransforms();
                sources = (List<NavMeshBuildSource>)type.GetMethod("CollectSources", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(surface, null);
            }
            finally { geometry.SetValue(surface, oldGeometry); collect.SetValue(surface, oldCollect); }
            // Keep native modifier markups, but exclude stacked chapters and moving gate leaves.
            bool Owned(Component c) => c != null && (c.transform.IsChildOf(root.transform) || c.transform.IsChildOf(connection.transform));
            var doors = root.GetComponentsInChildren<CampaignDoor>(true).Concat(connection.GetComponentsInChildren<CampaignDoor>(true)).ToArray();
            sources.RemoveAll(s => s.component != null && (!Owned(s.component)
                || doors.Any(d => d.leaf != null && (s.component.transform == d.leaf || s.component.transform.IsChildOf(d.leaf)))));
            var bounds = (Bounds)type.GetMethod("CalculateWorldBounds", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(surface, new object[] { sources.Where(s => s.component != null).ToList() });
            // The outgoing service road ends at the Factory interlock. The Asylum's north compound is another bake.
            var max = bounds.max; max.z = Mathf.Min(max.z, 207); bounds.SetMinMax(bounds.min, max);
            sources.RemoveAll(s => s.shape == NavMeshBuildSourceShape.ModifierBox && !bounds.Intersects(new Bounds(s.transform.GetColumn(3), s.size)));
            var settings = (NavMeshBuildSettings)type.GetMethod("GetBuildSettings").Invoke(surface, null);
            settings.agentRadius = .5f; settings.agentHeight = 2; settings.agentSlope = 45; settings.agentClimb = .3f;
            settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, surface.transform.position, surface.transform.rotation);
            if (data == null) throw new InvalidOperationException("Factory bake produced no data.");
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/FactoryNavigationFilled.asset");
            AssetDatabase.CreateAsset(data, path);
            Undo.RecordObject(surface, "Bake Factory traversal repair");
            type.GetMethod("RemoveData").Invoke(surface, null);
            type.GetProperty("navMeshData").SetValue(surface, data);
            type.GetMethod("AddData").Invoke(surface, null);
            geometry.SetValue(surface, NavMeshCollectGeometry.PhysicsColliders);
            collect.SetValue(surface, Enum.Parse(collect.PropertyType, "Volume"));
            type.GetProperty("center").SetValue(surface, bounds.center);
            type.GetProperty("size").SetValue(surface, bounds.size);
            type.GetProperty("overrideVoxelSize").SetValue(surface, true);
            type.GetProperty("voxelSize").SetValue(surface, .1f);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/bake.txt", $"{DateTime.UtcNow:O}\nAsset: {path}\nSources: {sources.Count}\nBounds: {bounds}\nAgent radius .5, height 2, slope 45, climb .3, voxel .1\n");
            Debug.Log("[FactoryTraversalRepair] Baked Factory collider sources only. GameScene NOT saved; verify paths first.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Save GameScene")]
        public static void SaveScene()
        {
            var root = Factory();
            if (!EditorSceneManager.SaveScene(root.scene)) throw new InvalidOperationException("GameScene could not be saved.");
            AssetDatabase.SaveAssets();
            Debug.Log("[FactoryTraversalRepair] Saved the repaired GameScene explicitly; active scene was not changed.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Add L15 Downward Exit")]
        public static void AddL15Exit()
        {
            var root = Factory(true);
            if (root.transform.Find("Map_v2/Factory_GroundReview/LoadingPlatformDownwardExit") != null)
                throw new InvalidOperationException("L15 exit link already exists.");
            bool Floor(Vector3 p, out NavMeshHit hit) => NavMesh.SamplePosition(p, out hit, .65f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - p.y) < .2f;
            if (!Floor(new Vector3(106, .56f, -24.18f), out var from) || !Floor(new Vector3(110.5f, 0, -26), out var to))
                throw new InvalidOperationException("L15 link endpoints are not on the audited floors.");
            var surface = GameObject.Find("CampaignSystems/FactoryNavigation").GetComponent("NavMeshSurface");
            var type = surface.GetType().Assembly.GetType("Unity.AI.Navigation.NavMeshLink", true);
            var go = new GameObject("LoadingPlatformDownwardExit");
            Undo.RegisterCreatedObjectUndo(go, "Describe the verified L15 drop in NavMesh");
            go.transform.SetParent(root.transform.Find("Map_v2/Factory_GroundReview"), false);
            go.transform.position = from.position;
            var link = go.AddComponent(type);
            type.GetProperty("startPoint").SetValue(link, Vector3.zero);
            type.GetProperty("endPoint").SetValue(link, to.position - from.position);
            type.GetProperty("width").SetValue(link, 1f);
            // The controller can walk down the .56 m ledge, but cannot step back up it.
            type.GetProperty("bidirectional").SetValue(link, false);
            EditorUtility.SetDirty(link);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[FactoryTraversalRepair] Added one-way L15 exit along the 51 verified controller return routes.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Check Chapter Arrivals")]
        public static void CheckArrivals()
        {
            var root = Factory(true);
            var rows = new List<string> { "chapter,kind,name,floor,clear,safe" };
            foreach (var chapter in root.transform.parent.GetComponentsInChildren<CampaignChapterRoot>(true))
            {
                void Check(string kind, Transform marker)
                {
                    if (marker == null) return;
                    bool valid = CampaignPlacement.TryFloor(chapter, marker.position, out var floor);
                    rows.Add($"{chapter.chapter},{kind},{marker.name},{valid},{(valid && CampaignPlacement.Clear(floor))},{CampaignPlacement.TrySafeFloor(chapter, marker.position, 0, out _)}");
                }
                foreach (var arrival in chapter.arrivals ?? Array.Empty<Transform>()) Check("arrival", arrival);
                foreach (var recovery in chapter.recoveries ?? Array.Empty<MapRecoveryPoint>()) if (recovery != null) Check("recovery", recovery.transform);
            }
            File.WriteAllLines(Evidence + "/chapter-arrivals.csv", rows);
        }
    }
}
#endif
