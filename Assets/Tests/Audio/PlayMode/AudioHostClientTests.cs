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
    /// Editor host + built Development Player client (Tools/FPS/Campaign/Build Peer Client). Each peer plays the
    /// sounds of what it sees replicated: director music and ambience, the other player's shots and reload,
    /// zombie pain and death, and a teammate going down (voice + breathing) and dying.
    /// </summary>
    public sealed class AudioHostClientTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Evidence = "Reports/Audio-2026-11-14/";
        const string PlayerPath = "Builds/CampaignPeer/FPS.exe";
        const string TankPath = "Assets/FPS/Features/Characters/Content/Enemies/Tanker/Prefabs/Tank.prefab";
        const string ZombiePath = "Assets/FPS/Features/Characters/Content/Enemies/ZombieRig/Prefabs/ZombieRig.prefab";
        const int SessionDisconnectTimeoutMS = 120000;

        PlayerHealth clientPlayer, hostPlayer;
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
            Debug.Log("[AudioHostClient] " + line);
        }

        [UnitySetUp]
        public IEnumerator StartSession()
        {
            if (!File.Exists(PlayerPath)) Assert.Ignore("Build the peer first: Tools/FPS/Campaign/Build Peer Client");
            started = Time.realtimeSinceStartup;
            lastSeen = null;
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
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
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
            hostPlayer = network.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
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

        static int Count(string summary, string name)
        {
            if (string.IsNullOrEmpty(summary)) return 0;
            foreach (string part in summary.Split(';'))
            {
                int eq = part.LastIndexOf('=');
                if (eq > 0 && part.Substring(0, eq) == name) return int.Parse(part.Substring(eq + 1));
            }
            return 0;
        }

        CampaignPeerProbe.Status lastSeen;

        /// <summary>The newest status the client wrote (the file briefly disappears while it is replaced).</summary>
        CampaignPeerProbe.Status Seen()
        {
            var status = Read();
            if (status != null) lastSeen = status;
            return lastSeen ?? new CampaignPeerProbe.Status();
        }

        int ClientCount(string name) => Count(Seen().audioCounts, name);

        static void Hit(PlayerHealth victim, float amount, HitboxZone zone) =>
            victim.TakeDamage(new DamageInfo(amount, ulong.MaxValue, -1, victim.transform.position + Vector3.up,
                damageType: DamageType.Melee, hitZone: zone, sourcePosition: victim.transform.position + victim.transform.forward * 2f));

        static void ShotEffects(PlayerHealth shooter)
        {
            var weapons = shooter.GetComponent<WeaponManager>();
            var handler = shooter.GetComponent<WeaponFireHandler>();
            Vector3 origin = shooter.transform.position + Vector3.up * 1.5f;
            // The server's shot presentation RPC, exactly as an accepted shot sends it.
            typeof(WeaponFireHandler).GetMethod("FireEffectsClientRpc", Private).Invoke(handler,
                new object[] { origin, shooter.transform.forward, (ushort)1, false, 0f, (byte)weapons.CurrentWeaponIndex });
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator CharacterHurtDownedAndDeathPlayOnceOnBothPeers()
        {
            var voices = new List<AudioStartInfo>();
            void Started(AudioStartInfo info) => voices.Add(info);
            GameAudio.VoiceStarted += Started;
            try
            {
                foreach (var victim in new[] { hostPlayer, clientPlayer })
                {
                    var audio = victim.GetComponent<PlayerAudio>();
                    string hurt = audio.Voice.hurt.name;
                    int hostBefore = GameAudio.CountOf(hurt), clientBefore = ClientCount(hurt);
                    typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(victim, 0.0);
                    Hit(victim, 10f, HitboxZone.Chest);
                    yield return WaitFor(() => GameAudio.CountOf(hurt) > hostBefore && ClientCount(hurt) > clientBefore, 10, hurt + " on both peers");
                    Assert.That(GameAudio.CountOf(hurt), Is.EqualTo(hostBefore + 1));
                    Assert.That(ClientCount(hurt), Is.EqualTo(clientBefore + 1));
                    var played = voices.Single(v => v.Sound == audio.Voice.hurt);
                    Assert.That(played.SpatialBlend, Is.EqualTo(victim == hostPlayer ? 0f : 1f));
                    Assert.That(played.Bus, Is.EqualTo(AudioBus.Player));
                    Clock($"{hurt}: exactly once on host and client; host clip={played.Clip.name}, spatialBlend={played.SpatialBlend}");
                    typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(victim, CampaignMissionController.Instance.Now + 1e6);
                }
                yield return Pause(1f);
                yield return ClientDownedAndDeath();
                var death = voices.Single(v => v.Sound == clientPlayer.GetComponent<PlayerAudio>().Voice.death);
                Assert.That(death.Clip.name, Is.EqualTo("Sage_Death_01"));
                Assert.That(death.Clip.length, Is.InRange(0.28f, 0.29f));
                Clock($"death source: {death.Clip.name}, length={death.Clip.length:0.0000}s, pitch={death.Pitch}, bus={death.Bus}");
            }
            finally { GameAudio.VoiceStarted -= Started; }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator MusicGunsReloadZombiesAndVoicesAcrossPeers()
        {
            // 1. Director music and ambience on both peers, same cue.
            yield return WaitFor(() => GameAudio.CurrentMusic != MusicCue.None && GameAudio.AmbiencePlaying, 20, "host music and ambience");
            yield return WaitFor(() => Seen().musicCue == GameAudio.CurrentMusic.ToString() && Seen().ambience, 20, "the client to play the same music cue");
            Clock($"music: director={AIDirector.Instance.AdaptivePhase} host={GameAudio.CurrentMusic} client={Seen().musicCue} clientListener={Seen().listener}");
            Assert.That(Seen().listener, Is.True, "The client has an audio listener");

            // 1b. A Tank in the level: both peers switch to the Tank music at once, and stop wanting it when it is gone.
            Vector3 tankSpot = hostPlayer.transform.position + hostPlayer.transform.forward * 25f;
            Assert.That(NavMesh.SamplePosition(tankSpot, out NavMeshHit tankHit, 15f, NavMesh.AllAreas), Is.True, "NavMesh for the Tank");
            var tank = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TankPath), tankHit.position, Quaternion.identity);
            tank.GetComponent<NetworkObject>().Spawn(true);
            yield return WaitFor(() => GameAudio.CurrentMusic == MusicCue.Tank && Seen().musicCue == "Tank", 10, "Tank music on both peers");
            Clock($"tank alive: host={GameAudio.CurrentMusic} client={Seen().musicCue}");
            tank.GetComponent<NetworkObject>().Despawn(true);
            yield return WaitFor(() => GameAudio.WantedMusic != MusicCue.Tank && Seen().musicWanted != "Tank", 5, "both peers to drop the Tank cue");
            Clock($"tank gone: host plays {GameAudio.CurrentMusic} wants {GameAudio.WantedMusic}; client plays {Seen().musicCue} wants {Seen().musicWanted} (steps down after the hold)");

            // 2. The host fires: the client hears it from the host position.
            var hostWeapons = hostPlayer.GetComponent<WeaponManager>();
            string hostFire = hostWeapons.GetWeapon(hostWeapons.CurrentWeaponIndex).Data.fireSound.name;
            int before = ClientCount(hostFire);
            ShotEffects(hostPlayer);
            yield return WaitFor(() => ClientCount(hostFire) > before && Seen().otherFired > 0, 10, "the client to hear the host shot");
            Clock($"client heard {hostFire}: {ClientCount(hostFire)} (otherFired={Seen().otherFired})");

            // 3. The client fires: the host hears it.
            var clientWeapons = clientPlayer.GetComponent<WeaponManager>();
            WeaponData clientGun = clientWeapons.GetWeapon(clientWeapons.CurrentWeaponIndex).Data;
            int hostBefore = GameAudio.CountOf(clientGun.fireSound.name);
            ShotEffects(clientPlayer);
            yield return WaitFor(() => GameAudio.CountOf(clientGun.fireSound.name) > hostBefore, 5, "the host to hear the client shot");
            Assert.That(clientWeapons.Audio.FiredCount, Is.GreaterThan(0));
            Clock($"host heard {clientGun.fireSound.name}: {GameAudio.CountOf(clientGun.fireSound.name)}");

            // 4. The server starts the client reload: cues on the host (3D) and on the client (its own, 2D).
            var clientHandler = clientPlayer.GetComponent<WeaponFireHandler>();
            var state = (WeaponServerState)typeof(WeaponFireHandler).GetMethod("GetCurrentServerState", Private).Invoke(clientHandler, new object[] { true });
            typeof(WeaponServerState).GetField("magazineAmmo", Private).SetValue(state, 1);
            bool accepted = (bool)typeof(WeaponFireHandler).GetMethod("TryBeginServerReload", Private).Invoke(clientHandler, null);
            Assert.That(accepted, Is.True, "The server accepts the reload");
            // The magazine-in foley: the cue nearest the server's ammo commit (or the shell-in event).
            string insert = clientGun.reloadCues.Length > 0
                ? clientGun.reloadCues.OrderBy(c => Mathf.Abs(c.time - clientGun.ReloadAmmoCommitDuration)).First().clip.name
                : clientGun.reloadInsertSound.name;
            int hostInsert = GameAudio.CountOf(insert), clientInsert = ClientCount(insert);
            for (int i = 0; i < 4; i++)
            {
                yield return Pause(0.75f);
                Clock($"reload t+{(i + 1) * 0.75f:0.00}s: serverReloading={clientHandler.IsServerReloading} mag={state.MagazineAmmo} hostScheduled={clientWeapons.Audio.ReloadsScheduled} hostCues={clientWeapons.Audio.ReloadCuesPlayed} clientCues={Seen().ownReloadCues} commit={clientGun.ReloadAmmoCommitDuration:0.00} duration={clientGun.ReloadDuration:0.00}");
            }
            yield return WaitFor(() => clientWeapons.Audio.ReloadCuesPlayed >= 2 && Seen().ownReloadCues >= 2, clientGun.ReloadDuration + 6f, "reload cues on both peers");
            Clock($"reload {clientGun.weaponName}: host cues={clientWeapons.Audio.ReloadCuesPlayed} ({insert} {hostInsert}->{GameAudio.CountOf(insert)}), client own cues={Seen().ownReloadCues} ({insert} {clientInsert}->{ClientCount(insert)})");
            Assert.That(GameAudio.CountOf(insert), Is.GreaterThan(hostInsert));
            Assert.That(ClientCount(insert), Is.GreaterThan(clientInsert));

            // 5. A zombie near the client: pain and death on both peers.
            Vector3 spot = clientPlayer.transform.position + clientPlayer.transform.forward * 5f;
            Assert.That(NavMesh.SamplePosition(spot, out NavMeshHit navHit, 4f, NavMesh.AllAreas), Is.True, "NavMesh near the client");
            var zombie = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePath), navHit.position,
                Quaternion.LookRotation(clientPlayer.transform.position - navHit.position));
            zombie.GetComponent<NetworkObject>().Spawn(true);
            var health = zombie.GetComponent<EnemyHealth>();
            yield return Pause(2f);
            var leg = zombie.GetComponentsInChildren<HitboxSegment>(true).First(s => s.Zone == HitboxZone.Leg);
            int hostHurt = GameAudio.CountOf("Zombie_Hurt"), clientHurt = ClientCount("Zombie_Hurt");
            health.TakeDamage(new DamageInfo(5f, 1, 1, leg.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Leg, sourcePosition: clientPlayer.transform.position + Vector3.up * 1.5f));
            yield return WaitFor(() => GameAudio.CountOf("Zombie_Hurt") > hostHurt && ClientCount("Zombie_Hurt") > clientHurt, 10, "zombie pain on both peers");
            int hostDeath = GameAudio.CountOf("Zombie_Death"), clientDeath = ClientCount("Zombie_Death");
            health.TakeDamage(new DamageInfo(health.MaxHealth * 2f, 1, 1, leg.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Chest, sourcePosition: clientPlayer.transform.position + Vector3.up * 1.5f));
            yield return WaitFor(() => GameAudio.CountOf("Zombie_Death") > hostDeath && ClientCount("Zombie_Death") > clientDeath, 10, "zombie death call on both peers");
            Clock($"zombie: hurt host={GameAudio.CountOf("Zombie_Hurt")} client={ClientCount("Zombie_Hurt")}, death host={GameAudio.CountOf("Zombie_Death")} client={ClientCount("Zombie_Death")}, idle client={ClientCount("Zombie_Idle")}");

            yield return ClientDownedAndDeath();
        }

        IEnumerator ClientDownedAndDeath()
        {
            var clientAudio = clientPlayer.GetComponent<PlayerAudio>();
            Assert.That(clientAudio.Voice.name, Is.EqualTo("Voice_Sage"));
            string downed = clientAudio.Voice.downed.name, death = clientAudio.Voice.death.name, hurt = clientAudio.Voice.hurt.name;
            typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(clientPlayer, 0.0);
            int hurtBefore = GameAudio.CountOf(hurt), clientHurtBefore = ClientCount(hurt);
            int downedBefore = GameAudio.CountOf(downed), clientDownedBefore = ClientCount(downed);
            Hit(clientPlayer, clientPlayer.CurrentHealth + 5f, HitboxZone.Chest);
            yield return WaitFor(() => clientPlayer.LifeState == PlayerLifeState.Downed, 5, "the server to down the client");
            yield return WaitFor(() => GameAudio.CountOf(downed) > downedBefore && clientAudio.Breathing
                && Seen().ownBreathing && ClientCount(downed) > clientDownedBefore, 10, "downed effort and breathing on both peers");
            Assert.That(GameAudio.CountOf(downed), Is.EqualTo(downedBefore + 1));
            Assert.That(ClientCount(downed), Is.EqualTo(clientDownedBefore + 1));
            Assert.That(GameAudio.CountOf(hurt), Is.EqualTo(hurtBefore), "No overlapping lethal Hurt on host");
            Assert.That(ClientCount(hurt), Is.EqualTo(clientHurtBefore), "No overlapping lethal Hurt on client");
            Clock($"{downed}: exactly once per peer; no lethal Hurt; breathing active");

            typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(clientPlayer, 0.0);
            int deathBefore = GameAudio.CountOf(death), clientDeathBefore = ClientCount(death);
            // Downed damage only shortens the bleed-out: deal enough to run it out.
            var campaign = CampaignMissionController.Instance;
            float bleedout = (float)((clientPlayer.LifeStateDeadline - campaign.Now) / Mathf.Max(0.001f, campaign.settings.bleedoutDamageSeconds));
            Hit(clientPlayer, bleedout + 10f, HitboxZone.Head);
            yield return WaitFor(() => clientPlayer.LifeState == PlayerLifeState.Dead || clientPlayer.LifeState == PlayerLifeState.Spectating, 5, "the client to die");
            yield return WaitFor(() => GameAudio.CountOf(death) > deathBefore && ClientCount(death) > clientDeathBefore
                && !clientAudio.Breathing && !Seen().ownBreathing, 10, "death effort, breathing stopped");
            yield return Pause(1f);
            Assert.That(GameAudio.CountOf(death), Is.EqualTo(deathBefore + 1));
            Assert.That(ClientCount(death), Is.EqualTo(clientDeathBefore + 1));
            Clock($"{death}: exactly once on host and client; breathing stopped");
            Clock("host counts: " + GameAudio.CountsSummary());
            Clock("client counts: " + Seen().audioCounts);
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
