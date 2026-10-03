using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.UI.Editor
{
    public static class SurvivalUiVerification
    {
        public static void CaptureAll()
        {
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768) })
            {
                Debug.Log(UiVisualVerification.Capture("GameScene", "survival", size.x, size.y));
                Debug.Log(UiVisualVerification.Capture("MainMenu", "controls", size.x, size.y));
            }
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        }
    }
}
