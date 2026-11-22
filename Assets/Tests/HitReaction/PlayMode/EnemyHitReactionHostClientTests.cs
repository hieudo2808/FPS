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
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// Two processes: this Editor hosts GameScene, the Development Player built by
    /// Tools/FPS/Campaign/Build Peer Client joins as a remote client. The server shoots a zombie in the
    /// leg and the chest; the remote client must present both reactions on the same bones, with blood.
    /// </summary>
    public sealed class EnemyHitReactionHostClientTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Evidence = "Reports/HitReaction-2026-11-13/";
        const string PlayerPath = "Builds/CampaignPeer/FPS.exe";
        const string ZombiePath = "Assets/FPS/Features/Characters/Content/Enemies/ZombieRig/Prefabs/ZombieRig.prefab";
        const int SessionDisconnectTimeoutMS = 120000;

        PlayerHealth clientPlayer;
        Process player;
        string exchange;
        string saveBackup;
        bool hadSave, hadPrevious;
        readonly List<string> log = new();
        float started;

        void Clock(string line)
        {
            line = $"[{Time.realtimeSinceStartup - started:0.0}s] {line}";
            log.Add(line);
            Debug.Log("[HitReactionHostClient] " + line);
        }

        [UnitySetUp]
        public IEnumerator StartSession()
        {
            if (!File.Exists(PlayerPath)) Assert.Ignore("Build the peer first: Tools/FPS/Campaign/Build Peer Client");
            started = Time.realtimeSinceStartup;
            saveBackup = Path.Combine(Application.temporaryCachePath, "campaign-save-backup-" + Guid.NewGuid().ToString("N"));
            hadSave = File.Exists(CampaignCheckpoint.SavePath);
            hadPrevious = File.Exists(CampaignCheckpoint.SavePath + ".previous");
            if (hadSave) File.Copy(CampaignCheckpoint.SavePath, saveBackup, true);
            if (hadPrevious) File.Copy(CampaignCheckpoint.SavePath + ".previous", saveBackup + ".previous", true);
            Directory.CreateDirectory(Evidence);
            exchange = Path.GetFullPath(Path.Combine(Evidence, "exchange"));
            if (Directory.Exists(exchange)) Directory.Delete(exchange, true);
            Directory.CreateDirectory(exchange);
            log.Clear();
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var network = NetworkManager.Singleton;
            Assert.That(network, Is.Not.Null);
            Assert.That(network.IsListening, Is.False);
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            ushort port;
            using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            var transport = network.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            // The Editor host stalls for tens of seconds while loading GameScene; do not drop the client for it.
            transport.DisconnectTimeoutMS = SessionDisconnectTimeoutMS;
            bool joined = false;
            network.OnClientConnectedCallback += id => { if (id != NetworkManager.ServerClientId) joined = true; Clock("connected " + id); };
            network.OnClientDisconnectCallback += id => Clock("disconnected " + id);
            Assert.That(network.StartHost(), Is.True);

            player = Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetFullPath(PlayerPath),
                Arguments = $"-campaignPeer {port} \"{exchange}\" -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile \"{Path.Combine(exchange, "player.log")}\"",
                UseShellExecute = false
            });
            yield return WaitFor(() => joined, 120, "the built client to connect");
            ulong remote = network.ConnectedClientsIds.First(id => id != NetworkManager.ServerClientId);

            bool loaded = false;
            void OnLoaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
            {
                if (scene != "GameScene") return;
                network.SceneManager.OnLoadEventCompleted -= OnLoaded;
                Clock($"GameScene loaded: completed=[{string.Join(",", completed)}] timedOut=[{string.Join(",", timedOut)}]");
                var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
                var characters = new[] { PlayerCharacterId.Brimstone, PlayerCharacterId.Sage };
                int slot = 0;
                foreach (ulong client in network.ConnectedClientsIds.OrderBy(id => id))
                {
                    Assert.That(catalog.TryGetPrefab(characters[slot], out var prefab), Is.True);
                    NetworkSpawnManager.Instance.TryGetNextSpawnPose(out var position, out var rotation);
                    var spawned = Object.Instantiate(prefab, position, rotation);
                    spawned.GetComponent<PlayerHealth>().PrepareInitialSpawn(new SessionPlayerId((ulong)(slot + 1)));
                    spawned.GetComponent<NetworkObject>().SpawnAsPlayerObject(client);
                    slot++;
                }
                NetworkMatchStateManager.Instance.EnterWarmup();
                loaded = true;
            }
            network.SceneManager.OnLoadEventCompleted += OnLoaded;
            network.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            yield return WaitFor(() => loaded, 180, "both peers to finish loading GameScene");
            yield return WaitFor(() => CampaignMissionController.Instance != null
                && CampaignMissionController.Instance.State.phase == CampaignPhase.Exploring, 150, "the two-operator insertion to finish");
            var campaign = CampaignMissionController.Instance;
            var hostPlayer = network.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            clientPlayer = network.ConnectedClients[remote].PlayerObject.GetComponent<PlayerHealth>();
            foreach (var p in new[] { hostPlayer, clientPlayer })
                typeof(PlayerHealth).GetField("campaignProtectionUntil", Private)?.SetValue(p, campaign.Now + 1e6);
            yield return WaitFor(() => Read()?.hasPlayer == true, 60, "the client to own its player");
        }

        [UnityTearDown]
        public IEnumerator StopSession()
        {
            File.WriteAllLines(Evidence + TestContext.CurrentContext.Test.Name + ".txt", log);
            try { if (player != null && !player.HasExited) player.Kill(); } catch (InvalidOperationException) { }
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            for (int i = 0; i < 5; i++) yield return null;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            if (saveBackup != null)
            {
                Restore(CampaignCheckpoint.SavePath, saveBackup, hadSave);
                Restore(CampaignCheckpoint.SavePath + ".previous", saveBackup + ".previous", hadPrevious);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        static void Restore(string path, string backup, bool existed)
        {
            if (existed) { File.Copy(backup, path, true); File.Delete(backup); }
            else if (File.Exists(path)) File.Delete(path);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator RemoteClientPresentsTheServersHitOnTheSameBoneWithBlood()
        {
            Vector3 spot = clientPlayer.transform.position + clientPlayer.transform.forward * 5f;
            Assert.That(NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas), Is.True, "NavMesh near the client");
            var zombie = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePath), hit.position,
                Quaternion.LookRotation(clientPlayer.transform.position - hit.position));
            zombie.GetComponent<NetworkObject>().Spawn(true);
            var ai = zombie.GetComponent<EnemyAI>();
            var health = zombie.GetComponent<EnemyHealth>();
            Clock($"zombie spawned at {hit.position} hp={health.MaxHealth}");
            yield return Pause(2f); // Let the client instantiate it.

            var segments = zombie.GetComponentsInChildren<HitboxSegment>(true);
            var leg = segments.First(s => s.Zone == HitboxZone.Leg);
            var chest = segments.First(s => s.Zone == HitboxZone.Chest);
            int before = Read().enemyHitsPresented, bloodBefore = Read().enemyBloodSpawned;
            Vector3 muzzle = clientPlayer.transform.position + Vector3.up * 1.5f;

            health.TakeDamage(new DamageInfo(5f, 1, 1, leg.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Leg, sourcePosition: muzzle));
            yield return WaitFor(() => ai.HitReactionCount == 1, 5, "the server to decide the leg hit");
            string legKind = ai.LastHitReaction.ToString(), legBone = EnemyHitReactionPresenter.LastBone;
            Clock($"server leg: kind={legKind} bone={legBone} hostBlood={EnemyHitReactionPresenter.BloodSpawnedCount}");
            yield return WaitFor(() => Read()?.enemyHitsPresented == before + 1, 10, "the client to present the leg hit");
            var seen = Read();
            Clock($"client leg: kind={seen.lastEnemyHitKind} bone={seen.lastEnemyHitBone} blood={seen.enemyBloodSpawned}");
            Assert.That(seen.lastEnemyHitKind, Is.EqualTo(legKind));
            Assert.That(seen.lastEnemyHitBone, Is.EqualTo(legBone).And.Not.Empty, "Same bone on both peers");
            Assert.That(seen.enemyBloodSpawned, Is.EqualTo(bloodBefore + 1), "The client sprays blood");

            yield return Pause(EnemyHitReactionRules.GameplayCooldown + 0.2f);
            health.TakeDamage(new DamageInfo(health.MaxHealth * 0.3f, 1, 1, chest.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Chest, sourcePosition: muzzle));
            yield return WaitFor(() => ai.HitReactionCount == 2, 5, "the server to decide the chest hit");
            string chestKind = ai.LastHitReaction.ToString(), chestBone = EnemyHitReactionPresenter.LastBone;
            Clock($"server chest: kind={chestKind} bone={chestBone}");
            yield return WaitFor(() => Read()?.enemyHitsPresented == before + 2, 10, "the client to present the chest hit");
            seen = Read();
            Clock($"client chest: kind={seen.lastEnemyHitKind} bone={seen.lastEnemyHitBone} blood={seen.enemyBloodSpawned}");
            Assert.That(seen.lastEnemyHitKind, Is.EqualTo(chestKind));
            Assert.That(seen.lastEnemyHitBone, Is.EqualTo(chestBone));
            Assert.That(chestKind, Is.Not.EqualTo(EnemyHitReactionKind.Flinch.ToString()), "A heavy hit after the cooldown changes gameplay");
            zombie.GetComponent<NetworkObject>().Despawn(true);
        }

        CampaignPeerProbe.Status Read()
        {
            try
            {
                string path = Path.Combine(exchange, "status.json");
                return File.Exists(path) ? JsonUtility.FromJson<CampaignPeerProbe.Status>(File.ReadAllText(path)) : null;
            }
            catch (IOException) { return null; }
        }

        static IEnumerator Pause(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= until)
                {
                    var seen = Read();
                    string detail = $"TIMEOUT waiting {seconds:0}s for {what} | host clients={NetworkManager.Singleton?.ConnectedClientsList.Count}"
                        + (seen != null ? $" | client seq={seen.sequence} connected={seen.connected} scene={seen.scene} player={seen.hasPlayer} hits={seen.enemyHitsPresented} msg={seen.lastMessage}" : " | client wrote no status")
                        + (player != null && player.HasExited ? $" | client process exited with {player.ExitCode}" : "");
                    Clock(detail);
                    Assert.Fail(detail);
                }
                yield return null;
            }
        }
    }
}
#endif
