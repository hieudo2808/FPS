using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Editor
{
    public static class OperatorGenericTestRunner
    {
        internal const string RunningKey = "FPS.OperatorGenericTests.Running";
        internal const string PhaseKey = "FPS.OperatorGenericTests.Phase";
        internal const string SceneKey = "FPS.OperatorGenericTests.Scene";
        internal const string PrefabStageKey = "FPS.OperatorGenericTests.PrefabStage";
        internal const string EditSummaryKey = "FPS.OperatorGenericTests.EditSummary";
        internal const string PendingRestoreKey =
            "FPS.OperatorGenericTests.PendingRestore";

        [MenuItem("FPS/Third Person/Generic Path-Bound Weapons/Run Production Tests")]
        public static void Run()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            UnityEditor.SessionState.SetBool(RunningKey, true);
            UnityEditor.SessionState.SetString(PhaseKey, "EditMode");
            UnityEditor.SessionState.SetString(SceneKey, SceneManager.GetActiveScene().path);
            UnityEditor.SessionState.SetString(PrefabStageKey, stage != null ? stage.assetPath : string.Empty);
            UnityEditor.SessionState.EraseString(EditSummaryKey);
            OperatorGenericTestCallback.Register();

            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "FPS.Tests.GenericPathBoundWeaponTests",
                    "FPS.Tests.ThirdPersonLeftHandIKTests",
                    "FPS.Tests.WeaponAnimatorFlowTests.PlayerPrefab_UsesProductionBodyAndDedicatedVandalThirdPersonVisual",
                    "FPS.Tests.WeaponAnimatorFlowTests.PlayerPrefab_ThirdPersonHoldPoseKeepsAuthoritativeGripsAligned"
                }
            };
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(filter));
        }

        [MenuItem("FPS/Third Person/Generic Path-Bound Weapons/Run PlayMode Tests Only")]
        public static void RunPlayModeOnly()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            UnityEditor.SessionState.SetBool(RunningKey, true);
            UnityEditor.SessionState.SetString(PhaseKey, "PlayMode");
            UnityEditor.SessionState.SetString(SceneKey, SceneManager.GetActiveScene().path);
            UnityEditor.SessionState.SetString(
                PrefabStageKey,
                stage != null ? stage.assetPath : string.Empty);
            UnityEditor.SessionState.EraseString(EditSummaryKey);
            OperatorGenericTestCallback.Register();

            var filter = new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[]
                {
                    "FPS.PlayModeTests.VandalThirdPersonProductionPlayModeTests"
                }
            };
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(filter));
        }

        [MenuItem("FPS/Third Person/Generic Path-Bound Weapons/Run Vandal Reload Probe")]
        public static void RunVandalReloadProbe()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            UnityEditor.SessionState.SetBool(RunningKey, true);
            UnityEditor.SessionState.SetString(PhaseKey, "PlayMode");
            UnityEditor.SessionState.SetString(SceneKey, SceneManager.GetActiveScene().path);
            UnityEditor.SessionState.SetString(
                PrefabStageKey,
                stage != null ? stage.assetPath : string.Empty);
            UnityEditor.SessionState.EraseString(EditSummaryKey);
            OperatorGenericTestCallback.Register();

            var filter = new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[]
                {
                    "FPS.PlayModeTests.VandalThirdPersonProductionPlayModeTests."
                    + "MovingReload_KeepsVandalBodyAndGunSynchronizedThroughImpactFrames"
                }
            };
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(filter));
        }
    }

    [InitializeOnLoad]
    internal sealed class OperatorGenericTestCallback
        : ScriptableSingleton<OperatorGenericTestCallback>, ICallbacks
    {
        private static TestRunnerApi registeredApi;

        static OperatorGenericTestCallback()
        {
            EditorApplication.delayCall += () =>
            {
                if (UnityEditor.SessionState.GetBool(
                        OperatorGenericTestRunner.PendingRestoreKey,
                        false))
                {
                    RestorePendingContext();
                    return;
                }
                if (UnityEditor.SessionState.GetBool(OperatorGenericTestRunner.RunningKey, false))
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
            Debug.Log("[GenericPathBoundTests] Test phase started.");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string phase = UnityEditor.SessionState.GetString(
                OperatorGenericTestRunner.PhaseKey,
                "EditMode");
            string phaseSummary = $"{phase}: Passed={result.PassCount}, "
                + $"Failed={result.FailCount}, Inconclusive={result.InconclusiveCount}, "
                + $"Skipped={result.SkipCount}, ResultState={result.ResultState}";
            Debug.Log("[GenericPathBoundTests] " + phaseSummary);

            if (phase == "PlayMode" && EditorApplication.isPlaying)
            {
                FinishRun(phaseSummary, deferRestoreUntilEditMode: true);
                return;
            }

            RestoreInitialSceneAndSave();

            if (phase == "EditMode" && result.FailCount == 0)
            {
                UnityEditor.SessionState.SetString(
                    OperatorGenericTestRunner.EditSummaryKey,
                    phaseSummary);
                UnityEditor.SessionState.SetString(
                    OperatorGenericTestRunner.PhaseKey,
                    "PlayMode");
                EditorApplication.delayCall += () =>
                {
                    RestoreInitialSceneAndSave();
                    var filter = new Filter
                    {
                        testMode = TestMode.PlayMode,
                        testNames = new[]
                        {
                            "FPS.PlayModeTests.VandalThirdPersonProductionPlayModeTests"
                        }
                    };
                    registeredApi.Execute(new ExecutionSettings(filter));
                };
                return;
            }

            FinishRun(phaseSummary, deferRestoreUntilEditMode: false);
        }

        private static void FinishRun(
            string phaseSummary,
            bool deferRestoreUntilEditMode)
        {
            string editSummary = UnityEditor.SessionState.GetString(
                OperatorGenericTestRunner.EditSummaryKey,
                string.Empty);
            string summary = string.IsNullOrEmpty(editSummary)
                ? phaseSummary
                : editSummary + System.Environment.NewLine + phaseSummary;
            string logDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Logs");
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(
                Path.Combine(logDirectory, "GenericPathBoundTestResults.txt"),
                summary);

            UnityEditor.SessionState.SetBool(OperatorGenericTestRunner.RunningKey, false);
            UnityEditor.SessionState.EraseString(OperatorGenericTestRunner.PhaseKey);
            UnityEditor.SessionState.EraseString(OperatorGenericTestRunner.EditSummaryKey);
            if (deferRestoreUntilEditMode)
            {
                UnityEditor.SessionState.SetBool(
                    OperatorGenericTestRunner.PendingRestoreKey,
                    true);
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
                return;
            }

            RestoreInitialContext();
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.ResultState != "Passed")
            {
                Debug.LogWarning(
                    $"[GenericPathBoundTests] {result.FullName}: "
                    + $"{result.ResultState} - {result.Message}");
            }
        }

        private static void RestoreInitialContext()
        {
            string initialScene = UnityEditor.SessionState.GetString(
                OperatorGenericTestRunner.SceneKey,
                string.Empty);
            string prefabStage = UnityEditor.SessionState.GetString(
                OperatorGenericTestRunner.PrefabStageKey,
                string.Empty);
            UnityEditor.SessionState.EraseString(OperatorGenericTestRunner.SceneKey);
            UnityEditor.SessionState.EraseString(OperatorGenericTestRunner.PrefabStageKey);
            RestoreInitialSceneAndSave(initialScene);

            if (!string.IsNullOrEmpty(prefabStage))
            {
                EditorApplication.delayCall += () =>
                {
                    Object prefab = AssetDatabase.LoadAssetAtPath<Object>(prefabStage);
                    if (prefab != null)
                        AssetDatabase.OpenAsset(prefab);
                    EditorSceneManager.SaveOpenScenes();
                    AssetDatabase.SaveAssets();
                };
            }
        }

        private static void HandlePlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.delayCall += RestorePendingContext;
        }

        private static void RestorePendingContext()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RestorePendingContext;
                return;
            }

            UnityEditor.SessionState.SetBool(
                OperatorGenericTestRunner.PendingRestoreKey,
                false);
            RestoreInitialContext();
        }

        private static void RestoreInitialSceneAndSave()
        {
            RestoreInitialSceneAndSave(UnityEditor.SessionState.GetString(
                OperatorGenericTestRunner.SceneKey,
                string.Empty));
        }

        private static void RestoreInitialSceneAndSave(string initialScene)
        {
            // Unity Test Framework can leave a dirty, pathless temporary scene.
            // It is not user-authored state and calling SaveOpenScenes on it opens
            // a blocking Save Scene dialog. Restore the already-saved initial
            // project scene first, then perform the mandatory post-test save gate.
            if (!string.IsNullOrEmpty(initialScene)
                && SceneManager.GetActiveScene().path != initialScene)
            {
                EditorSceneManager.OpenScene(initialScene, OpenSceneMode.Single);
            }
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }
    }
}
