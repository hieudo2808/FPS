using System.Linq;
using FPS.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    public sealed class ApprovedPropSceneTests
    {
        Scene scene;
        Scene previousActiveScene;
        bool opened;

        [OneTimeSetUp]
        public void OpenSceneIfNeeded()
        {
            // Test Runner starts in its own scene; direct in-place checks retain the user's loaded scene.
            previousActiveScene = SceneManager.GetActiveScene();
            scene = SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity", OpenSceneMode.Additive);
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void RestoreOnlyTestOwnedSceneState()
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void SourcedMicroscopes_ImportNonemptyMeshesWithoutFailingUv2Unwrap()
        {
            const string path = "Assets/FPS/Features/World/Content/ExperimentFacility/Models/LabEquipment/OmaxMicroscope.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.generateSecondaryUV, Is.False, "This source's UV2 packing previously discarded the imported mesh.");
            var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
            Assert.That(meshes.Length, Is.GreaterThan(0));
            Assert.That(meshes.All(m => m.vertexCount > 0 && m.bounds.size.sqrMagnitude > 0), Is.True);
            var scene = SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            Assert.That(scene.isLoaded, Is.True);
            var instances = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                .Where(m => AssetDatabase.GetAssetPath(m.sharedMesh) == path).ToArray();
            Assert.That(instances.Length, Is.EqualTo(4));
            Assert.That(instances.All(m => m.sharedMesh.vertexCount > 0 && m.GetComponent<Renderer>().enabled
                && m.gameObject.activeInHierarchy && m.GetComponent<Renderer>().bounds.size.sqrMagnitude > 0), Is.True);
        }

        [Test]
        public void FinalApprovedLabBatch_PreservesSourceMeshesFootprintsAndSupport()
        {
            Assert.DoesNotThrow(ApprovedPropImplementation.ValidateRemainingLab);
        }

        [Test]
        public void ApprovedCartsAndCases_PreserveFootprintsAndStackSupport()
        {
            Assert.DoesNotThrow(ApprovedPropImplementation.ValidateCartsAndCases);
        }

        [Test]
        public void DownloadedObjectiveDevices_HaveMeasuredSupportAndNoLegacyOverlap()
        {
            Assert.That(SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity").isLoaded, Is.True);
            Assert.DoesNotThrow(ApprovedPropImplementation.VerifyObjectiveDevices);
        }

        [Test]
        public void ApprovedLabFurniture_PreservesFootprintsSupportAndInteraction()
        {
            Assert.That(SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity").isLoaded, Is.True);
            Assert.DoesNotThrow(ApprovedPropImplementation.ValidateApprovedLabBenchFamily);
            Assert.DoesNotThrow(ApprovedPropImplementation.VerifyRemainingLocalProps);
        }

        [Test]
        public void DownloadedRuntimeVisuals_HaveOnlySourcedActiveMeshesAndSeparateProjectileKinds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SurvivalCatalog>("Assets/FPS/Features/Survival/Content/Resources/SurvivalCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            for (int i = 0; i < 4; i++)
            {
                foreach (var visual in new[] { catalog.itemVisuals[i], catalog.pickups[i] })
                {
                    var meshes = visual.GetComponentsInChildren<MeshFilter>();
                    Assert.That(meshes.Length, Is.GreaterThan(0));
                    foreach (var mesh in meshes)
                        Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Does.StartWith("Assets/ThirdParty/ApprovedProps/"), mesh.name);
                }
                var bounds = ApprovedPropImplementation.ActiveBounds(catalog.itemVisuals[i]);
                Assert.That(bounds.size.magnitude, Is.InRange(.09f, .4f));
                var pickup = catalog.pickups[i];
                Assert.That(pickup.transform.Find("Visual").gameObject.activeSelf, Is.False);
                var presentation = new SerializedObject(pickup.GetComponent<SurvivalPickupPresentation>());
                Assert.That(presentation.FindProperty("visual").objectReferenceValue, Is.Not.Null);
                Assert.That(presentation.FindProperty("animateIdle").boolValue, Is.False);
            }
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Features/Survival/Content/Prefabs/GrenadeProjectile.prefab");
            var state = new SerializedObject(projectile.GetComponent<SurvivalProjectile>());
            var frag = (GameObject)state.FindProperty("fragVisual").objectReferenceValue;
            var fire = (GameObject)state.FindProperty("incendiaryCore").objectReferenceValue;
            Assert.That(frag, Is.Not.Null); Assert.That(fire, Is.Not.Null);
            Assert.That(frag, Is.Not.SameAs(fire));
            Assert.That(projectile.transform.Find("Body").gameObject.activeSelf, Is.False);
            Assert.That(projectile.transform.Find("IncendiaryCore").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DownloadedSupplies_PreserveStableIdsMappingsGatesAndSupportedFootprints()
        {
            // Inspect the current staged scene, never save/reload/discard the user's pending scene edits.
            var scene = SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            Assert.That(scene.isLoaded, Is.True);
            Physics.SyncTransforms();
            ApprovedPropImplementation.ValidateDownloadedSupplies();
            var supplies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignSupply>(true)).ToArray();
            Assert.That(supplies.Count(s => s.ammoWeapon != null), Is.EqualTo(27));
            foreach (var s in supplies.Where(s => s.supplyId.StartsWith("survival-")))
                Assert.That(s.minimumPartySize, Is.EqualTo(s.supplyId.Contains("_3-") ? 3 : 1), s.supplyId);
            string[,] mapping = { { "Classic", "Operator", "Vandal" }, { "Vandal", "Odin", "Bucky" }, { "Bucky", "Classic", "Classic" } };
            foreach (var s in supplies.Where(s => s.ammoWeapon != null))
            {
                var parts = s.supplyId.Split('_');
                int station = int.Parse(parts[1]), row = int.Parse(parts[2]);
                Assert.That(s.ammoWeapon.weaponName, Is.EqualTo(mapping[station, row]), s.supplyId);
                Assert.That(s.minimumPartySize, Is.EqualTo(new[] { 1, 2, 4 }[row]), s.supplyId);
                Assert.That(s.ammo, Is.EqualTo(Mathf.Min(station == 2 ? 45 : 30, s.ammoWeapon.ReserveCapacity)), s.supplyId);
            }
            Assert.That(supplies.Single(s => s.supplyId == "Laboratory_1_0").chooseReward, Is.True);
        }

        [Test]
        public void SupplyCases_FitOriginalFootprintWithoutDuplicatingOrFloating()
        {
            const string path = "Assets/FPS/Scenes/GameScene.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var cases = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name == "EquipmentCase").ToArray();
                Assert.That(cases.Length, Is.EqualTo(27));
                foreach (var old in cases)
                {
                    var models = old.parent.Cast<Transform>().Where(t => t.name == "ApprovedEquipmentCase").ToArray();
                    Assert.That(models.Length, Is.EqualTo(1));
                    var original = old.GetComponent<Renderer>().bounds;
                    var actual = ApprovedPropImplementation.RenderBounds(models[0].gameObject);
                    Assert.That(old.gameObject.activeSelf, Is.False);
                    Assert.That(actual.size.x, Is.InRange(.1f, original.size.x + .001f));
                    Assert.That(actual.size.z, Is.InRange(.1f, original.size.z + .001f));
                    Assert.That(actual.size.y, Is.InRange(.05f, .25f));
                    Assert.That(actual.center.x, Is.EqualTo(original.center.x).Within(.001f));
                    Assert.That(actual.center.z, Is.EqualTo(original.center.z).Within(.001f));
                    Assert.That(actual.min.y, Is.EqualTo(original.min.y).Within(.001f));
                    Assert.That(old.parent.Find("CaseTrim").gameObject.activeSelf, Is.False);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ReplacementDevices_HaveReachableBindingsAndNoActivePedestals()
        {
            const string path = "Assets/FPS/Scenes/GameScene.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                ApprovedPropImplementation.VerifyLocalProps();
                ApprovedPropImplementation.VerifyRemainingLocalProps();
                var c = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignMissionController>()).Single();
                foreach (var id in new[] { CampaignObjectiveId.FactoryCase, CampaignObjectiveId.LabCase })
                {
                    var o = c.Objective(id);
                    var body = o.interactionBody;
                    Assert.That(body, Is.Not.Null, id.ToString());
                    var bounds = ApprovedPropImplementation.RenderBounds(body.gameObject);
                    Assert.That(Vector3.Distance(body.bounds.center, bounds.center), Is.LessThan(.005f), id.ToString());
                    Assert.That(Vector3.Distance(body.bounds.size, bounds.size), Is.LessThan(.005f), id.ToString());
                    Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.EqualTo(.62f).Within(.005f));
                    Assert.That(CampaignFileAuthoring.TryReader(c.chapters[(int)CampaignRules.ChapterOf(id)],
                        o.Point, c.settings.interactionRange, o, out _), Is.True, id.ToString());
                    // Start above the floor: a case sits flush, unlike the slightly raised paper interaction box.
                    Assert.That(Physics.Raycast(new Vector3(bounds.center.x, bounds.min.y + .02f, bounds.center.z),
                        Vector3.down, .06f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True,
                        "Case must sit on a real surface: " + id);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void SharedDevice_AdvancesAndRollsBackWithAuthoritativeObjectiveState()
        {
            var device = new GameObject("device");
            var first = new GameObject("install");
            var next = new GameObject("power");
            try
            {
                var proxy = device.AddComponent<CampaignInteractionProxy>();
                proxy.target = first.AddComponent<CampaignInteractable>();
                proxy.target.objectiveId = CampaignObjectiveId.AsylumInstall;
                proxy.nextTarget = next.AddComponent<CampaignInteractable>();
                proxy.nextTarget.objectiveId = CampaignObjectiveId.AsylumPower;
                var state = new CampaignState();
                Assert.That(proxy.Resolve(null), Is.SameAs(proxy.target));
                Assert.That(proxy.Resolve(state), Is.SameAs(proxy.target));
                state.completed |= CampaignState.Bit(CampaignObjectiveId.AsylumInstall);
                Assert.That(proxy.Resolve(state), Is.SameAs(proxy.nextTarget));
                state.completed = 0;
                Assert.That(proxy.Resolve(state), Is.SameAs(proxy.target));
                proxy.nextTarget = null;
                state.completed |= CampaignState.Bit(CampaignObjectiveId.AsylumInstall);
                Assert.That(proxy.Resolve(state), Is.SameAs(proxy.target));
            }
            finally
            {
                Object.DestroyImmediate(device);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(next);
            }
        }
    }
}
