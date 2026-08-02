#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FPS.EditorTools
{
    public static class CodexA1Build
    {
        public static void BuildWindowsStandalone()
        {
            const string outputPath = "Builds/A1/FPS.exe";
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"A1 standalone build failed: {report.summary.result}");

            Debug.Log($"[CodexA1Build] Built {outputPath} ({report.summary.totalSize} bytes)");
        }

        public static void EnsurePlayerInteractionManager()
        {
            const string prefabPath = "Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<FPS.InteractionManager>() == null)
                {
                    root.AddComponent<FPS.InteractionManager>();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log("[CodexA1Build] Added InteractionManager to Player prefab.");
                }
                else
                    Debug.Log("[CodexA1Build] Player prefab already has InteractionManager.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
