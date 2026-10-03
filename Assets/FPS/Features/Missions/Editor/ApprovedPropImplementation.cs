using System;
using System.IO;
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
        [Serializable] private sealed class Audit
        {
            public Supply[] supplies;
            public string[] ammoPickups;
            public string[] weapons;
        }
        [Serializable] private sealed class Supply
        {
            public string id, path, reward, ammoWeapon;
            public bool medicine, choice;
            public int party, ammo;
            public Vector3 position;
        }

        [MenuItem("Tools/FPS/Approved Props/Audit Supplies")]
        public static void AuditSupplies()
        {
            var scene = SceneManager.GetActiveScene();
            var audit = new Audit
            {
                supplies = Object.FindObjectsByType<CampaignSupply>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(s => s.gameObject.scene == scene).OrderBy(s => s.supplyId).Select(s => new Supply
                    {
                        id = s.supplyId, path = PathOf(s.transform), reward = s.survivalReward.ToString(),
                        medicine = s.medicineOnly, choice = s.chooseReward, party = s.minimumPartySize,
                        ammo = s.ammo, ammoWeapon = s.ammoWeapon != null ? s.ammoWeapon.name : null, position = s.transform.position
                    }).ToArray(),
                ammoPickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(p => p.gameObject.scene == scene && p.Type == PickupType.Ammo)
                    .Select(p => PathOf(p.transform)).ToArray(),
                weapons = AssetDatabase.FindAssets("t:WeaponData").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => { var w = AssetDatabase.LoadAssetAtPath<WeaponData>(p); return $"{p} | {w.weaponName} | magazine={w.magazineSize} total={w.totalAmmo} capacity={w.ReserveCapacity}"; }).ToArray()
            };
            File.WriteAllText("Library/ApprovedPropSupplyAudit.json", JsonUtility.ToJson(audit, true));
            Debug.Log($"Approved prop audit: {audit.supplies.Length} supplies, {audit.ammoPickups.Length} ammo pickups. Library/ApprovedPropSupplyAudit.json");
        }

        private static string PathOf(Transform t) => t.parent != null ? PathOf(t.parent) + "/" + t.name : t.name;

        [MenuItem("Tools/FPS/Approved Props/Audit Local Surfaces")]
        public static void AuditLocalSurfaces()
        {
            var targets = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == "LabBench" || t.name == "ElectricalCabinet" || t.name == "PC_Monitor (1)"
                    || PathOf(t).EndsWith("ExamRoom/TableOffice") || PathOf(t).EndsWith("Morgue/TableWhite"));
            File.WriteAllLines("Library/ApprovedPropSurfaces.txt", targets.Select(t =>
                $"{PathOf(t)} | pos={t.position:F3} rot={t.eulerAngles:F2} " + string.Join("; ", t.GetComponentsInChildren<Renderer>().Select(r => $"{r.name} bounds={r.bounds.center:F3} size={r.bounds.size:F3}"))));
            Debug.Log("Local surface audit: Library/ApprovedPropSurfaces.txt");
        }

        [MenuItem("Tools/FPS/Approved Props/Apply Local Props")]
        public static void ApplyLocalProps()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null || c.gameObject.scene.isDirty
                || c.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("Requires clean GameScene in Edit Mode. Save user work explicitly first.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace approved local objective props");
            try
            {
                var pc = Required("CampaignWorld/Asylum/Asylum/1stFloor/GuardRoom/PC_Monitor (1)");
                Bind(c, CampaignObjectiveId.AsylumAccess, pc, pc.GetComponent<Renderer>().bounds.center);
                MovePaper(c, CampaignObjectiveId.AsylumPatient, CampaignFileId.AsylumPatientRecord,
                    Required("CampaignWorld/Asylum/Asylum/2ndFloor/ExamRoom/TableOffice"));
                MovePaper(c, CampaignObjectiveId.AsylumTransfer, CampaignFileId.AsylumMortuaryTransfer,
                    Required("CampaignWorld/Asylum/Asylum/Basement/Morgue/TableWhite"));
                var cabinet = Required("CampaignWorld/Laboratory/Props/D_POWER SYSTEMS/ElectricalCabinet");
                var body = cabinet.GetComponentsInChildren<Collider>().First(b => b.enabled);
                var point = body.bounds.center;
                point.y = cabinet.transform.position.y + 1.25f;
                point.z = body.bounds.max.z + .01f;
                Bind(c, CampaignObjectiveId.LabPower, body.gameObject, point);
                ValidateLocalProps(c);
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("LOCAL_PROPS_APPLIED: AsylumAccess, AsylumPatient, AsylumTransfer, LabPower. Not saved; verify before saving.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static GameObject Required(string path) => GameObject.Find(path) ?? throw new InvalidOperationException("Missing " + path);

        static void MovePaper(CampaignMissionController c, CampaignObjectiveId id, CampaignFileId fileId, GameObject table)
        {
            var source = c.Document(fileId);
            if (source == null || source.pickupVisual == null) throw new InvalidOperationException("Missing authored paper " + fileId);
            var body = table.GetComponent<Collider>();
            var center = body.bounds.center;
            if (!body.Raycast(new Ray(new Vector3(center.x, body.bounds.max.y + .2f, center.z), Vector3.down), out var hit, 2))
                throw new InvalidOperationException("No tabletop " + table.name);
            Undo.RecordObject(source.transform, "Move existing paper onto real desk");
            source.transform.position += hit.point + Vector3.up * .025f - source.Point;
            Bind(c, id, table, source.Point);
        }

        static void Bind(CampaignMissionController c, CampaignObjectiveId id, GameObject visual, Vector3 point, bool nextStep = false)
        {
            var objective = c.Objective(id) ?? throw new InvalidOperationException("Missing objective " + id);
            var body = visual.GetComponent<Collider>() ?? throw new InvalidOperationException("Missing collider " + visual.name);
            var proxy = visual.GetComponent<CampaignInteractionProxy>() ?? Undo.AddComponent<CampaignInteractionProxy>(visual);
            if (!nextStep && proxy.target != null && proxy.target != objective) throw new InvalidOperationException("Occupied interaction proxy " + visual.name);
            if (nextStep && proxy.nextTarget != null && proxy.nextTarget != objective) throw new InvalidOperationException("Occupied next interaction " + visual.name);
            Undo.RecordObject(proxy, "Bind existing prop interaction");
            if (nextStep) proxy.nextTarget = objective; else proxy.target = objective;
            PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
            var anchor = objective.transform.Find("ApprovedInteractionPoint");
            if (anchor == null)
            {
                var go = new GameObject("ApprovedInteractionPoint");
                Undo.RegisterCreatedObjectUndo(go, "Objective anchor");
                go.transform.SetParent(objective.transform, false); anchor = go.transform;
            }
            Undo.RecordObject(anchor, "Place interaction anchor"); anchor.position = point;
            Undo.RecordObject(objective, "Rebind objective collider");
            objective.interactionPoint = anchor; objective.interactionBody = body;
            foreach (string name in new[] { "EquipmentPedestal", "ControlFace" })
            {
                var old = objective.transform.Find(name);
                if (old == null) continue;
                Undo.RecordObject(old.gameObject, "Disable obsolete primitive visual and collision");
                old.gameObject.SetActive(false);
            }
        }

        [MenuItem("Tools/FPS/Approved Props/Verify Local Props")]
        public static void VerifyLocalProps() => ValidateLocalProps(Object.FindFirstObjectByType<CampaignMissionController>());

        [MenuItem("Tools/FPS/Approved Props/Apply Remaining Local Props")]
        public static void ApplyRemainingLocalProps()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null || c.gameObject.scene.isDirty
                || c.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("Requires clean GameScene in Edit Mode.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place approved cabinet, index and archive terminal");
            try
            {
                var install = c.Objective(CampaignObjectiveId.AsylumInstall);
                var cabinet = ExistingPrefab("ApprovedServiceCabinet", install.transform,
                    "Assets/FPS/Features/World/Content/Other_props/Electric_box/Electric_box_v2/Electric_box_v2.prefab");
                Undo.RecordObject(cabinet.transform, "Place cabinet against existing wall");
                cabinet.transform.SetPositionAndRotation(new Vector3(141, -2.34f, 238.01f), Quaternion.identity);
                var bounds = RenderBounds(cabinet);
                var point = new Vector3(bounds.center.x, -2.45f, bounds.max.z + .01f);
                Bind(c, CampaignObjectiveId.AsylumInstall, cabinet, point);
                Bind(c, CampaignObjectiveId.AsylumPower, cabinet, point, true);

                var benches = c.chapters[(int)CampaignChapter.Laboratory].GetComponentsInChildren<Transform>()
                    .Where(t => t.name == "LabBench").ToArray();
                var indexBench = benches.OrderBy(t => (t.position - new Vector3(219.09f, -18, 250.49f)).sqrMagnitude).First();
                var archiveBench = benches.OrderBy(t => (t.position - new Vector3(216.99f, -18, 297.89f)).sqrMagnitude).First();
                var indexSurface = BenchSurface(indexBench.gameObject);
                var archiveSurface = BenchSurface(archiveBench.gameObject);
                var indexFile = c.Document(CampaignFileId.LabIndex);
                Undo.RecordObject(indexFile.transform, "Place index on clear desktop beside the sink");
                indexFile.transform.position += SurfacePoint(indexSurface, indexBench.position + new Vector3(-1f, 0, -.1f))
                    + Vector3.up * .025f - indexFile.Point;
                Bind(c, CampaignObjectiveId.LabIndex, indexSurface.gameObject, indexFile.Point);

                var archive = c.Objective(CampaignObjectiveId.LabArchive);
                var pc = ExistingPrefab("ApprovedArchivePC", archive.transform,
                    "Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/PC_Monitor.prefab");
                var surfacePoint = SurfacePoint(archiveSurface, archiveBench.position + new Vector3(.95f, 0, 0));
                Undo.RecordObject(pc.transform, "Place archive terminal on existing bench");
                pc.transform.SetPositionAndRotation(surfacePoint, Quaternion.identity);
                var pcBounds = RenderBounds(pc);
                pc.transform.position += Vector3.up * (surfacePoint.y - pcBounds.min.y);
                pcBounds = RenderBounds(pc);
                Bind(c, CampaignObjectiveId.LabArchive, pc, pcBounds.center);
                var keyboard = ExistingPrefab("ApprovedArchiveKeyboard", archive.transform,
                    "Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Keyboard.prefab");
                Undo.RecordObject(keyboard.transform, "Place existing keyboard beside the archive terminal");
                var keyPoint = SurfacePoint(archiveSurface, archiveBench.position + new Vector3(.95f, 0, -.3f));
                keyboard.transform.SetPositionAndRotation(keyPoint, Quaternion.identity);
                keyboard.transform.position += Vector3.up * (keyPoint.y - RenderBounds(keyboard).min.y);
                var file = c.Document(CampaignFileId.LabOriginalArchive);
                Undo.RecordObject(file.transform, "Keep physical archive transcript on same desk");
                file.transform.position += SurfacePoint(archiveSurface, archiveBench.position + new Vector3(-1f, 0, -.1f))
                    + Vector3.up * .025f - file.Point;
                foreach (var item in new[] { cabinet, pc, keyboard }) PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                Physics.SyncTransforms();
                VerifyRemainingLocalProps();
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("REMAINING_LOCAL_PROPS_APPLIED. Verify visuals before saving.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static GameObject ExistingPrefab(string name, Transform parent, string path)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing approved asset " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate approved prop");
            instance.name = name;
            return instance;
        }

        static MeshCollider BenchSurface(GameObject bench)
        {
            var filter = bench.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Missing bench mesh");
            var box = bench.GetComponent<BoxCollider>();
            if (box != null) { Undo.RecordObject(box, "Use actual desktop geometry"); box.enabled = false; }
            var surface = filter.GetComponent<MeshCollider>();
            if (surface == null) surface = Undo.AddComponent<MeshCollider>(filter.gameObject);
            Undo.RecordObject(surface, "Use existing mesh for static surface collision");
            surface.sharedMesh = filter.sharedMesh;
            surface.convex = false;
            surface.enabled = true;
            Physics.SyncTransforms();
            return surface;
        }

        static Vector3 SurfacePoint(Collider surface, Vector3 position)
        {
            var origin = new Vector3(position.x, surface.bounds.max.y + .1f, position.z);
            float distance = surface.bounds.size.y + .2f;
            if (surface.Raycast(new Ray(origin, Vector3.down), out var hit, distance) && hit.normal.y >= .98f)
                return hit.point;
            // PhysX can miss an exact triangle seam at these world coordinates.
            // Require all four neighbours, within 2 mm, to agree on the flat height.
            float height = 0f;
            var offsets = new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1),
                new Vector3(1, 0, -1), new Vector3(-1, 0, -1) };
            for (int i = 0; i < offsets.Length; i++)
            {
                if (!surface.Raycast(new Ray(origin + offsets[i] * .002f, Vector3.down), out hit, distance)
                    || hit.normal.y < .98f || (i > 0 && Mathf.Abs(hit.point.y - height) > .001f))
                    throw new InvalidOperationException("No flat desk at " + position);
                height = hit.point.y;
            }
            return new Vector3(position.x, height, position.z);
        }

        [MenuItem("Tools/FPS/Approved Props/Verify Remaining Local Props")]
        public static void VerifyRemainingLocalProps()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            Physics.SyncTransforms();
            foreach (var id in new[] { CampaignObjectiveId.AsylumInstall, CampaignObjectiveId.AsylumPower,
                CampaignObjectiveId.LabIndex, CampaignObjectiveId.LabArchive })
            {
                var o = c.Objective(id);
                var proxy = o.interactionBody != null ? o.interactionBody.GetComponent<CampaignInteractionProxy>() : null;
                if (proxy == null || (proxy.target != o && proxy.nextTarget != o)
                    || !o.interactionBody.enabled || !o.interactionBody.gameObject.activeInHierarchy
                    || o.transform.Find("EquipmentPedestal").gameObject.activeSelf
                    || o.transform.Find("ControlFace").gameObject.activeSelf)
                    throw new InvalidOperationException("Broken replacement " + id);
                if (!CampaignFileAuthoring.TryReader(c.chapters[(int)CampaignRules.ChapterOf(id)], o.Point,
                    c.settings.interactionRange, o, out var reader))
                    throw new InvalidOperationException("No reachable reader for " + id + " at " + o.Point);
                Debug.Log($"APPROVED_PROP_VERIFIED {id}: point={o.Point:F3} reader={reader:F3}");
            }
        }

        [MenuItem("Tools/FPS/Approved Props/Apply Evidence Cases")]
        public static void ApplyEvidenceCases()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null || c.gameObject.scene.isDirty
                || c.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("Requires clean GameScene in Edit Mode. Save user work explicitly first.");
            var evidence = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/ApprovedProps/RPaciorekHardCases/EvidenceCase.fbx");
            if (evidence == null) throw new InvalidOperationException("Imported EvidenceCase.fbx is missing.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Place approved evidence cases");
            try
            {
                PlaceCase(c.Objective(CampaignObjectiveId.FactoryCase), evidence, false);
                PlaceCase(c.Objective(CampaignObjectiveId.LabCase), evidence, true);
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("EVIDENCE_CASES_APPLIED: Factory/Lab case visuals use RPaciorek authored FBX. Not saved; verify before saving.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        [MenuItem("Tools/FPS/Approved Props/Apply Equipment Cases")]
        public static void ApplyEquipmentCases()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null || c.gameObject.scene.isDirty
                || c.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("Requires clean GameScene in Edit Mode. Save user work explicitly first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/ApprovedProps/RPaciorekHardCases/EquipmentCase.fbx");
            if (prefab == null) throw new InvalidOperationException("Imported EquipmentCase.fbx is missing.");
            var oldCases = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == "EquipmentCase" && t.gameObject.scene == c.gameObject.scene).ToArray();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Replace approved equipment case visuals");
            try
            {
                int count = 0;
                foreach (var old in oldCases)
                {
                    if (old.parent == null) continue;
                    var existing = old.parent.Find("ApprovedEquipmentCase");
                    var instance = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.parent);
                    if (existing == null) Undo.RegisterCreatedObjectUndo(instance, "Approved equipment case");
                    instance.name = "ApprovedEquipmentCase";
                    Undo.RecordObject(instance.transform, "Fit imported case to original footprint");
                    instance.transform.SetPositionAndRotation(old.position, old.rotation * Quaternion.Euler(0, 90, 0));
                    instance.transform.localScale = Vector3.one;
                    var original = old.GetComponent<Renderer>().bounds;
                    var bounds = RenderBounds(instance);
                    // FBX source units/pivots differ between models; fit measured bounds, never a shared x100 guess.
                    float scale = Mathf.Min(original.size.x / bounds.size.x, original.size.z / bounds.size.z);
                    instance.transform.localScale *= scale;
                    bounds = RenderBounds(instance);
                    instance.transform.position += new Vector3(original.center.x - bounds.center.x,
                        original.min.y - bounds.min.y, original.center.z - bounds.center.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    Undo.RecordObject(old.gameObject, "Disable obsolete case visual"); old.gameObject.SetActive(false);
                    var trim = old.parent.Find("CaseTrim");
                    if (trim != null) { Undo.RecordObject(trim.gameObject, "Disable obsolete trim"); trim.gameObject.SetActive(false); }
                    count++;
                }
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log($"EQUIPMENT_CASES_APPLIED: {count} supply case visuals use RPaciorek authored FBX. Not saved; verify before saving.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        public static Bounds RenderBounds(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Missing render mesh: " + item.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void PlaceCase(CampaignInteractable objective, GameObject prefab, bool lab)
        {
            if (objective == null) throw new InvalidOperationException("Missing evidence case objective");
            string name = lab ? "EvidenceCase_Lab" : "EvidenceCase_Factory";
            var existing = objective.transform.Find(name);
            if (existing != null) { FitCaseCollider(existing.gameObject); return; }
            var old = objective.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.transform != objective.transform);
            var position = old != null ? old.bounds.center : objective.transform.position + Vector3.up * (lab ? .85f : .35f);
            if (lab) position.y = objective.transform.position.y + .85f;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, objective.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Approved evidence case");
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = objective.transform.rotation;
            instance.transform.localScale = Vector3.one * 100f;
            var collider = FitCaseCollider(instance);
            var proxy = Undo.AddComponent<CampaignInteractionProxy>(instance); proxy.target = objective;
            Undo.RecordObject(objective, "Bind evidence case");
            objective.interactionBody = collider;
            objective.interactionPoint = instance.transform;
            foreach (var child in objective.transform.Cast<Transform>().Where(t => t != instance.transform && (t.name == "EquipmentPedestal" || t.name == "ControlFace" || t.name == "AuthoredCounterSample")))
            {
                Undo.RecordObject(child.gameObject, "Disable obsolete case visual"); child.gameObject.SetActive(false);
            }
        }

        [MenuItem("Tools/FPS/Approved Props/Inspect Pending Case Placement")]
        public static void InspectPendingCasePlacement()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            foreach (var id in new[] { CampaignObjectiveId.FactoryCase, CampaignObjectiveId.LabCase })
            {
                var objective = c.Objective(id);
                var visual = objective.transform.Find(id == CampaignObjectiveId.LabCase ? "EvidenceCase_Lab" : "EvidenceCase_Factory");
                var r = visual.GetComponent<Renderer>();
                Debug.Log($"CASE_BOUNDS {id} center={r.bounds.center:F3} size={r.bounds.size:F3}");
            }
            Capture("Lab-bench-before", new Vector3(217,-16.3f,295.4f), new Vector3(217,-17,297.9f));
            Capture("Asylum-PC-after", new Vector3(129.7f,1.65f,232.2f), new Vector3(131.45f,1.06f,230.75f));
        }

        [MenuItem("Tools/FPS/Approved Props/Normalize Evidence Cases")]
        public static void NormalizeEvidenceCases()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("Requires clean GameScene in Edit Mode.");
            var cases = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene == scene && (t.name == "EvidenceCase_Lab" || t.name == "EvidenceCase_Factory")).ToArray();
            if (cases.Length != 2) throw new InvalidOperationException("Expected two approved evidence cases.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair evidence case collision and support");
            try
            {
                foreach (var item in cases)
                {
                    Undo.RecordObject(item, "Normalize imported case scale");
                    item.localScale = Vector3.one;
                    var bounds = RenderBounds(item.gameObject);
                    item.localScale *= .62f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    FitCaseCollider(item.gameObject);
                }
                Physics.SyncTransforms();
                foreach (var item in cases)
                {
                    var bounds = RenderBounds(item.gameObject);
                    Vector3 support;
                    if (item.name == "EvidenceCase_Lab")
                    {
                        var bench = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t =>
                            t.name == "LabBench" && Vector3.Distance(t.position, new Vector3(216.99f, -18, 297.89f)) < .1f);
                        var hits = Physics.RaycastAll(bench.position + new Vector3(-1.6f, 2, 0), Vector3.down, 2.5f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Where(h => !h.transform.IsChildOf(item) && h.normal.y > .98f).OrderBy(h => h.distance).ToArray();
                        if (hits.Length == 0) throw new InvalidOperationException("No floor beside archive bench.");
                        support = hits[0].point;
                    }
                    else
                    {
                        var hits = Physics.RaycastAll(bounds.center, Vector3.down, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Where(h => !h.transform.IsChildOf(item) && h.normal.y > .98f).OrderBy(h => h.distance).ToArray();
                        if (hits.Length == 0) throw new InvalidOperationException("No support under Factory case.");
                        support = hits[0].point;
                    }
                    item.position += new Vector3(support.x - bounds.center.x, support.y - bounds.min.y, support.z - bounds.center.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                    var objective = item.GetComponentInParent<CampaignInteractable>();
                    Bind(Object.FindFirstObjectByType<CampaignMissionController>(), objective.objectiveId,
                        item.gameObject, RenderBounds(item.gameObject).center);
                }
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("EVIDENCE_COLLIDERS_REPAIRED: two mesh-sized colliders; cases placed on real support. Not saved.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static BoxCollider FitCaseCollider(GameObject item)
        {
            var body = item.GetComponent<BoxCollider>();
            if (body == null) body = Undo.AddComponent<BoxCollider>(item);
            Undo.RecordObject(body, "Fit evidence collider to authored mesh");
            var bounds = item.GetComponent<MeshFilter>().sharedMesh.bounds;
            body.center = bounds.center;
            body.size = bounds.size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            EditorUtility.SetDirty(body);
            return body;
        }

        static void Capture(string name, Vector3 eye, Vector3 target)
        {
            var go = new GameObject("ApprovedPropVerificationCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target-eye));
            camera.fieldOfView = 60; camera.nearClipPlane = .03f; camera.farClipPlane = 100;
            var rt = new RenderTexture(1280,720,24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                File.WriteAllBytes("Documents/UIUX/Prop-Approval-2026-09-27/"+name+".png",texture.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; camera.targetTexture=null; Object.DestroyImmediate(texture); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        }

        static void ValidateLocalProps(CampaignMissionController c)
        {
            Physics.SyncTransforms();
            foreach (var id in new[] { CampaignObjectiveId.AsylumAccess, CampaignObjectiveId.AsylumPatient,
                CampaignObjectiveId.AsylumTransfer, CampaignObjectiveId.LabPower })
            {
                var objective = c.Objective(id);
                if (objective.interactionBody == null || !objective.interactionBody.enabled
                    || !objective.interactionBody.gameObject.activeInHierarchy
                    || objective.interactionBody.GetComponent<CampaignInteractionProxy>()?.target != objective)
                    throw new InvalidOperationException("Broken prop binding " + id);
                foreach (string name in new[] { "EquipmentPedestal", "ControlFace" })
                    if (objective.transform.Find(name)?.gameObject.activeSelf == true)
                        throw new InvalidOperationException("Obsolete primitive remains active: " + id + "/" + name);
                if (!CampaignFileAuthoring.TryReader(c.chapters[(int)CampaignRules.ChapterOf(id)], objective.Point,
                    c.settings.interactionRange, objective, out var reader))
                    throw new InvalidOperationException("Prop lacks a clear reachable reader position: " + id);
                Debug.Log($"LOCAL_PROP_VERIFIED {id}: point={objective.Point:F3} reader={reader:F3} body={PathOf(objective.interactionBody.transform)}");
            }
        }
    }
}
