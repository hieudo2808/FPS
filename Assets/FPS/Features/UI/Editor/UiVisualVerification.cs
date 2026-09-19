using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FPS.UI.Editor
{
    /// <summary>Renders isolated UI copies and checks visible text and control bounds.</summary>
    public static class UiVisualVerification
    {
        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        public static string Capture(string sceneName, string screen, int width = 1920, int height = 1080)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use authored UI verification in Edit mode.");
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            string path = "Assets/FPS/Scenes/" + sceneName + ".unity";
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            var previous = SceneManager.GetActiveScene();
            Scene preview = default; RenderTexture target = null; Texture2D image = null;
            var oldTarget = RenderTexture.active;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var source = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.transform.parent == null);
                preview = EditorSceneManager.NewPreviewScene();
                var clone = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(clone, preview);
                foreach (var scaler in clone.GetComponentsInChildren<CanvasScaler>(true)) scaler.enabled = false;
                foreach (var safeArea in clone.GetComponentsInChildren<UiSafeArea>(true)) safeArea.enabled = false;
                foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
                var canvas = clone.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                float scale = Mathf.Min(width / 1920f, height / 1080f);
                var rect = (RectTransform)clone.transform;
                rect.position = Vector3.zero; rect.rotation = Quaternion.identity; rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(width / scale, height / scale);
                SetActive(clone.transform, "PlayPopup", screen == "play");
                SetActive(clone.transform, "PausePanel", screen == "pause");
                bool settings = screen == "audio" || screen == "graphics" || screen == "controls";
                SetActive(clone.transform, "SettingsPanel", settings);
                if (settings)
                {
                    SetActive(clone.transform, "AudioPanel", screen == "audio");
                    SetActive(clone.transform, "GraphicsPanel", screen == "graphics");
                    SetActive(clone.transform, "InputPanel", screen == "controls");
                    foreach (var tab in new[] { "AudioTabBtn", "GraphicsTabBtn", "InputTabBtn" })
                    {
                        bool selected = screen == (tab == "AudioTabBtn" ? "audio" : tab == "GraphicsTabBtn" ? "graphics" : "controls");
                        var button = Find(clone.transform, tab);
                        var indicator = button.Find("SelectionRule"); if (indicator != null) indicator.gameObject.SetActive(selected);
                        button.GetComponentInChildren<TMP_Text>().color = selected ? TacticalUiTheme.Accent : TacticalUiTheme.Muted;
                    }
                }
                if (sceneName == "LobbyScene")
                {
                    var controller = clone.GetComponentInChildren<WaitingRoomUI>(true);
                    var serialized = new SerializedObject(controller);
                    var list = (Transform)serialized.FindProperty("playerListContainer").objectReferenceValue;
                    var entry = (GameObject)serialized.FindProperty("playerEntryPrefab").objectReferenceValue;
                    for (int i = 0; i < 4; i++)
                    {
                        var row = Object.Instantiate(entry, list);
                        row.GetComponent<PlayerRosterEntryView>().SetEmpty(i + 1);
                    }
                    // Stress the longest supported player name, character and readiness independently.
                    list.GetChild(0).GetComponent<PlayerRosterEntryView>().SetPlayer("Survivor_With_Long_Name24", true, true, PlayerCharacterId.Brimstone);
                    Find(clone.transform, "PlayerCountText").GetComponent<TMP_Text>().text = "1/4 Players";
                }
                var cameraGo = new GameObject("UI verification camera"); SceneManager.MoveGameObjectToScene(cameraGo, preview);
                var camera = cameraGo.AddComponent<Camera>(); camera.scene = preview;
                camera.orthographic = true; camera.orthographicSize = rect.rect.height / 2;
                camera.transform.position = new Vector3(0, 0, -100); camera.cullingMask = 1 << 5;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .085f, .075f);
                target = new RenderTexture(width, height, 24); camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                foreach (var layout in clone.GetComponentsInChildren<LayoutGroup>(true)) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                Canvas.ForceUpdateCanvases();
                foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("Documents/UIUX");
                string result = $"Documents/UIUX/{sceneName}-{screen}-{width}x{height}";
                File.WriteAllBytes(result + ".png", image.EncodeToPNG());
                var issues = CheckBounds(clone.transform, rect);
                File.WriteAllLines(result + ".txt", issues.Count > 0 ? issues : new List<string> { "PASS: visible text fits; controls remain inside the canvas." });
                return result + ": " + issues.Count + " layout issues";
            }
            finally
            {
                RenderTexture.active = oldTarget;
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (target != null) Object.DestroyImmediate(target);
                if (image != null) Object.DestroyImmediate(image);
                if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            }
        }

        static void SetActive(Transform root, string name, bool active)
        {
            var t = Find(root, name); if (t != null) t.gameObject.SetActive(active);
        }
        static List<string> CheckBounds(Transform root, RectTransform canvas)
        {
            var issues = new List<string>();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>())
            {
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                if (t.GetComponentInParent<TMP_InputField>() is TMP_InputField field && field.placeholder == t && !string.IsNullOrEmpty(field.text)) continue;
                if (t.isTextOverflowing && t.name != "OperatorName") issues.Add("TEXT OVERFLOW: " + t.transform.parent.name + "/" + t.name + " = " + t.text);
            }
            var corners = new Vector3[4];
            foreach (var control in root.GetComponentsInChildren<Selectable>())
            {
                ((RectTransform)control.transform).GetWorldCorners(corners);
                if (corners.Any(p => !canvas.rect.Contains(canvas.InverseTransformPoint(p)))) issues.Add("CONTROL OFFSCREEN: " + control.name);
            }
            return issues;
        }
    }
}
