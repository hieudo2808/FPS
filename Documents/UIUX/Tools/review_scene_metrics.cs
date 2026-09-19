// One-off read-only audit body for Gerty unity_execute; not compiled into the game.
if (UnityEditor.EditorApplication.isPlaying)
    throw new System.InvalidOperationException("Run the metrics audit in Edit Mode.");
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
UnityEditor.AssetDatabase.SaveAssets();
var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var lines = new System.Collections.Generic.List<string>();
lines.Add("scene\tpath\tactive\ttext\tfontSizeTMP\tinkHeightCanvas\trectWidth\trectHeight\tcolor");
try
{
    foreach (var sceneName in new[] { "MainMenu", "LobbyScene", "GameScene" })
    {
        string path = "Assets/FPS/Scenes/" + sceneName + ".unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                t.ForceMeshUpdate(true);
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                for (int i = 0; i < t.textInfo.characterCount; i++)
                {
                    var c = t.textInfo.characterInfo[i];
                    if (!c.isVisible) continue;
                    low = Mathf.Min(low, c.bottomLeft.y); high = Mathf.Max(high, c.topRight.y);
                }
                float ink = float.IsInfinity(low) ? 0 : high - low;
                string objectPath = UnityEditor.AnimationUtility.CalculateTransformPath(t.transform, null);
                lines.Add(sceneName + "\t" + objectPath + "\t" + t.gameObject.activeInHierarchy + "\t" + t.text.Replace("\t", " ").Replace("\n", " | ") + "\t" + t.fontSize.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "\t" + ink.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "\t" + t.rectTransform.rect.width.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "\t" + t.rectTransform.rect.height.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "\t" + ColorUtility.ToHtmlStringRGBA(t.color));
            }
        }
        finally
        {
            if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }
    System.IO.File.WriteAllLines("Documents/UIUX/ui-review-scene-metrics.tsv", lines);
    Debug.Log("UI metrics recorded: " + (lines.Count - 1) + " text elements, including inactive/sample states. TMP mesh bounds are diagnostic, not raster body-height certification.");
}
finally
{
    if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    UnityEditor.AssetDatabase.SaveAssets();
}
