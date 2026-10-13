using System.Collections.Generic;
using System.Linq;
using FPS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class HudRegressionTests
    {
        [Test]
        public void SaveGateLeavesUnsavedScenesUntouched()
        {
            SaveAuthoredScenes();
            // EditMode Test Runner already supplies an unsaved bootstrap scene.
            var temporary = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .First(scene => string.IsNullOrEmpty(scene.path));
            var fixture = new GameObject("Unsaved save-gate fixture");
            SceneManager.MoveGameObjectToScene(fixture, temporary);
            try
            {
                EditorSceneManager.MarkSceneDirty(temporary);
                Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/Save Authored Scenes"), Is.True);
                Assert.That(temporary.path, Is.Empty);
                Assert.That(temporary.isDirty, Is.True, "An unsaved scene must be skipped, with no Save Scene dialog.");
            }
            finally
            {
                Object.DestroyImmediate(fixture);
                SaveAuthoredScenes();
            }
        }

        [Test]
        public void FpsSampleAuthoringIsRepeatableAndKeepsOnlyImageFeedback()
        {
            SaveAuthoredScenes();
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity", OpenSceneMode.Additive);
            try
            {
                Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/Apply Gameplay HUD Only"), Is.True);
                var canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
                int count = canvas.GetComponentsInChildren<Transform>(true).Length;
                Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/Apply Gameplay HUD Only"), Is.True);
                Assert.That(canvas.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HUDManager>(true)).Single();
                var data = new SerializedObject(manager);
                var marker = (Image)data.FindProperty("hitMarker").objectReferenceValue;
                Assert.That(marker.sprite, Is.EqualTo(HudSkin.Get("FPSSample/HitMarker")));
                Assert.That(marker.rectTransform.sizeDelta, Is.EqualTo(new Vector2(48, 48)));
                Assert.That(marker.GetComponent<TMPro.TMP_Text>(), Is.Null);
                Assert.That(marker.transform.IsChildOf(canvas.transform.Find("MinimalHUDRoot/Crosshair")), Is.False);
                Assert.That(data.FindProperty("directionalDamageIndicators").arraySize, Is.EqualTo(4));
                var blood = (Image)data.FindProperty("healthDangerBackground").objectReferenceValue;
                Assert.That(blood.sprite, Is.EqualTo(HudSkin.Get("FPSSample/DamageVignette")));
                Assert.That(blood.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(blood.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(blood.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "TreatmentProgressFill"), Is.False);
                Assert.That(canvas.GetComponentsInChildren<Image>(true).Any(i => i.sprite != null && i.sprite.name.Contains("Inifity")), Is.False);
                Assert.That(canvas.transform.Find("HudSafeArea/WeaponPanel").GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(176));
                foreach (var image in marker.transform.parent.GetComponentsInChildren<Image>(true)) Assert.That(image.raycastTarget, Is.False);
                foreach (var name in new[] { "HitMarker", "DamageIndicator", "DamageVignette", "GoalMarker", "GoalMarkerCentre", "GoalProgress", "UI_HUD_LMB", "UI_HUD_RMB", "UI_Hud_Bullets" })
                    Assert.That(HudSkin.Get("FPSSample/" + name), Is.Not.Null, name);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                SaveAuthoredScenes();
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void DamageCopiesKeepSourceSeparateFromImpactPoint()
        {
            var info = new DamageInfo(30, hitPoint: Vector3.up, sourcePosition: Vector3.right * 20);
            var copy = (DamageInfo)typeof(EnemyHealth).GetMethod("CopyWithAmount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { info, 10f });
            Assert.That(copy.amount, Is.EqualTo(10));
            Assert.That(copy.sourcePosition, Is.EqualTo(info.sourcePosition));
            Assert.That(copy.hitPoint, Is.EqualTo(Vector3.up));
            Assert.That(new DamageInfo(1).sourcePosition, Is.Null);
        }

        static void SaveAuthoredScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path) && !scene.name.StartsWith("Untitled")
                    && !scene.path.StartsWith("Assets/InitTestScene", System.StringComparison.Ordinal)
                    && !EditorSceneManager.IsPreviewScene(scene) && scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void TrackingUsesNearestCompleteRouteThenKeepsManualSelection()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
            var lengths = new Dictionary<CampaignObjectiveId, float?>
            { [ids[0]] = 30, [ids[1]] = null, [ids[2]] = 12 };
            var chosen = CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, id => lengths[id]);
            Assert.That(chosen, Is.EqualTo(ids[2]));
            lengths[ids[0]] = 1;
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, chosen, id => lengths[id]), Is.EqualTo(chosen));
            state.completed |= CampaignState.Bit(chosen); // Includes a teammate's authoritative completion.
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, chosen, id => lengths[id]), Is.EqualTo(ids[0]));
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => null), Is.EqualTo(ids[0]));
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => float.NaN), Is.EqualTo(ids[0]));
        }

        [Test]
        public void TrackingInvalidatesOnChapterPhaseAndCheckpointRollback()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, completed = CampaignState.Bit(CampaignObjectiveId.FactoryIsolate) };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup };
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[1], _ => 1), Is.EqualTo(ids[1]));
            state.completed = 0; // Restore a checkpoint before the prerequisite.
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[1], _ => 1), Is.EqualTo(ids[0]));
            state.chapter = CampaignChapter.Asylum;
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[0], _ => 1), Is.EqualTo(CampaignObjectiveId.None));
            state.phase = CampaignPhase.Preparing;
            Assert.That(CampaignObjectiveSelection.Resolve(state, new[] { CampaignObjectiveId.AsylumAccess }, CampaignObjectiveId.AsylumAccess, _ => 1), Is.EqualTo(CampaignObjectiveId.None));
        }

        [Test]
        public void ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner()
        {
            var bounds = new Rect(100, 80, 1080, 560);
            var center = new Vector2(640, 360);
            var root = new GameObject("Marker projection camera", typeof(Camera));
            try
            {
                var camera = root.GetComponent<Camera>(); camera.pixelRect = new Rect(0, 0, 1280, 720);
                var front = new Vector3(0, 0, 20);
                Assert.That(HudMarkerProjection.Project(camera, front, bounds, out var edge), Is.EqualTo(center));
                Assert.That(edge, Is.False);
                foreach (float depth in new[] { -20f, -.001f, 0f, .001f, 20f })
                {
                    var right = HudMarkerProjection.Project(camera, new Vector3(20, 0, depth), bounds, out edge);
                    Assert.That(edge, Is.True);
                    Assert.That(right.x, Is.EqualTo(bounds.xMax).Within(.01f), "Right bearing stays right across the camera plane.");
                    Assert.That(right.y, Is.EqualTo(center.y).Within(.01f));
                    var left = HudMarkerProjection.Project(camera, new Vector3(-20, 0, depth), bounds, out edge);
                    Assert.That(left.x, Is.EqualTo(bounds.xMin).Within(.01f));
                }
                var behind = HudMarkerProjection.Project(camera, new Vector3(0, 0, -20), bounds, out edge);
                Assert.That(edge, Is.True);
                Assert.That(float.IsNaN(behind.x) || float.IsInfinity(behind.y), Is.False);
                Assert.That(behind.y, Is.EqualTo(bounds.yMin));
            }
            finally { Object.DestroyImmediate(root); }
            var adjusted = HudMarkerProjection.AvoidCornerHud(new Vector2(100, 90), bounds, new Rect(0, 0, 400, 300), 40);
            Assert.That(adjusted.y, Is.GreaterThanOrEqualTo(340));
            Assert.That(adjusted.x, Is.EqualTo(bounds.xMin));
            var crowded = new[] { new Rect(0, 0, 400, 570), new Rect(0, 644, 500, 220) };
            var safe = HudMarkerProjection.AvoidHud(new Vector2(130, 580), new Rect(130, 100, 1276, 684), crowded, 60);
            foreach (var area in crowded)
            {
                var expanded = new Rect(area.xMin - 60, area.yMin - 60, area.width + 120, area.height + 120);
                Assert.That(expanded.Contains(safe), Is.False, "Avoid all corner groups together at the 125% fixture.");
            }
        }

        [Test]
        public void PromptsFollowOverridesAndReset()
        {
            var root = new GameObject("HUD binding regression");
            var saved = new Dictionary<string, string>();
            InputManager input = null;
            try
            {
                input = root.AddComponent<InputManager>();
                foreach (var action in input.ActionsAsset)
                    Save("Input_" + action.name);
                Save("InputBindings");
                int updates = 0;
                input.PromptBindingsChanged += () => updates++;
                input.RebindKey("Grenade", KeyCode.H);
                Assert.That(input.GetPromptDisplayName("Grenade"), Is.EqualTo("H").IgnoreCase);
                Assert.That(updates, Is.GreaterThan(0));
                input.RebindKey("Fire", KeyCode.F);
                input.RebindKey("Aim", KeyCode.Mouse0);
                Assert.That(input.GetPromptBindingPath("Fire"), Is.EqualTo("<Keyboard>/f").IgnoreCase);
                Assert.That(input.GetPromptBindingPath("Aim"), Is.EqualTo("<Mouse>/leftButton").IgnoreCase);
                input.ResetBindingsToDefault();
                Assert.That(input.GetPromptBindingPath("Fire"), Is.EqualTo("<Mouse>/leftButton").IgnoreCase);
                Assert.That(input.GetPromptBindingPath("Aim"), Is.EqualTo("<Mouse>/rightButton").IgnoreCase);
                Assert.That(input.GetPromptDisplayName("Grenade"), Is.EqualTo("G").IgnoreCase);
                Assert.That(input.GetPromptDisplayName("CycleGrenade"), Does.Contain("3"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var pair in saved)
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetString(pair.Key, pair.Value);
                PlayerPrefs.Save();
            }
            void Save(string key) => saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }
    }
}
