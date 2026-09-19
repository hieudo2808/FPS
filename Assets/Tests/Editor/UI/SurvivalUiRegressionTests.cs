using System;
using System.Linq;
using System.Reflection;
using FPS;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public class SurvivalUiRegressionTests
    {
        const string SettingsPath = "Assets/FPS/Features/UI/Content/Prefabs/SettingsPanel.prefab";
        const string RosterPath = "Assets/FPS/Features/UI/Content/Prefabs/PlayerEntry_Tactical.prefab";

        [Test]
        public void CancellingRebindTwiceCompletesOnceAndPreservesBinding()
        {
            var go = new GameObject("UI input regression");
            try
            {
                var input = go.AddComponent<InputManager>();
                var before = input.GetBindingDisplayName("Fire");
                int calls = 0;
                Assert.That(input.StartInteractiveRebind("Fire", accepted => { Assert.That(accepted, Is.False); calls++; }), Is.True);
                Assert.DoesNotThrow(input.CancelInteractiveRebind);
                Assert.DoesNotThrow(input.CancelInteractiveRebind);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(input.GetBindingDisplayName("Fire"), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MainMenuModalBlocksBackgroundAndRestoresItOnBack()
        {
            const string path = "Assets/FPS/Scenes/MainMenu.unity";
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            var previous = SceneManager.GetActiveScene(); GameObject clone = null;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var original = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LobbyUI>(true)).First();
                clone = Object.Instantiate(original.transform.root.gameObject);
                var lobby = clone.GetComponentInChildren<LobbyUI>(true);
                var s = new SerializedObject(lobby);
                var main = (GameObject)s.FindProperty("mainPanel").objectReferenceValue;
                var play = (GameObject)s.FindProperty("playPopup").objectReferenceValue;
                var settings = (GameObject)s.FindProperty("settingsPopup").objectReferenceValue;
                Assert.That(s.FindProperty("statusText").objectReferenceValue, Is.Not.Null);
                Invoke(lobby, "OpenPlayPopup");
                Assert.That(play.activeSelf, Is.True); Assert.That(settings.activeSelf, Is.False);
                Assert.That(main.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Invoke(lobby, "OpenSettingsPopup");
                Assert.That(play.activeSelf, Is.False); Assert.That(settings.activeSelf, Is.True);
                Invoke(lobby, "OpenMainMenu");
                Assert.That(main.GetComponent<CanvasGroup>().interactable, Is.True);
                Assert.That(main.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(settings.activeSelf || play.activeSelf, Is.False);
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void SettingsBindingsStayInsideContentAndDoNotOverlap()
        {
            var root = PrefabUtility.LoadPrefabContents(SettingsPath);
            try
            {
                var content = Find(root.transform, "Content");
                var input = Find(root.transform, "InputPanel");
                var rows = input.Cast<Transform>().Where(t => t.name.EndsWith("Row") && t.name != "SensitivitySliderRow").Cast<RectTransform>().ToArray();
                Assert.That(rows.Length, Is.EqualTo(7));
                var rects = rows.Select(r => new Rect(r.anchoredPosition.x, -r.anchoredPosition.y, r.sizeDelta.x, r.sizeDelta.y)).ToArray();
                foreach (var r in rects) { Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(r.xMax, Is.LessThanOrEqualTo(content.rect.width)); Assert.That(r.yMax, Is.LessThanOrEqualTo(content.rect.height)); }
                for (int i = 0; i < rects.Length; i++) for (int j = i + 1; j < rects.Length; j++) Assert.That(rects[i].Overlaps(rects[j]), Is.False);
                foreach (var b in input.GetComponentsInChildren<Button>(true))
                {
                    var label = b.GetComponentInChildren<TMP_Text>(true);
                    Assert.That(label.fontSize, Is.LessThanOrEqualTo(22));
                    Assert.That(((RectTransform)b.transform).rect.height, Is.GreaterThanOrEqualTo(48));
                }
                Assert.That(root.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(TacticalUiTheme.CampaignOrder));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void LongPlayerNamesCannotOverwriteReadinessOrInjectMarkup()
        {
            var root = PrefabUtility.LoadPrefabContents(RosterPath);
            try
            {
                root.GetComponent<PlayerRosterEntryView>().SetPlayer("<b>Survivor_Long_Name</b>", true, true, PlayerCharacterId.Brimstone);
                var name = Find(root.transform, "OperatorName").GetComponent<TMP_Text>();
                var readiness = Find(root.transform, "Readiness").GetComponent<TMP_Text>();
                Assert.That(name.richText, Is.False);
                Assert.That(readiness.text, Is.EqualTo("READY"));
                Assert.That(name.rectTransform.offsetMax.x, Is.LessThan(-readiness.rectTransform.rect.width));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void MissingMatchManagerClearsAuthoredPhaseLabel()
        {
            var root = new GameObject("HUD phase regression");
            try
            {
                Assert.That(NetworkMatchStateManager.Instance, Is.Null);
                var hud = root.AddComponent<HUDManager>();
                var labelObject = new GameObject("Legacy phase", typeof(RectTransform));
                labelObject.transform.SetParent(root.transform);
                var label = labelObject.AddComponent<TextMeshProUGUI>();
                label.text = "WARMUP";
                var serialized = new SerializedObject(hud);
                serialized.FindProperty("matchStateText").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Invoke(hud, "UpdateMatchFlowInfo");

                Assert.That(label.text, Is.Empty);
                Assert.That(label.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PrimaryButtonAndMutedTextHaveReadableContrast()
        {
            Assert.That(Contrast(TacticalUiTheme.Text, TacticalUiTheme.Action), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(Contrast(TacticalUiTheme.Muted, TacticalUiTheme.Surface), Is.GreaterThanOrEqualTo(4.5f));
        }

        static RectTransform Find(Transform root, string name) => root.GetComponentsInChildren<RectTransform>(true).First(r => r.name == name);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        static float Contrast(Color a, Color b) { float x = Luminance(a), y = Luminance(b); return (Mathf.Max(x, y) + .05f) / (Mathf.Min(x, y) + .05f); }
        static float Luminance(Color c) => .2126f * Linear(c.r) + .7152f * Linear(c.g) + .0722f * Linear(c.b);
        static float Linear(float c) => c <= .04045f ? c / 12.92f : Mathf.Pow((c + .055f) / 1.055f, 2.4f);
    }
}
