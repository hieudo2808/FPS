using System;
using System.Diagnostics;
using UnityDebug = UnityEngine.Debug;

namespace FPS
{
    public static class GameLog
    {
        public static bool Enabled => UnityDebug.isDebugBuild;

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
