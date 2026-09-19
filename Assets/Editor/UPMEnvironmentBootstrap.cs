using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FPS.AIKnowledge.Editor
{
    /// <summary>
    /// Ensures standard Windows environment variables required by Unity Package Manager (Node.js daemon)
    /// are present in the current Unity Editor process, even when Unity is launched from stripped environments (CLI, IDEs, sandboxes).
    /// </summary>
    [InitializeOnLoad]
    internal static class UPMEnvironmentBootstrap
    {
        static UPMEnvironmentBootstrap()
        {
            Bootstrap();
        }

        private static void Bootstrap()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return;
            }

            string allUsers = Environment.GetEnvironmentVariable("ALLUSERSPROFILE", EnvironmentVariableTarget.Process);
            string upmGlobal = Environment.GetEnvironmentVariable("UPM_GLOBAL_CONFIG_FILE", EnvironmentVariableTarget.Process);

            // If ALLUSERSPROFILE is already present and valid, no bootstrap needed
            if (!string.IsNullOrEmpty(allUsers) && !string.IsNullOrEmpty(upmGlobal))
            {
                return;
            }

            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(programData))
            {
                programData = Environment.GetEnvironmentVariable("ProgramData", EnvironmentVariableTarget.Process);
            }
            if (string.IsNullOrEmpty(programData))
            {
                programData = @"C:\ProgramData";
            }

            bool environmentUpdated = false;

            if (string.IsNullOrEmpty(allUsers))
            {
                Environment.SetEnvironmentVariable("ALLUSERSPROFILE", programData, EnvironmentVariableTarget.Process);
                environmentUpdated = true;
            }

            if (string.IsNullOrEmpty(upmGlobal))
            {
                string globalConfig = Path.Combine(programData, "Unity", "config", "upmconfig.toml");
                Environment.SetEnvironmentVariable("UPM_GLOBAL_CONFIG_FILE", globalConfig, EnvironmentVariableTarget.Process);
                environmentUpdated = true;
            }

            string upmLegacy = Environment.GetEnvironmentVariable("UPM_CONFIG_FILE", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(upmLegacy))
            {
                string legacyConfig = Path.Combine(programData, "Unity", "config", "upm.json");
                Environment.SetEnvironmentVariable("UPM_CONFIG_FILE", legacyConfig, EnvironmentVariableTarget.Process);
                environmentUpdated = true;
            }

            if (environmentUpdated)
            {
                Debug.Log($"[UPMEnvironmentBootstrap] Initialized missing environment variables in Editor process memory (ALLUSERSPROFILE='{programData}').");

                // If UnityPackageManager.exe was already launched with the incomplete environment block prior to C# loading,
                // terminate it so Unity will spawn a fresh daemon instance with the updated environment block.
                RestartStaleUpmProcesses();
            }
        }

        private static void RestartStaleUpmProcesses()
        {
            try
            {
                Process[] upmProcesses = Process.GetProcessesByName("UnityPackageManager");
                foreach (Process proc in upmProcesses)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill();
                            proc.WaitForExit(1000);
                        }
                    }
                    catch
                    {
                        // Ignored if process already exited or access denied
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UPMEnvironmentBootstrap] Note: Could not restart stale UPM process: {ex.Message}");
            }
        }
    }
}
