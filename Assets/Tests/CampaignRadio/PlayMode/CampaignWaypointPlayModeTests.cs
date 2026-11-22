#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace FPS.Tests
{
    public sealed class CampaignWaypointPlayModeTests
    {
        private const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string GameScene = "GameScene";
        private const string MainMenu = "MainMenu";
        private readonly List<(string path, string backup)> checkpoints = new();
        private readonly List<int> addedSizes = new();
        private NetworkManager network;
        private Process peer;
        private PlayerHealth player;
        private CampaignMissionController campaign;
        private CampaignHUD hud;
        private RectTransform marker, tracker;
        private Transform title;
        private (RectTransform root, RectTransform dot, RectTransform label) power, logistics;
        private Camera bodyCam;
        private string output, backupDirectory;
        private bool previousIgnoreLogs, sessionStarted;
        private EditorWindow gameView;
        private PropertyInfo sizeIndex;
        private object sizeGroup;
        private int previousSize;

        [UnityTest, Explicit("Run in a focused Editor Game view. Captures the current HUD without requiring a route."), Timeout(600000)]
        public IEnumerator CaptureFactoryWaypointBaseline() => CaptureFactory(false);

        [UnityTest, Explicit("Run in a focused Editor Game view after the waypoint fix."), Timeout(600000)]
        public IEnumerator VerifyFactoryWaypointAfterFix() => CaptureFactory(true);

        [UnityTest, Explicit("Run against a freshly built Development client."), Timeout(600000)]
        public IEnumerator VerifyFactoryWaypointOnClient() => CaptureFactory(true, true);

        private IEnumerator CaptureFactory(bool after, bool remotePeer = false)
        {
            checkpoints.Clear();
            addedSizes.Clear();
            backupDirectory = null;
            gameView = null;
            sizeIndex = null;
            network = null;
            peer = null;
            sessionStarted = false;
            Assert.That(Application.isBatchMode, Is.False, "ScreenCapture needs an interactive Editor Game view.");
            Assert.That(NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening, Is.True,
                "Stop any live session before running this fixture.");
            string root = Environment.GetEnvironmentVariable("FPS_WAYPOINT_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(root)) root = Application.temporaryCachePath;
            output = Path.GetFullPath(Path.Combine(root, $"factory-waypoint-{(after ? "after" : "baseline")}-{Guid.NewGuid():N}"));
            Assert.That(output.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), Is.False, "Keep captures outside Assets.");
            Directory.CreateDirectory(output);
            Note("Capture directory: " + output);
            BackUpCheckpoints();
            previousIgnoreLogs = LogAssert.ignoreFailingMessages;
            sessionStarted = true;
            LogAssert.ignoreFailingMessages = true; // Same unrelated scene diagnostics as CampaignRadioHostClientTests.

            yield return StartHost(remotePeer);
            marker = HudChild("NavigationMarker");
            tracker = HudChild("MissionTracker");
            title = tracker.Find("TrackedObjective");
            Assert.That(title, Is.Not.Null, "Expected tracker heading.");
            Assert.That(Visible(marker), Is.False, "No exploration waypoint during Insertion.");
            if (after)
            {
                power = BranchMarker("FactoryPowerMarker");
                logistics = BranchMarker("FactoryLogisticsMarker");
                Assert.That(Visible(power.root) || Visible(logistics.root), Is.False, "No Factory branch destinations during Insertion.");
            }
            yield return Wait(() => campaign.State.phase == CampaignPhase.Exploring && player.CanUseCombat &&
                !player.CampaignTransferPending && hud.HasMissionTracker, 105, "Factory Exploring with the host HUD");
            Assert.That(campaign.State.chapter, Is.EqualTo(CampaignChapter.Factory));
            Assert.That(campaign.InsertionPartySize, Is.EqualTo(remotePeer ? 2 : 1));
            Assert.That(CampaignCheckpoint.TryLoad(out var checkpoint, out string error), Is.True, error);
            Assert.That(checkpoint.state.chapter, Is.EqualTo(CampaignChapter.Factory));
            Assert.That(checkpoint.state.phase, Is.EqualTo(CampaignPhase.Exploring));
            Assert.That(campaign.State.completed, Is.Zero);
            Note($"Insertion -> Exploring; landing={campaign.InsertionParticipant(0).arrival:F3}, host feet={player.transform.position:F3}");

            yield return Wait(() => campaign.doors.All(door => door == null || door.rule != CampaignDoorRule.AfterInsertion || door.IsOpen),
                10, "insertion gate opens on host");
            if (remotePeer)
            {
                yield return Wait(() => PeerStatus() is { connected: true, isServer: false, inputReady: true, hudPresent: true, rosterSize: 2 } snapshot
                    && snapshot.phase == (int)CampaignPhase.Exploring, 30, "remote player's own Factory HUD");
                yield return new WaitForSecondsRealtime(1f);
                string serverBefore = JsonUtility.ToJson(campaign.State);
                File.WriteAllText(Path.Combine(output, "commands.txt"), "waypoint" + Environment.NewLine);
                CampaignPeerProbe.Status checkedPeer = null;
                yield return Wait(() => !string.IsNullOrEmpty((checkedPeer = PeerStatus())?.waypointQa), 20, "remote waypoint assertions");
                Assert.That(checkedPeer.waypointQa, Does.StartWith("PASS "));
                Note($"Remote client {checkedPeer.localClientId}, server={checkedPeer.isServer}, peers={checkedPeer.rosterSize}, feet={checkedPeer.position:F2}: {checkedPeer.waypointQa}");
                Assert.That(JsonUtility.ToJson(campaign.State), Is.EqualTo(serverBefore), "Client tracking and camera motion must not change server state.");
                yield break;
            }

            var mouse = player.GetComponent<MouseMovement>();
            bodyCam = mouse != null ? mouse.BodyCam : null;
            Assert.That(bodyCam != null && bodyCam.isActiveAndEnabled, Is.True, "The host must have its real BodyCam.");
            bool mouseEnabled = mouse.enabled;
            Quaternion originalLook = bodyCam.transform.rotation;
            mouse.enabled = false; // Keep the body camera at the test angle without injecting movement input.
            try
            {
                var isolate = Objective(CampaignObjectiveId.FactoryIsolate);
                var manifest = Objective(CampaignObjectiveId.FactoryManifest);
                string serverBefore = JsonUtility.ToJson(campaign.State);
                var sizes = after ? new[] { (1920, 1080), (3840, 2160), (1440, 1080) }
                    : new[] { (1920, 1080), (3840, 2160) };
                foreach (var (width, height) in sizes)
                {
                    yield return SetGameView(width, height);
                    if (!after)
                    {
                        foreach (var (label, item) in new[] { ("power", isolate), ("logistics", manifest) })
                        {
                            Assert.That(hud.TryTrackObjective(item.objectiveId), Is.True);
                            yield return Wait(() => UiText(title).Contains(ActionTitle(item.objectiveId)), 10, "baseline tracked title");
                            yield return Aim(Quaternion.LookRotation(item.TargetPosition - bodyCam.transform.position));
                            Capture($"{label}-{width}x{height}.png", width, height);
                        }
                        continue;
                    }

                    var eye = bodyCam.transform.position;
                    var between = (isolate.TargetPosition - eye).normalized + (manifest.TargetPosition - eye).normalized;
                    var look = OffsetLook(between);
                    yield return Aim(look);
                    AssertSteps(isolate.objectiveId, manifest.objectiveId);
                    Assert.That(tracker.GetComponentsInChildren<Graphic>().Any(g => UiText(g.transform).Contains("] SWITCH")), Is.False,
                        "The tracker must not ask players to switch branches.");
                    AssertAt(power, isolate, "POWER", centered: true);
                    AssertAt(logistics, manifest, "LOGISTICS", centered: true);
                    Capture($"both-{width}x{height}.png", width, height);
                    Note($"{width}x{height} real destinations: Power {isolate.TargetPosition:F2} -> {ScreenPoint(power.dot):F1}; " +
                        $"Logistics {manifest.TargetPosition:F2} -> {ScreenPoint(logistics.dot):F1}");

                    foreach (var selected in new[] { manifest, isolate })
                    {
                        hud.TryTrackObjective(selected.objectiveId, true);
                        yield return Aim(Quaternion.AngleAxis(selected == manifest ? 3f : -3f, Vector3.up) * look);
                        AssertAt(power, isolate, "POWER");
                        AssertAt(logistics, manifest, "LOGISTICS");
                        AssertSteps(isolate.objectiveId, manifest.objectiveId);
                    }
                    yield return Aim(Quaternion.AngleAxis(180f, Vector3.up) * look);
                    Assert.That(bodyCam.WorldToScreenPoint(isolate.TargetPosition).z < 0f
                        && bodyCam.WorldToScreenPoint(manifest.TargetPosition).z < 0f, Is.True, "Both real targets are behind the camera.");
                    Assert.That(Visible(power.root) || Visible(logistics.root) || Visible(marker), Is.False,
                        "Behind-camera destinations hide completely, with no legacy edge arrow.");
                    AssertSteps(isolate.objectiveId, manifest.objectiveId);
                    yield return Aim(look);
                    AssertAt(power, isolate, "POWER");
                    AssertAt(logistics, manifest, "LOGISTICS");
                }
                Assert.That(JsonUtility.ToJson(campaign.State), Is.EqualTo(serverBefore), "Tracking and camera motion are presentation only.");
                if (after)
                {
                    yield return VerifyPlacementAndProgress(isolate, manifest);
                    VerifyUnresolvedTank();
                }
            }
            finally
            {
                if (bodyCam != null) bodyCam.transform.rotation = originalLook;
                if (mouse != null) mouse.enabled = mouseEnabled;
            }
        }

        private IEnumerator StartHost(bool remotePeer)
        {
            yield return SceneManager.LoadSceneAsync(MainMenu);
            network = NetworkManager.Singleton;
            Assert.That(network != null, Is.True, "MainMenu did not create its NetworkManager.");
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            ushort port;
            using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            var transport = network.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            transport.DisconnectTimeoutMS = 120000;
            Assert.That(network.StartHost(), Is.True);

            if (remotePeer)
            {
                string executable = Path.GetFullPath("Builds/CampaignPeer/FPS.exe");
                Assert.That(File.Exists(executable), Is.True, "Build the Development peer first.");
                var launch = new ProcessStartInfo(executable) { UseShellExecute = false };
                foreach (string arg in new[] { "-campaignPeer", port.ToString(), output, "-screen-fullscreen", "0",
                    "-screen-width", "1920", "-screen-height", "1080", "-logFile", Path.Combine(output, "player.log") })
                    launch.ArgumentList.Add(arg);
                peer = Process.Start(launch);
                yield return Wait(() => network.ConnectedClientsIds.Count == 2, 120, "Development peer connects");
            }

            List<ulong> timedOut = null;
            var scenes = network.SceneManager;
            scenes.OnLoadEventCompleted += OnLoaded;
            void OnLoaded(string name, LoadSceneMode mode, List<ulong> complete, List<ulong> failed)
            {
                if (name == GameScene) timedOut = failed;
            }
            try
            {
                Assert.That(scenes.LoadScene(GameScene, LoadSceneMode.Single), Is.EqualTo(SceneEventProgressStatus.Started));
                yield return Wait(() => timedOut != null, 180, "NGO GameScene load completed");
            }
            finally { scenes.OnLoadEventCompleted -= OnLoaded; }
            Assert.That(timedOut, Is.Empty);

            var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
            foreach (ulong client in network.ConnectedClientsIds)
            {
                bool host = client == NetworkManager.ServerClientId;
                Assert.That(catalog.TryGetPrefab(host ? PlayerCharacterId.Brimstone : PlayerCharacterId.Sage, out var prefab), Is.True);
                NetworkSpawnManager.Instance.TryGetNextSpawnPose(out var position, out var rotation);
                var instance = Object.Instantiate(prefab, position, rotation);
                var health = instance.GetComponent<PlayerHealth>();
                health.PrepareInitialSpawn(new SessionPlayerId(host ? 1UL : 2UL));
                instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(client);
                if (host) player = health;
            }
            NetworkMatchStateManager.Instance.EnterWarmup();

            yield return Wait(() => CampaignMissionController.Instance != null && CampaignMissionController.Instance.IsSpawned
                && CampaignHUD.Instance != null, 30, "Factory controller and HUD");
            campaign = CampaignMissionController.Instance;
            hud = CampaignHUD.Instance;
            Assert.That(campaign.State.chapter, Is.EqualTo(CampaignChapter.Factory));
            Assert.That(campaign.State.phase, Is.EqualTo(CampaignPhase.Insertion));
            Note($"Editor host={network.IsHost}, scene={SceneManager.GetActiveScene().name}, port={port}, phase={campaign.State.phase}");
        }

        private CampaignPeerProbe.Status PeerStatus()
        {
            try { return JsonUtility.FromJson<CampaignPeerProbe.Status>(File.ReadAllText(Path.Combine(output, "status.json"))); }
            catch (IOException) { return null; }
        }

        private IEnumerator VerifyPlacementAndProgress(CampaignInteractable isolate, CampaignInteractable manifest)
        {
            yield return SetGameView(1920, 1080);
            yield return Aim(Quaternion.Euler(0f, bodyCam.transform.eulerAngles.y, 0f));
            var backup = Objective(CampaignObjectiveId.FactoryBackup);
            var shipping = Objective(CampaignObjectiveId.FactoryShipping);
            var items = new[] { isolate, manifest, backup, shipping };
            var originalBodies = items.Select(item => item.interactionBody).ToArray();
            var bodies = new BoxCollider[items.Length];
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var standIn = new GameObject("Waypoint QA target " + items[i].objectiveId) { layer = 2 }; // Ignore Raycast
                    bodies[i] = standIn.AddComponent<BoxCollider>();
                    bodies[i].isTrigger = true;
                    bodies[i].size = Vector3.one * .02f;
                    items[i].interactionBody = bodies[i];
                }

                Vector3 At(float x, float y, float depth) => bodyCam.ViewportToWorldPoint(new Vector3(x, y, depth));
                Vector3 high = At(.62f, .68f, 45f);
                Vector3 low = At(.78f, .33f, 65f);
                void Place(int index, Vector3 point)
                {
                    bodies[index].transform.position = point;
                    Physics.SyncTransforms();
                }
                Place(0, high);
                Place(1, high);
                Place(2, At(.76f, .66f, 40f));
                Place(3, At(.52f, .44f, 35f));
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER", suffix: "HIGHER");
                AssertAt(logistics, manifest, "LOGISTICS", suffix: "HIGHER");
                Assert.That(ScreenPoint(power.label).x, Is.LessThan(ScreenPoint(power.dot).x), "Power label goes left on equal projections.");
                Assert.That(ScreenPoint(logistics.label).x, Is.GreaterThan(ScreenPoint(logistics.dot).x), "Logistics label goes right on equal projections.");
                Capture("cluster-1920x1080.png", 1920, 1080);
                Note("Coincident destinations: both dots retain the same projection, with labels on opposite sides.");

                Place(0, At(.79f, .236f, 45f));
                Place(1, At(.79f, .31f, 65f));
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER", suffix: "LOWER");
                AssertAt(logistics, manifest, "LOGISTICS", suffix: "LOWER");
                Assert.That(ScreenPoint(power.label).x, Is.LessThan(ScreenPoint(logistics.label).x),
                    "Labels that converge after avoiding the lower HUD must split even when the dots are far apart vertically.");

                Place(0, At(.53f, .165f, 50f));
                yield return Aim(bodyCam.transform.rotation);
                AssertProjected(power, isolate);
                var gameplayHud = HUDManager.Instance;
                gameplayHud.SetInteractionPrompt("[F] QA PROMPT", true);
                try
                {
                    var prompt = (RectTransform)typeof(HUDManager).GetProperty("VisibleInteractionPrompt", AnyMember).GetValue(gameplayHud);
                    Assert.That(prompt, Is.Not.Null, "The independent gameplay prompt must be visible during this probe.");
                    Canvas.ForceUpdateCanvases();
                    Vector2 centre = ScreenPoint(prompt);
                    Place(0, bodyCam.ScreenToWorldPoint(new Vector3(centre.x, centre.y, 45f)));
                    typeof(CampaignHUD).GetMethod("UpdateNavigation", AnyMember).Invoke(hud, new object[] { player });
                    Assert.That(Visible(power.root), Is.False, "An occupied gameplay prompt hides the overlapped dot.");
                }
                finally { gameplayHud.SetInteractionPrompt("", false); }
                Note("Lower screen: labels split after HUD avoidance; empty prompt band leaves the dot visible, active prompt hides it.");

                Place(0, bodyCam.ScreenToWorldPoint(new Vector3(1170, 460, 85f)));
                Place(1, bodyCam.ScreenToWorldPoint(new Vector3(1100, 420, 65f)));
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER", suffix: "LOWER");
                AssertAt(logistics, manifest, "LOGISTICS", suffix: "LOWER");
                Assert.That(ScreenPoint(power.label).y, Is.GreaterThan(ScreenPoint(power.dot).y),
                    "The separated Power label must flip above its own dot if it would cover the Logistics dot.");
                Note("Staggered destinations: separated labels avoid the other branch's dot.");

                Place(0, high);
                Place(1, low);
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER", centered: true, suffix: "HIGHER");
                AssertAt(logistics, manifest, "LOGISTICS", centered: true, suffix: "LOWER");

                var trackerCorners = new Vector3[4];
                tracker.GetWorldCorners(trackerCorners);
                float panelRight = trackerCorners[2].x;
                float panelMiddle = (trackerCorners[0].y + trackerCorners[2].y) * .5f;
                float dotHalf = power.dot.rect.width * power.dot.GetComponentInParent<Canvas>().scaleFactor * .5f;
                Vector3 JustRightOfTracker(float distance) => bodyCam.ScreenToWorldPoint(new Vector3(panelRight + distance, panelMiddle, 35f));
                Place(0, JustRightOfTracker(dotHalf * .5f));
                yield return Aim(bodyCam.transform.rotation);
                Assert.That(Visible(power.root), Is.False, "The dot footprint under the tracker hides Power, even when its centre is outside.");
                AssertAt(logistics, manifest, "LOGISTICS", suffix: "LOWER");

                Place(0, JustRightOfTracker(dotHalf + 4f));
                yield return Aim(bodyCam.transform.rotation);
                AssertProjected(power, isolate);
                Assert.That(Visible(power.label), Is.False, "The tracker masks the label without dropping its unobstructed dot.");

                Place(0, At(.5f, .5f, 30f));
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER"); // Aiming through the reticle is allowed.

                float insideEdge = (bodyCam.pixelRect.xMax - dotHalf * .5f) / Screen.width;
                Place(0, At(insideEdge, .68f, 45f));
                yield return Aim(bodyCam.transform.rotation);
                Assert.That(Visible(power.root) || Visible(marker), Is.False, "A dot straddling the viewport edge hides instead of clamping or clipping.");
                AssertAt(logistics, manifest, "LOGISTICS", suffix: "LOWER");
                Place(0, high);
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER", suffix: "HIGHER");
                AssertSteps(isolate.objectiveId, manifest.objectiveId);

                Assert.That(CampaignRules.CanUse(campaign.State, backup.objectiveId), Is.EqualTo(CampaignResult.Prerequisite));
                Assert.That(CampaignRules.CanUse(campaign.State, shipping.objectiveId), Is.EqualTo(CampaignResult.Prerequisite));
                string beforeTracking = JsonUtility.ToJson(campaign.State);
                hud.TryTrackObjective(backup.objectiveId, true);
                yield return Aim(bodyCam.transform.rotation);
                AssertAt(power, isolate, "POWER");
                AssertAt(logistics, manifest, "LOGISTICS");
                AssertSteps(isolate.objectiveId, manifest.objectiveId);
                Assert.That(JsonUtility.ToJson(campaign.State), Is.EqualTo(beforeTracking), "Inspecting a locked step cannot advance either branch.");
                Vector2 oldPower = ScreenPoint(power.dot), oldLogistics = ScreenPoint(logistics.dot);

                Assert.That(CampaignRules.TryComplete(campaign.State, isolate.objectiveId), Is.EqualTo(CampaignResult.Accepted));
                PublishProgress();
                yield return Aim(bodyCam.transform.rotation);
                AssertSteps(backup.objectiveId, manifest.objectiveId);
                AssertAt(power, backup, "POWER");
                AssertAt(logistics, manifest, "LOGISTICS");
                Assert.That(Vector2.Distance(ScreenPoint(power.dot), oldPower), Is.GreaterThan(20f));
                Assert.That(Vector2.Distance(ScreenPoint(logistics.dot), oldLogistics), Is.LessThanOrEqualTo(1.5f));

                Vector2 newPower = ScreenPoint(power.dot);
                CampaignRules.RecordFile(campaign.State, CampaignFileId.FactoryManifest);
                PublishProgress();
                yield return Aim(bodyCam.transform.rotation);
                AssertSteps(backup.objectiveId, shipping.objectiveId);
                AssertAt(power, backup, "POWER");
                AssertAt(logistics, shipping, "LOGISTICS");
                Assert.That(Vector2.Distance(ScreenPoint(power.dot), newPower), Is.LessThanOrEqualTo(1.5f));
                Assert.That(Vector2.Distance(ScreenPoint(logistics.dot), oldLogistics), Is.GreaterThan(20f));

                Assert.That(CampaignRules.TryComplete(campaign.State, backup.objectiveId), Is.EqualTo(CampaignResult.Accepted));
                Assert.That(CampaignRules.TryComplete(campaign.State, CampaignObjectiveId.FactoryGenerator), Is.EqualTo(CampaignResult.Accepted));
                PublishProgress();
                yield return Aim(bodyCam.transform.rotation);
                AssertSteps(CampaignObjectiveId.None, shipping.objectiveId);
                Assert.That(Visible(power.root), Is.False, "A completed branch has no destination while the other branch is still working.");
                AssertAt(logistics, shipping, "LOGISTICS");
                Note("Server progress: Isolate -> Backup; Manifest -> Shipping; Power -> COMPLETE. Each update changed only its own destination.");
            }
            finally
            {
                for (int i = 0; i < items.Length; i++)
                {
                    items[i].interactionBody = originalBodies[i];
                    if (bodies[i] != null) Object.Destroy(bodies[i].gameObject);
                }
                Physics.SyncTransforms();
            }
        }

        private void PublishProgress()
        {
            Assert.That(network.IsServer, Is.True);
            typeof(CampaignMissionController).GetMethod("Publish", AnyMember).Invoke(campaign, null);
        }

        private CampaignInteractable Objective(CampaignObjectiveId id)
        {
            var item = campaign.Objective(id);
            Assert.That(item != null, Is.True, $"Missing authored objective {id}.");
            return item;
        }

        private (RectTransform root, RectTransform dot, RectTransform label) BranchMarker(string name)
        {
            var root = HudChild(name);
            var dot = root.Find("Destination") as RectTransform;
            var label = root.Find("BranchLabel") as RectTransform;
            Assert.That(dot != null && label != null && Tmp(label) != null, Is.True, $"{name} needs a Destination and a TMP BranchLabel.");
            return (root, dot, label);
        }

        private void AssertProjected((RectTransform root, RectTransform dot, RectTransform label) branch, CampaignInteractable item)
        {
            var image = branch.dot.GetComponent<Image>();
            Assert.That(Visible(branch.root) && image != null && image.isActiveAndEnabled && image.color.a > 0f, Is.True,
                $"{item.objectiveId} needs a visible destination dot.");
            Assert.That(branch.dot.rect.size, Is.EqualTo(new Vector2(28, 28)), "Destination size is in canvas units.");
            Vector3 expected = bodyCam.WorldToScreenPoint(item.TargetPosition);
            Assert.That(expected.z, Is.GreaterThan(0f), $"Fixture aim must put {item.objectiveId} in front of the camera.");
            Vector2 actual = ScreenPoint(branch.dot);
            Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(1.5f),
                $"{item.objectiveId} projects to {(Vector2)expected:F2}, Destination is at {actual:F2}.");
            Assert.That(Visible(marker), Is.False, "Early Factory must not also show legacy navigation.");
        }

        private void AssertAt((RectTransform root, RectTransform dot, RectTransform label) branch,
            CampaignInteractable item, string name, bool centered = false, string suffix = null)
        {
            AssertProjected(branch, item);
            Assert.That(Visible(branch.label), Is.True, $"{name} label in free screen space.");
            string label = UiText(branch.label).Replace("\r", "");
            if (suffix != null) Assert.That(label, Is.EqualTo(name + "\n" + suffix));
            else Assert.That(label == name || label == name + "\nHIGHER" || label == name + "\nLOWER", Is.True,
                $"Unexpected {name} label: '{label}'.");
            if (centered)
            {
                Vector2 text = ScreenPoint(branch.label), dot = ScreenPoint(branch.dot);
                Assert.That(text.x, Is.EqualTo(dot.x).Within(1.5f), $"{name} label centres under an isolated dot.");
                Assert.That(text.y, Is.LessThan(dot.y));
            }
        }

        private void AssertSteps(CampaignObjectiveId powerId, CampaignObjectiveId logisticsId)
        {
            Assert.That(Visible(tracker), Is.True);
            Assert.That(UiText(title), Does.Contain(CampaignRules.ChapterGoal(CampaignChapter.Factory)));
            AssertStep("PowerRow/PowerStep", powerId);
            AssertStep("LogisticsRow/LogisticsStep", logisticsId);
        }

        private void AssertStep(string name, CampaignObjectiveId id)
        {
            var label = tracker.Find(name);
            Assert.That(Visible(label) && Tmp(label) != null, Is.True, $"{name} should be a visible TMP label.");
            Assert.That(UiText(label), Does.Contain(id == CampaignObjectiveId.None ? "COMPLETE" : ActionTitle(id)), name);
        }

        private static string ActionTitle(CampaignObjectiveId id)
        {
            string action = CampaignHUD.ObjectiveAction(id);
            Assert.That(action, Is.Not.Empty, $"Missing catalog title for {id}.");
            return action;
        }

        private Quaternion OffsetLook(Vector3 direction)
        {
            bodyCam.transform.rotation = Quaternion.LookRotation(direction);
            var ray = bodyCam.ViewportPointToRay(new Vector3(.60f, .63f));
            return Quaternion.FromToRotation(ray.direction, direction.normalized) * bodyCam.transform.rotation;
        }

        private IEnumerator Aim(Quaternion look)
        {
            bodyCam.transform.rotation = look;
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        private void VerifyUnresolvedTank()
        {
            var stateField = typeof(CampaignMissionController).GetField("state", AnyMember);
            var current = campaign.State;
            var missingTank = current.Copy();
            missingTank.completed = CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
            missingTank.tankStage = CampaignTankStage.Active;
            try
            {
                // Only the HUD observes this snapshot. The server does not tick or publish it.
                stateField.SetValue(campaign, missingTank);
                typeof(CampaignHUD).GetMethod("UpdateNavigation", AnyMember).Invoke(hud, new object[] { player });
                Assert.That(Visible(marker), Is.False, "An unresolved Tank must not point toward boarding.");
                Assert.That(Visible(power.root) || Visible(logistics.root), Is.False, "Factory branch markers must clear when Tank is next.");
                Assert.That(UiText(title), Does.Contain("Eliminate the Tank").And.Contain("ROUTE UNAVAILABLE"));
                Note("Unresolved Tank reference: action preserved, no boarding waypoint or distance.");
            }
            finally { stateField.SetValue(campaign, current); }
        }

        private RectTransform HudChild(string name) => hud.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name);
        private static bool Visible(Transform node) => node != null && node.gameObject.activeInHierarchy;
        private static Vector2 ScreenPoint(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        // Keep the existing test assembly independent of TMP.
        private static Graphic Tmp(Transform node) => node.GetComponents<Graphic>().FirstOrDefault(g => g.GetType().Name == "TextMeshProUGUI");
        private static string UiText(Transform node)
        {
            if (node.TryGetComponent(out Text legacy)) return legacy.text ?? "";
            var tmp = Tmp(node);
            return tmp?.GetType().GetProperty("text")?.GetValue(tmp) as string ?? "";
        }

        private IEnumerator SetGameView(int width, int height)
        {
            var editor = typeof(EditorWindow).Assembly;
            if (gameView == null)
            {
                gameView = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView", true));
                sizeIndex = gameView.GetType().GetProperty("selectedSizeIndex", AnyMember);
                previousSize = (int)sizeIndex.GetValue(gameView);
                var sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", AnyMember).GetValue(null);
                object groupType = gameView.GetType().GetProperty("currentSizeGroupType", AnyMember).GetValue(gameView);
                sizeGroup = sizesType.GetMethod("GetGroup", AnyMember).Invoke(sizes, new[] { groupType });
            }
            var groupTypeInfo = sizeGroup.GetType();
            var getSize = groupTypeInfo.GetMethod("GetGameViewSize", AnyMember);
            int count = (int)groupTypeInfo.GetMethod("GetTotalCount", AnyMember).Invoke(sizeGroup, null);
            var sizeType = editor.GetType("UnityEditor.GameViewSize", true);
            var kindType = editor.GetType("UnityEditor.GameViewSizeType", true);
            object fixedKind = Enum.Parse(kindType, "FixedResolution");
            string qaSize = $"Waypoint QA {width}x{height}";
            int index = 0;
            for (; index < count; index++)
            {
                var size = getSize.Invoke(sizeGroup, new object[] { index });
                if (Equals(sizeType.GetProperty("sizeType", AnyMember).GetValue(size), fixedKind)
                    && (int)sizeType.GetProperty("width", AnyMember).GetValue(size) == width
                    && (int)sizeType.GetProperty("height", AnyMember).GetValue(size) == height)
                {
                    // Reclaim a test size left behind by an interrupted run, while preserving the selected size.
                    if (index > previousSize && (string)sizeType.GetProperty("baseText", AnyMember).GetValue(size) == qaSize
                        && !addedSizes.Contains(index)) addedSizes.Add(index);
                    break;
                }
            }
            if (index == count)
            {
                var size = sizeType.GetConstructor(AnyMember, null, new[] { kindType, typeof(int), typeof(int), typeof(string) }, null)
                    .Invoke(new[] { fixedKind, (object)width, height, qaSize });
                groupTypeInfo.GetMethod("AddCustomSize", AnyMember).Invoke(sizeGroup, new[] { size });
                addedSizes.Add(index);
            }
            sizeIndex.SetValue(gameView, index);
            gameView.Show();
            gameView.Focus();
            gameView.Repaint();
            yield return Wait(() => Screen.width == width && Screen.height == height, 30, $"Game view at {width}x{height}");
        }

        private void Capture(string name, int width, int height)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That((texture.width, texture.height), Is.EqualTo((width, height)), "Game view capture dimensions.");
                File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            }
            finally { Object.Destroy(texture); }
        }

        private void Note(string message)
        {
            Debug.Log("[WaypointQA] " + message);
            File.AppendAllText(Path.Combine(output, "observations.txt"), message + Environment.NewLine);
        }

        private static IEnumerator Wait(Func<bool> ready, int seconds, string label)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready())
            {
                if (Time.realtimeSinceStartupAsDouble >= until) Assert.Fail("Timed out waiting for " + label);
                yield return null;
            }
        }

        private void BackUpCheckpoints()
        {
            string save = CampaignCheckpoint.SavePath;
            backupDirectory = Path.Combine(Path.GetDirectoryName(save), "waypoint-qa-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupDirectory);
            Note("Checkpoint recovery directory: " + backupDirectory);
            foreach (string path in new[] { save, save + ".previous", save + ".tmp" })
            {
                string backup = null;
                if (File.Exists(path))
                {
                    backup = Path.Combine(backupDirectory, Path.GetFileName(path));
                    File.Move(path, backup); // Same volume: retain the original bytes and filesystem metadata.
                }
                checkpoints.Add((path, backup));
            }
        }

        [UnityTearDown]
        public IEnumerator StopSession()
        {
            try
            {
                if (peer != null)
                {
                    File.AppendAllText(Path.Combine(output, "commands.txt"), "quit" + Environment.NewLine);
                    float deadline = Time.realtimeSinceStartup + 8;
                    while (!peer.HasExited && Time.realtimeSinceStartup < deadline) yield return null;
                    if (!peer.HasExited) peer.CloseMainWindow();
                    peer.Dispose();
                    peer = null;
                }
                if (network != null && network.IsListening)
                {
                    network.Shutdown();
                    yield return Wait(() => network == null || !network.IsListening, 20, "Editor host shutdown");
                }
            }
            finally
            {
                if (sessionStarted) LogAssert.ignoreFailingMessages = previousIgnoreLogs;
                try
                {
                    var failures = new List<Exception>();
                    foreach (var (path, backup) in checkpoints)
                    {
                        try
                        {
                            if (backup != null && !File.Exists(backup)) throw new FileNotFoundException("Missing original checkpoint", backup);
                            File.Delete(path);
                            if (backup != null) File.Move(backup, path);
                        }
                        catch (Exception error) { failures.Add(error); }
                    }
                    if (failures.Count > 0) throw new AggregateException("Restore checkpoints from " + backupDirectory, failures);
                    if (backupDirectory != null) Directory.Delete(backupDirectory);
                }
                finally
                {
                    if (sizeIndex != null && gameView != null) sizeIndex.SetValue(gameView, previousSize);
                    addedSizes.Sort();
                    for (int i = addedSizes.Count - 1; i >= 0; i--)
                        sizeGroup.GetType().GetMethod("RemoveCustomSize", AnyMember).Invoke(sizeGroup, new object[] { addedSizes[i] });
                }
            }
            if (sessionStarted) yield return SceneManager.LoadSceneAsync(MainMenu);
        }
    }
}
#endif
