#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS
{
    /// <summary>
    /// Opt-in Development Player fixture for host + client acceptance. Launched with
    /// <c>-campaignPeer &lt;port&gt; &lt;outputDir&gt;</c> it joins a loopback host as an ordinary client, reports what
    /// that client observes to <c>status.json</c>, and sends the requests listed in <c>commands.txt</c> through the
    /// same public request methods the HUD uses. It never touches server state directly.
    /// </summary>
    public sealed class CampaignPeerProbe : MonoBehaviour
    {
        [Serializable]
        public sealed class Status
        {
            public int sequence;
            public bool started, connected, isServer, hasCampaign, hasPlayer, inputReady, hudPresent;
            public ulong localClientId;
            public string scene, lastMessage;
            public uint revision;
            public int chapter, phase, tankStage, rosterSize, lifeState, commandsDone;
            public ulong completed, discoveredFiles;
            public Vector3 position;
            public string supplyVisible; // ids this client currently shows on the tables, ordinal order
            public int reserveAmmo;
        }

        private string output;
        private ushort port;
        private int commandsRead;
        private float nextStatus;
        private readonly Status status = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            if (Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-campaignPeer");
            if (index < 0 || index + 2 >= args.Length || !ushort.TryParse(args[index + 1], out ushort port)) return;
            var probe = new GameObject("Campaign peer probe").AddComponent<CampaignPeerProbe>();
            probe.port = port;
            probe.output = Path.GetFullPath(args[index + 2]);
            DontDestroyOnLoad(probe.gameObject);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            float deadline = Time.realtimeSinceStartup + 30;
            while (NetworkManager.Singleton == null && Time.realtimeSinceStartup < deadline) yield return null;
            var network = NetworkManager.Singleton;
            if (network == null) { status.lastMessage = "NetworkManager missing in " + SceneManager.GetActiveScene().name; yield break; }
            // Mirror the review host's loopback session; both peers must agree on these.
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            var transport = network.GetComponent<UnityTransport>();
            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.Port = port;
            // The Editor host freezes for tens of seconds while it loads GameScene; do not treat that silence as a dead host.
            transport.DisconnectTimeoutMS = 120000;
            // Connection diagnostics: written straight to disk because the host kills this process without a flush.
            string trace = Path.Combine(output, "probe-trace.log");
            void Trace(string line) { try { File.AppendAllText(trace, $"[{Time.realtimeSinceStartup:0.0}s f{Time.frameCount}] {line}" + Environment.NewLine); } catch (IOException) { } }
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Log && !message.Contains("Netcode")) return;
                Trace(type + ": " + message + (type == LogType.Log ? string.Empty : Environment.NewLine + stack));
            };
            network.OnClientConnectedCallback += id => Trace($"OnClientConnected {id}");
            network.OnClientDisconnectCallback += id => Trace($"OnClientDisconnect {id} reason='{network.DisconnectReason}'" + Environment.NewLine + Environment.StackTrace);
            network.OnClientStopped += wasHost => Trace("OnClientStopped" + Environment.NewLine + Environment.StackTrace);
            network.OnTransportFailure += () => Trace("OnTransportFailure");
            SceneManager.sceneLoaded += (scene, mode) => Trace($"sceneLoaded {scene.name} {mode}");
            SceneManager.sceneUnloaded += scene => Trace($"sceneUnloaded {scene.name}");
            Trace("StartClient");
            status.started = network.StartClient();
            if (network.SceneManager != null)
                network.SceneManager.OnSceneEvent += e => Trace($"scene event {e.SceneEventType} {e.SceneName} progress={(e.AsyncOperation != null ? e.AsyncOperation.progress : -1f):0.00}");
            status.lastMessage = "StartClient=" + status.started;
        }

        private void Update()
        {
            ReadCommands();
            if (Time.realtimeSinceStartup < nextStatus) return;
            nextStatus = Time.realtimeSinceStartup + .2f;
            WriteStatus();
        }

        private void WriteStatus()
        {
            var network = NetworkManager.Singleton;
            var campaign = CampaignMissionController.Instance;
            status.sequence++;
            status.connected = network != null && network.IsConnectedClient;
            status.isServer = network != null && network.IsServer;
            status.localClientId = network != null ? network.LocalClientId : 0;
            status.scene = SceneManager.GetActiveScene().name;
            status.hasCampaign = campaign != null && campaign.IsSpawned;
            status.hudPresent = CampaignHUD.Instance != null;
            if (status.hasCampaign)
            {
                var state = campaign.State;
                status.revision = state.revision; status.chapter = (int)state.chapter; status.phase = (int)state.phase;
                status.completed = state.completed; status.discoveredFiles = state.discoveredFiles; status.tankStage = (int)state.tankStage;
                status.rosterSize = campaign.InsertionPartySize;
                var visible = new System.Collections.Generic.List<string>();
                foreach (var supply in campaign.supplies)
                    if (supply != null && supply.chapter == state.chapter && supply.IsAvailable) visible.Add(supply.supplyId);
                visible.Sort(StringComparer.Ordinal);
                status.supplyVisible = string.Join(",", visible);
            }
            var player = network != null && network.LocalClient != null && network.LocalClient.PlayerObject != null
                ? network.LocalClient.PlayerObject.GetComponent<PlayerHealth>() : null;
            status.hasPlayer = player != null;
            if (player != null)
            {
                status.lifeState = (int)player.LifeState; status.inputReady = player.IsInputReady; status.position = player.transform.position;
                var weapons = player.GetComponent<WeaponManager>();
                var weapon = weapons != null ? weapons.GetWeapon(weapons.CurrentWeaponIndex) : null;
                status.reserveAmmo = weapon != null ? weapon.ReservedAmmo : -1;
            }
            try
            {
                string path = Path.Combine(output, "status.json"), temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(status));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (IOException) { } // The host is reading; the next tick writes again.
        }

        private void ReadCommands()
        {
            string path = Path.Combine(output, "commands.txt");
            if (!File.Exists(path)) return;
            string[] lines;
            try { lines = File.ReadAllLines(path); } catch (IOException) { return; }
            for (; commandsRead < lines.Length; commandsRead++)
            {
                string[] parts = lines[commandsRead].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                var campaign = CampaignMissionController.Instance;
                status.lastMessage = "command: " + lines[commandsRead];
                switch (parts[0])
                {
                    case "hold" when campaign != null && parts.Length >= 5:
                        StartCoroutine(Hold((CampaignObjectiveId)int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), float.Parse(parts[4])));
                        continue; // Counted when the hold ends.
                    case "discover" when campaign != null && parts.Length >= 2: campaign.RequestDiscoverFile((CampaignFileId)int.Parse(parts[1])); break;
                    case "retry" when campaign != null: campaign.RequestRetry(true); break;
                    case "ready" when campaign != null: campaign.RequestReady(true); break;
                    case "supply" when campaign != null && parts.Length >= 3: campaign.RequestSupply(parts[1], parts[2] == "1"); break;
                    case "quit": Application.Quit(); break;
                }
                status.commandsDone++;
            }
        }

        private IEnumerator Hold(CampaignObjectiveId id, int first, int second, float seconds)
        {
            var campaign = CampaignMissionController.Instance;
            campaign.RequestHold(id, first, second, false);
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until && CampaignMissionController.Instance != null)
            {
                campaign.RequestHold(id, first, second, true);
                yield return new WaitForSecondsRealtime(.25f);
            }
            if (CampaignMissionController.Instance != null) campaign.RequestHold(id, first, second, false);
            status.commandsDone++;
        }
    }
}
#endif
