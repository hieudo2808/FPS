#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS
{
    /// <summary>One-time placement on existing scene surfaces. No runtime placement or second interaction system.</summary>
    public static class CampaignFileAuthoring
    {
        const string PaperPath = "Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Paper.prefab";
        static readonly CampaignObjectiveId[] OptionalLocations =
        {
            CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryCase,
            CampaignObjectiveId.AsylumAccess, CampaignObjectiveId.AsylumPatient, CampaignObjectiveId.AsylumLift, CampaignObjectiveId.AsylumTransfer,
            CampaignObjectiveId.LabTrace, CampaignObjectiveId.LabIndex, CampaignObjectiveId.LabPower, CampaignObjectiveId.LabTransmit, CampaignObjectiveId.LabArchive
        };

        sealed class Placement
        {
            public CampaignFileDefinition file;
            public CampaignInteractable existing;
            public CampaignInteractable seed;
            public Vector3 point;
            public Vector3 reader;
            public string support;
        }

        static CampaignMissionController Controller(bool clean)
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null
                || c.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity" || clean && c.gameObject.scene.isDirty)
                throw new InvalidOperationException("Open GameScene in Edit Mode; authoring requires a clean scene.");
            return c;
        }

        [MenuItem("Tools/FPS/Campaign/Files/Audit Placement")]
        public static void Audit()
        {
            var c = Controller(false);
            var paper = AssetDatabase.LoadAssetAtPath<GameObject>(PaperPath);
            if (paper == null) throw new InvalidOperationException("Existing paper prefab is missing.");
            Debug.Log("[CampaignFiles] Paper render bounds: " + string.Join("; ", paper.GetComponentsInChildren<Renderer>().Select(r => r.bounds.ToString())));
            foreach (var p in Plan(c))
                Debug.Log($"[CampaignFiles] {p.file.id}: {p.point} on {p.support}; reader={p.reader}; seed={p.seed.objectiveId}");
        }

        static List<Placement> Plan(CampaignMissionController c)
        {
            Physics.SyncTransforms();
            var plans = new List<Placement>();
            var used = c.fileSources.Where(s => s != null && s.pickupVisual != null).Select(s => s.Point).ToList();
            foreach (var file in c.ContentCatalog.files.OrderBy(f => f.order).ThenBy(f => f.id))
            {
                var existing = c.Document(file.id);
                if (existing != null && existing.pickupVisual != null) continue;
                if (file.required && existing == null)
                    throw new InvalidOperationException("Required document registration is missing: " + file.id);
                var seed = existing != null ? existing.GetComponentsInParent<CampaignInteractable>().FirstOrDefault(s => !s.documentOnly)
                    : c.Objective(OptionalLocations[(int)file.id - 10]);
                if (seed == null) throw new InvalidOperationException("Missing story location for " + file.id);
                var chapter = c.chapters[(int)file.chapter];
                Placement best = null;
                float bestScore = float.MaxValue;
                Vector3 origin = seed.transform.position;
                for (int x = -8; x <= 8; x++)
                    for (int z = -8; z <= 8; z++)
                    {
                        var ray = origin + new Vector3(x * .5f, 2.4f, z * .5f);
                        if (!Physics.Raycast(ray, Vector3.down, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            || hit.normal.y < .98f || hit.collider.GetComponentInParent<CampaignChapterRoot>() != chapter
                            || hit.collider.GetComponentInParent<CampaignInteractable>()?.documentOnly == true) continue;
                        Vector3 point = hit.point + Vector3.up * .025f;
                        if (used.Any(p => Vector3.Distance(point, p) < .85f)) continue;
                        // Reject a ledge smaller than the paper's authored 32 x 42 cm footprint.
                        bool supported = true;
                        foreach (var offset in new[] { new Vector3(.17f, 0, .22f), new Vector3(-.17f, 0, .22f), new Vector3(.17f, 0, -.22f), new Vector3(-.17f, 0, -.22f) })
                            if (!Physics.Raycast(point + offset + Vector3.up * .1f, Vector3.down, out var edge, .2f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || Mathf.Abs(edge.point.y - hit.point.y) > .025f) supported = false;
                        if (!supported || !TryReader(chapter, point, c.settings.interactionRange, null, out var reader)) continue;
                        float height = point.y - reader.y;
                        float score = Vector3.Distance(origin, point) + (height < .4f ? 8f : 0f);
                        if (score >= bestScore) continue;
                        bestScore = score;
                        best = new Placement { file = file, existing = existing, seed = seed, point = point, reader = reader, support = hit.collider.name };
                    }
                if (best == null) throw new InvalidOperationException("No supported, reachable paper location for " + file.id);
                used.Add(best.point);
                plans.Add(best);
            }
            return plans;
        }

        public static bool TryReader(CampaignChapterRoot chapter, Vector3 point, float range, CampaignInteractable source, out Vector3 reader)
        {
            for (float height = .1f; height <= 1.6f; height += .3f)
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8;
                    var candidate = point + new Vector3(Mathf.Cos(angle) * 1.1f, -height, Mathf.Sin(angle) * 1.1f);
                    if (!CampaignPlacement.TryFloor(chapter, candidate, out var floor) || !CampaignPlacement.Clear(floor)) continue;
                    Vector3 eye = floor + Vector3.up * 1.55f;
                    if (Vector3.Distance(eye, point) > range) continue;
                    if (Physics.Linecast(eye, point, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        && (source == null || !source.OwnsCollider(hit.collider))) continue;
                    reader = floor;
                    return true;
                }
            reader = default;
            return false;
        }

        [MenuItem("Tools/FPS/Campaign/Files/Align Lab Papers To Desktops")]
        public static void AlignLabPapers()
        {
            var c = Controller(true);
            var ids = new[] { CampaignFileId.LabPowerRecord, CampaignFileId.OptionalLab03, CampaignFileId.OptionalLab01 };
            var positions = new[] { new Vector3(238.84f, -18, 280.885f), new Vector3(237.54f, -18, 280.885f), new Vector3(178.34f, -18, 243.985f) };
            var desks = c.chapters[(int)CampaignChapter.Laboratory].GetComponentsInChildren<BoxCollider>()
                .Where(b => b.name == "ControlDesk").ToArray();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Align Lab papers to real desk surfaces");
            int group = Undo.GetCurrentGroup();
            try
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    var source = c.Document(ids[i]);
                    if (source?.pickupVisual == null) throw new InvalidOperationException("Place document visuals first.");
                    var desk = desks.OrderBy(b => Vector3.Distance(b.transform.position, positions[i])).First();
                    var filter = desk.GetComponentInChildren<MeshFilter>();
                    if (filter?.sharedMesh == null) throw new InvalidOperationException("Control desk render mesh is missing.");
                    // The original single box encloses the monitor and empty air above the desktop.
                    // Use the existing static render mesh for just these two reading desks, not a global collider replacement.
                    Undo.RecordObject(desk, "Retain coarse desk collider disabled");
                    desk.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(desk);
                    var surface = filter.GetComponent<MeshCollider>();
                    if (surface == null) surface = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    Undo.RecordObject(surface, "Use visible desk geometry");
                    surface.sharedMesh = filter.sharedMesh;
                    surface.convex = false;
                    surface.enabled = true;
                    Physics.SyncTransforms();
                    var ray = new Ray(positions[i] + Vector3.up * 2, Vector3.down);
                    if (!surface.Raycast(ray, out var hit, 2) || hit.normal.y < .98f)
                        throw new InvalidOperationException("No flat desktop at " + positions[i]);
                    Undo.RecordObject(source.transform, "Set paper on actual desktop");
                    source.transform.position = hit.point + Vector3.up * .025f;
                    Physics.SyncTransforms();
                    if (!TryReader(c.chapters[(int)CampaignChapter.Laboratory], source.Point, c.settings.interactionRange, source, out _))
                        throw new InvalidOperationException("Desktop file is not reachable: " + ids[i]);
                    Debug.Log($"[CampaignFiles] Aligned {ids[i]} to visible desktop at {source.Point}");
                }
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        [MenuItem("Tools/FPS/Campaign/Files/Place Missing Documents")]
        public static void PlaceMissingDocuments()
        {
            var c = Controller(true);
            var paper = AssetDatabase.LoadAssetAtPath<GameObject>(PaperPath);
            if (paper == null) throw new InvalidOperationException("Existing paper prefab is missing.");
            var plans = Plan(c); // Validate the entire batch before touching the scene.
            if (plans.Count == 0) { Debug.Log("[CampaignFiles] All documents already have visuals; scene unchanged."); return; }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Place campaign documents");
            int group = Undo.GetCurrentGroup();
            try
            {
                Undo.RecordObject(c, "Register campaign documents");
                var sources = c.fileSources.ToList();
                foreach (var p in plans)
                {
                    var source = p.existing;
                    if (source == null)
                    {
                        var root = new GameObject("FILE_" + p.file.id);
                        Undo.RegisterCreatedObjectUndo(root, "Place optional document");
                        root.transform.SetParent(p.seed.transform.parent, false);
                        source = root.AddComponent<CampaignInteractable>();
                        source.documentOnly = true;
                        source.fileId = p.file.id;
                        source.holdSeconds = 0;
                        sources.Add(source);
                    }
                    Undo.RecordObjects(new Object[] { source, source.transform }, "Present campaign document");
                    source.transform.SetPositionAndRotation(p.point, Quaternion.identity);
                    source.transform.localScale = Vector3.one;
                    source.displayName = p.file.title;
                    source.interactionPoint = source.transform;
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(paper, source.transform);
                    Undo.RegisterCreatedObjectUndo(visual, "Use existing paper art");
                    visual.name = "PaperVisual";
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    foreach (var collider in visual.GetComponentsInChildren<Collider>())
                    {
                        collider.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    }
                    var renderer = visual.GetComponentInChildren<Renderer>();
                    if (renderer == null) throw new InvalidOperationException("Paper prefab has no renderer.");
                    float extent = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
                    visual.transform.localScale *= .42f / extent;
                    // Imported art may have an offset pivot. Center the visible sheet over its support.
                    var bounds = renderer.bounds;
                    visual.transform.position += p.point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                    source.pickupVisual = visual;
                    var body = source.GetComponent<BoxCollider>();
                    if (body == null) body = Undo.AddComponent<BoxCollider>(source.gameObject);
                    Undo.RecordObject(body, "Match paper interaction bounds");
                    body.center = new Vector3(0, .02f, 0);
                    body.size = new Vector3(.36f, .06f, .46f);
                    body.isTrigger = false;
                    body.enabled = true;
                    source.interactionBody = body;
                    Debug.Log($"[CampaignFiles] Placed {p.file.id} at {p.point} on {p.support}");
                }
                c.fileSources = sources.ToArray();
                Physics.SyncTransforms();
                foreach (var source in c.fileSources)
                {
                    var file = c.ContentCatalog.FindFile(source.fileId);
                    if (!TryReader(c.chapters[(int)file.chapter], source.Point, c.settings.interactionRange, source, out _))
                        throw new InvalidOperationException("Paper collider blocked reader access: " + source.fileId);
                }
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log($"[CampaignFiles] {plans.Count} sources authored; total={sources.Count}. Scene NOT saved; verify before saving.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}
#endif
