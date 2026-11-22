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
    /// Review footage, not a pass/fail test: the Editor hosts GameScene with the built peer as teammate and records
    /// each weapon in first person, each enemy type, and going down, with every sound logged against the frames.
    /// </summary>
    public sealed class AudioVisualCapture
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
            Debug.Log("[AudioVisualCapture] " + line);
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

        AvRecorder rec;
        static readonly BindingFlags Field = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string OutputDirectory => Path.Combine(Application.temporaryCachePath, "AudioVisualCapture");

        Transform HostEye()
        {
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled) return l.transform;
            return hostPlayer.transform;
        }

        void Shot(WeaponManager weapons, Weapon weapon, int slot, ushort sequence)
        {
            Transform eye = HostEye();
            weapon.PlayMuzzleEffect();
            weapon.SpawnVisualProjectiles(eye.position, eye.forward, sequence, hostPlayer.OwnerClientId, (byte)slot, false, 0f);
            weapons.CompleteActionsForAcceptedShot();
            weapon.PlayShootSound();
        }

        bool ServerReload(PlayerHealth owner, int magazine)
        {
            var handler = owner.GetComponent<WeaponFireHandler>();
            var state = (WeaponServerState)typeof(WeaponFireHandler).GetMethod("GetCurrentServerState", Private).Invoke(handler, new object[] { true });
            typeof(WeaponServerState).GetField("magazineAmmo", Private).SetValue(state, magazine);
            return (bool)typeof(WeaponFireHandler).GetMethod("TryBeginServerReload", Private).Invoke(handler, null);
        }

        IEnumerator WeaponScene(string key, string title, int slot, PrimaryWeaponId? primary)
        {
            var weapons = hostPlayer.GetComponent<WeaponManager>();
            if (primary.HasValue)
            {
                Assert.That(weapons.TryReplacePrimaryWeaponServer(primary.Value), Is.True, title);
                yield return Pause(0.4f);
            }
            if (weapons.CurrentWeaponIndex != slot) weapons.SetEquippedWeaponServer(slot);
            yield return WaitFor(() => weapons.CurrentWeaponIndex == slot, 5, "the host to hold " + title);
            var weapon = weapons.GetWeapon(slot);
            WeaponData data = weapon.Data;
            yield return Pause(data.EquipAnimationDuration + 0.6f);

            rec.Begin(key, title, null);
            yield return Pause(0.5f);
            bool auto = data.fireMode == FireMode.Auto;
            int shots = auto ? Mathf.RoundToInt(1.2f / data.FireInterval) : data.FireInterval > 0.5f ? 2 : 3;
            float gap = auto ? data.FireInterval : Mathf.Max(data.FireInterval, 0.35f);
            for (int k = 0; k < shots; k++)
            {
                Shot(weapons, weapon, slot, (ushort)(k + 1));
                yield return Pause(gap);
            }
            yield return Pause(0.9f + (data.fireCues.Length > 0 ? data.fireCues[^1].time : 0f));
            bool perShell = data.reloadMode == ReloadMode.PerShell;
            Assert.That(ServerReload(hostPlayer, perShell ? 0 : Mathf.Max(1, data.magazineSize / 3)), Is.True, title + " reload accepted");
            float reload = perShell
                ? data.PerShellOpeningDuration + data.magazineSize * data.PerShellInterval + data.PerShellClosingDuration
                : data.ReloadDuration;
            yield return Pause(reload + 0.9f);
            yield return rec.End();
            Clock($"{key}: {data.weaponName} {shots} shots, reload {reload:0.00}s");
        }

        IEnumerator EnemyScene(string key, string title, string prefabPath, float distance, float watch, int hits)
        {
            Transform eye = HostEye();
            Vector3 flat = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            Vector3 spot = hostPlayer.transform.position + flat * distance;
            Assert.That(NavMesh.SamplePosition(spot, out NavMeshHit nav, 6f, NavMesh.AllAreas), Is.True, "NavMesh for " + title);
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), nav.position,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(hostPlayer.transform.position - nav.position, Vector3.up)));
            enemy.GetComponent<NetworkObject>().Spawn(true);
            var health = enemy.GetComponent<EnemyHealth>();
            rec.Begin(key, title, null);
            yield return Pause(watch);
            var segments = enemy.GetComponentsInChildren<HitboxSegment>(true);
            var chest = segments.FirstOrDefault(s => s.Zone == HitboxZone.Chest) ?? segments.First();
            for (int i = 0; i < hits; i++)
            {
                health.TakeDamage(new DamageInfo(Mathf.Min(25f, health.MaxHealth * 0.05f), 1, 1, chest.GetComponent<Collider>().bounds.center,
                    damageType: DamageType.Bullet, hitZone: chest.Zone, sourcePosition: eye.position));
                yield return Pause(1.2f);
            }
            health.TakeDamage(new DamageInfo(health.MaxHealth * 2f, 1, 1, chest.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: chest.Zone, sourcePosition: eye.position));
            yield return Pause(3.2f);
            yield return rec.End();
            var net = enemy != null ? enemy.GetComponent<NetworkObject>() : null;
            if (net != null && net.IsSpawned) net.Despawn(true);
            yield return Pause(0.5f);
        }

        Camera ReviewCamera(PlayerHealth subject, Vector3 offsetLocal)
        {
            var go = new GameObject("AV review camera");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            foreach (string layer in new[] { "FirstPerson", "Weapon" })
                if (LayerMask.NameToLayer(layer) >= 0) camera.cullingMask &= ~(1 << LayerMask.NameToLayer(layer));
            Vector3 focus = subject.transform.position + Vector3.up * 0.6f;
            go.transform.position = focus + subject.transform.rotation * offsetLocal;
            go.transform.LookAt(focus);
            return camera;
        }

        public static string VideoDirectory => Path.Combine(Application.temporaryCachePath, "GunVideo");

        /// <summary>One gun as a video take: drawn from the other slot, fired, then reloaded from a part-empty magazine.</summary>
        IEnumerator GunVideoScene(string key, string title, int slot, int otherSlot, PrimaryWeaponId? primary)
        {
            var weapons = hostPlayer.GetComponent<WeaponManager>();
            if (primary.HasValue)
            {
                Assert.That(weapons.TryReplacePrimaryWeaponServer(primary.Value), Is.True, title);
                yield return Pause(0.4f);
            }
            if (weapons.CurrentWeaponIndex != otherSlot) weapons.SetEquippedWeaponServer(otherSlot);
            yield return WaitFor(() => weapons.CurrentWeaponIndex == otherSlot, 5, "the host to hold the other weapon");
            yield return Pause(weapons.GetWeapon(otherSlot).Data.EquipAnimationDuration + 0.6f);

            var weapon = weapons.GetWeapon(slot);
            WeaponData data = weapon.Data;
            rec.BeginVideo(key, title);
            yield return Pause(0.4f);
            weapons.SetEquippedWeaponServer(slot);
            yield return WaitFor(() => weapons.CurrentWeaponIndex == slot, 5, "the host to draw " + title);
            yield return Pause(data.EquipAnimationDuration + 0.3f);

            bool auto = data.fireMode == FireMode.Auto;
            float cycle = data.fireCues.Length > 0 ? data.fireCues[^1].time + 0.5f : 0f;
            int shots = auto ? Mathf.RoundToInt(1.0f / data.FireInterval) : data.fireCues.Length > 0 ? 2 : 3;
            float gap = auto ? data.FireInterval : Mathf.Max(data.FireInterval, 0.45f, cycle);
            for (int k = 0; k < shots; k++)
            {
                Shot(weapons, weapon, slot, (ushort)(k + 1));
                yield return Pause(gap);
            }
            yield return Pause(0.7f);
            bool perShell = data.reloadMode == ReloadMode.PerShell;
            Assert.That(ServerReload(hostPlayer, perShell ? 0 : Mathf.Max(1, data.magazineSize / 3)), Is.True, title + " reload accepted");
            float reload = perShell
                ? data.ReloadAnimationDuration + (data.magazineSize - 1) * data.PerShellInterval
                : data.ReloadAnimationDuration;
            yield return Pause(reload + 0.7f);
            yield return rec.End();
            Clock($"{key}: {data.weaponName} {shots} shots, equip {data.EquipAnimationDuration:0.00}s, reload {reload:0.00}s, cues equip={weapons.Audio.EquipCuesPlayed} reload={weapons.Audio.ReloadCuesPlayed}");
        }

        [UnityTest, Explicit("Records gun review video"), Timeout(1800000)]
        public IEnumerator RecordGunVideo()
        {
            string dir = VideoDirectory;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            rec = new GameObject("AV Recorder").AddComponent<AvRecorder>();
            rec.Init(dir);
            var weapons = hostPlayer.GetComponent<WeaponManager>();
            int sidearm = weapons.FindDownedWeaponSlot();
            int primary = sidearm == 0 ? 1 : 0;

            // Warm-up, not recorded: the first reload of a session hitches while its code paths and clips load.
            if (weapons.CurrentWeaponIndex != sidearm) weapons.SetEquippedWeaponServer(sidearm);
            yield return WaitFor(() => weapons.CurrentWeaponIndex == sidearm, 5, "the host to hold the sidearm");
            yield return Pause(weapons.GetWeapon(sidearm).Data.EquipAnimationDuration + 0.5f);
            Shot(weapons, weapons.GetWeapon(sidearm), sidearm, 1);
            yield return Pause(0.5f);
            Assert.That(ServerReload(hostPlayer, 1), Is.True, "warm-up reload accepted");
            yield return Pause(weapons.GetWeapon(sidearm).Data.ReloadAnimationDuration + 0.8f);

            yield return GunVideoScene("classic", "Classic", sidearm, primary, null);
            yield return GunVideoScene("vandal", "Vandal", primary, sidearm, PrimaryWeaponId.Vandal);
            yield return GunVideoScene("odin", "Odin", primary, sidearm, PrimaryWeaponId.Odin);
            yield return GunVideoScene("warden", "Warden", primary, sidearm, PrimaryWeaponId.Warden);
            yield return GunVideoScene("operator", "Operator", primary, sidearm, PrimaryWeaponId.Operator);
            yield return GunVideoScene("bucky", "Bucky", primary, sidearm, PrimaryWeaponId.Bucky);

            Clock("written: " + string.Join(", ", rec.Written.Select(Path.GetFileName)));
            Clock("output: " + dir);
            Object.Destroy(rec.gameObject);
        }

        [UnityTest, Explicit("Records review footage"), Timeout(1800000)]
        public IEnumerator RecordSoundAgainstPicture()
        {
            string dir = OutputDirectory;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            rec = new GameObject("AV Recorder").AddComponent<AvRecorder>();
            rec.Init(dir);
            var weapons = hostPlayer.GetComponent<WeaponManager>();
            int sidearm = weapons.FindDownedWeaponSlot();
            int primary = sidearm == 0 ? 1 : 0;

            yield return WeaponScene("classic", "Classic (súng lục)", sidearm, null);
            yield return WeaponScene("vandal", "Vandal", primary, PrimaryWeaponId.Vandal);
            yield return WeaponScene("odin", "Odin", primary, PrimaryWeaponId.Odin);
            yield return WeaponScene("warden", "Warden", primary, PrimaryWeaponId.Warden);
            yield return WeaponScene("operator", "Operator", primary, PrimaryWeaponId.Operator);
            yield return WeaponScene("bucky", "Bucky", primary, PrimaryWeaponId.Bucky);

            const string Enemies = "Assets/FPS/Features/Characters/Content/Enemies/";
            yield return EnemyScene("zombie-rig", "Zombie (ZombieRig)", Enemies + "ZombieRig/Prefabs/ZombieRig.prefab", 7f, 6f, 2);
            yield return EnemyScene("zombie-girl", "Zombie nữ (Zombiegirl)", Enemies + "ZombiegirlW/Prefabs/Zombiegirl W Kurniawan.prefab", 7f, 6f, 2);
            yield return EnemyScene("infector", "Infector", Enemies + "Infector/Prefabs/Infector.prefab", 8f, 4f, 2);
            yield return EnemyScene("screamer", "Screamer", Enemies + "Screamer/Prefabs/BookHeadMonster_withBlood.prefab", 8f, 4f, 2);
            yield return EnemyScene("tank", "Tank", Enemies + "Tanker/Prefabs/Tank.prefab", 14f, 6f, 2);

            // The host goes down in first person, then is revived.
            typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(hostPlayer, 0.0);
            rec.Begin("host-downed", "Bản thân bị gục rồi được cứu (Brimstone, 1P)", null);
            yield return Pause(0.6f);
            Hit(hostPlayer, hostPlayer.CurrentHealth + 5f, HitboxZone.Chest);
            yield return WaitFor(() => hostPlayer.LifeState == PlayerLifeState.Downed, 5, "the host to go down");
            yield return Pause(5f);
            hostPlayer.ReviveCampaign(50f);
            yield return Pause(2f);
            yield return rec.End();
            typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(hostPlayer, CampaignMissionController.Instance.Now + 1e6);

            // The client (Sage) goes down and dies, seen from beside her.
            var camera = ReviewCamera(clientPlayer, new Vector3(2.4f, 1.3f, 2.2f));
            typeof(PlayerHealth).GetField("campaignProtectionUntil", Private).SetValue(clientPlayer, 0.0);
            rec.Begin("teammate-down", "Đồng đội bị gục rồi chết (Sage, 3P)", camera);
            yield return Pause(0.6f);
            Hit(clientPlayer, clientPlayer.CurrentHealth + 5f, HitboxZone.Chest);
            yield return WaitFor(() => clientPlayer.LifeState == PlayerLifeState.Downed, 5, "the client to go down");
            yield return Pause(4.5f);
            var campaign = CampaignMissionController.Instance;
            float bleedout = (float)((clientPlayer.LifeStateDeadline - campaign.Now) / Mathf.Max(0.001f, campaign.settings.bleedoutDamageSeconds));
            Hit(clientPlayer, bleedout + 10f, HitboxZone.Head);
            yield return Pause(3.5f);
            yield return rec.End();
            Object.Destroy(camera.gameObject);

            Clock("written: " + string.Join(", ", rec.Written.Select(Path.GetFileName)));
            Clock("output: " + dir);
            Object.Destroy(rec.gameObject);
        }

        static void Hit(PlayerHealth victim, float amount, HitboxZone zone) =>
            victim.TakeDamage(new DamageInfo(amount, ulong.MaxValue, -1, victim.transform.position + Vector3.up,
                damageType: DamageType.Melee, hitZone: zone, sourcePosition: victim.transform.position + victim.transform.forward * 2f));

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
