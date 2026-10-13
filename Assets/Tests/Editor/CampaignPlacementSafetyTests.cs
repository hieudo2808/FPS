using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace FPS.Tests
{
    public sealed class CampaignPlacementSafetyTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        CampaignChapterRoot chapter;
        MapRecoveryPoint recovery;
        NavMeshData data;
        NavMeshDataInstance navigation;
        readonly Vector3 connected = new(-1000, 0, -1000);
        readonly Vector3 isolated = new(-990, 0, -1000);

        [SetUp]
        public void CreateTwoDisconnectedFloors()
        {
            root = new GameObject("PlacementSafetyProbe") { hideFlags = HideFlags.HideAndDontSave };
            chapter = root.AddComponent<CampaignChapterRoot>();
            chapter.chapter = CampaignChapter.Factory;
            var sources = new List<NavMeshBuildSource>();
            foreach (var position in new[] { connected, isolated })
            {
                var floor = new GameObject("Floor") { hideFlags = HideFlags.HideAndDontSave };
                floor.transform.SetParent(root.transform);
                floor.transform.position = position - Vector3.up * .1f;
                floor.AddComponent<BoxCollider>().size = new Vector3(4, .2f, 4);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = floor.transform.localToWorldMatrix, size = new Vector3(4, .2f, 4), area = 0 });
            }
            var point = new GameObject("Recovery") { hideFlags = HideFlags.HideAndDontSave };
            point.transform.SetParent(root.transform); point.transform.position = connected + Vector3.up * .12f;
            recovery = point.AddComponent<MapRecoveryPoint>();
            chapter.recoveries = new[] { recovery }; chapter.arrivals = new[] { point.transform };
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(connected + Vector3.right * 5, new Vector3(30, 10, 20)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            navigation = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void RemoveOnlyProbeObjectsAndNavigation()
        {
            navigation.Remove();
            if (data != null) Object.DestroyImmediate(data);
            if (root != null) Object.DestroyImmediate(root);
            Physics.SyncTransforms();
        }

        [Test]
        public void ClearNavMeshFloorNeedsAWorkingReturnRouteForSaveAndReconnect()
        {
            Assert.That(CampaignPlacement.TryFloor(chapter, isolated, out var floor), Is.True);
            Assert.That(CampaignPlacement.Clear(floor), Is.True, "The old floor/clear checks accept this isolated island.");
            Assert.That(CampaignPlacement.TrySafeFloor(chapter, isolated, 0, out _), Is.False);
            Assert.That(CampaignPlacement.TryReconnectPosition(chapter, isolated, 0, out _), Is.False);
            Assert.That(CampaignPlacement.TrySafeFloor(chapter, connected, 0, out _), Is.True);
            var otherChapter = new GameObject("Other chapter") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<CampaignChapterRoot>();
            otherChapter.transform.SetParent(root.transform);
            otherChapter.recoveries = chapter.recoveries;
            Assert.That(CampaignPlacement.TrySafeFloor(otherChapter, connected, 0, out _), Is.False, "A floor owned by another chapter is not safe.");
            Assert.That(CampaignPlacement.TryReconnectPosition(chapter, connected, 0, out _), Is.True);
            recovery.enabled = false;
            Assert.That(CampaignPlacement.TrySafeFloor(chapter, connected, 0, out _), Is.False);
            recovery.enabled = true;
            chapter.recoveries = System.Array.Empty<MapRecoveryPoint>();
            Assert.That(CampaignPlacement.TrySafeFloor(chapter, connected, 0, out _), Is.False);
            Assert.That(CampaignPlacement.TrySafeFloor(null, connected, 0, out _), Is.False);
        }

        [Test]
        public void PreviouslySavedIsolatedPoseFallsBackToValidatedArrival()
        {
            var service = root.AddComponent<WorldRecoveryService>();
            service.Configure(chapter.recoveries);
            var actor = new GameObject("Actor", typeof(NetworkObject)) { hideFlags = HideFlags.HideAndDontSave };
            actor.transform.SetParent(root.transform);
            var player = actor.AddComponent<PlayerHealth>();
            var poses = (Dictionary<ulong, Pose>)typeof(WorldRecoveryService).GetField("campaignSafePoses", Private).GetValue(service);
            poses[player.NetworkObjectId] = new Pose(isolated + Vector3.up * .12f, Quaternion.identity);
            object[] args = { chapter, player, default(Pose) };
            bool recovered = (bool)typeof(WorldRecoveryService).GetMethod("TryCampaignRecovery", Private).Invoke(service, args);
            Assert.That(recovered, Is.True);
            Assert.That(Vector3.Distance(((Pose)args[2]).position, connected + Vector3.up * .12f), Is.LessThan(.1f),
                "A clear but isolated legacy pose must not be used as the recovery fallback.");
        }
    }
}
