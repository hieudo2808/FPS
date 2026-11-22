using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.EditorTools
{
    public static class DirectorBenchmarkBuild
    {
        private static string Root => Path.GetDirectoryName(Application.dataPath);
        private static string Report => Path.Combine(Root, "Reports/DirectorBenchmark-2026-11-20/v2");
        [MenuItem("Tools/FPS/Director Benchmark/Save Gate")]
        public static void SaveGate()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || string.IsNullOrEmpty(scene.path) || !AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path))) continue;
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed: " + scene.path);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/FPS/Director Benchmark/Build Before")]
        public static void Before() => Build("before");
        [MenuItem("Tools/FPS/Director Benchmark/Build After")]
        public static void After() => Build("after");
        [MenuItem("Tools/FPS/Director Benchmark/Exit For Measurement")]
        public static void ExitForMeasurement()
        {
            SaveGate();
            var scene = SceneManager.GetActiveScene();
            File.WriteAllText(Path.Combine(Report, "editor-before-measurement.txt"), $"scene={scene.path}\nisDirty={scene.isDirty}\n");
            EditorApplication.Exit(0);
        }
        private static void Build(string version)
        {
            SaveGate();
            Directory.CreateDirectory(Report);
            string resultPath = Path.Combine(Report, "build-" + version + ".txt");
            try
            {
                string path = Path.Combine(Root, "Builds/DirectorBenchmark/v2/" + version + "/FPS.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var director = UnityEngine.Object.FindAnyObjectByType<AIDirector>();
                string flags = director != null ? EditorJsonUtility.ToJson(director, true) : "Director not in active scene";
                File.WriteAllText(Path.Combine(Report, "scene-config-" + version + ".json"), flags);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/FPS/Scenes/MainMenu.unity", "Assets/FPS/Scenes/LobbyScene.unity", "Assets/FPS/Scenes/GameScene.unity" },
                    locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Root, "Assets/FPS/Features/AI/Runtime/AIDirector.AdaptiveBridge.cs")))).Replace("-", "");
                string fixtureHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Root, "Assets/FPS/Features/AI/Runtime/DirectorBenchmark.cs")))).Replace("-", "");
                File.WriteAllText(resultPath, $"result={report.summary.result}\nerrors={report.summary.totalErrors}\nwarnings={report.summary.totalWarnings}\n"
                    + $"seconds={report.summary.totalTime.TotalSeconds:F1}\nbackend={PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone)}\n"
                    + $"sourceSha256={hash}\nfixtureSha256={fixtureHash}\npath={path}\n");
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Benchmark build failed.");
            }
            catch (Exception ex) { File.AppendAllText(resultPath, ex.ToString()); Debug.LogException(ex); }
            finally { SaveGate(); }
        }
    }
}
