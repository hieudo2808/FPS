using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPS.EditorTools
{
    public static partial class ApprovedPropImplementation
    {
        static readonly string[] RemainingLabTypes = {
            "ReagentCabinet", "ServerRack", "LabStool", "ColdStorage", "LabSink", "Centrifuge", "SpecimenChamber"
        };
        static readonly int[] RemainingLabCounts = { 9, 7, 1, 5, 2, 3, 1 };

        [MenuItem("Tools/FPS/Approved Props/Prepare Final Approved Lab Batch")]
        public static void PrepareRemainingLab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            PrepareModel("LAB_Cabinet", "ReagentCabinet", null, 1.8f, true);
            PrepareModel("LAB_Rack", "ServerRack", null, 2f, true,
                "Assets/ThirdParty/ApprovedProps/LAB_Rack/Models/LAB_Rack.dae");
            PrepareModel("LAB_Stool", "LabStool", null, .65f, true);
            PrepareModel("Asylum", "ColdStorage", null, 1.8f, true,
                "Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Fridge.prefab");
            PrepareModel("Asylum", "LabSink", null, 1.17f, true,
                "Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Sink_V1.prefab");
            PrepareModel("LAB_Centrifuge", "Centrifuge", new[] { "instance_0" }, .6f, true,
                "Assets/ThirdParty/ApprovedProps/LAB_Centrifuge/Models/LAB_Centrifuge.dae");
            PrepareModel("LAB_Cryopod", "SpecimenChamber", null, 2.15f, true,
                "Assets/ThirdParty/ApprovedProps/LAB_Cryopod/Models/LAB_Cryopod_LOD.fbx");
            Debug.Log("Prepared seven approved Lab models; source geometry/materials retained; GameScene untouched.");
        }

        // Editor-only derivative: five material groups instead of 164 tiny renderers. No new geometry.
        static void CombineCentrifugeSource(GameObject source)
        {
            const string directory = "Assets/ThirdParty/ApprovedProps/LAB_Centrifuge/Derived";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets/ThirdParty/ApprovedProps/LAB_Centrifuge", "Derived");
            var filters = source.GetComponentsInChildren<MeshFilter>();
            var groups = filters.SelectMany(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(i => new {
                filter = f, submesh = i, material = f.GetComponent<MeshRenderer>().sharedMaterials[i]
            })).GroupBy(p => p.material).OrderBy(g => g.Key.name).ToArray();
            int originalTriangles = filters.Sum(f => f.sharedMesh.triangles.Length / 3);
            int combinedTriangles = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                var mesh = new Mesh { name = "ProgressTH_Open_" + i, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(groups[i].Select(p => new CombineInstance {
                    mesh = p.filter.sharedMesh, subMeshIndex = p.submesh,
                    transform = source.transform.worldToLocalMatrix * p.filter.transform.localToWorldMatrix
                }).ToArray(), true, true);
                combinedTriangles += mesh.triangles.Length / 3;
                string path = directory + "/Open_" + i + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); AssetDatabase.SaveAssetIfDirty(saved); Object.DestroyImmediate(mesh); }
                var part = new GameObject("SourceMaterial_" + i);
                part.transform.SetParent(source.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = saved;
                part.AddComponent<MeshRenderer>().sharedMaterial = groups[i].Key;
            }
            if (combinedTriangles != originalTriangles || combinedTriangles != 18847)
                throw new InvalidOperationException("Source triangle count changed during mesh combining.");
            // Remove only the temporary original configuration, never project source or scene legacy.
            Object.DestroyImmediate(source.transform.Find("instance_0").gameObject);
        }

        static Transform[] RemainingLabRoots()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("GameScene Edit Mode required.");
            var roots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => RemainingLabTypes.Contains(t.name) && t.Find("Visuals") != null)
                .OrderBy(t => PathOf(t), StringComparer.Ordinal).ThenBy(t => t.position.x).ToArray();
            for (int i = 0; i < RemainingLabTypes.Length; i++)
                if (roots.Count(t => t.name == RemainingLabTypes[i]) != RemainingLabCounts[i])
                    throw new InvalidOperationException("Changed target count: " + RemainingLabTypes[i]);
            return roots;
        }

        [MenuItem("Tools/FPS/Approved Props/Apply Final Approved Lab Batch")]
        public static void ApplyRemainingLab()
        {
            var roots = RemainingLabRoots();
            foreach (string type in RemainingLabTypes) Prepared(type); // Fail before mutating if preparation is incomplete.
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace final 28 approved Lab props");
            try
            {
                foreach (var root in roots)
                {
                    var legacy = root.Find("Visuals");
                    if (legacy.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                        throw new InvalidOperationException("Unexpected behaviour inside legacy visual: " + PathOf(root));
                    var old = RenderBounds(legacy.gameObject);
                    var visual = Instance(Prepared(root.name), root, "Approved_" + root.name);
                    Undo.RecordObject(visual.transform, "Uniformly fit approved source model");
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = Vector3.one;
                    bool reverse = root.name == "ServerRack" || root.name == "ColdStorage" || root.name == "SpecimenChamber";
                    visual.transform.localRotation = Quaternion.Euler(0, reverse ? 180 : 0, 0);
                    var b = ActiveBounds(visual);
                    // Preserve plausible prepared size; only shrink to fit the original envelope, never stretch axes.
                    visual.transform.localScale *= Mathf.Min(1f, old.size.x / b.size.x, old.size.z / b.size.z, old.size.y / b.size.y);
                    b = ActiveBounds(visual);
                    visual.transform.position += new Vector3(old.center.x - b.center.x, old.min.y - b.min.y, old.center.z - b.center.z);
                    DisableVisual(legacy);
                    var box = root.GetComponent<BoxCollider>();
                    if (box != null) { Undo.RecordObject(box, "Retire legacy enclosing collision"); box.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(box); }
                    Physics.SyncTransforms();
                    b = ActiveBounds(visual);
                    var hits = Physics.RaycastAll(new Vector3(b.center.x, old.min.y + .05f, b.center.z), Vector3.down, .15f,
                        ~0, QueryTriggerInteraction.Ignore).Where(h => !h.transform.IsChildOf(root) && h.normal.y > .95f)
                        .OrderByDescending(h => h.point.y).ToArray();
                    if (hits.Length == 0) throw new InvalidOperationException("No nearby real support: " + PathOf(root));
                    visual.transform.position += Vector3.up * (hits[0].point.y - b.min.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                }
                Physics.SyncTransforms();
                ValidateRemainingLab();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(group);
                Debug.Log("STAGED 28 approved Lab props, legacy inactive, stable roots preserved. Scene NOT saved.");
            }
            catch { Undo.RevertAllDownToGroup(group); Physics.SyncTransforms(); throw; }
        }

        [MenuItem("Tools/FPS/Approved Props/Validate Final Approved Lab Batch")]
        public static void ValidateRemainingLab()
        {
            Physics.SyncTransforms();
            foreach (var root in RemainingLabRoots())
            {
                var legacy = root.Find("Visuals");
                var visual = root.Find("Approved_" + root.name);
                if (legacy.gameObject.activeSelf || visual == null || !visual.gameObject.activeInHierarchy
                    || root.GetComponent<BoxCollider>()?.enabled == true)
                    throw new InvalidOperationException("Legacy/replacement state mismatch: " + PathOf(root));
                var old = RenderBounds(legacy.gameObject); var b = ActiveBounds(visual.gameObject);
                if (b.min.x < old.min.x - .005f || b.max.x > old.max.x + .005f || b.min.z < old.min.z - .005f || b.max.z > old.max.z + .005f)
                    throw new InvalidOperationException("Expanded old footprint: " + PathOf(root));
                var scale = visual.localScale;
                if (Mathf.Abs(scale.x - scale.y) > .0001f || Mathf.Abs(scale.x - scale.z) > .0001f)
                    throw new InvalidOperationException("Non-uniform replacement scale: " + PathOf(root));
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    bool sourced = path.StartsWith("Assets/ThirdParty/ApprovedProps/", StringComparison.Ordinal)
                        || path.StartsWith("Assets/FPS/Features/World/Content/AsylumFacility/Models/", StringComparison.Ordinal);
                    var body = filter.GetComponent<MeshCollider>();
                    if (!sourced || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0
                        || body == null || !body.enabled || body.sharedMesh != filter.sharedMesh)
                        throw new InvalidOperationException("Missing sourced mesh/collision: " + PathOf(filter.transform));
                }
                if (visual.GetComponentsInChildren<Transform>(true).Any(t => t.gameObject.layer != root.gameObject.layer)
                    || visual.GetComponentsInChildren<Renderer>().Any(r => !r.enabled || r.sharedMaterials.Any(m => m == null)))
                    throw new InvalidOperationException("Missing material or collision layer changed: " + PathOf(root));
                foreach (float x in new[] { -.7f, .7f })
                    foreach (float z in new[] { -.7f, .7f })
                    {
                        var origin = new Vector3(b.center.x + b.extents.x * x, b.min.y + .012f, b.center.z + b.extents.z * z);
                        if (!Physics.RaycastAll(origin, Vector3.down, .024f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(h => !h.transform.IsChildOf(root) && h.normal.y > .95f))
                            throw new InvalidOperationException("Unsupported base corner: " + PathOf(root));
                    }
            }
            Debug.Log("PASS: final 28 source models, 112 support probes, uniform scales, contained footprints, layers and inactive legacy.");
        }
    }
}
