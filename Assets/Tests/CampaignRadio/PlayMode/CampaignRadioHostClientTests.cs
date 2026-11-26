#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using static FPS.CampaignRadioVoice;

namespace FPS.Tests
{
    public sealed class CampaignRadioHostClientTests
    {
        private const string PeerPath = "Builds/CampaignPeer/FPS.exe";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Process peer;
        private string exchange;
        private byte[] save, previous;
        private bool backedUp;
        private CampaignMissionController campaign;
        private NetworkManager network;
        private Text hostCaption;
        private EditorWindow gameView;
        private PropertyInfo sizeProperty;
        private int previousSize;
        private static string Tag(string text) => text?.Replace('\n', ' ') ?? "";

        [UnityTest, Timeout(900000)]
        public IEnumerator BothPeersHearTheSameCastAndCompleteRadioExchanges()
        {
            Assert.That(File.Exists(PeerPath), Is.True, "Build the peer client from Tools/FPS/Campaign before running this test.");
            string path = CampaignCheckpoint.SavePath;
            if (File.Exists(path)) save = File.ReadAllBytes(path);
            if (File.Exists(path + ".previous")) previous = File.ReadAllBytes(path + ".previous");
            backedUp = true;
            exchange = Path.Combine(Application.temporaryCachePath, "campaign-radio48-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(exchange);
            Debug.Log("[Radio48] QA directory: " + exchange);
            LogAssert.ignoreFailingMessages = true; // Large scene has unrelated import/runtime diagnostics.
            yield return SceneManager.LoadSceneAsync("MainMenu");
            network = NetworkManager.Singleton;
            Assert.That(network, Is.Not.Null);
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            ushort port;
            using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            var transport = network.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            transport.DisconnectTimeoutMS = 120000;
            Assert.That(network.StartHost(), Is.True);
            peer = Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetFullPath(PeerPath),
                Arguments = $"-campaignPeer {port} \"{exchange}\" -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile \"{Path.Combine(exchange, "player.log")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            yield return Wait(() => network.ConnectedClientsIds.Count == 2, 120, "built client connected to Editor host");
            ulong remote = network.ConnectedClientsIds.Single(id => id != NetworkManager.ServerClientId);

            bool spawned = false;
            network.SceneManager.OnLoadEventCompleted += Spawn;
            void Spawn(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
            {
                if (scene != "GameScene") return;
                network.SceneManager.OnLoadEventCompleted -= Spawn;
                Assert.That(timedOut, Is.Empty);
                var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
                var peers = new[] { NetworkManager.ServerClientId, remote };
                for (int slot = 0; slot < peers.Length; slot++)
                {
                    Assert.That(catalog.TryGetPrefab(slot == 0 ? PlayerCharacterId.Brimstone : PlayerCharacterId.Sage, out var prefab), Is.True);
                    NetworkSpawnManager.Instance.TryGetNextSpawnPose(out var position, out var rotation);
                    var instance = Object.Instantiate(prefab, position, rotation);
                    instance.GetComponent<PlayerHealth>().PrepareInitialSpawn(new SessionPlayerId((ulong)(slot + 1)));
                    instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(peers[slot]);
                }
                Debug.Log($"[Radio48] GameScene loaded on clients [{string.Join(",", completed)}]");
                NetworkMatchStateManager.Instance.EnterWarmup();
                spawned = true;
            }
            Assert.That(network.SceneManager.LoadScene("GameScene", LoadSceneMode.Single), Is.EqualTo(SceneEventProgressStatus.Started));
            yield return Wait(() => spawned && CampaignHUD.Instance != null && Peer()?.hasPlayer == true, 180, "both GameScene HUDs");
            campaign = CampaignMissionController.Instance;
            hostCaption = CampaignHUD.Instance.GetComponentsInChildren<Text>(true).Single(t => t.name == "MandatorySubtitles");
            yield return Compare(CampaignDialogueId.InsertionBriefing, Mason, None);
            yield return Wait(() => campaign.State.phase == CampaignPhase.Exploring && campaign.Players.All(p => p.CanUseCombat), 100, "squad exploring");
            SetPrivate(campaign, "readingUntil", campaign.Now + 300);
            yield return Compare(CampaignDialogueId.FactoryChapter, Mason, None);

            var mason = network.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            var doc = network.ConnectedClients[remote].PlayerObject.GetComponent<PlayerHealth>();
            doc.RelocateCampaign(mason.transform.position + mason.transform.right * 1.2f, mason.transform.rotation);
            yield return Wait(() => doc.CanUseCombat, 6, "Doc in reach of Mason");
            SetPrivate(doc, "campaignProtectionUntil", 0.0);
            doc.TakeDamage(new DamageInfo(doc.CurrentHealth + 5, ulong.MaxValue, -1, doc.transform.position + Vector3.up,
                damageType: DamageType.Melee, hitZone: HitboxZone.Leg, sourcePosition: doc.transform.position + Vector3.forward * 2));
            Assert.That(doc.LifeState, Is.EqualTo(PlayerLifeState.Downed));
            yield return Compare(CampaignDialogueId.OperatorDowned, Doc, Mason);
            double reviveDeadline = Time.realtimeSinceStartupAsDouble + campaign.settings.reviveSeconds + 4;
            while (doc.LifeState == PlayerLifeState.Downed && Time.realtimeSinceStartupAsDouble < reviveDeadline)
            {
                campaign.RequestAid(doc.NetworkObjectId, false, true);
                yield return new WaitForSecondsRealtime(.2f);
            }
            campaign.RequestAid(doc.NetworkObjectId, false, false);
            Assert.That(doc.LifeState, Is.EqualTo(PlayerLifeState.Alive), "Mason completed the actual hold-to-revive action");
            yield return Compare(CampaignDialogueId.Revived, Doc, Mason);

            doc.SetCampaignSpectating();
            Emit(CampaignDialogueId.FactoryGenerator);
            yield return Compare(CampaignDialogueId.FactoryGenerator, Mason, None);
            Emit(CampaignDialogueId.AsylumPower);
            yield return Compare(CampaignDialogueId.AsylumPower, Mason, None);
            Emit(CampaignDialogueId.ClientSuppressionOrder);
            Emit(CampaignDialogueId.EvidenceTransmitted);
            yield return Compare(CampaignDialogueId.ClientSuppressionOrder, Mason, None);
            yield return Compare(CampaignDialogueId.CommandRejectsOrder, Mason, None);
            yield return Compare(CampaignDialogueId.EvidenceTransmitted, Mason, None);

            // Local presentation check on the actual host renderer; restore the user's Game View size during teardown.
            yield return CaptureLayout(1920, 1080);
            yield return CaptureLayout(3840, 2160);
        }

        private void Emit(CampaignDialogueId id) => typeof(CampaignMissionController).GetMethod("EmitDialogue", Private)
            .Invoke(campaign, new object[] { id, null });

        private static void SetPrivate(object instance, string name, object value) =>
            instance.GetType().GetField(name, Private).SetValue(instance, value);

        private IEnumerator Compare(CampaignDialogueId id, CampaignRadioVoice first, CampaignRadioVoice second)
        {
            foreach (var cue in campaign.Dialogue(id).CuesFor(second))
            {
                string expected = cue.Format(first, second);
                yield return Wait(() => CampaignTextHighlight.StripRichText(hostCaption.text) == expected && Peer()?.radioCaption == expected,
                    14, $"D{(int)id:00}: {Tag(expected)}");
                Debug.Log($"[Radio48] D{(int)id:00} host = client: {Tag(expected)}");
            }
        }

        private CampaignPeerProbe.Status Peer()
        {
            try
            {
                string path = Path.Combine(exchange, "status.json");
                return File.Exists(path) ? JsonUtility.FromJson<CampaignPeerProbe.Status>(File.ReadAllText(path)) : null;
            }
            catch (IOException) { return null; }
        }

        private IEnumerator Wait(Func<bool> condition, float seconds, string expected)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline || peer is { HasExited: true })
                    Assert.Fail($"Waiting for {expected}. Host: '{Tag(hostCaption?.text)}'; client: '{Tag(Peer()?.radioCaption)}'; player log: {exchange}");
                yield return null;
            }
        }

        private IEnumerator CaptureLayout(int width, int height)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var assembly = typeof(EditorWindow).Assembly;
            gameView ??= EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView", true));
            if (sizeProperty == null)
            {
                sizeProperty = gameView.GetType().GetProperty("selectedSizeIndex", all);
                previousSize = (int)sizeProperty.GetValue(gameView);
            }
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", all).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            var group = sizesType.GetMethod("GetGroup", all).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            int count = (int)group.GetType().GetMethod("GetTotalCount", all).Invoke(group, null);
            int index = Enumerable.Range(0, count).Where(i =>
            {
                var size = group.GetType().GetMethod("GetGameViewSize", all).Invoke(group, new object[] { i });
                return (int)sizeType.GetProperty("width", all).GetValue(size) == width && (int)sizeType.GetProperty("height", all).GetValue(size) == height;
            }).DefaultIfEmpty(-1).First();
            if (index == -1)
            {
                var kind = assembly.GetType("UnityEditor.GameViewSizeType", true);
                var size = sizeType.GetConstructor(all, null, new[] { kind, typeof(int), typeof(int), typeof(string) }, null)
                    .Invoke(new object[] { Enum.Parse(kind, "FixedResolution"), width, height, $"Radio QA {width}x{height}" });
                group.GetType().GetMethod("AddCustomSize", all).Invoke(group, new[] { size });
                index = count;
            }
            sizeProperty.SetValue(gameView, index);
            gameView.Show(); gameView.Focus(); gameView.Repaint();
            yield return Wait(() => Screen.width == width && Screen.height == height && hostCaption.text.Length == 0, 15, $"clear HUD at {width}x{height}");
            var hud = CampaignHUD.Instance;
            var refresh = typeof(CampaignHUD).GetMethod("RefreshPresentation", Private);
            var definition = campaign.Dialogue(CampaignDialogueId.FactoryGenerator);
            void Render(string value)
            {
                hostCaption.text = value;
                refresh.Invoke(hud, null);
                Canvas.ForceUpdateCanvases();
            }
            Render(definition.speaker + ": " + definition.english);
            yield return new WaitForEndOfFrame();
            Shot($"before-{height}.png");
            int checkedCues = 0;
            foreach (var scene in campaign.ContentCatalog.dialogue)
            foreach (var second in scene.secondVoice == None ? new[] { None } : new[] { Vega, None })
            foreach (var cue in scene.CuesFor(second))
            foreach (bool vietnamese in new[] { false, true })
            {
                Render(cue.Format(Raven, second, vietnamese));
                Assert.That(hostCaption.cachedTextGenerator.lineCount, Is.InRange(1, 2), hostCaption.text);
                Assert.That(hostCaption.preferredHeight, Is.LessThanOrEqualTo(hostCaption.rectTransform.rect.height + 1), hostCaption.text);
                ++checkedCues;
            }
            Render(definition.cues[0].Format(Vega, Raven));
            yield return new WaitForEndOfFrame();
            Shot($"after-{height}.png");
            Render("");
            Debug.Log($"[Radio48] {width}x{height}: {checkedCues} localized captions checked on the actual HUD, at most two lines, no vertical clipping");
        }

        private void Shot(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That((texture.width, texture.height), Is.EqualTo((Screen.width, Screen.height)));
            File.WriteAllBytes(Path.Combine(exchange, name), texture.EncodeToPNG());
            Object.Destroy(texture);
        }

        [UnityTearDown]
        public IEnumerator StopSession()
        {
            try
            {
                if (sizeProperty != null && gameView != null) sizeProperty.SetValue(gameView, previousSize);
                if (peer != null && !peer.HasExited) peer.Kill();
                peer?.Dispose();
                if (network != null && network.IsListening) network.Shutdown();
                for (int i = 0; i < 5; i++) yield return null;
                yield return SceneManager.LoadSceneAsync("MainMenu");
            }
            finally
            {
                if (backedUp)
                {
                    Restore(CampaignCheckpoint.SavePath, save);
                    Restore(CampaignCheckpoint.SavePath + ".previous", previous);
                }
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static void Restore(string path, byte[] bytes)
        {
            if (bytes != null) File.WriteAllBytes(path, bytes);
            else if (File.Exists(path)) File.Delete(path);
        }
    }
}
#endif
