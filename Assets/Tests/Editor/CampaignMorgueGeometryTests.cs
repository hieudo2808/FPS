using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    /// <summary>Actual scene geometry checks; no gameplay state or checkpoint writes.</summary>
    public sealed class CampaignMorgueGeometryTests
    {
        Scene scene;
        bool opened;

        [SetUp]
        public void OpenSceneIfNeeded()
        {
            const string path = "Assets/FPS/Scenes/GameScene.unity";
            scene = SceneManager.GetSceneByPath(path);
            opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void CloseOnlyTestOwnedScene()
        {
            if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void MorgueTank_RegisteredHiddenAnchorsHaveBodyClearRoutesToReaderAndLift()
        {
            var campaign = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignMissionController>()).Single();
            var spawns = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DirectorSpawnService>()).Single();
            var chapter = campaign.chapters[(int)CampaignChapter.Asylum];
            var source = chapter.objectives.Single(o => o.objectiveId == CampaignObjectiveId.AsylumTransfer);
            var file = campaign.Document(CampaignFileId.AsylumMortuaryTransfer);
            Assert.That(file, Is.Not.Null, "Mortuary document must remain registered.");
            Assert.That(file.transform.IsChildOf(source.transform), Is.True);
            Assert.That(file.pickupVisual, Is.Not.Null);
            Assert.That(source.ProvidesFile(campaign.ContentCatalog.FindFile(file.fileId), campaign.ContentCatalog), Is.True);
            Assert.That(source.interactionBody, Is.Not.Null);
            Assert.That(source.interactionBody.name, Is.EqualTo("TableWhite"));
            Assert.That(source.interactionBody.transform.parent.name, Is.EqualTo("Morgue"));
            Assert.That(source.interactionBody.enabled && source.interactionBody.gameObject.activeInHierarchy, Is.True);
            Assert.That(source.interactionBody.GetComponent<CampaignInteractionProxy>()?.target, Is.SameAs(source));
            foreach (string name in new[] { "EquipmentPedestal", "ControlFace" })
                Assert.That(source.transform.Find(name)?.gameObject.activeSelf == true, Is.False,
                    "Obsolete console must not remain an invisible interaction point: " + name);
            Assert.That(CampaignFileAuthoring.TryReader(chapter, file.Point, campaign.settings.interactionRange, file, out var reader),
                Is.True, "Find a reachable reader at the real document, not the removed console.");
            var eye = reader + Vector3.up * 1.55f;
            Assert.That(Vector3.Distance(source.Point, file.Point), Is.LessThan(.05f), "Objective and paper share the real table.");
            Assert.That(Vector3.Distance(source.Point, eye), Is.LessThanOrEqualTo(campaign.settings.interactionRange));
            if (Physics.Linecast(eye, source.Point, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Assert.That(source.OwnsCollider(hit.collider), Is.True, "Objective line of sight from document reader.");
            var anchors = chapter.anchors.Where(a => a != null && a.name.StartsWith("Tank_MorgueArena_")).ToArray();
            Assert.That(anchors.Length, Is.EqualTo(2));
            // Keep the corridor/lift checks and also cover the approved table's actual reader.
            foreach (var actorPosition in new[] { reader, new Vector3(152, -3.84f, 239.8f), new Vector3(146.3f, -3.84f, 238.79f) })
            {
                Assert.That(NavMesh.SamplePosition(actorPosition, out var floor, .5f, NavMesh.AllAreas), Is.True, "Same-floor actor location");
                Assert.That(Mathf.Abs(floor.position.y - actorPosition.y), Is.LessThan(.2f));
                Assert.That(CampaignPlacement.Clear(floor.position), Is.True, "Player body must fit at the route target.");
                foreach (var anchor in anchors)
                {
                    string context = anchor.name + " -> " + actorPosition;
                    Assert.That(anchor.Zone != null && anchor.Zone.AllowsSpawning && anchor.Zone.Contains(anchor.SpawnPosition), Is.True, context);
                    Assert.That(DirectorSpawnService.HasTankArenaClearance(anchor.SpawnPosition), Is.True, context);
                    float distance = Vector3.Distance(anchor.SpawnPosition, actorPosition);
                    Assert.That(distance, Is.InRange(chapter.spawnMinimumDistance, chapter.spawnMaximumDistance), context);
                    Assert.That(Physics.Linecast(actorPosition + Vector3.up * 1.55f, anchor.SpawnPosition + Vector3.up * .9f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "Spawn must be concealed: " + context);
                    Assert.That(spawns.HasCampaignSpecialRoute(anchor.SpawnPosition, actorPosition, true), Is.True, context);
                }
            }
        }
    }
}
