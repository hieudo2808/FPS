using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Editor
{
    public static class InfectionTestRunner
    {
        internal const string RunningSessionKey = "FPS.InfectionTestRunner.IsRunning";
        internal const string PhaseSessionKey = "FPS.InfectionTestRunner.Phase";
        internal const string InitialSceneSessionKey = "FPS.InfectionTestRunner.InitialScene";
        internal const string EditSummarySessionKey = "FPS.InfectionTestRunner.EditSummary";

        [MenuItem("FPS/Run Infection & Infector Tests")]
        public static void RunInfectionTests()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            UnityEditor.SessionState.SetBool(RunningSessionKey, true);
            UnityEditor.SessionState.SetString(PhaseSessionKey, "EditMode");
            UnityEditor.SessionState.SetString(InitialSceneSessionKey, SceneManager.GetActiveScene().path);
            UnityEditor.SessionState.EraseString(EditSummarySessionKey);
            InfectionTestRunCallback.Register();

            var editMode = new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "FPS.Tests.InfectionSystemTests",
                    "FPS.Tests.InfectorPrefabSetupTests",
                    "FPS.Tests.InfectorAIStateTests"
                }
            };

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(editMode));
        }
    }

    [InitializeOnLoad]
    internal sealed class InfectionTestRunCallback : ScriptableSingleton<InfectionTestRunCallback>, ICallbacks
    {
        private static TestRunnerApi registeredApi;

        static InfectionTestRunCallback()
        {
            EditorApplication.delayCall += () =>
            {
                if (UnityEditor.SessionState.GetBool(InfectionTestRunner.RunningSessionKey, false))
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
            Debug.Log("[InfectionTestRunner] Starting Infection & Infector tests...");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string phase = UnityEditor.SessionState.GetString(InfectionTestRunner.PhaseSessionKey, "EditMode");
            string phaseSummary = $"{phase}: Passed={result.PassCount}, Failed={result.FailCount}, "
                + $"Inconclusive={result.InconclusiveCount}, Skipped={result.SkipCount}, ResultState={result.ResultState}";
            Debug.Log("[InfectionTestRunner] " + phaseSummary);

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            if (phase == "EditMode" && result.FailCount == 0)
            {
                UnityEditor.SessionState.SetString(InfectionTestRunner.EditSummarySessionKey, phaseSummary);
                UnityEditor.SessionState.SetString(InfectionTestRunner.PhaseSessionKey, "PlayMode");
                var playMode = new Filter
                {
                    testMode = TestMode.PlayMode,
                    testNames = new[] { "FPS.PlayModeTests.InfectorGameScenePlayModeTests" }
                };
                EditorApplication.delayCall += () =>
                {
                    EditorSceneManager.SaveOpenScenes();
                    AssetDatabase.SaveAssets();
                    registeredApi.Execute(new ExecutionSettings(playMode));
                };
                return;
            }

            string editSummary = UnityEditor.SessionState.GetString(InfectionTestRunner.EditSummarySessionKey, string.Empty);
            string summary = string.IsNullOrEmpty(editSummary)
                ? phaseSummary
                : $"{editSummary}{System.Environment.NewLine}{phaseSummary}";

            string logDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(Path.Combine(logDirectory, "InfectionTestResults.txt"), summary);

            UnityEditor.SessionState.SetBool(InfectionTestRunner.RunningSessionKey, false);
            UnityEditor.SessionState.EraseString(InfectionTestRunner.PhaseSessionKey);
            UnityEditor.SessionState.EraseString(InfectionTestRunner.EditSummarySessionKey);

            string initialScene = UnityEditor.SessionState.GetString(InfectionTestRunner.InitialSceneSessionKey, string.Empty);
            UnityEditor.SessionState.EraseString(InfectionTestRunner.InitialSceneSessionKey);
            if (!string.IsNullOrEmpty(initialScene) && SceneManager.GetActiveScene().path != initialScene)
                EditorSceneManager.OpenScene(initialScene, OpenSceneMode.Single);

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.ResultState != "Passed")
            {
                Debug.LogWarning($"[InfectionTestRunner] Test {result.Name} {result.ResultState}: {result.Message}");
            }
        }
    }
}
