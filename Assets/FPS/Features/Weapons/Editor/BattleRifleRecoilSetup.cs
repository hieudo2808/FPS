using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FPS.EditorTools
{
    public static class BattleRifleRecoilSetup
    {
        [MenuItem("Tools/Recoil/Save Authored Assets")]
        public static void Save()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path) && !scene.name.StartsWith("Untitled")
                    && !scene.path.StartsWith("Assets/InitTestScene", StringComparison.Ordinal)
                    && !EditorSceneManager.IsPreviewScene(scene) && scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
