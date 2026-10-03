using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    public sealed class CampaignFileSceneTests
    {
        [Test]
        public void EveryCatalogFile_HasUniqueVisibleSupportedReachablePhysicalSource()
        {
            const string path = "Assets/FPS/Scenes/GameScene.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Physics.SyncTransforms();
                var c = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CampaignMissionController>()).Single();
                Assert.That(c.fileSources.Length, Is.EqualTo(21));
                Assert.That(c.fileSources.Select(s => s.fileId).Distinct().Count(), Is.EqualTo(21));
                Assert.That(c.ContentCatalog.files.Count(f => !f.required), Is.EqualTo(12));
                foreach (var file in c.ContentCatalog.files)
                {
                    string context = file.id.ToString();
                    var source = c.Document(file.id);
                    Assert.That(source, Is.Not.Null, context);
                    Assert.That(source.documentOnly && source.isActiveAndEnabled, Is.True, context);
                    Assert.That(source.pickupVisual, Is.Not.Null, context);
                    Assert.That(source.pickupVisual != source.gameObject && source.pickupVisual.transform.IsChildOf(source.transform), Is.True, context);
                    Assert.That(source.pickupVisual.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.True, context);
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(source.pickupVisual), Is.Not.Null, "Reuse imported art: " + context);
                    Assert.That(source.interactionBody != null && source.interactionBody.enabled, Is.True, context);
                    Assert.That(CampaignFileAuthoring.TryReader(c.chapters[(int)file.chapter], source.Point,
                        c.settings.interactionRange, source, out var reader), Is.True, context);
                    Vector3 eye = reader + Vector3.up * 1.55f;
                    Vector3 ray = source.Point - eye;
                    Assert.That(Physics.Raycast(eye, ray.normalized, out var hit, ray.magnitude + .1f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, context);
                    Assert.That(hit.collider.GetComponentInParent<CampaignInteractable>(), Is.SameAs(source), "F ray must select this File, not its console: " + context);
                    // Start below the small interaction box to check a real map surface supports the sheet.
                    Assert.That(Physics.Raycast(source.Point - Vector3.up * .012f, Vector3.down, out var support, .06f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "Paper must not float: " + context);
                    if (file.id == CampaignFileId.LabPowerRecord || file.id == CampaignFileId.OptionalLab03 || file.id == CampaignFileId.OptionalLab01)
                    {
                        Assert.That(support.collider, Is.TypeOf<MeshCollider>(), "Do not place papers on the coarse box enclosing a monitor: " + context);
                        Assert.That(source.Point.y, Is.LessThan(-16.9f), "Use the desktop, not the monitor top: " + context);
                    }
                    foreach (string character in new[] { "Brimstone", "Clove", "Gekko", "Sage" })
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab");
                        using var interaction = new SerializedObject(prefab.GetComponent<InteractionManager>());
                        int mask = interaction.FindProperty("interactableLayer").intValue;
                        Assert.That(mask == 0 || (mask & (1 << source.gameObject.layer)) != 0, Is.True, character + " ray layer: " + context);
                        Assert.That(ray.magnitude, Is.LessThanOrEqualTo(interaction.FindProperty("interactRange").floatValue), character + " ray range: " + context);
                    }
                }
            }
            finally { if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
