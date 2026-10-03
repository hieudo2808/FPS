using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.EditorTools
{
    public static partial class ApprovedPropImplementation
    {
        [MenuItem("Tools/FPS/Approved Props/Apply Objective Devices")]
        public static void ApplyObjectiveDevices()
        {
            var c = Object.FindFirstObjectByType<CampaignMissionController>();
            var scene = c != null ? c.gameObject.scene : default;
            if (EditorApplication.isPlayingOrWillChangePlaymode || c == null || scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new InvalidOperationException("GameScene Edit Mode required.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace campaign objective devices with sourced models");
            try
            {
                BindFuse(c);
                BindTraceWorkstation(c);
                BindTransmitRadio(c);
                VerifyObjectiveDevices(c);
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("OBJECTIVE_DEVICES_APPLIED: AsylumFuse=K1_LOD, LabTrace=L2_R parts, LabTransmit=R1; legacy console visuals inactive; not saved.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        static GameObject Prepared(string code) => AssetDatabase.LoadAssetAtPath<GameObject>(PreparedPath(code))
            ?? throw new InvalidOperationException("Missing prepared visual " + code);

        static GameObject Instance(GameObject prefab, Transform parent, string name)
        {
            var old = parent.Find(name);
            var go = old != null ? old.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (old == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "Place sourced objective device");
                go.name = name;
            }
            foreach (var child in go.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.layer == parent.gameObject.layer) continue;
                Undo.RecordObject(child.gameObject, "Preserve gameplay owner's collision layer");
                child.gameObject.layer = parent.gameObject.layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
            return go;
        }

        static Collider Body(GameObject go)
        {
            var collider = go.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.enabled && !(c is BoxCollider && c.gameObject == go));
            if (collider != null) return collider;
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(go);
            Undo.RecordObject(box, "Fit objective collider in model-local coordinates");
            // Transform every renderer corner; a world AABB has swapped axes on a rotated radio.
            var points = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).SelectMany(r =>
                from x in new[] { r.localBounds.min.x, r.localBounds.max.x }
                from y in new[] { r.localBounds.min.y, r.localBounds.max.y }
                from z in new[] { r.localBounds.min.z, r.localBounds.max.z }
                select go.transform.InverseTransformPoint(r.transform.TransformPoint(new Vector3(x, y, z)))).ToArray();
            if (points.Length == 0) throw new InvalidOperationException("No rendered objective body: " + go.name);
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            box.center = bounds.center;
            box.size = bounds.size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            return box;
        }

        static void UseObjectiveBody(CampaignInteractable objective, GameObject visual, Vector3 point)
        {
            var body = Body(visual);
            var proxy = visual.GetComponent<CampaignInteractionProxy>();
            if (proxy == null) proxy = Undo.AddComponent<CampaignInteractionProxy>(visual);
            Undo.RecordObject(proxy, "Bind sourced objective proxy");
            proxy.target = objective;
            Undo.RecordObject(objective, "Bind sourced objective interaction body");
            objective.interactionBody = body;
            var anchor = objective.transform.Find("ApprovedInteractionPoint");
            if (anchor == null)
            {
                anchor = new GameObject("ApprovedInteractionPoint").transform;
                Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Objective interaction anchor");
                anchor.SetParent(objective.transform, false);
            }
            Undo.RecordObject(anchor, "Place sourced objective interaction point");
            anchor.position = point;
            objective.interactionPoint = anchor;
            foreach (var old in objective.transform.Cast<Transform>().Where(t => t.name == "EquipmentPedestal" || t.name == "ControlFace"))
            {
                Undo.RecordObject(old.gameObject, "Disable legacy objective primitive");
                old.gameObject.SetActive(false);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(objective);
            PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
        }

        static void PlaceOnSurface(GameObject visual, Collider surface, Vector3 xz, Quaternion rotation)
        {
            Undo.RecordObject(visual.transform, "Place sourced objective visual on measured surface");
            visual.transform.rotation = rotation;
            Physics.SyncTransforms();
            var point = SurfacePoint(surface, xz);
            var bounds = ActiveBounds(visual);
            visual.transform.position += new Vector3(xz.x - bounds.center.x, point.y - bounds.min.y, xz.z - bounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            Physics.SyncTransforms();
        }

        static void AssertSupported(GameObject visual, Collider support)
        {
            var bounds = ActiveBounds(visual);
            foreach (var x in new[] { bounds.min.x + .005f, bounds.max.x - .005f })
                foreach (var z in new[] { bounds.min.z + .005f, bounds.max.z - .005f })
                {
                    if (!support.Raycast(new Ray(new Vector3(x, bounds.min.y + .01f, z), Vector3.down), out var hit, .02f)
                        || hit.normal.y < .98f || Mathf.Abs(hit.point.y - bounds.min.y) > .004f)
                        throw new InvalidOperationException("Unsupported visual corner: " + visual.name);
                }
        }

        static void BindFuse(CampaignMissionController c)
        {
            var objective = c.Objective(CampaignObjectiveId.AsylumFuse);
            var visual = Instance(Prepared("K1"), objective.transform, "Approved_ServiceFuse");
            var support = Required("CampaignWorld/Asylum/Asylum/1stFloor/Stock/SmallMetalicCase")
                .GetComponentsInChildren<Collider>(true).First(collider => collider.enabled);
            PlaceOnSurface(visual, support, new Vector3(154.10f, 0f, 244.05f), Quaternion.identity);
            AssertSupported(visual, support);
            UseObjectiveBody(objective, visual, ActiveBounds(visual).center);
        }

        static void BindTraceWorkstation(CampaignMissionController c)
        {
            var objective = c.Objective(CampaignObjectiveId.LabTrace);
            var target = c.chapters[(int)CampaignChapter.Laboratory].GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "ControlDesk")
                .OrderBy(t => Vector3.Distance(t.position, objective.transform.position)).First();
            var table = ReplaceLabFurniture(target, "LabWorkBench", .78f, true);
            var workstation = PlaceWorkstation(table.transform, objective.transform, "Approved_LabTrace");
            UseObjectiveBody(objective, workstation, ActiveBounds(workstation).center);
        }

        static GameObject PlaceWorkstation(Transform table, Transform parent, string prefix)
        {
            var desk = table.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001");
            var workstation = Instance(Prepared("Workstation"), parent, prefix + "_Workstation");
            var keyboard = Instance(Prepared("Keyboard"), parent, prefix + "_Keyboard");
            var mouse = Instance(Prepared("Mouse"), parent, prefix + "_Mouse");
            var tower = Instance(Prepared("ComputerTower"), parent, prefix + "_Tower");
            var middle = desk.bounds.center;
            PlaceOnSurface(workstation, desk, middle + table.forward * .1f, table.rotation);
            PlaceOnSurface(keyboard, desk, middle + table.forward * .3f, table.rotation);
            PlaceOnSurface(mouse, desk, middle + table.right * .28f + table.forward * .29f, table.rotation);
            // Leave the two paper positions at +/- .65 m clear, including their F interaction boxes.
            PlaceOnSurface(tower, desk, middle - table.right * .98f, table.rotation);
            foreach (var part in new[] { workstation, keyboard, mouse, tower }) Body(part);
            Physics.SyncTransforms();
            AssertSupported(workstation, desk);
            AssertSupported(keyboard, desk);
            AssertSupported(mouse, desk);
            AssertSupported(tower, desk);
            return workstation;
        }

        static void BindTransmitRadio(CampaignMissionController c)
        {
            var objective = c.Objective(CampaignObjectiveId.LabTransmit);
            var radio = Instance(Prepared("R1"), objective.transform, "Approved_LabTransmit_Radio");
            var table = Required("CampaignWorld/Laboratory/CampaignSupplies/ApprovedTable_Laboratory_2");
            var surface = table.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001" && m.enabled);
            // The final tabletop has one unused column at its right edge; keep the radio
            // on that existing surface rather than inventing another console or pedestal.
            var chosen = new Vector3(surface.bounds.max.x - .18f, 0f, surface.bounds.center.z);
            PlaceOnSurface(radio, surface, new Vector3(chosen.x, 0f, chosen.z), Quaternion.Euler(0, 90, 0));
            AssertSupported(radio, surface);
            var radioBounds = ActiveBounds(radio);
            foreach (var supply in Object.FindObjectsByType<CampaignSupply>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(s => s.supplyId.StartsWith("Laboratory_2_") || s.supplyId.StartsWith("survival-Laboratory_2_")))
            {
                var item = supply.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Approved_"));
                if (item != null && radioBounds.Intersects(ActiveBounds(item.gameObject)))
                    throw new InvalidOperationException("Radio overlaps supply " + supply.supplyId);
            }
            UseObjectiveBody(objective, radio, ActiveBounds(radio).center);
        }

        [MenuItem("Tools/FPS/Approved Props/Verify Objective Devices")]
        public static void VerifyObjectiveDevices()
        {
            VerifyObjectiveDevices(Object.FindFirstObjectByType<CampaignMissionController>());
        }

        static void VerifyObjectiveDevices(CampaignMissionController c)
        {
            Physics.SyncTransforms();
            foreach (var id in new[] { CampaignObjectiveId.AsylumFuse, CampaignObjectiveId.LabTrace, CampaignObjectiveId.LabTransmit })
            {
                var objective = c.Objective(id);
                if (objective == null || objective.interactionBody == null || !objective.interactionBody.enabled
                    || !objective.interactionBody.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Objective has no active sourced interaction body: " + id);
                if (objective.transform.Find("EquipmentPedestal")?.gameObject.activeSelf == true
                    || objective.transform.Find("ControlFace")?.gameObject.activeSelf == true)
                    throw new InvalidOperationException("Legacy objective visual still active: " + id);
                var proxy = objective.interactionBody.GetComponentInParent<CampaignInteractionProxy>();
                if (proxy == null || proxy.target != objective) throw new InvalidOperationException("Proxy mismatch: " + id);
                if (id == CampaignObjectiveId.AsylumFuse)
                    AssertSupported(objective.interactionBody.transform.gameObject,
                        Required("CampaignWorld/Asylum/Asylum/1stFloor/Stock/SmallMetalicCase").GetComponent<Collider>());
                else if (id == CampaignObjectiveId.LabTrace)
                {
                    var target = c.chapters[(int)CampaignChapter.Laboratory].GetComponentsInChildren<Transform>(true)
                        .Where(t => t.name == "ControlDesk")
                        .OrderBy(t => Vector3.Distance(t.position, objective.transform.position)).First();
                    if (target.Find("Visuals").gameObject.activeSelf || target.GetComponent<BoxCollider>().enabled)
                        throw new InvalidOperationException("Legacy ControlDesk still active under new computer.");
                    var desk = target.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001" && m.enabled);
                    AssertSupported(objective.interactionBody.transform.gameObject, desk);
                    AssertSupported(objective.transform.Find("Approved_LabTrace_Keyboard").gameObject, desk);
                    AssertSupported(objective.transform.Find("Approved_LabTrace_Mouse").gameObject, desk);
                    AssertSupported(objective.transform.Find("Approved_LabTrace_Tower").gameObject, desk);
                }
                else if (id == CampaignObjectiveId.LabTransmit)
                    AssertSupported(objective.interactionBody.transform.gameObject,
                        Required("CampaignWorld/Laboratory/CampaignSupplies/ApprovedTable_Laboratory_2")
                            .GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Plane.001" && m.enabled));
                var bounds = ActiveBounds(objective.interactionBody.gameObject);
                if (Vector3.Distance(bounds.center, objective.interactionBody.bounds.center) > .005f
                    || Vector3.Distance(bounds.size, objective.interactionBody.bounds.size) > .005f)
                    throw new InvalidOperationException("Objective collider does not fit its model: " + id);
                if (!CampaignFileAuthoring.TryReader(c.chapters[(int)CampaignRules.ChapterOf(id)], objective.Point,
                    c.settings.interactionRange, objective, out var reader))
                    throw new InvalidOperationException("No reachable reader for " + id);
                Debug.Log($"OBJECTIVE_DEVICE_PASS {id}: body={objective.interactionBody.name} point={objective.Point:F3} reader={reader:F3}");
            }
        }
    }
}
