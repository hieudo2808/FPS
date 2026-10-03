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
    // Explicit authoring for the approved campaign slice, not a runtime prop framework.
    public static partial class ApprovedPropImplementation
    {
        public const string PreparedProps = "Assets/FPS/Features/Missions/Content/ApprovedProps";
        static readonly string[] ItemCodes = { "G1", "G2", "M1_S1", "M2" };
        static readonly string[] AmmoNames = { "Classic", "Vandal", "Bucky", "Operator", "Odin" };
        static readonly string[,] AmmoStations = {
            { "Classic", "Operator", "Vandal" },
            { "Vandal", "Odin", "Bucky" },
            { "Bucky", "Classic", "Classic" }
        };
        static string PreparedPath(string code) => $"{PreparedProps}/{code}.prefab";

        public static Bounds ActiveBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No active mesh: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Tools/FPS/Approved Props/Preview Remaining Lab Candidates")]
        public static void PreviewRemainingLabCandidates()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            foreach (string code in new[] { "LAB_Centrifuge", "LAB_C4" })
            {
                string path = $"Assets/ThirdParty/ApprovedProps/{code}/Models/{code}" + (code == "LAB_Centrifuge" ? ".dae" : ".fbx");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) throw new InvalidOperationException("Missing candidate " + path);
                var preview = new PreviewRenderUtility();
                Texture2D image = null;
                try
                {
                    var go = Object.Instantiate(model);
                    preview.AddSingleGO(go);
                    if (code == "LAB_C4")
                        foreach (var child in go.transform.Cast<Transform>().ToArray())
                            if (child.name != "Cube") Object.DestroyImmediate(child.gameObject);
                    var b = ActiveBounds(go);
                    preview.camera.fieldOfView = 32;
                    preview.camera.nearClipPlane = .001f;
                    preview.camera.farClipPlane = Mathf.Max(100, b.size.magnitude * 5);
                    preview.camera.transform.position = b.center + new Vector3(1, .65f, 1.5f).normalized * b.size.magnitude * 1.8f;
                    preview.camera.transform.LookAt(b.center);
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.camera.backgroundColor = new Color(.065f, .075f, .09f);
                    preview.lights[0].intensity = 1.5f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(40, 40, 0);
                    preview.lights[1].intensity = 1;
                    preview.ambientColor = new Color(.5f, .5f, .5f);
                    preview.BeginStaticPreview(new Rect(0, 0, 1280, 720));
                    preview.Render(true);
                    image = preview.EndStaticPreview();
                    File.WriteAllBytes("Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/" + code + "-Candidate.png", image.EncodeToPNG());
                    Debug.Log($"CANDIDATE {code}: bounds={b.size:F3}, renderers={go.GetComponentsInChildren<Renderer>().Length}; preview only, no scene placement.");
                }
                finally { if (image != null) Object.DestroyImmediate(image); preview.Cleanup(); }
            }
        }

        [MenuItem("Tools/FPS/Approved Props/Prepare Downloaded Visuals")]
        public static void PrepareDownloadedVisuals()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            Directory.CreateDirectory(PreparedProps);
            AssetDatabase.Refresh();
            for (int i = 0; i < ItemCodes.Length; i++)
                PrepareModel(ItemCodes[i], ItemCodes[i], null, new[] { .16f, .18f, .23f, .11f }[i], false);
            for (int i = 0; i < 5; i++)
                PrepareModel("AM" + (i + 1), "AM" + (i + 1), i == 4 ? new[] { "BoxBase_low", "BoxLid_low" } : null, .23f, false);
            PrepareModel("R1", "R1", null, .3f, false);
            PrepareModel("K2", "K2", null, .085f, false);
            // Use the author's front workbench and its four base cupboards only. No pipes, room shell or mannequin.
            PrepareModel("LAB_C3", "LabWorkBench", new[] { "Plane.001", "Plane.002", "Cabinet1", "Cabinet1.001", "Cabinet1.002", "Cabinet1.003" }, 2.16f, true);
            PrepareModel("LAB_C2", "FumeHood", null, 2.2f, true);
            PrepareModel("L2_R", "Workstation", new[] { "Monitor" }, .48f, true);
            PrepareModel("L2_R", "Keyboard", new[] { "Keyboard" }, .32f, true);
            PrepareModel("L2_R", "Mouse", new[] { "Mouse" }, .08f, true);
            PrepareModel("L2_R", "ComputerTower", new[] { "Computer" }, .48f, true);
            PrepareModel("K1", "K1", null, .14f, false);
            Debug.Log("DOWNLOADED_PREFABS_PREPARED: sourced meshes only; GameScene not edited.");
        }

        static void PrepareModel(string sourceCode, string code, string[] parts, float longest, bool floorPivot, string sourcePath = null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath ?? ($"Assets/ThirdParty/ApprovedProps/{sourceCode}/Models/{sourceCode}" + (sourceCode == "G2" ? ".dae" : sourceCode == "K1" ? "_LOD.fbx" : ".fbx")));
            if (model == null) throw new InvalidOperationException("Missing source: " + sourceCode);
            var preview = new PreviewRenderUtility();
            try
            {
                var root = new GameObject(code);
                preview.AddSingleGO(root);
                var source = Object.Instantiate(model, root.transform);
                if (parts != null)
                    foreach (var child in source.transform.Cast<Transform>().ToArray())
                        if (!parts.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
                if (sourceCode == "L2_R" && parts?.Length == 1)
                {
                    var part = source.transform.GetChild(0);
                    part.rotation = Quaternion.Euler(part.eulerAngles.x, 0, part.eulerAngles.z);
                }
                if (sourceCode == "LAB_Cabinet")
                    source.transform.Find("lab_cabinet_r_door_NakedSingularity").localRotation = Quaternion.identity;
                if (sourceCode == "LAB_Centrifuge") CombineCentrifugeSource(source);
                var b = ActiveBounds(root);
                source.transform.localScale *= longest / Mathf.Max(b.size.x, b.size.y, b.size.z);
                b = ActiveBounds(root);
                source.transform.position -= new Vector3(b.center.x, floorPivot ? b.min.y : b.center.y, b.center.z);
                if (code == "LabWorkBench" || code == "FumeHood" || code == "UtilityCart" || code == "TransitHardCase" || RemainingLabTypes.Contains(code))
                    foreach (var f in root.GetComponentsInChildren<MeshFilter>())
                    {
                        foreach (var existing in f.GetComponents<Collider>()) Object.DestroyImmediate(existing);
                        var body = f.gameObject.AddComponent<MeshCollider>();
                        body.sharedMesh = f.sharedMesh;
                    }
                if (PrefabUtility.SaveAsPrefabAsset(root, PreparedPath(code)) == null)
                    throw new InvalidOperationException("Could not save prepared visual " + code);
            }
            finally { preview.Cleanup(); }
        }

        [MenuItem("Tools/FPS/Approved Props/Bind Downloaded Runtime Visuals")]
        public static void BindDownloadedRuntimeVisuals()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SurvivalCatalog>("Assets/FPS/Features/Survival/Content/Resources/SurvivalCatalog.asset");
            Undo.RecordObject(catalog, "Bind approved survival visuals");
            for (int i = 0; i < ItemCodes.Length; i++)
            {
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(PreparedPath(ItemCodes[i]));
                if (visual == null) throw new InvalidOperationException("Prepare visuals first.");
                catalog.itemVisuals[i] = visual;
                string path = AssetDatabase.GetAssetPath(catalog.pickups[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var old = root.transform.Find("Visual");
                    if (old == null) throw new InvalidOperationException("Missing pickup Visual: " + path);
                    old.gameObject.SetActive(false);
                    var item = AddPrepared(ItemCodes[i], root.transform);
                    SetPresentation(root.GetComponent<SurvivalPickupPresentation>(), item.transform, false);
                    FitBody(root, ActiveBounds(item));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            const string projectilePath = "Assets/FPS/Features/Survival/Content/Prefabs/GrenadeProjectile.prefab";
            var projectile = PrefabUtility.LoadPrefabContents(projectilePath);
            try
            {
                projectile.transform.Find("Body").gameObject.SetActive(false);
                projectile.transform.Find("IncendiaryCore").gameObject.SetActive(false);
                var frag = AddPrepared("G1", projectile.transform);
                var fire = AddPrepared("G2", projectile.transform);
                fire.SetActive(false);
                var so = new SerializedObject(projectile.GetComponent<SurvivalProjectile>());
                so.FindProperty("fragVisual").objectReferenceValue = frag;
                so.FindProperty("incendiaryCore").objectReferenceValue = fire;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(projectile, projectilePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(projectile); }
            Debug.Log("RUNTIME_VISUALS_BOUND: pickup, FP/TP catalog and separate grenade projectile visuals. Network/physics roots preserved.");
        }

        static GameObject AddPrepared(string code, Transform parent)
        {
            string name = "Approved_" + code;
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PreparedPath(code));
            if (asset == null) throw new InvalidOperationException("Missing prepared prop " + code);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Approved downloaded prop");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            return go;
        }

        static void SetPresentation(SurvivalPickupPresentation presentation, Transform visual, bool idle)
        {
            if (presentation == null) return;
            var so = new SerializedObject(presentation);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("animateIdle").boolValue = idle;
            so.ApplyModifiedProperties();
        }

        static void FitBody(GameObject owner, Bounds world)
        {
            var body = owner.GetComponent<BoxCollider>() ?? Undo.AddComponent<BoxCollider>(owner);
            Undo.RecordObject(body, "Fit existing interaction body to real model");
            body.center = owner.transform.InverseTransformPoint(world.center);
            var scale = owner.transform.lossyScale;
            // Supply owners are unrotated; reject accidental rotated calls instead of making a misleading AABB.
            if (Quaternion.Angle(owner.transform.rotation, Quaternion.identity) > .1f)
                throw new InvalidOperationException("Rotated pickup owner: " + owner.name);
            body.size = new Vector3(world.size.x / Mathf.Abs(scale.x), world.size.y / Mathf.Abs(scale.y), world.size.z / Mathf.Abs(scale.z));
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }

        static void DisableVisual(Transform t)
        {
            if (t == null) return;
            Undo.RecordObject(t.gameObject, "Keep legacy visual inactive for rollback");
            t.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }

        [MenuItem("Tools/FPS/Approved Props/Prepare Approved Carts and Transit Cases")]
        public static void PrepareCartsAndCases()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            PrepareModel("LAB_C1", "UtilityCart", new[] { "pCube26" }, 1f, true);
            PrepareModel("RPaciorekHardCases", "TransitHardCase", null, .8f, true,
                "Assets/ThirdParty/ApprovedProps/RPaciorekHardCases/EquipmentCase.fbx");
        }

        static Transform[] CartAndCaseRoots()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("GameScene Edit Mode required.");
            var roots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "TransitCase" || t.name == "LabCart" || t.name == "TransferTrolley")
                .OrderBy(t => t.position.y).ToArray();
            if (roots.Count(t => t.name == "TransitCase") != 20 || roots.Count(t => t.name == "LabCart") != 2
                || roots.Count(t => t.name == "TransferTrolley") != 3)
                throw new InvalidOperationException("Expected 20 cases and 5 carts; scene baseline changed.");
            return roots;
        }

        [MenuItem("Tools/FPS/Approved Props/Apply Approved Carts and Transit Cases")]
        public static void ApplyCartsAndCases()
        {
            var roots = CartAndCaseRoots();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace 25 approved carts and transit cases");
            try
            {
                foreach (var root in roots)
                {
                    var legacy = root.Find("Visuals");
                    if (legacy == null) throw new InvalidOperationException("Missing legacy " + PathOf(root));
                    var old = RenderBounds(legacy.gameObject);
                    string code = root.name == "TransitCase" ? "TransitHardCase" : "UtilityCart";
                    var visual = Instance(Prepared(code), root, "Approved_" + code);
                    Undo.RecordObject(visual.transform, "Fit sourced model without changing stable root");
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    visual.transform.localScale = Vector3.one;
                    var b = ActiveBounds(visual);
                    visual.transform.localScale *= Mathf.Min(old.size.x / b.size.x, old.size.z / b.size.z, old.size.y / b.size.y);
                    b = ActiveBounds(visual);
                    visual.transform.position += new Vector3(old.center.x - b.center.x, old.min.y - b.min.y, old.center.z - b.center.z);
                    DisableVisual(legacy);
                    var box = root.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        Undo.RecordObject(box, "Retire enclosing legacy collision"); box.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                    }
                    Physics.SyncTransforms();
                    // Lower cases first. Upper visuals settle onto the actual lower case, not its old height.
                    b = ActiveBounds(visual);
                    var hits = Physics.RaycastAll(new Vector3(b.center.x, old.min.y + .15f, b.center.z), Vector3.down, 2f,
                        ~0, QueryTriggerInteraction.Ignore).Where(h => !h.transform.IsChildOf(root) && h.normal.y > .95f)
                        .OrderByDescending(h => h.point.y).ToArray();
                    if (hits.Length == 0) throw new InvalidOperationException("No real support under " + PathOf(root));
                    visual.transform.position += Vector3.up * (hits[0].point.y - b.min.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                    Physics.SyncTransforms();
                }
                ValidateCartsAndCases();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(group);
                Debug.Log("STAGED: 20 TransitCase, 2 LabCart, 3 TransferTrolley; roots preserved; not saved.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        [MenuItem("Tools/FPS/Approved Props/Validate Approved Carts and Transit Cases")]
        public static void ValidateCartsAndCases()
        {
            Physics.SyncTransforms();
            foreach (var root in CartAndCaseRoots())
            {
                var legacy = root.Find("Visuals");
                var visual = root.Find(root.name == "TransitCase" ? "Approved_TransitHardCase" : "Approved_UtilityCart");
                if (legacy == null || legacy.gameObject.activeSelf || visual == null || !visual.gameObject.activeInHierarchy
                    || root.GetComponent<BoxCollider>()?.enabled == true)
                    throw new InvalidOperationException("Legacy/new state mismatch " + PathOf(root));
                var old = RenderBounds(legacy.gameObject); var b = ActiveBounds(visual.gameObject);
                if (visual.GetComponentsInChildren<Transform>(true).Any(t => t.gameObject.layer != root.gameObject.layer)
                    || visual.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m == null)))
                    throw new InvalidOperationException("Missing material or changed gameplay layer " + PathOf(root));
                if (b.min.x < old.min.x - .005f || b.max.x > old.max.x + .005f
                    || b.min.z < old.min.z - .005f || b.max.z > old.max.z + .005f)
                    throw new InvalidOperationException("Expanded footprint " + PathOf(root));
                foreach (var mesh in visual.GetComponentsInChildren<MeshFilter>())
                {
                    string path = AssetDatabase.GetAssetPath(mesh.sharedMesh);
                    string source = root.name == "TransitCase" ? "RPaciorekHardCases" : "LAB_C1";
                    if (mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0 || !path.StartsWith("Assets/ThirdParty/ApprovedProps/" + source + "/")
                        || mesh.GetComponent<MeshCollider>()?.enabled != true)
                        throw new InvalidOperationException("Invalid sourced mesh/collision " + PathOf(mesh.transform));
                }
                foreach (float x in new[] { -.7f, .7f })
                    foreach (float z in new[] { -.7f, .7f })
                    {
                        var origin = new Vector3(b.center.x + b.extents.x * x, b.min.y + .012f, b.center.z + b.extents.z * z);
                        if (!Physics.RaycastAll(origin, Vector3.down, .024f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(h => !h.transform.IsChildOf(root) && h.normal.y > .95f))
                            throw new InvalidOperationException("Unsupported base " + PathOf(root));
                    }
            }
            Debug.Log("PASS: 25 sourced carts/cases, 100 supported base probes, original horizontal footprints and inactive legacy.");
        }

        public static void ApplyDownloadedSupplies()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("GameScene Edit Mode required.");
            var supplies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignSupply>(true)).ToArray();
            if (supplies.Length != 108 || supplies.Select(s => s.supplyId).Distinct().Count() != 108)
                throw new InvalidOperationException("Supply baseline changed.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Approved downloaded supplies and real support tables");
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                foreach (var bench in all.Where(t => t.name.StartsWith("SupplyBench_")))
                {
                    string station = bench.name.Substring("SupplyBench_".Length);
                    var members = supplies.Where(s => s.supplyId.StartsWith(station + "_") || s.supplyId.StartsWith("survival-" + station + "_")).OrderBy(s => s.supplyId).ToArray();
                    if (members.Length != 12) throw new InvalidOperationException("Expected 12 supplies at " + station);
                    var existing = bench.parent.Find("ApprovedTable_" + station);
                    var table = existing != null ? existing.gameObject : AddPrepared("LabWorkBench", bench.parent);
                    // Each station gets its own instance, even if siblings share a chapter parent.
                    Undo.RecordObject(table, "Name station support"); table.name = "ApprovedTable_" + station;
                    Undo.RecordObject(table.transform, "Place source workbench within original footprint");
                    table.transform.SetPositionAndRotation(bench.position, Quaternion.identity);
                    var b = ActiveBounds(table);
                    table.transform.position += new Vector3(bench.position.x - b.center.x, bench.position.y - b.min.y, bench.position.z - b.center.z);
                    DisableVisual(bench);
                    var surface = table.GetComponentsInChildren<MeshCollider>().Single(x => x.name == "Plane.001");
                    Physics.SyncTransforms();
                    b = surface.bounds;
                    for (int i = 0; i < members.Length; i++)
                    {
                        var supply = members[i];
                        string code;
                        Undo.RecordObject(supply, "Restore authored party and weapon mapping");
                        if (supply.supplyId.StartsWith("survival-"))
                        {
                            int reward = Array.IndexOf(new[] { PickupType.FragGrenade, PickupType.IncendiaryGrenade, PickupType.Medkit, PickupType.Antidote }, supply.survivalReward);
                            if (reward < 0) throw new InvalidOperationException("Unknown survival reward");
                            code = ItemCodes[reward];
                            supply.minimumPartySize = supply.supplyId.Contains("_3-") ? 3 : 1;
                        }
                        else if (supply.medicineOnly) code = "M1_S1";
                        else
                        {
                            var parts = supply.supplyId.Split('_');
                            int index = int.Parse(parts[1]), row = int.Parse(parts[2]);
                            string weapon = AmmoStations[index, row];
                            supply.ammoWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/FPS/Features/Weapons/Content/{weapon}/{weapon}.asset");
                            if (supply.ammoWeapon == null) throw new InvalidOperationException("Missing weapon " + weapon);
                            supply.ammo = Mathf.Min(index == 2 ? 45 : 30, supply.ammoWeapon.ReserveCapacity);
                            if (row == 2 && index == 1) supply.ammo = 30;
                            supply.minimumPartySize = new[] { 1, 2, 4 }[row];
                            code = "AM" + (Array.IndexOf(AmmoNames, weapon) + 1);
                        }
                        DisableVisual(supply.transform.Find("Visual"));
                        DisableVisual(supply.transform.Find("CaseVisual"));
                        var visual = AddPrepared(code, supply.transform);
                        Undo.RecordObject(visual.transform, "Fit actual supply footprint to tabletop slot");
                        visual.transform.localScale = Vector3.one;
                        visual.transform.localRotation = Quaternion.identity;
                        var vb = ActiveBounds(visual);
                        // Reserve one column at the final Lab station for the real transmission radio.
                        int columns = station == "Laboratory_2" ? 7 : 6;
                        float cellX = (b.size.x - .06f) / columns, cellZ = (b.size.z - .04f) / 2;
                        visual.transform.localScale *= Mathf.Min(1f, (cellX - .025f) / vb.size.x, (cellZ - .015f) / vb.size.z);
                        var point = new Vector3(b.min.x + .03f + cellX * (i % 6 + .5f), b.max.y, b.min.z + .02f + cellZ * (i / 6 + .5f));
                        point = SurfacePoint(surface, point);
                        Undo.RecordObject(supply.transform, "Ground whole pickup and its interaction origin");
                        supply.transform.position = point + Vector3.up * .08f;
                        vb = ActiveBounds(visual);
                        visual.transform.position += point - new Vector3(vb.center.x, vb.min.y, vb.center.z);
                        FitBody(supply.gameObject, ActiveBounds(visual));
                        SetPresentation(supply.GetComponent<SurvivalPickupPresentation>(), visual.transform, false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(supply);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(supply.transform);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(table.transform);
                }
                Physics.SyncTransforms();
                ValidateDownloadedSupplies();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("108_SUPPLIES_REPLACED: IDs/rewards/choice preserved, 9 sourced tables, no scene save.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        public static void ValidateDownloadedSupplies()
        {
            var supplies = Object.FindObjectsByType<CampaignSupply>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (supplies.Length != 108 || supplies.Select(s => s.supplyId).Distinct().Count() != 108)
                throw new InvalidOperationException("Stable supply identity mismatch");
            foreach (var s in supplies)
            {
                var visual = s.transform.Cast<Transform>().Single(t => t.name.StartsWith("Approved_"));
                var b = ActiveBounds(visual.gameObject);
                foreach (var x in new[] { b.min.x + .002f, b.max.x - .002f })
                    foreach (var z in new[] { b.min.z + .002f, b.max.z - .002f })
                    {
                        var hits = Physics.RaycastAll(new Vector3(x, b.min.y + .012f, z), Vector3.down, .04f, ~0, QueryTriggerInteraction.Ignore);
                        if (!hits.Any(h => !h.transform.IsChildOf(s.transform) && h.normal.y > .95f
                            && h.transform.GetComponentInParent<Transform>() != null && h.collider is MeshCollider))
                            throw new InvalidOperationException("Unsupported supply corner: " + s.supplyId);
                    }
                if (s.ammoWeapon != null && (s.ammo <= 0 || s.ammo > s.ammoWeapon.ReserveCapacity))
                    throw new InvalidOperationException("Invalid ammo amount: " + s.supplyId);
                foreach (string name in new[] { "Visual", "CaseVisual" })
                    if (s.transform.Find(name)?.gameObject.activeSelf == true)
                        throw new InvalidOperationException("Legacy still active: " + s.supplyId);
            }
            Debug.Log("PASS: 108 unique supplies; 432 supported footprint corners; capacity guards; legacy inactive.");
        }

        [MenuItem("Tools/FPS/Approved Props/Apply Approved Lab Benches and Fume Hoods")]
        public static void ApplyApprovedLabBenchFamily()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("GameScene Edit Mode required.");
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene == scene && (t.name == "LabBench" || t.name == "FumeHood" || t.name == "ControlDesk")).ToArray();
            if (roots.Count(t => t.name == "LabBench") != 7 || roots.Count(t => t.name == "FumeHood") != 2
                || roots.Count(t => t.name == "ControlDesk") != 9)
                throw new InvalidOperationException("Expected 7 LabBench, 2 FumeHood and 9 ControlDesk roots.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace approved LabBench and FumeHood visuals");
            try
            {
                var c = Object.FindFirstObjectByType<CampaignMissionController>();
                var traceDesk = roots.Where(t => t.name == "ControlDesk")
                    .OrderBy(t => Vector3.Distance(t.position, c.Objective(CampaignObjectiveId.LabTrace).transform.position)).First();
                foreach (var root in roots)
                {
                    if (root == traceDesk) { BindTraceWorkstation(c); continue; }
                    var front = root.name == "FumeHood" ? -root.forward : root.forward;
                    bool reverse = !(CampaignPlacement.TryFloor(c.chapters[2], root.position + front * 1.3f, out var floor)
                        && CampaignPlacement.Clear(floor));
                    var table = ReplaceLabFurniture(root, root.name == "FumeHood" ? "FumeHood" : "LabWorkBench",
                        root.name == "ControlDesk" ? .78f : .92f, reverse);
                    if (root.name == "ControlDesk") PlaceWorkstation(table.transform, root, "Approved_ControlDesk");
                }
                // LabIndex formerly routed through the legacy bench mesh. Keep its owner
                // and file, but transfer the physical interaction to the new countertop.
                var index = c.Objective(CampaignObjectiveId.LabIndex);
                var bench = roots.Where(t => t.name == "LabBench")
                    .OrderBy(t => Vector3.Distance(t.position, c.Document(CampaignFileId.LabIndex).Point)).First();
                var surface = bench.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001");
                var previousProxy = index.interactionBody.GetComponent<CampaignInteractionProxy>();
                var next = previousProxy != null ? previousProxy.nextTarget : null;
                Bind(c, CampaignObjectiveId.LabIndex, surface.gameObject, c.Document(CampaignFileId.LabIndex).Point);
                var proxy = surface.GetComponent<CampaignInteractionProxy>();
                Undo.RecordObject(proxy, "Preserve objective chain");
                proxy.nextTarget = next;
                PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
                Physics.SyncTransforms();
                ValidateApprovedLabBenchFamily();
                VerifyRemainingLocalProps();
                VerifyObjectiveDevices(c);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("LAB_BENCH_FAMILY_REPLACED: 7 LabBench + 2 FumeHood + 9 ControlDesk sourced visuals; legacy subtrees inactive; not saved.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static GameObject ReplaceLabFurniture(Transform root, string code, float worktopHeight, bool reverse = false)
        {
            var oldVisual = root.Find("Visuals") ?? throw new InvalidOperationException("Missing legacy visual: " + PathOf(root));
            var original = RenderBounds(oldVisual.gameObject);
            var approved = Instance(Prepared(code), root, "Approved_" + code);
            Undo.RecordObject(approved.transform, "Fit sourced furniture to measured footprint");
            approved.transform.localPosition = Vector3.zero;
            approved.transform.localScale = Vector3.one;
            approved.transform.localRotation = Quaternion.Euler(0, (code == "FumeHood" ? 90 : 0) + (reverse ? 180 : 0), 0);
            Physics.SyncTransforms();
            var source = ActiveBounds(approved);
            if (code == "FumeHood")
                approved.transform.localScale *= Mathf.Min(original.size.x / source.size.x,
                    original.size.z / source.size.z, original.size.y / source.size.y);
            else
            {
                var surface = approved.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001");
                float height = SurfacePoint(surface, surface.bounds.center).y - source.min.y;
                bool quarterTurn = Mathf.Abs(approved.transform.right.z) > .5f;
                // Match the measured tabletop, not the legacy model's 1.7 m backsplash.
                approved.transform.localScale = new Vector3(
                    quarterTurn ? original.size.z / source.size.z : original.size.x / source.size.x,
                    worktopHeight / height,
                    quarterTurn ? original.size.x / source.size.x : original.size.z / source.size.z);
            }
            var fitted = ActiveBounds(approved);
            approved.transform.position += new Vector3(original.center.x - fitted.center.x,
                original.min.y - fitted.min.y, original.center.z - fitted.center.z);
            var oldCollider = root.GetComponent<BoxCollider>();
            if (oldCollider != null)
            {
                Undo.RecordObject(oldCollider, "Disable enclosing legacy collision; use sourced mesh collision");
                oldCollider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(oldCollider);
            }
            DisableVisual(oldVisual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(approved.transform);
            Physics.SyncTransforms();
            return approved;
        }

        [MenuItem("Tools/FPS/Approved Props/Verify Approved Lab Benches and Fume Hoods")]
        public static void ValidateApprovedLabBenchFamily()
        {
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.gameObject.scene.IsValid() && (t.name == "LabBench" || t.name == "FumeHood" || t.name == "ControlDesk")).ToArray();
            if (roots.Count(t => t.name == "LabBench") != 7 || roots.Count(t => t.name == "FumeHood") != 2
                || roots.Count(t => t.name == "ControlDesk") != 9)
                throw new InvalidOperationException("Lab equipment root count changed.");
            foreach (var root in roots)
            {
                string code = root.name == "FumeHood" ? "FumeHood" : "LabWorkBench";
                var approved = root.Find("Approved_" + code);
                if (approved == null || !approved.gameObject.activeInHierarchy || approved.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled))
                    throw new InvalidOperationException("Missing active sourced visual: " + PathOf(root));
                if (root.Find("Visuals")?.gameObject.activeSelf == true)
                    throw new InvalidOperationException("Legacy laboratory visual remains active: " + PathOf(root));
                if (root.GetComponent<BoxCollider>()?.enabled == true
                    || !approved.GetComponentsInChildren<MeshCollider>().Any(m => m.enabled))
                    throw new InvalidOperationException("New mesh collision missing or legacy enclosing collider active: " + PathOf(root));
                var oldBounds = RenderBounds(root.Find("Visuals").gameObject);
                var bounds = ActiveBounds(approved.gameObject);
                if (bounds.min.x < oldBounds.min.x - .005f || bounds.max.x > oldBounds.max.x + .005f
                    || bounds.min.z < oldBounds.min.z - .005f || bounds.max.z > oldBounds.max.z + .005f
                    || Mathf.Abs(bounds.min.y - oldBounds.min.y) > .005f)
                    throw new InvalidOperationException("Furniture exceeds original footprint or floats: " + PathOf(root));
                if (root.name != "FumeHood")
                {
                    var surface = approved.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001");
                    float height = root.name == "ControlDesk" ? .78f : .92f;
                    if (Mathf.Abs(SurfacePoint(surface, surface.bounds.center).y - (oldBounds.min.y + height)) > .004f)
                        throw new InvalidOperationException("Incorrect measured worktop height: " + PathOf(root));
                    if (root.name == "ControlDesk")
                    {
                        var c = Object.FindFirstObjectByType<CampaignMissionController>();
                        var parent = root.Find("Approved_ControlDesk_Workstation") != null
                            ? root : c.Objective(CampaignObjectiveId.LabTrace).transform;
                        string prefix = parent == root ? "Approved_ControlDesk_" : "Approved_LabTrace_";
                        var parts = new[] { "Workstation", "Keyboard", "Mouse", "Tower" }
                            .Select(s => parent.Cast<Transform>().Single(t => t.name == prefix + s).gameObject).ToArray();
                        foreach (var part in parts)
                        {
                            AssertSupported(part, surface);
                            var body = part.GetComponent<Collider>();
                            if (body == null || !body.enabled || !part.activeInHierarchy)
                                throw new InvalidOperationException("Missing computer collision: " + part.name);
                            foreach (var file in c.fileSources.Where(f => f != null && Vector3.Distance(f.Point, root.position) < 2f))
                                if (body.bounds.Intersects(file.interactionBody.bounds))
                                    throw new InvalidOperationException("Computer obscures file: " + file.fileId);
                            foreach (var other in parts.Where(p => p != part))
                                if (ActiveBounds(part).Intersects(ActiveBounds(other)))
                                    throw new InvalidOperationException("Overlapping computer parts: " + part.name);
                            foreach (var mesh in part.GetComponentsInChildren<MeshFilter>())
                                if (mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0
                                    || !AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/ThirdParty/ApprovedProps/L2_R/"))
                                    throw new InvalidOperationException("Missing or unsourced computer mesh: " + part.name);
                        }
                    }
                }
                var chapter = root.GetComponentInParent<CampaignChapterRoot>();
                var front = root.name == "FumeHood" ? approved.right : approved.forward;
                if (!CampaignPlacement.TryFloor(chapter, root.position + front * 1.3f, out var floor)
                    || !CampaignPlacement.Clear(floor))
                    throw new InvalidOperationException("Cabinet front is not accessible: " + PathOf(root));
                foreach (var mesh in approved.GetComponentsInChildren<MeshFilter>(true))
                    if (!AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/ThirdParty/ApprovedProps/"))
                        throw new InvalidOperationException("Unsourced replacement mesh: " + PathOf(mesh.transform));
            }
            Debug.Log("PASS: 7 LabBench + 2 FumeHood + 9 ControlDesk; sourced meshes; legacy inactive; support, approach, collision and paper separation.");
        }
    }
}
