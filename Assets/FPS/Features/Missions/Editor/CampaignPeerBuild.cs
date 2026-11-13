using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FPS.EditorTools
{
    /// <summary>Development Player used as the remote peer in CampaignHostClientTests.</summary>
    public static class CampaignPeerBuild
    {
        public const string PlayerPath = "Builds/CampaignPeer/FPS.exe";

        [MenuItem("Tools/FPS/Campaign/Build Peer Client")]
        public static void Build()
        {
            string path = Path.GetFullPath(PlayerPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/FPS/Scenes/MainMenu.unity", "Assets/FPS/Scenes/LobbyScene.unity", "Assets/FPS/Scenes/GameScene.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "build-result.txt"),
                report.summary.result + "\nerrors=" + report.summary.totalErrors + "\nwarnings=" + report.summary.totalWarnings
                + "\nseconds=" + report.summary.totalTime.TotalSeconds.ToString("0") + "\nbytes=" + report.summary.totalSize);
            Debug.Log("[Campaign peer build] " + report.summary.result + " -> " + path);
        }
    }
}
