#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using TMPro;
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
            public string scene, lastMessage, radioCaption, waypointQa;
            public uint revision;
            public int chapter, phase, tankStage, rosterSize, lifeState, commandsDone;
            public ulong completed, discoveredFiles;
            public Vector3 position;
            public string supplyVisible; // ids this client currently shows on the tables, ordinal order
            public int reserveAmmo;
            // Enemy hit reactions this client presented from replicated state.
            public int enemyHitsPresented, enemyBloodSpawned;
            public string lastEnemyHitKind, lastEnemyHitBone;
            // Player life presentation seen by this client.
            public string heldWeapon, otherLifeClip;
            public bool sidearmUsable, primaryUsable;
            public float eyeDrop, otherRaise;
            public int otherLifeState;
            // Audio this client played: per-event counts, music cue and the other player's emitters.
            public string audioCounts, musicCue, musicWanted;
            public bool ambience, listener, ownBreathing, otherBreathing;
            public int ownReloadCues, otherFired, otherVoiceLines;
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
            status.radioCaption = CampaignHUD.Instance?.RadioCaption ?? string.Empty;
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
            status.enemyHitsPresented = EnemyHitReactionPresenter.PlayedCount;
            status.enemyBloodSpawned = EnemyHitReactionPresenter.BloodSpawnedCount;
            status.lastEnemyHitKind = EnemyHitReactionPresenter.LastKind.ToString();
            status.lastEnemyHitBone = EnemyHitReactionPresenter.LastBone;
            status.hasPlayer = player != null;
            foreach (var other in FindObjectsByType<PlayerLifePresenter>(FindObjectsInactive.Exclude))
            {
                var otherHealth = other.GetComponent<PlayerHealth>();
                if (otherHealth == null || otherHealth == player) continue;
                status.otherLifeState = (int)otherHealth.LifeState;
                status.otherLifeClip = other.ActiveClipName;
                status.otherRaise = other.RaiseWeight;
                var otherAudio = other.GetComponent<PlayerAudio>();
                status.otherBreathing = otherAudio != null && otherAudio.Breathing;
                status.otherVoiceLines = otherAudio != null ? otherAudio.VoiceLinesPlayed : 0;
                var otherWeapons = other.GetComponent<WeaponAudio>();
                status.otherFired = otherWeapons != null ? otherWeapons.FiredCount : 0;
            }
            status.audioCounts = GameAudio.CountsSummary();
            status.musicCue = GameAudio.CurrentMusic.ToString();
            status.musicWanted = GameAudio.WantedMusic.ToString();
            status.ambience = GameAudio.AmbiencePlaying;
            status.listener = GameAudio.TryGetListenerPosition(out _);
            if (player != null)
            {
                status.lifeState = (int)player.LifeState; status.inputReady = player.IsInputReady; status.position = player.transform.position;
                var weapons = player.GetComponent<WeaponManager>();
                var weapon = weapons != null ? weapons.GetWeapon(weapons.CurrentWeaponIndex) : null;
                status.reserveAmmo = weapon != null ? weapon.ReservedAmmo : -1;
                status.heldWeapon = weapon != null && weapon.Data != null ? weapon.Data.weaponName : string.Empty;
                status.sidearmUsable = weapons != null && weapons.FindDownedWeaponSlot() >= 0 && player.CanUseWeapon(weapons.GetWeapon(weapons.FindDownedWeaponSlot()).Data);
                status.primaryUsable = weapons != null && player.CanUseWeapon(weapons.GetWeapon(0)?.Data);
                status.eyeDrop = player.GetComponent<PlayerLifePresenter>()?.EyeDrop ?? 0f;
                var ownAudio = player.GetComponent<PlayerAudio>();
                status.ownBreathing = ownAudio != null && ownAudio.Breathing;
                var ownWeapons = player.GetComponent<WeaponAudio>();
                status.ownReloadCues = ownWeapons != null ? ownWeapons.ReloadCuesPlayed : 0;
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
                    case "waypoint" when CampaignHUD.Instance != null && MouseMovement.LocalInstance != null:
                        StartCoroutine(CheckWaypoint(CampaignHUD.Instance, MouseMovement.LocalInstance)); break;
                    case "quit": Application.Quit(); break;
                }
                status.commandsDone++;
            }
        }

        private IEnumerator CheckWaypoint(CampaignHUD hud, MouseMovement mouse)
        {
            status.waypointQa = null;
            var campaign = CampaignMissionController.Instance;
            var camera = mouse.BodyCam;
            if (campaign == null || campaign.IsServer || campaign.State.chapter != CampaignChapter.Factory
                || campaign.State.phase != CampaignPhase.Exploring || camera == null || !camera.isActiveAndEnabled)
            { status.waypointQa = "Waypoint probe needs a remote client in Factory Exploring with its own BodyCam."; yield break; }

            var rects = hud.GetComponentsInChildren<RectTransform>(true);
            RectTransform Find(string name) => Array.Find(rects, rect => rect.name == name);
            var legacy = Find("NavigationMarker");
            var tracker = Find("MissionTracker");
            var branches = new[]
            {
                (root: Find("FactoryPowerMarker"), name: "POWER", step: "PowerRow/PowerStep", id: CampaignObjectiveId.FactoryIsolate),
                (root: Find("FactoryLogisticsMarker"), name: "LOGISTICS", step: "LogisticsRow/LogisticsStep", id: CampaignObjectiveId.FactoryManifest)
            };
            if (legacy == null || tracker == null || branches[0].root == null || branches[1].root == null)
            { status.waypointQa = "Missing client Factory destination HUD roots."; yield break; }

            var isolate = campaign.Objective(branches[0].id);
            var manifest = campaign.Objective(branches[1].id);
            if (isolate == null || manifest == null)
            { status.waypointQa = "Missing authored client Power or Logistics target."; yield break; }

            string TrackerError()
            {
                if (legacy.gameObject.activeInHierarchy) return "Legacy NavigationMarker appeared during early Factory.";
                var title = tracker.Find("TrackedObjective")?.GetComponent<TMP_Text>();
                if (title == null || !title.isActiveAndEnabled || !title.text.Contains(CampaignRules.ChapterGoal(CampaignChapter.Factory)))
                    return "Factory tracker is missing the chapter goal.";
                foreach (var branch in branches)
                {
                    var label = tracker.Find(branch.step)?.GetComponent<TMP_Text>();
                    string action = CampaignHUD.ObjectiveAction(branch.id);
                    if (label == null || !label.isActiveAndEnabled || action.Length == 0 || !label.text.Contains(action))
                        return $"{branch.step} should show the catalog title for {branch.id}.";
                }
                return null;
            }

            Quaternion previousLook = camera.transform.rotation;
            bool mouseEnabled = mouse.enabled;
            string before = JsonUtility.ToJson(campaign.State);
            var trackedBefore = hud.TrackedObjectiveId;
            mouse.enabled = false;
            try
            {
                var eye = camera.transform.position;
                var between = (isolate.TargetPosition - eye).normalized + (manifest.TargetPosition - eye).normalized;
                camera.transform.rotation = WaypointLook(camera, between, new Vector2(.60f, .63f));
                yield return new WaitForEndOfFrame();
                var a = camera.WorldToViewportPoint(isolate.TargetPosition);
                var b = camera.WorldToViewportPoint(manifest.TargetPosition);
                bool simultaneous = ClearWaypointViewport(a) && ClearWaypointViewport(b);
                if (simultaneous)
                {
                    var error = TrackerError() ?? WaypointProjectionError(camera, branches[0].root, isolate, "POWER")
                        ?? WaypointProjectionError(camera, branches[1].root, manifest, "LOGISTICS");
                    if (error != null) { status.waypointQa = "Shared view: " + error; yield break; }
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "client-both-in-view.png"));
                    yield return new WaitForEndOfFrame();
                }

                for (int i = 0; i < branches.Length; i++)
                {
                    var branch = branches[i];
                    var item = i == 0 ? isolate : manifest;
                    var other = branches[1 - i].id;
                    hud.TryTrackObjective(other, true);
                    var direction = item.TargetPosition - camera.transform.position;
                    var look = WaypointLook(camera, direction, new Vector2(.60f, .63f));
                    camera.transform.rotation = look;
                    yield return new WaitForEndOfFrame();
                    var error = TrackerError() ?? WaypointProjectionError(camera, branch.root, item, branch.name);
                    if (error != null) { status.waypointQa = $"After selecting {other}: {error}"; yield break; }
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, $"client-{branch.id}-in-view.png"));
                    yield return new WaitForEndOfFrame();

                    camera.transform.rotation = WaypointLook(camera, direction, new Vector2(1.08f, .63f));
                    yield return new WaitForEndOfFrame();
                    var outside = camera.WorldToViewportPoint(item.TargetPosition);
                    if (outside.z <= 0f || outside.x <= 1f || branch.root.gameObject.activeInHierarchy || legacy.gameObject.activeInHierarchy)
                    { status.waypointQa = $"{branch.id} should hide offscreen (viewport {outside:F2}), with no edge arrow."; yield break; }

                    camera.transform.rotation = Quaternion.AngleAxis(180f, Vector3.up) * look;
                    yield return new WaitForEndOfFrame();
                    if (camera.WorldToScreenPoint(item.TargetPosition).z >= 0f || branch.root.gameObject.activeInHierarchy)
                    { status.waypointQa = $"{branch.id} should hide behind the camera."; yield break; }

                    camera.transform.rotation = look;
                    yield return new WaitForEndOfFrame();
                    error = TrackerError() ?? WaypointProjectionError(camera, branch.root, item, branch.name);
                    if (error != null) { status.waypointQa = "Return to view: " + error; yield break; }
                }
                yield return new WaitForSecondsRealtime(.5f);
                if (JsonUtility.ToJson(campaign.State) != before)
                { status.waypointQa = "Looking or tracking changed the client's replicated mission state."; yield break; }
                status.waypointQa = $"PASS Power and Logistics use real target projections, hide offscreen/behind and return unchanged; "
                    + (simultaneous ? "both visible together; " : $"separate real views (shared viewport {a:F2}, {b:F2}); ")
                    + "replicated state unchanged.";
            }
            finally
            {
                if (trackedBefore != CampaignObjectiveId.None) hud.TryTrackObjective(trackedBefore, true);
                camera.transform.rotation = previousLook;
                mouse.enabled = mouseEnabled;
            }
        }

        private static Quaternion WaypointLook(Camera camera, Vector3 direction, Vector2 viewport)
        {
            camera.transform.rotation = Quaternion.LookRotation(direction);
            var ray = camera.ViewportPointToRay(viewport);
            return Quaternion.FromToRotation(ray.direction, direction.normalized) * camera.transform.rotation;
        }

        // A shared view is optional when the real targets land near the HUD corners.
        private static bool ClearWaypointViewport(Vector3 point) => point.z > 0f
            && point.x is > .28f and < .92f && point.y is > .29f and < .84f;

        private static string WaypointProjectionError(Camera camera, RectTransform root, CampaignInteractable item, string branch)
        {
            var dot = root.Find("Destination") as RectTransform;
            var label = root.Find("BranchLabel")?.GetComponent<TMP_Text>();
            var image = dot != null ? dot.GetComponent<UnityEngine.UI.Image>() : null;
            if (image == null || !image.isActiveAndEnabled || image.color.a <= 0f || dot.rect.size != new Vector2(28, 28))
                return $"{item.objectiveId} needs a visible 28×28 destination dot.";
            Vector3 expected = camera.WorldToScreenPoint(item.TargetPosition);
            Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, dot.TransformPoint(dot.rect.center));
            if (expected.z <= 0f || Vector2.Distance(actual, expected) > 1.5f)
                return $"{item.objectiveId} projects to {(Vector2)expected:F2}, Destination is at {actual:F2}.";
            string caption = label != null ? label.text.Replace("\r", "") : "";
            return label != null && label.isActiveAndEnabled
                && (caption == branch || caption == branch + "\nHIGHER" || caption == branch + "\nLOWER")
                ? null : $"{item.objectiveId} has no visible {branch} label in clear screen space (text: '{caption}').";
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
