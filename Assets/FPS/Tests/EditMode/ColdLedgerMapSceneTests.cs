using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    public sealed class ColdLedgerMapSceneTests
    {
        private const string ScenePath = "Assets/FPS/Scenes/GameScene.unity";
        private const string SourceMapPath = "Assets/FPS/Features/World/Content/Map_v2.unity";
        private const string RootName = "ColdLedger_Extension";
        private const int BaselinePrefabCount = 747;
        private const int AnchorCount = 82;
        private static readonly string[] BaselineRoots = { "Static", "Particles", "Dynamic", "Light" };

        [Test]
        public void GameScene_PreservesExactMapV2PrefabManifest()
        {
            Scene gameScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject gameMap = FindRoot(gameScene, "Map_v2");
            string[] gameManifest = BuildBaselineManifest(gameMap);

            Scene sourceScene = EditorSceneManager.OpenScene(SourceMapPath, OpenSceneMode.Additive);
            try
            {
                GameObject sourceMap = FindRoot(sourceScene, "Map_v2");
                string[] sourceManifest = BuildBaselineManifest(sourceMap);

                Assert.AreEqual(BaselinePrefabCount, sourceManifest.Length,
                    "Map_v2 source prefab count changed; update the approved baseline deliberately.");
                Assert.AreEqual(sourceManifest.Length, gameManifest.Length,
                    "GameScene must preserve every Map_v2 outer prefab instance.");
                CollectionAssert.AreEqual(sourceManifest, gameManifest,
                    "A Map_v2 prefab source or transform drifted inside GameScene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }
        }

        [Test]
        public void GameScene_ColdLedgerSceneContract_IsCompleteAndReferenced()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject map = FindRoot(scene, "Map_v2");
            Transform root = map.transform.Find(RootName);
            Assert.NotNull(root, "ColdLedger_Extension is missing from GameScene.");

            string[] requiredPaths =
            {
                "Architecture/ContinuousFacility/Foundations/WorldBase_Continuous",
                "Architecture/ContinuousFacility/Foundations/SouthFreightRaisedApron",
                "Architecture/ContinuousFacility/Foundations/WestProcessPlinth",
                "Architecture/ContinuousFacility/BuildingMasses",
                "Architecture/ContinuousFacility/StructuralBoundaries/NarrativePerimeter",
                "Architecture/ContinuousFacility/PrimaryCirculation",
                "Architecture/ContinuousFacility/SecondaryCirculation/PavedFacilitySurface",
                "Architecture/ContinuousFacility/ProcessInfrastructure/WestToFactoryPipeRack",
                "Architecture/ContinuousFacility/ProcessInfrastructure/WestCompressorCatwalk",
                "Architecture/ContinuousFacility/FlightCourtyards/LZ_Clearance",
                "Architecture/ContinuousFacility/FlightCourtyards/Helipad_Clearance",
                "ExistingMapBindings/SouthDeconOverride",
                "ExistingMapBindings/ColdStorageObjective/SampleVaultControl",
                "Gameplay/MissionController/Objectives",
                "Gameplay/GatesAndShortcuts",
                "Gameplay/Recovery/RecoveryService",
                "Gameplay/Recovery/RecoveryPoints",
                "Gameplay/SpawnPoints",
                "Director/DirectorSpawnService/Zones",
                "Director/DirectorSpawnService/Anchors",
                "Cinematics/CinematicController",
                "Validation"
            };
            foreach (string path in requiredPaths)
                Assert.NotNull(root.Find(path), $"Required Cold Ledger hierarchy path is missing: {path}");

            FactoryMissionController[] missions = root.GetComponentsInChildren<FactoryMissionController>(true);
            Assert.AreEqual(1, missions.Length, "GameScene must contain exactly one Cold Ledger mission controller.");
            Assert.NotNull(missions[0].GetComponent<NetworkObject>());

            FactoryObjectiveInteractable[] objectives = root.GetComponentsInChildren<FactoryObjectiveInteractable>(true);
            Assert.AreEqual(7, objectives.Length);
            Assert.AreEqual(7, objectives.Select(item => item.ObjectiveId).Distinct().Count());
            Assert.True(objectives.All(item => item.transform.IsChildOf(missions[0].transform)),
                "Every mission objective must remain below MissionController.");
            foreach (FactoryObjectiveInteractable objective in objectives)
            {
                SerializedProperty point = new SerializedObject(objective).FindProperty("interactionPoint");
                Assert.NotNull(point.objectReferenceValue, $"{objective.name} must reference its InteractionPoint child.");
            }

            FactoryMissionGate[] gates = root.GetComponentsInChildren<FactoryMissionGate>(true);
            Assert.AreEqual(5, gates.Length);
            foreach (FactoryMissionGate gate in gates)
            {
                SerializedProperty controller = new SerializedObject(gate).FindProperty("controller");
                Assert.AreSame(missions[0], controller.objectReferenceValue, $"{gate.name} has no direct mission reference.");
                Assert.NotNull(gate.GetComponent<NavMeshModifier>(), $"{gate.name} must be excluded from the static bake.");
                Assert.NotNull(gate.GetComponent<NavMeshObstacle>(), $"{gate.name} must carve while closed.");
            }

            Assert.AreEqual(0, root.Find("Architecture").GetComponentsInChildren<NetworkObject>(true).Length,
                "Static extension architecture must not generate network traffic.");
            Assert.AreEqual(0, root.Find("ExistingMapBindings").GetComponentsInChildren<NetworkObject>(true).Length,
                "Map_v2 frontage bindings must remain static.");
            Assert.AreEqual(0, root.GetComponentsInChildren<NavMeshLink>(true).Length,
                "Critical traversal may not rely on an off-mesh jump/link.");

            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject),
                    $"Missing script on {AnimationUtility.CalculateTransformPath(item, root)}.");

            Transform legacyWall = map.transform.Find("Static/Concrete_fence_v1_wall_set_v2 (2)");
            Assert.NotNull(legacyWall, "The approved Map_v2 decon frontage override target is missing.");
            Assert.True(legacyWall.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled));
            Assert.True(legacyWall.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled));
        }

        [Test]
        public void GameScene_FoundationsAreContinuousAtTheirAuthoredDatums()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);
            Transform worldBase = root.Find("Architecture/ContinuousFacility/Foundations/WorldBase_Continuous");
            AssertFoundation(worldBase, 0f);
            Assert.AreEqual(380f, worldBase.GetComponent<BoxCollider>().bounds.size.x, 0.02f);
            Assert.AreEqual(370f, worldBase.GetComponent<BoxCollider>().bounds.size.z, 0.02f);

            Transform visualGround = root.Find("Architecture/ContinuousFacility/Foundations/FacilityGround_Visual");
            Assert.NotNull(visualGround, "The continuous collider also needs a continuous visible asphalt surface.");
            Assert.NotNull(visualGround.GetComponent<MeshFilter>()?.sharedMesh);
            Assert.True(visualGround.GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(0, visualGround.GetComponents<Collider>().Length,
                "The visual surface may not add a second walkable collider or NavMesh seam.");
            Assert.AreEqual(380f, visualGround.GetComponent<MeshRenderer>().bounds.size.x, 0.02f);
            Assert.AreEqual(370f, visualGround.GetComponent<MeshRenderer>().bounds.size.z, 0.02f);

            AssertFoundation(
                root.Find("Architecture/ContinuousFacility/Foundations/SouthFreightRaisedApron/RaisedGround"), 2.09f);
            AssertFoundation(
                root.Find("Architecture/ContinuousFacility/Foundations/WestProcessPlinth/ProcessGround"), 1.67f);

            string[] generatedPaths = root.GetComponentsInChildren<Transform>(true)
                .Select(item => AnimationUtility.CalculateTransformPath(item, root))
                .ToArray();
            Assert.False(generatedPaths.Any(path => path.Contains("Z1_WestInsertion") || path.Contains("Z7_NorthExtraction")),
                "Architecture may not be partitioned into gameplay-zone campuses.");
        }

        [Test]
        public void GameScene_ExtensionUsesMapV2ArchitecturalGrammarInsteadOfIsolatedPropRegions()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);
            Transform facility = root.Find("Architecture/ContinuousFacility");
            Transform masses = facility.Find("BuildingMasses");
            Transform[] buildings = masses.Cast<Transform>().ToArray();
            Assert.GreaterOrEqual(buildings.Length, 40,
                "The extension needs building mass before industrial dressing.");

            float[] nearestDistances = buildings.Select(building => buildings
                    .Where(other => other != building)
                    .Min(other => HorizontalDistance(building.position, other.position)))
                .OrderBy(value => value)
                .ToArray();
            Assert.LessOrEqual(nearestDistances[nearestDistances.Length / 2], 25f,
                "Building spacing drifted beyond the Map_v2 median architectural rhythm.");
            Assert.LessOrEqual(nearestDistances.Max(), 45f,
                "A generated building mass is visually isolated from the facility.");

            Transform circulation = facility.Find("PrimaryCirculation");
            Transform secondary = facility.Find("SecondaryCirculation");
            GameObject[] roadRoots = circulation.GetComponentsInChildren<Transform>(true)
                .Concat(secondary.GetComponentsInChildren<Transform>(true))
                .Select(item => item.gameObject)
                .Where(PrefabUtility.IsOutermostPrefabInstanceRoot)
                .Where(item => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(item))
                    .IndexOf("/Road_set_v1_", StringComparison.Ordinal) >= 0)
                .ToArray();
            Assert.GreaterOrEqual(roadRoots.Length, 120);
            foreach (GameObject road in roadRoots)
            {
                float yaw = Mathf.Repeat(road.transform.eulerAngles.y, 90f);
                Assert.True(yaw < 0.1f || yaw > 89.9f, $"{road.name} is not orthogonal.");
                Assert.LessOrEqual(Vector3.Distance(Vector3.one, road.transform.lossyScale), 0.001f,
                    $"{road.name} may not be stretched into an arbitrary road strip.");
                Assert.True(road.GetComponentsInChildren<Collider>(true).All(item => !item.enabled),
                    $"{road.name} visual collider must not create NavMesh seams.");
            }

            Assert.GreaterOrEqual(facility.Find("ProcessInfrastructure")
                .GetComponentsInChildren<Transform>(true)
                .Count(item => PrefabUtility.IsOutermostPrefabInstanceRoot(item.gameObject)), 40,
                "Pipe racks, catwalks and process systems must connect the building masses.");
        }

        [Test]
        public void GameScene_LandingClearancesAndPerimetersArePhysicallySafe()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);

            Transform architecture = root.Find("Architecture/ContinuousFacility");
            AssertCircularClearance(architecture, new Vector3(-125f, 0f, -137f), 27.5f, "insertion LZ");
            AssertCircularClearance(architecture, new Vector3(125f, 0f, 140f), 27.5f, "extraction helipad");

            Transform backstops = architecture.Find("StructuralBoundaries/ColliderBackstops");
            Assert.NotNull(backstops);
            BoxCollider[] colliders = backstops.GetComponentsInChildren<BoxCollider>(true);
            Assert.GreaterOrEqual(colliders.Length, 6);
            Assert.True(colliders.All(item => item.bounds.size.y >= 5.95f));
        }

        [Test]
        public void GameScene_DirectorAnchorsMeetQuotaOwnershipGroundAndNavMeshContract()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);
            DirectorSpawnAnchor[] anchors = root.GetComponentsInChildren<DirectorSpawnAnchor>(true);
            Assert.AreEqual(AnchorCount, anchors.Length);

            var quotas = new Dictionary<string, int>
            {
                ["Z02_FactoryHub"] = 12,
                ["Z03_Utilities"] = 12,
                ["Z04_Logistics"] = 12,
                ["Z05_ColdStorage"] = 10,
                ["Z06_Maintenance"] = 8,
                ["Z07_Extraction"] = 16,
                ["C01_InsertionLink"] = 6,
                ["C02_ExtractionLink"] = 6
            };

            foreach ((string zoneId, int quota) in quotas)
                Assert.AreEqual(quota, anchors.Count(anchor => anchor.Zone != null && anchor.Zone.ZoneId == zoneId),
                    $"Director anchor quota mismatch for {zoneId}.");

            foreach (DirectorSpawnAnchor anchor in anchors)
            {
                Assert.NotNull(anchor.Zone, $"{anchor.name} has no owning zone.");
                Assert.True(anchor.Zone.Contains(anchor.SpawnPosition), $"{anchor.name} lies outside {anchor.Zone.ZoneId}.");
                Assert.True(NavMesh.SamplePosition(anchor.SpawnPosition, out NavMeshHit navHit, 1.25f, NavMesh.AllAreas),
                    $"{anchor.name} has no NavMesh beneath it.");
                Assert.LessOrEqual(Vector3.Distance(anchor.SpawnPosition, navHit.position), 1.25f);
                Assert.True(Physics.Raycast(anchor.SpawnPosition + Vector3.up * 1.5f, Vector3.down,
                        out RaycastHit ground, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                    $"{anchor.name} has no physical ground.");
                Assert.GreaterOrEqual(ground.normal.y, 0.55f, $"{anchor.name} is on an unsafe slope.");
            }
        }

        [Test, Timeout(180000)]
        public void GameScene_RepresentativeDirectorPosesHaveHiddenReachableSpawns()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject map = FindRoot(scene, "Map_v2");
            Transform root = map.transform.Find(RootName);
            DirectorSpawnAnchor[] anchors = root.GetComponentsInChildren<DirectorSpawnAnchor>(true);
            NavMeshSurface surface = map.GetComponentInChildren<NavMeshSurface>(true);
            Assert.NotNull(surface);

            FactoryMissionGate[] gates = root.GetComponentsInChildren<FactoryMissionGate>(true);
            Collider[] gateColliders = gates.SelectMany(gate => gate.GetComponentsInChildren<Collider>(true)).ToArray();
            bool[] colliderStates = gateColliders.Select(collider => collider.enabled).ToArray();
            NavMeshObstacle[] obstacles = gates.SelectMany(gate => gate.GetComponentsInChildren<NavMeshObstacle>(true)).ToArray();
            bool[] obstacleStates = obstacles.Select(obstacle => obstacle.enabled).ToArray();

            var poses = new[]
            {
                ("Hub_Courtyard", new Vector3(15f, 1f, -5f), Vector3.forward, false),
                ("Hub_South", new Vector3(-5f, 1f, -48f), Vector3.right, false),
                ("Hub_NorthRoad", new Vector3(-10f, 1f, 31f), Vector3.back, false),
                ("Utilities_BreakerWest", new Vector3(102.87f, 1f, 14.57f), Vector3.right, false),
                ("Utilities_Generator", new Vector3(97.91f, 1f, 33.59f), Vector3.back, false),
                ("Utilities_BreakerEast", new Vector3(77.91f, 1f, 17.8f), Vector3.left, false),
                ("Logistics_Manifest", new Vector3(100.3f, 2f, -77.46f), Vector3.right, false),
                ("Logistics_Recovery", new Vector3(80f, 2f, -75f), Vector3.forward, false),
                ("Logistics_Security", new Vector3(76.76f, 2f, -79.91f), Vector3.left, false),
                ("Cold_BlastDoor", new Vector3(68f, 1f, 72.5f), Vector3.right, false),
                ("Cold_ServiceRoad", new Vector3(93f, 1f, 50f), Vector3.forward, false),
                ("Cold_Sample", new Vector3(118.6f, 1f, 58f), Vector3.left, false),
                ("Maintenance_North", new Vector3(-30f, 2f, -50f), Vector3.back, false),
                ("Maintenance_Center", new Vector3(-30f, 2f, -75f), Vector3.right, false),
                ("Maintenance_South", new Vector3(-30f, 2f, -100f), Vector3.forward, false),
                ("Extraction_South", new Vector3(125f, 1f, 115f), Vector3.forward, true),
                ("Extraction_Center", new Vector3(125f, 1f, 140f), Vector3.right, true),
                ("Extraction_North", new Vector3(125f, 1f, 168f), Vector3.back, true)
            };

            try
            {
                foreach (Collider collider in gateColliders)
                    collider.enabled = false;
                foreach (NavMeshObstacle obstacle in obstacles)
                    obstacle.enabled = false;
                surface.RemoveData();
                surface.BuildNavMesh();

                var failures = new List<string>();
                foreach ((string id, Vector3 rawPosition, Vector3 lookDirection, bool finale) in poses)
                {
                    if (!NavMesh.SamplePosition(rawPosition, out NavMeshHit poseHit, 12f, NavMesh.AllAreas))
                    {
                        failures.Add($"{id}: no nearby NavMesh");
                        continue;
                    }
                    int validCount = anchors.Count(anchor => IsRepresentativeSpawnValid(
                        anchor, poseHit.position, lookDirection, finale));
                    int requiredCount = finale ? 2 : 1;
                    if (validCount < requiredCount)
                        failures.Add($"{id}: {validCount}/{requiredCount} valid anchors at {poseHit.position}");
                }
                if (failures.Count > 0)
                {
                    string message = "COLD_LEDGER_DIRECTOR_POSE_FAILURES\n" + string.Join("\n", failures);
                    Debug.LogError(message);
                    Assert.Fail(message);
                }
            }
            finally
            {
                for (int i = 0; i < gateColliders.Length; i++)
                    gateColliders[i].enabled = colliderStates[i];
                for (int i = 0; i < obstacles.Length; i++)
                    obstacles[i].enabled = obstacleStates[i];
                surface.RemoveData();
                surface.BuildNavMesh();
            }
        }

        [Test]
        public void GameScene_CinematicDurationsAndHelicopterPhysicsMatchContract()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);
            AssertDirectorDuration(root, "Cinematics/Insertion", 18d);
            AssertDirectorDuration(root, "Cinematics/ExtractionApproach", 85d);
            AssertDirectorDuration(root, "Cinematics/ExtractionOutro", 8d);

            foreach (ColdLedgerHelicopterRig rig in root.GetComponentsInChildren<ColdLedgerHelicopterRig>(true))
            {
                Assert.AreEqual(0, rig.GetComponentsInChildren<Collider>(true).Length,
                    $"Cinematic rig {rig.name} must not collide with gameplay.");
                Assert.AreEqual(0, rig.GetComponentsInChildren<Rigidbody>(true).Length,
                    $"Cinematic rig {rig.name} must not own Rigidbody physics.");
                Assert.True(rig.GetComponentsInChildren<Transform>(true)
                    .All(item => item.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")));
            }
        }

        [Test]
        public void GameScene_RecoveryUsesAuthoredPointsAndRejectsImmediateTeleportLoops()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);
            WorldRecoveryService service = root.GetComponentInChildren<WorldRecoveryService>(true);
            Assert.NotNull(service);
            Assert.NotNull(service.GetComponent<NetworkObject>());
            Assert.AreEqual(8, service.RecoveryPoints.Count);
            Assert.AreEqual(8, service.RecoveryPoints.Select(point => point.PointId).Distinct().Count());

            OutOfBoundsRecoveryVolume volume = root.GetComponentInChildren<OutOfBoundsRecoveryVolume>(true);
            Assert.NotNull(volume);
            BoxCollider volumeCollider = volume.GetComponent<BoxCollider>();
            Assert.True(volumeCollider.isTrigger);
            Assert.Less(volumeCollider.bounds.max.y, -10f);

            foreach (MapRecoveryPoint point in service.RecoveryPoints)
            {
                Assert.NotNull(point);
                Assert.True(NavMesh.SamplePosition(point.transform.position, out _, 4f, NavMesh.AllAreas),
                    $"{point.PointId} has no nearby NavMesh.");
            }

            GameObject probe = new("RecoveryProbe");
            try
            {
                probe.AddComponent<NetworkObject>();
                PlayerMovement movement = probe.AddComponent<PlayerMovement>();
                CapsuleCollider probeCollider = probe.AddComponent<CapsuleCollider>();
                probe.transform.position = new Vector3(0f, -20f, 0f);
                MapRecoveryPoint destination = service.RecoveryPoints[0];

                Assert.True(service.TryRecover(probeCollider, destination));
                Assert.AreEqual(destination.transform.position, movement.transform.position);
                Assert.False(service.TryRecover(probeCollider, destination),
                    "Recovery cooldown must prevent a same-frame teleport loop.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void GameScene_WithMissionGatesPhysicallyOpen_HasCompleteObjectivePaths()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = FindRoot(scene, "Map_v2").transform.Find(RootName);
            FactoryMissionGate[] gates = root.GetComponentsInChildren<FactoryMissionGate>(true);
            Collider[] gateColliders = gates.SelectMany(gate => gate.GetComponentsInChildren<Collider>(true)).ToArray();
            bool[] colliderStates = gateColliders.Select(collider => collider.enabled).ToArray();
            NavMeshObstacle[] obstacles = gates.SelectMany(gate => gate.GetComponentsInChildren<NavMeshObstacle>(true)).ToArray();
            bool[] obstacleStates = obstacles.Select(obstacle => obstacle.enabled).ToArray();
            NavMeshSurface surface = FindRoot(scene, "Map_v2").GetComponentInChildren<NavMeshSurface>(true);
            Assert.NotNull(surface);
            try
            {
                foreach (Collider collider in gateColliders)
                    collider.enabled = false;
                foreach (NavMeshObstacle obstacle in obstacles)
                    obstacle.enabled = false;
                surface.RemoveData();
                surface.BuildNavMesh();

                Transform spawn = root.Find("Gameplay/SpawnPoints/InsertionSpawn_1");
                Assert.True(NavMesh.SamplePosition(spawn.position, out NavMeshHit start, 12f, NavMesh.AllAreas));
                foreach (FactoryObjectiveInteractable objective in root.GetComponentsInChildren<FactoryObjectiveInteractable>(true))
                {
                    Assert.True(NavMesh.SamplePosition(objective.transform.position, out NavMeshHit end, 8f, NavMesh.AllAreas),
                        $"{objective.name} has no nearby NavMesh.");
                    var path = new NavMeshPath();
                    Assert.True(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path));
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status,
                        $"{objective.name} is not reachable after its mission gates open.");
                }
            }
            finally
            {
                for (int i = 0; i < gateColliders.Length; i++)
                    gateColliders[i].enabled = colliderStates[i];
                for (int i = 0; i < obstacles.Length; i++)
                    obstacles[i].enabled = obstacleStates[i];
                surface.RemoveData();
                surface.BuildNavMesh();
            }
        }

        private static string[] BuildBaselineManifest(GameObject map)
        {
            var result = new List<string>();
            foreach (string rootName in BaselineRoots)
            {
                Transform container = map.transform.Find(rootName);
                Assert.NotNull(container, $"Map_v2 baseline root {rootName} is missing.");
                foreach (Transform item in container.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(item.gameObject))
                        continue;
                    UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
                    result.Add(string.Join("|",
                        rootName,
                        AssetDatabase.GetAssetPath(source),
                        Vector(item.position),
                        QuaternionValue(item.rotation),
                        Vector(item.lossyScale)));
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static void AssertFoundation(Transform foundation, float expectedTop)
        {
            Assert.NotNull(foundation);
            BoxCollider collider = foundation.GetComponent<BoxCollider>();
            Assert.NotNull(collider);
            Assert.AreEqual(LayerMask.NameToLayer("Ground"), foundation.gameObject.layer);
            Assert.AreEqual(expectedTop, collider.bounds.max.y, 0.015f);

            Bounds bounds = collider.bounds;
            for (float x = bounds.min.x + 1f; x <= bounds.max.x - 1f; x += 2f)
            {
                for (float z = bounds.min.z + 1f; z <= bounds.max.z - 1f; z += 2f)
                {
                    var ray = new Ray(new Vector3(x, expectedTop + 2f, z), Vector3.down);
                    Assert.True(collider.Raycast(ray, out RaycastHit hit, 4f),
                        $"Foundation hole detected at ({x:F1}, {z:F1}).");
                    Assert.AreEqual(expectedTop, hit.point.y, 0.015f);
                }
            }
        }

        private static void AssertCircularClearance(Transform zone, Vector3 center, float radius, string label)
        {
            Assert.NotNull(zone, $"Missing zone for {label}.");
            foreach (Collider collider in zone.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger || collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
                    continue;
                if (collider.transform.IsChildOf(zone.Find("StructuralBoundaries/ColliderBackstops")))
                    continue;

                Bounds bounds = collider.bounds;
                float dx = Mathf.Max(bounds.min.x - center.x, 0f, center.x - bounds.max.x);
                float dz = Mathf.Max(bounds.min.z - center.z, 0f, center.z - bounds.max.z);
                float horizontalDistance = Mathf.Sqrt(dx * dx + dz * dz);
                Assert.GreaterOrEqual(horizontalDistance, radius - 0.05f,
                    $"{collider.name} intrudes into the {label} clearance by {radius - horizontalDistance:F2} m.");
            }
        }

        private static void AssertRectangularPerimeter(
            Transform perimeter, Bounds bounds, Func<Vector3, bool> isApprovedOpening, string label)
        {
            Assert.NotNull(perimeter, $"Missing perimeter for {label}.");
            BoxCollider[] backstops = perimeter.GetComponentsInChildren<BoxCollider>(true)
                .Where(item => item.enabled && item.name.StartsWith("ColliderBackstop_", StringComparison.Ordinal))
                .ToArray();
            Assert.Greater(backstops.Length, 0, $"{label} has no collider backstops.");
            Assert.True(backstops.All(item => item.bounds.size.y >= 5.95f),
                $"{label} contains a backstop lower than 6 m.");

            float y = bounds.center.y;
            for (float x = bounds.min.x; x <= bounds.max.x + 0.01f; x += 1f)
            {
                AssertPerimeterSample(new Vector3(x, y, bounds.min.z), backstops, isApprovedOpening, label);
                AssertPerimeterSample(new Vector3(x, y, bounds.max.z), backstops, isApprovedOpening, label);
            }
            for (float z = bounds.min.z; z <= bounds.max.z + 0.01f; z += 1f)
            {
                AssertPerimeterSample(new Vector3(bounds.min.x, y, z), backstops, isApprovedOpening, label);
                AssertPerimeterSample(new Vector3(bounds.max.x, y, z), backstops, isApprovedOpening, label);
            }
        }

        private static void AssertPerimeterSample(
            Vector3 point, BoxCollider[] backstops, Func<Vector3, bool> isApprovedOpening, string label)
        {
            if (isApprovedOpening(point))
                return;
            bool covered = backstops.Any(backstop => backstop.bounds.SqrDistance(point) <= 0.08f);
            Assert.True(covered, $"{label} perimeter gap detected at ({point.x:F1}, {point.z:F1}).");
        }

        private static void AssertDirectorDuration(Transform root, string path, double duration)
        {
            PlayableDirector director = root.Find(path)?.GetComponent<PlayableDirector>();
            Assert.NotNull(director, $"Missing PlayableDirector at {path}.");
            Assert.NotNull(director.playableAsset);
            Assert.AreEqual(duration, director.playableAsset.duration, 0.01d);
        }

        private static bool IsRepresentativeSpawnValid(
            DirectorSpawnAnchor anchor,
            Vector3 playerPosition,
            Vector3 lookDirection,
            bool finale)
        {
            if (anchor == null || anchor.Zone == null || !anchor.Zone.AllowsSpawning)
                return false;
            if (finale && (anchor.AnchorTypes & DirectorSpawnAnchorType.Finale) == 0)
                return false;

            Vector3 candidate = anchor.SpawnPosition;
            float distance = Vector3.Distance(playerPosition, candidate);
            if (distance < 28f || distance > 95f)
                return false;

            Vector3 direction = (candidate - playerPosition).normalized;
            bool inFallbackFrustum = Vector3.Dot(lookDirection.normalized, direction)
                >= Mathf.Cos(50f * Mathf.Deg2Rad)
                && Mathf.Abs(direction.y) <= 0.4f;
            if (inFallbackFrustum)
                return false;

            Vector3 eye = playerPosition + Vector3.up * 1.55f;
            Vector3 target = candidate + Vector3.up * 0.9f;
            if (!Physics.Linecast(eye, target, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit start, 2f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(playerPosition, out NavMeshHit end, 3f, NavMesh.AllAreas))
                return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject result = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
            Assert.NotNull(result, $"Scene {scene.path} has no root named {name}.");
            return result;
        }

        private static string Vector(Vector3 value) => string.Join(",",
            Float(value.x), Float(value.y), Float(value.z));

        private static string QuaternionValue(Quaternion value) => string.Join(",",
            Float(value.x), Float(value.y), Float(value.z), Float(value.w));

        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static float HorizontalDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
