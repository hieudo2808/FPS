using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.EditorTools
{
    public static class DirectorBuildTools
    {
        [MenuItem("Tools/AI Director/Configure Acceptance")]
        public static void Configure()
        {
            DirectorLiveProbe.Save();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/FPS/Scenes/GameScene.unity")
                throw new System.InvalidOperationException("Open authored GameScene before configuring acceptance.");
            var director = Object.FindFirstObjectByType<AIDirector>();
            var manager = Object.FindFirstObjectByType<DifficultyManager>();
            if (director == null || manager == null) throw new System.InvalidOperationException("Missing scene managers.");
            var policy = AssetDatabase.LoadAssetAtPath<AdaptiveDirectorPolicyAsset>("Assets/FPS/Features/AI/Content/AdaptiveDirectorPolicy.asset");
            var difficulty = AssetDatabase.LoadAssetAtPath<AdaptiveDifficultyPolicyAsset>("Assets/FPS/Features/AI/Content/AdaptiveDifficultyPolicy.asset");
            var medium = AssetDatabase.LoadAssetAtPath<StaticDifficultyProfileAsset>("Assets/FPS/Features/AI/Content/MediumSmartProfile.asset");
            if (policy == null || difficulty == null || medium == null)
                throw new System.InvalidOperationException("Explicit policy assets must exist.");
            var p = new SerializedObject(policy);
            p.FindProperty("specialCooldownSeconds").floatValue = 5;
            p.ApplyModifiedPropertiesWithoutUndo();
            var d = new SerializedObject(director);
            d.FindProperty("enableAdaptiveDirector").boolValue = true;
            d.FindProperty("adaptiveObserveOnly").boolValue = false;
            d.FindProperty("useSmartSpawning").boolValue = true;
            d.FindProperty("enableSpecialInfected").boolValue = true;
            d.FindProperty("adaptiveDirectorPolicy").objectReferenceValue = policy;
            d.FindProperty("adaptiveDifficultyPolicy").objectReferenceValue = difficulty;
            d.ApplyModifiedPropertiesWithoutUndo();
            var m = new SerializedObject(medium);
            m.FindProperty("stats").FindPropertyRelative("enableRubberBanding").boolValue = true;
            m.ApplyModifiedPropertiesWithoutUndo();
            var dm = new SerializedObject(manager);
            var overrides = dm.FindProperty("profileOverrides");
            bool assigned = false;
            for (int i = 0; i < overrides.arraySize; i++)
            {
                var existing = overrides.GetArrayElementAtIndex(i).objectReferenceValue as StaticDifficultyProfileAsset;
                if (existing != null && existing.Difficulty == DifficultyLevel.Medium)
                { overrides.GetArrayElementAtIndex(i).objectReferenceValue = medium; assigned = true; break; }
            }
            if (!assigned) { overrides.arraySize++; overrides.GetArrayElementAtIndex(overrides.arraySize - 1).objectReferenceValue = medium; }
            dm.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            DirectorLiveProbe.Save();
            Debug.Log("[Director config] adaptive=true observe=false smart=true special=true Medium rubber=true specialDelay=5 sceneDirty=" + scene.isDirty);
        }
        [MenuItem("Tools/AI Director/Build Clean Baseline")]
        public static void BuildBaseline() => Build("DirectorBaseline");

        [MenuItem("Tools/AI Director/Build Acceptance Player")]
        public static void BuildAcceptance() => Build("DirectorAcceptance");

        private static void Build(string label)
        {
            DirectorLiveProbe.Save();
            string path = Path.GetFullPath("Builds/" + label + "/FPS.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/FPS/Scenes/MainMenu.unity", "Assets/FPS/Scenes/LobbyScene.unity", "Assets/FPS/Scenes/GameScene.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | (label == "DirectorBaseline" ? BuildOptions.CleanBuildCache : BuildOptions.None)
            });
            Directory.CreateDirectory("Documents/AI-Director-Defense/Captures");
            File.WriteAllText("Documents/AI-Director-Defense/Captures/" + label + "-build.txt",
                report.summary.result + "\nerrors=" + report.summary.totalErrors + "\nseconds=" + report.summary.totalTime.TotalSeconds);
            DirectorLiveProbe.Save();
            Debug.Log("[Director build] " + label + ": " + report.summary.result);
        }
    }
}
