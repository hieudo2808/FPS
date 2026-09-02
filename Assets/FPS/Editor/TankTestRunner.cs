using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FPS.Editor
{
    public static class TankTestRunner
    {
        internal const string RunningSessionKey = "FPS.TankTestRunner.IsRunning";

        [MenuItem("FPS/Run Tank Tests")]
        public static void RunTankTests()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            TankPrefabUtility.EnsureTankPrefab();

            UnityEditor.SessionState.SetBool(RunningSessionKey, true);
            TankTestRunCallback.Register();

            var editMode = new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "FPS.Tests.TankTests",
                    "FPS.Tests.TankRegistryTests",
                    "FPS.Tests.TankStaggerTests",
                    "FPS.Tests.PlayerKnockbackTests",
                    "FPS.Tests.TankPrefabSetupTests",
                    "FPS.Tests.TankVerificationSuite"
                }
            };
            var playMode = new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { "FPS.PlayModeTests.TankPlayModeSmokeTests" }
            };

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(editMode, playMode));
        }
    }

    [InitializeOnLoad]
    internal sealed class TankTestRunCallback : ScriptableSingleton<TankTestRunCallback>, ICallbacks
    {
        private static TestRunnerApi registeredApi;

        static TankTestRunCallback()
        {
            EditorApplication.delayCall += () =>
            {
                if (UnityEditor.SessionState.GetBool(TankTestRunner.RunningSessionKey, false))
                    Register();
            };
        }

        internal static void Register()
        {
            if (registeredApi != null)
                return;

            registeredApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            registeredApi.RegisterCallbacks(instance);
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log("[TankTestRunner] Starting Tank EditMode + PlayMode tests...");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string summary = $"Tank Tests Result: Passed={result.PassCount}, Failed={result.FailCount}, "
                + $"Inconclusive={result.InconclusiveCount}, Skipped={result.SkipCount}, ResultState={result.ResultState}";
            Debug.Log("[TankTestRunner] " + summary);

            string logDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(Path.Combine(logDirectory, "TankTestResults.txt"), summary);

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            UnityEditor.SessionState.SetBool(TankTestRunner.RunningSessionKey, false);
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
                Debug.LogError($"[TankTestRunner] FAILED: {result.FullName} -> {result.Message}");
        }
    }
}
