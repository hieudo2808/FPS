using System;
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FPS.Editor
{
    public static class CodexPerformanceBaselineRunner
    {
        private const string TestName = "FPS.PlayModeTests.PerformanceBaselinePlayModeTests.HordePerformanceBaseline_RecordsSixtySecondRuntimeMetrics";
        private const string ResultsPath = "Logs/CodexPerformanceBaselineResults.xml";
        private const string StatusPath = "Logs/Performance/CodexPerformanceBaselineStatus.txt";

        public static void Run()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Performance"));
            File.WriteAllText(ProjectPath(StatusPath), $"Running {TestName}{Environment.NewLine}StartedUtc: {DateTime.UtcNow:O}{Environment.NewLine}");

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new BaselineCallbacks(api);
            api.RegisterCallbacks(callbacks);

            string jobId = api.Execute(new ExecutionSettings(new Filter
            {
                testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode,
                testNames = new[] { TestName }
            }));

            File.AppendAllText(ProjectPath(StatusPath), $"JobId: {jobId}{Environment.NewLine}");
            Debug.Log($"[CodexPerformanceBaselineRunner] Started {TestName} ({jobId})");
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class BaselineCallbacks : ICallbacks
        {
            private readonly TestRunnerApi api;

            public BaselineCallbacks(TestRunnerApi api)
            {
                this.api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                File.AppendAllText(ProjectPath(StatusPath), $"RunStartedUtc: {DateTime.UtcNow:O}{Environment.NewLine}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, ResultsPath);
                File.AppendAllText(ProjectPath(StatusPath),
                    $"RunFinishedUtc: {DateTime.UtcNow:O}{Environment.NewLine}" +
                    $"Status: {result.TestStatus}{Environment.NewLine}" +
                    $"ResultState: {result.ResultState}{Environment.NewLine}" +
                    $"Passed: {result.PassCount}{Environment.NewLine}" +
                    $"Failed: {result.FailCount}{Environment.NewLine}" +
                    $"Skipped: {result.SkipCount}{Environment.NewLine}" +
                    $"DurationSeconds: {result.Duration:F3}{Environment.NewLine}");

                api.UnregisterCallbacks(this);
                UnityEngine.Object.DestroyImmediate(api);
                Debug.Log($"[CodexPerformanceBaselineRunner] Finished {result.TestStatus} in {result.Duration:F3}s");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.FullName == TestName && result.ResultState.StartsWith("Failed", StringComparison.Ordinal))
                {
                    File.AppendAllText(ProjectPath(StatusPath),
                        $"FailureMessage: {result.Message}{Environment.NewLine}" +
                        $"FailureStackTrace: {result.StackTrace}{Environment.NewLine}");
                }
            }
        }
    }
}
