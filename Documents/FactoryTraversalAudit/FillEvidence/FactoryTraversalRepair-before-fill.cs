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
        const string RampPath = "CampaignWorld/Factory/Map_v2/Factory_GroundReview/LogisticsSouthEscape";
        const string Evidence = "Documents/FactoryTraversalAudit/FixEvidence";

        static GameObject Factory(bool clean = false)
        {
            var root = GameObject.Find("CampaignWorld/Factory");
            if (EditorApplication.isPlayingOrWillChangePlaymode || root == null
                || root.scene.path != "Assets/FPS/Scenes/GameScene.unity" || clean && root.scene.isDirty)
                throw new InvalidOperationException("Load saved GameScene in Edit Mode before authoring Factory.");
            return root;
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Create Logistics Escape")]
        public static void CreateEscape()
        {
            var root = Factory(true);
            if (GameObject.Find(RampPath) != null || AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/LogisticsSouthEscape.asset") != null)
                throw new InvalidOperationException("Escape already exists; inspect it rather than duplicate it.");
            var parent = root.transform.Find("Map_v2/Factory_GroundReview");
            var foundation = root.transform.Find("Map_v2/FactoryRepair_Architecture/BuildingFoundations/Plinth_13_Hangar_v2_outbuilding1");
            var material = foundation?.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (parent == null || material == null) throw new InvalidOperationException("Audited authoring parent/material is missing.");
            Physics.SyncTransforms();
            if (!Physics.Raycast(new Vector3(13.5f, 3, -94), Vector3.down, out var road, 2)
                || Mathf.Abs(road.point.y - 2.092f) > .1f)
                throw new InvalidOperationException("Logistics road height changed; audit before placing the ramp.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/FPS/Features/World/Content/Campaign", "FactoryTraversal");
            var mesh = RampMesh(road.point.y);
            AssetDatabase.CreateAsset(mesh, Folder + "/LogisticsSouthEscape.asset");
            var ramp = new GameObject("LogisticsSouthEscape", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            Undo.RegisterCreatedObjectUndo(ramp, "Create Logistics pit escape");
            ramp.transform.SetParent(parent, false);
            ramp.transform.position = new Vector3(13.5f, 0, -101.5f);
            ramp.layer = foundation.gameObject.layer;
            ramp.GetComponent<MeshFilter>().sharedMesh = mesh;
            ramp.GetComponent<MeshCollider>().sharedMesh = mesh;
            ramp.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(ramp, GameObjectUtility.GetStaticEditorFlags(foundation.gameObject));
            EditorSceneManager.MarkSceneDirty(root.scene);
            Physics.SyncTransforms();
            Debug.Log("[FactoryTraversalRepair] Created 3 m wide Logistics escape. GameScene NOT saved; inspect geometry first.");
        }

        static Mesh RampMesh(float rise, float length = 7)
        {
            var corners = new[] {
                new Vector3(-1.5f, -.12f, 0), new Vector3(1.5f, -.12f, 0),
                new Vector3(1.5f, -.12f, length), new Vector3(-1.5f, -.12f, length),
                new Vector3(-1.5f, 0, 0), new Vector3(1.5f, 0, 0),
                new Vector3(1.5f, rise, length), new Vector3(-1.5f, rise, length) };
            int[] faces = { 4,7,6,5, 0,1,2,3, 1,5,6,2, 0,3,7,4, 0,4,5,1, 3,2,6,7 };
            var vertices = new Vector3[24]; var uv = new Vector2[24]; var triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int v = f * 4, t = f * 6;
                for (int i = 0; i < 4; i++) vertices[v+i] = corners[faces[v+i]];
                float width = Vector3.Distance(vertices[v], vertices[v+3]);
                float faceLength = Vector3.Distance(vertices[v], vertices[v+1]);
                uv[v] = Vector2.zero; uv[v+1] = new Vector2(0, faceLength);
                uv[v+2] = new Vector2(width, faceLength); uv[v+3] = new Vector2(width, 0);
                triangles[t] = v; triangles[t+1] = v+1; triangles[t+2] = v+2;
                triangles[t+3] = v; triangles[t+4] = v+2; triangles[t+5] = v+3;
            }
            var mesh = new Mesh { name = "LogisticsSouthEscape", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Open Escape Landing")]
        public static void OpenLanding()
        {
            var root = Factory();
            var ramp = GameObject.Find(RampPath);
            var wall = root.transform.Find("Map_v2/Static/Concrete_fence_v1_wall_set_v2 (17)").gameObject;
            if (ramp == null || !wall.activeSelf || wall.GetComponentInParent<CampaignDoor>() != null)
                throw new InvalidOperationException("Inspect the existing ramp/decorative wall before editing.");
            float rise = ramp.GetComponent<MeshFilter>().sharedMesh.bounds.max.y;
            var mesh = ramp.GetComponent<MeshFilter>().sharedMesh;
            var replacement = RampMesh(rise, 6);
            Undo.RecordObject(mesh, "Shorten escape incline");
            EditorUtility.CopySerialized(replacement, mesh);
            UnityEngine.Object.DestroyImmediate(replacement);
            ramp.GetComponent<MeshCollider>().sharedMesh = null;
            ramp.GetComponent<MeshCollider>().sharedMesh = mesh;
            var landing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            landing.name = "Landing";
            Undo.RegisterCreatedObjectUndo(landing, "Create escape landing");
            landing.transform.SetParent(ramp.transform, false);
            landing.transform.localPosition = new Vector3(0, rise - .06f, 6.5f);
            landing.transform.localScale = new Vector3(3, .12f, 1);
            landing.layer = ramp.layer;
            landing.GetComponent<MeshRenderer>().sharedMaterial = ramp.GetComponent<MeshRenderer>().sharedMaterial;
            GameObjectUtility.SetStaticEditorFlags(landing, GameObjectUtility.GetStaticEditorFlags(ramp));
            Bounds wallBounds = wall.GetComponent<Collider>().bounds;
            void Section(string name, float min, float max)
            {
                var section = UnityEngine.Object.Instantiate(wall, wall.transform.parent);
                section.name = name;
                Undo.RegisterCreatedObjectUndo(section, "Open decorative fence for escape");
                var scale = section.transform.localScale; scale.z *= (max - min) / wallBounds.size.x;
                section.transform.localScale = scale;
                var position = section.transform.position; position.x = (min + max) / 2;
                section.transform.position = position;
            }
            Section("LogisticsEscapeFence_West", wallBounds.min.x, 11.75f);
            Section("LogisticsEscapeFence_East", 15.25f, wallBounds.max.x);
            Undo.RecordObject(wall, "Replace decorative fence with two sections");
            wall.SetActive(false);
            EditorUtility.SetDirty(mesh);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Physics.SyncTransforms();
            Debug.Log("[FactoryTraversalRepair] Opened 3.5 m decorative fence gap and level landing. Original fence retained inactive.");
        }

        [MenuItem("Tools/FPS/Factory Traversal Repair/Inspect Escape")]
        public static void InspectEscape()
        {
            Factory();
            var ramp = GameObject.Find(RampPath);
            if (ramp == null || ramp.GetComponent<MeshCollider>().sharedMesh != ramp.GetComponent<MeshFilter>().sharedMesh)
                throw new InvalidOperationException("Escape mesh/collider is missing or mismatched.");
            Physics.SyncTransforms();
            var rows = new List<string> { "x,z,height,slope,clear,collider" };
            int blocked = 0;
            for (int x = 0; x < 3; x++)
                for (int z = 0; z <= 32; z++)
                {
                    var origin = new Vector3(12.5f + x, 3, -102 + z * .25f);
                    if (!Physics.Raycast(origin, Vector3.down, out var hit, 4, ~0, QueryTriggerInteraction.Ignore))
                        throw new InvalidOperationException("Missing escape floor at " + origin);
                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    bool clear = CampaignPlacement.Clear(hit.point);
                    string overlaps = string.Join("|", Physics.OverlapCapsule(hit.point + Vector3.up * .57f, hit.point + Vector3.up * 1.55f, .5f, ~0, QueryTriggerInteraction.Ignore).Select(c => c.name));
                    rows.Add(FormattableString.Invariant($"{origin.x},{origin.z},{hit.point.y},{slope},{clear},{hit.collider.name}:{overlaps}"));
                    if (!clear || slope > 45) blocked++;
                }
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Evidence + "/escape-geometry.csv", rows);
            Debug.Log($"[FactoryTraversalRepair] Escape inspected: {99 - blocked}/99 floor/clearance samples passed. See escape-geometry.csv.");
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
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/FactoryNavigation.asset");
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
