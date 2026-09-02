using System;
using UnityEditor;

namespace FPS.Editor
{
    /// <summary>
    /// Ensures essential system environment variables like ALLUSERSPROFILE and ProgramData
    /// are present in the Unity Editor process so that Unity Package Manager (Node.js backend)
    /// does not fail with "The 'path' argument must be of type string. Received undefined".
    /// </summary>
    [InitializeOnLoad]
    public static class EnsureUpmEnvironment
    {
        static EnsureUpmEnvironment()
        {
            EnsureVariables();
        }

        [InitializeOnLoadMethod]
        private static void EnsureVariables()
        {
            try
            {
                var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALLUSERSPROFILE")) && !string.IsNullOrEmpty(commonAppData))
                {
                    Environment.SetEnvironmentVariable("ALLUSERSPROFILE", commonAppData, EnvironmentVariableTarget.Process);
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramData")) && !string.IsNullOrEmpty(commonAppData))
                {
                    Environment.SetEnvironmentVariable("ProgramData", commonAppData, EnvironmentVariableTarget.Process);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[EnsureUpmEnvironment] Failed to ensure environment variables: {ex.Message}");
            }
        }
    }
}
