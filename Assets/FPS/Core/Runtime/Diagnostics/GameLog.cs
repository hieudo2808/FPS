using System;
using System.Diagnostics;
using System.IO;
using UnityDebug = UnityEngine.Debug;

namespace FPS
{
    public static class GameLog
    {
        public static bool Enabled => UnityDebug.isDebugBuild;

        public static void DebugSession(string runId, string hypothesisId, string location, string message, string data)
        {
            try
            {
                string path = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "debug-d90a1c.log");
                string escapedData = string.IsNullOrEmpty(data) ? "{}" : data;
                string payload = $"{{\"sessionId\":\"d90a1c\",\"runId\":\"{runId}\",\"hypothesisId\":\"{hypothesisId}\",\"location\":\"{location}\",\"message\":\"{message}\",\"data\":{escapedData},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
                File.AppendAllText(path, payload + Environment.NewLine);
            }
            catch
            {
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message)
        {
            UnityDebug.Log(message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Info(Func<string> messageFactory)
        {
            if (messageFactory != null)
                UnityDebug.Log(messageFactory());
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(string message)
        {
            UnityDebug.LogWarning(message);
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(Func<string> messageFactory)
        {
            if (messageFactory != null)
                UnityDebug.LogWarning(messageFactory());
        }

        public static void Error(string message)
        {
            UnityDebug.LogError(message);
        }
    }
}
