using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    // Also usable when UnitySkills' zero-match job remains stuck after Test Runner
    // has completed. Runs the real Unity TestRunner API and exports every failure.
    public static class DirectorAcceptanceTestRunner
    {
        private static TestRunnerApi api;
        private static string label;
        [InitializeOnLoadMethod]
        private static void RestoreCallbacks()
        {
            label = UnityEditor.SessionState.GetString("Director.TestLabel", "");
            if (string.IsNullOrEmpty(label)) return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }
        [Serializable] private sealed class Results
        {
            public string utc, result;
            public int passed, failed, skipped;
            public List<string> failures = new List<string>();
        }
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
        [MenuItem("Tools/AI Director/Tests/Run Domain")]
        public static void Domain() => Run("domain", TestMode.EditMode, new[] {
            "FPS.Tests.AdaptiveTelemetryTests", "FPS.Tests.AdaptivePacingTests",
            "FPS.Tests.AdaptiveEvidenceTests", "FPS.Tests.DirectorPressureTests" });

        [MenuItem("Tools/AI Director/Tests/Run Settings")]
        public static void Settings() => Run("settings", TestMode.EditMode, new[] { "FPS.Tests.SettingsDifficultyRegressionTests" });

        [MenuItem("Tools/AI Director/Tests/Run Director Integration")]
        public static void DirectorIntegration() => Run("director-integration", TestMode.PlayMode,
            new[] { "FPS.Tests.AdaptiveDirectorIntegrationTests" });

        [MenuItem("Tools/AI Director/Tests/Run Integration")]
        public static void Integration() => Run("integration", TestMode.PlayMode, new[] {
            "FPS.Tests.AdaptiveDirectorIntegrationTests", "FPS.Tests.CampaignLifecycleTests", "FPS.Tests.SurvivalNetworkTests" });

        private static void Run(string name, TestMode mode, string[] names)
        {
            Save();
            label = name;
            UnityEditor.SessionState.SetString("Director.TestLabel", name);
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = mode, testNames = names }));
        }
        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                var report = new Results { utc = DateTime.UtcNow.ToString("O"), result = result.ResultState,
                    passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount };
                Collect(result, report.failures);
                Directory.CreateDirectory("Documents/AI-Director-Defense/Captures");
                File.WriteAllText("Documents/AI-Director-Defense/Captures/tests-" + label + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json",
                    JsonUtility.ToJson(report, true));
                Save();
                UnityEditor.SessionState.EraseString("Director.TestLabel");
                Debug.Log($"[Director tests] {label}: {report.passed} passed, {report.failed} failed, {report.skipped} skipped");
            }
            private static void Collect(ITestResultAdaptor result, List<string> failures)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                    failures.Add(result.Test.FullName + "\n" + result.Message + "\n" + result.StackTrace);
                foreach (var child in result.Children) Collect(child, failures);
            }
        }
    }
}
