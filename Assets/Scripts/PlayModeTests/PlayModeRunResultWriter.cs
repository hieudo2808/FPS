using System.IO;
using System.Text;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(FPS.PlayModeTests.PlayModeRunResultWriter))]

namespace FPS.PlayModeTests
{
    /// <summary>
    /// Ghi kết quả test run ra Logs/ClaudeTestRunSummary.txt.
    /// Dùng TestRunCallback attribute vì instance callback của TestRunnerApi
    /// không sống qua PlayMode domain reload trong editor.
    /// </summary>
    public class PlayModeRunResultWriter : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun)
        {
        }

        public void TestStarted(ITest test)
        {
        }

        public void TestFinished(ITestResult result)
        {
        }

        public void RunFinished(ITestResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"state={result.ResultState}");
            sb.AppendLine($"passed={result.PassCount}");
            sb.AppendLine($"failed={result.FailCount}");
            sb.AppendLine($"skipped={result.SkipCount}");
            sb.AppendLine($"duration={result.Duration:F3}");
            AppendFailures(result, sb);

            string path = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Logs",
                "ClaudeTestRunSummary.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
        }

        private static void AppendFailures(ITestResult result, StringBuilder sb)
        {
            if (!result.HasChildren)
            {
                if (result.ResultState.Status == TestStatus.Failed)
                    sb.AppendLine($"FAILED {result.FullName}: {result.Message}");
                return;
            }

            foreach (ITestResult child in result.Children)
                AppendFailures(child, sb);
        }
    }
}
