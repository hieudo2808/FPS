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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// Two processes: this Editor hosts GameScene, the Development Player built by Tools/FPS/Campaign/Build Peer
    /// Client joins as a remote client. Downed, revived and killed players: server rules (sidearm only while
    /// downed), the third-person presentation each peer shows of the other, and the downed first-person view.
    /// </summary>
    public sealed class PlayerLifeHostClientTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Evidence = "Reports/PlayerLife-2026-11-14/fix/";
        const string PlayerPath = "Builds/CampaignPeer/FPS.exe";
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
            Debug.Log("[PlayerLifeHostClient] " + line);
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

        static readonly BindingFlags Field = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Height of a bone of the visible third-person body above the player's capsule base.</summary>
        static float BodyHeight(PlayerHealth player, HumanBodyBones bone)
        {
            var body = player.GetComponent<WeaponManager>().CharacterAnimation;
            Transform t = body != null && body.isHuman ? body.GetBoneTransform(bone) : null;
            return t != null ? t.position.y - player.transform.position.y : float.NaN;
        }

        void Vulnerable(PlayerHealth player) => typeof(PlayerHealth).GetField("campaignProtectionUntil", Field).SetValue(player, 0.0);

        static void Hit(PlayerHealth victim, float amount, HitboxZone zone, Vector3 fromLocal) =>
            victim.TakeDamage(new DamageInfo(amount, ulong.MaxValue, -1, victim.transform.position + Vector3.up,
                damageType: DamageType.Melee, hitZone: zone, sourcePosition: victim.transform.TransformPoint(fromLocal)));

        /// <summary>Third-person review shot of <paramref name="subject"/> from a temporary camera.</summary>
        IEnumerator ShotOf(Transform subject, Vector3 offsetLocal, string file)
        {
            var cameraObject = new GameObject("Review camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            // A spectator's view: the host's own first-person arms and weapon are not in the world.
            foreach (string layer in new[] { "FirstPerson", "Weapon" })
                if (LayerMask.NameToLayer(layer) >= 0) camera.cullingMask &= ~(1 << LayerMask.NameToLayer(layer));
            var rt = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = rt;
            // Frame the body, not the capsule: a Death clip can carry the body a few metres away.
            var body = subject.GetComponent<WeaponManager>()?.CharacterAnimation;
            Transform hips = body != null && body.isHuman ? body.GetBoneTransform(HumanBodyBones.Hips) : null;
            Vector3 focus = hips != null ? hips.position : subject.position + Vector3.up * 0.35f;
            camera.transform.position = focus + subject.rotation * offsetLocal;
            camera.transform.LookAt(focus);
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Evidence + file, texture.EncodeToPNG());
            Object.Destroy(texture);
            camera.targetTexture = null;
            rt.Release();
            Object.Destroy(cameraObject);
            Clock("screenshot " + file);
        }

        /// <summary>What the local (host) player sees, HUD included.</summary>
        IEnumerator GameViewShot(string file)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Evidence + file, texture.EncodeToPNG());
            Object.Destroy(texture);
            Clock("screenshot " + file);
        }

        /// <summary>Third-person shot looking at a world point (used while standing, when the body has no humanoid binding).</summary>
        IEnumerator ShotAt(Vector3 focus, Quaternion frame, Vector3 offsetLocal, string file)
        {
            var cameraObject = new GameObject("Review camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.05f;
            foreach (string layer in new[] { "FirstPerson", "Weapon" })
                if (LayerMask.NameToLayer(layer) >= 0) camera.cullingMask &= ~(1 << LayerMask.NameToLayer(layer));
            var rt = new RenderTexture(1280, 720, 24);
            camera.targetTexture = rt;
            camera.transform.position = focus + frame * offsetLocal;
            camera.transform.LookAt(focus);
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Evidence + file, texture.EncodeToPNG());
            Object.Destroy(texture);
            camera.targetTexture = null;
            rt.Release();
            Object.Destroy(cameraObject);
            Clock("screenshot " + file);
        }

        /// <summary>
        /// Reference for the downed arms: how the remote client holds the Classic in third person while standing,
        /// exactly as other players see it. Screenshots plus the local rotation of every arm, finger and
        /// shoulder-helper bone, written to standing-classic-hold.json.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureStandingSidearmHold()
        {
            var clientWeapons = clientPlayer.GetComponent<WeaponManager>();
            int sidearm = clientWeapons.FindDownedWeaponSlot();
            clientWeapons.SetEquippedWeaponServer(sidearm);
            yield return WaitFor(() => clientWeapons.CurrentWeaponIndex == sidearm, 5, "the client to draw the Classic");
            yield return Pause(2.5f); // Equip animation finished, idle hold.
            var animator = clientWeapons.CharacterAnimation;
            var presenter = clientPlayer.GetComponent<PlayerLifePresenter>();
            var avatar = (Avatar)typeof(PlayerLifePresenter).GetField("humanoidAvatar", Field).GetValue(presenter);
            var names = avatar.humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            Transform Find(string human) => names.TryGetValue(human, out var bone)
                ? animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == bone) : null;
            Transform chest = Find("UpperChest") ?? Find("Chest");
            Clock($"standing hold: controller={animator.runtimeAnimatorController?.name} avatar={(animator.avatar != null ? animator.avatar.name : "none")} chest={chest?.name}");

            // Every bone of both arms (shoulder down to the finger tips), plus helpers hanging off the shoulders.
            var lines = new List<string>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform shoulder = Find(side + "Shoulder") ?? Find(side + "UpperArm");
                if (shoulder == null) continue;
                foreach (var t in shoulder.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponentInParent<Animator>(true) != animator) continue; // Not the gun model.
                    var q = t.localRotation;
                    string human = names.FirstOrDefault(n => n.Value == t.name).Key ?? "";
                    lines.Add($"{{\"bone\":\"{t.name}\",\"human\":\"{human}\",\"parent\":\"{t.parent.name}\",\"q\":[{q.x:R},{q.y:R},{q.z:R},{q.w:R}]}}");
                }
            }
            File.WriteAllText(Evidence + "standing-classic-hold.json", "[\n" + string.Join(",\n", lines) + "\n]\n");
            Clock($"standing hold: {lines.Count} arm/finger/helper bones recorded");

            Vector3 focus = chest != null ? chest.position : clientPlayer.transform.position + Vector3.up * 1.3f;
            Quaternion frame = Quaternion.Euler(0f, clientPlayer.transform.eulerAngles.y, 0f);
            yield return ShotAt(focus, frame, new Vector3(0.3f, 0.15f, 2.2f), "3P-standing-classic-front.png");
            yield return ShotAt(focus, frame, new Vector3(2.2f, 0.1f, 0.4f), "3P-standing-classic-right.png");
            yield return ShotAt(focus, frame, new Vector3(-1.6f, 0.5f, 1.6f), "3P-standing-classic-threequarter.png");
            yield return ShotAt(focus + frame * new Vector3(0f, -0.1f, 0.35f), frame, new Vector3(0.5f, 0.25f, 0.9f), "3P-standing-classic-hands.png");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator DownedSidearmReviveAndDirectionalDeathAcrossPeers()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlayerLifeAnimationSet>("Assets/FPS/Features/Characters/Animation/Content/3P/PlayerLifeAnimationSet.asset");
            var clientWeapons = clientPlayer.GetComponent<WeaponManager>();
            var clientView = clientPlayer.GetComponent<PlayerLifePresenter>(); // The host's 3P view of the client.
            var hostView = hostPlayer.GetComponent<PlayerLifePresenter>();
            Assert.That(clientView, Is.Not.Null, "Player prefab carries PlayerLifePresenter");
            int sidearm = clientWeapons.FindDownedWeaponSlot();
            Assert.That(sidearm, Is.GreaterThanOrEqualTo(0), "Loadout has a sidearm usable while downed");
            int heldBefore = clientWeapons.CurrentWeaponIndex;
            int primary = sidearm == 0 ? 1 : 0;
            Clock($"client holds slot {heldBefore} ({clientWeapons.GetWeapon(heldBefore)?.Data?.weaponName}), sidearm slot {sidearm}");

            // 1. Down the client: two players stand, so it goes Downed, not Dead.
            Vulnerable(clientPlayer);
            Hit(clientPlayer, clientPlayer.CurrentHealth + 5f, HitboxZone.Leg, Vector3.forward * 2f);
            yield return WaitFor(() => clientPlayer.LifeState == PlayerLifeState.Downed, 5, "the server to down the client");
            yield return WaitFor(() => clientWeapons.CurrentWeaponIndex == sidearm, 5, "the server to draw the sidearm");
            Assert.That(clientPlayer.CanUseWeapon(clientWeapons.GetWeapon(sidearm).Data), Is.True, "Downed: sidearm usable");
            Assert.That(clientPlayer.CanUseWeapon(clientWeapons.GetWeapon(primary).Data), Is.False, "Downed: primary refused");
            yield return WaitFor(() => { var s = Read(); return s?.lifeState == (int)PlayerLifeState.Downed && s.heldWeapon == "Classic"; }, 10, "the client to see itself downed with the Classic");
            yield return WaitFor(() => Read()?.eyeDrop > 0.9f, 5, "the client's camera to sink to the lying head");
            var seen = Read();
            Clock($"client own view: life={seen.lifeState} held={seen.heldWeapon} sidearmUsable={seen.sidearmUsable} primaryUsable={seen.primaryUsable} eyeDrop={seen.eyeDrop:0.00}");
            Assert.That(seen.sidearmUsable && !seen.primaryUsable, Is.True, "The client applies the same rule to its own input");
            yield return WaitFor(() => clientView.IsOverridingCharacter && clientView.RaiseWeight >= 1f, 6, "the host to show the client downed and propped up");
            Clock($"host view of client: clip={clientView.ActiveClipName} raise={clientView.RaiseWeight:0.00} arms={clientView.ArmsWeight:0.00}");
            Assert.That(clientView.ActiveClipName, Is.EqualTo(set.downedFall.name));
            float downedHead = BodyHeight(clientPlayer, HumanBodyBones.Head), downedHips = BodyHeight(clientPlayer, HumanBodyBones.Hips);
            Clock($"host view of client body: head {downedHead:0.00} m, hips {downedHips:0.00} m above the capsule base");
            Assert.That(downedHips, Is.LessThan(0.45f), "The visible body is on the ground");
            Assert.That(downedHead, Is.InRange(0.25f, 1.1f), "Shoulders raised off the ground, not standing");
            float footL = BodyHeight(clientPlayer, HumanBodyBones.LeftFoot), footR = BodyHeight(clientPlayer, HumanBodyBones.RightFoot);
            float kneeR = BodyHeight(clientPlayer, HumanBodyBones.RightLowerLeg);
            Clock($"host view of client legs: feet L {footL:0.00} R {footR:0.00} m, right knee {kneeR:0.00} m");
            {
                var anim = clientPlayer.GetComponent<WeaponManager>().CharacterAnimation;
                float handGap = Vector3.Distance(anim.GetBoneTransform(HumanBodyBones.LeftHand).position, anim.GetBoneTransform(HumanBodyBones.RightHand).position);
                Clock($"host view of client hands: {handGap:0.00} m apart");
                Assert.That(handGap, Is.LessThan(0.2f), "Both hands hold the sidearm");
                var defaults = (Dictionary<Transform, Quaternion>)typeof(PlayerLifePresenter).GetField("bodyDefaults", Private).GetValue(clientView);
                foreach (var leg in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                {
                    var knee = anim.GetBoneTransform(leg);
                    var corrective = knee.Find(knee.name + "_Ndl");
                    Assert.That(corrective, Is.Not.Null);
                    float error = Quaternion.Angle(knee.localRotation * corrective.localRotation,
                        Quaternion.Slerp(defaults[knee], knee.localRotation, 0.5f) * defaults[corrective]);
                    Clock($"knee corrective {knee.name}: half-angle orientation error={error:F3} deg");
                    Assert.That(error, Is.LessThan(0.1f), "The corrective bisects the knee bend");
                }
                var skin = anim.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.sharedMesh.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape) >= 0);
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape)), Is.EqualTo(100f).Within(0.1f));
            }
            Clock($"host view of client helper bones: {clientView.HelperBoneCount} collected, {clientView.HelperBonesBentByAnimator} bent by the weapon controller this frame (reset to rest)");
            Assert.That(clientView.HelperBoneCount, Is.GreaterThan(0), "Helper bones are managed while lying");
            Assert.That(footL < 0.35f && footR < 0.45f && footL > -0.1f && footR > -0.1f, Is.True, "Both feet rest on the ground");
            Assert.That(kneeR, Is.GreaterThan(footR + 0.15f), "Right knee drawn up");
            {
                var anim = clientPlayer.GetComponent<WeaponManager>().CharacterAnimation;
                var head = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                Clock($"diag: charAnim={anim.name} active={anim.isActiveAndEnabled} human={anim.isHuman} avatar={(anim.avatar != null ? anim.avatar.name : "null")} " +
                      $"controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")} culling={anim.cullingMode} " +
                      $"headY={(head != null ? head.position.y - clientPlayer.transform.position.y : -1f):0.00}");
                {
                    Vector3 facing = Vector3.ProjectOnPlane(clientPlayer.transform.forward, Vector3.up);
                    Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
                    Vector3 feet = Vector3.ProjectOnPlane((B(HumanBodyBones.LeftFoot).position + B(HumanBodyBones.RightFoot).position) * 0.5f - B(HumanBodyBones.Hips).position, Vector3.up);
                    Vector3 forearm = B(HumanBodyBones.RightHand).position - B(HumanBodyBones.RightLowerArm).position;
                    Vector3 upper = B(HumanBodyBones.RightLowerArm).position - B(HumanBodyBones.RightUpperArm).position;
                    Clock($"diag: feet vs facing {Vector3.SignedAngle(feet, facing, Vector3.up):0} deg, right forearm vs facing {Vector3.Angle(forearm, facing):0} deg (y {forearm.normalized.y:0.00}), upper arm {Vector3.Angle(upper, facing):0} deg, hand height {B(HumanBodyBones.RightHand).position.y - clientPlayer.transform.position.y:0.00}");
                    Assert.That(Mathf.Abs(Vector3.SignedAngle(feet, facing, Vector3.up)), Is.LessThan(25f), "Lies with the feet towards where the player looks");
                    Vector3 gunLine = (B(HumanBodyBones.LeftHand).position + B(HumanBodyBones.RightHand).position) * 0.5f
                        - (B(HumanBodyBones.LeftUpperArm).position + B(HumanBodyBones.RightUpperArm).position) * 0.5f;
                    Clock($"diag: gun line (shoulders to hands) vs facing {Vector3.Angle(Vector3.ProjectOnPlane(gunLine, Vector3.up), facing):0} deg horizontal, elevation {Mathf.Asin(gunLine.normalized.y) * Mathf.Rad2Deg:0} deg");
                    var vis = clientPlayer.GetComponent<PlayerVisibilityController>();
                    foreach (var presentation in vis.ThirdPersonWeaponPresentations)
                    {
                        var w = presentation?.WeaponObject;
                        if (w == null || !w.activeInHierarchy) continue;
                        float gap = Vector3.Distance(w.transform.position, B(HumanBodyBones.RightHand).position);
                        Clock($"diag: 3P weapon {w.name} hand gap {gap:0.00} m");
                        Assert.That(gap, Is.LessThan(0.4f), "The 3P weapon stays in the right hand");
                        var muzzle = w.GetComponentsInChildren<Transform>(true).First(t => t.name == "MuzzlePoint");
                        float barrelError = Vector3.Angle(-muzzle.right, facing + Vector3.up * 0.12f);
                        Clock($"actual barrel vs aim={barrelError:F3} deg");
                        Assert.That(barrelError, Is.LessThan(0.5f), "The actual Classic barrel follows aim");
                        Clock($"diag: 3P weapon {w.name} parent={w.transform.parent?.name} rigMode={presentation.CharacterRigMode} children={string.Join(",", w.GetComponentsInChildren<Transform>(true).Select(t => t.name).Where(n => n.ToLower().Contains("muzzle") || n.ToLower().Contains("grip") || n.ToLower().Contains("hand") || n.ToLower().Contains("root")).Take(8))}");
                    }
                }
                foreach (var other in clientPlayer.GetComponentsInChildren<Animator>(true))
                    Clock($"diag: animator {other.name} active={other.isActiveAndEnabled} human={other.isHuman} ctrl={(other.runtimeAnimatorController != null ? other.runtimeAnimatorController.name : "null")}");
                foreach (var smr in clientPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy).Take(6))
                    Clock($"diag: visible mesh {smr.name} rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")} under charAnim={smr.transform.IsChildOf(anim.transform)} bonesUnderAnim={(smr.bones.Length > 0 && smr.bones[0] != null && smr.bones[0].IsChildOf(anim.transform))}");
                foreach (var behaviour in clientPlayer.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null && b.GetType().Name.Contains("IK") || b != null && b.GetType().Name.Contains("Aim") || b != null && b.GetType().Name.Contains("Rig")))
                    Clock($"diag: behaviour {behaviour.GetType().Name} on {behaviour.name} enabled={behaviour.enabled}");
            }
            yield return ShotOf(clientPlayer.transform, new Vector3(1.6f, 1.4f, 2.6f), "3P-downed-front.png");
            yield return ShotOf(clientPlayer.transform, new Vector3(-2.4f, 1.6f, -0.6f), "3P-downed-side.png");
            yield return ShotOf(clientPlayer.transform, new Vector3(-0.9f, 0.7f, 0.9f), "3P-downed-closeup.png");

            // 2. Revive: back on the feet with the weapon that was in hand.
            clientPlayer.ReviveCampaign(50f);
            yield return WaitFor(() => !clientView.IsOverridingCharacter, 5, "the host to blend the client back to its Animator");
            yield return WaitFor(() => clientWeapons.CurrentWeaponIndex == heldBefore, 5, "the server to give back the weapon held before");
            yield return WaitFor(() => { var s = Read(); return s?.lifeState == (int)PlayerLifeState.Alive && s.eyeDrop < 0.05f && s.primaryUsable; }, 10, "the client camera to rise and the primary to come back");
            Clock($"revived: host overriding={clientView.IsOverridingCharacter} slot={clientWeapons.CurrentWeaponIndex} client eyeDrop={Read()?.eyeDrop:0.00}");
            foreach (var skin in clientWeapons.CharacterAnimation.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int shape = skin.sharedMesh.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape);
                if (shape >= 0) Assert.That(skin.GetBlendShapeWeight(shape), Is.Zero, "Revive clears the knee correction");
            }

            // 3. Down the host, then kill the client from behind with a heavy head hit: nobody else stands,
            //    so the client dies outright with the Valorant-style Death chosen from that hit.
            Vulnerable(hostPlayer);
            Hit(hostPlayer, hostPlayer.CurrentHealth + 5f, HitboxZone.Body, Vector3.left * 2f);
            yield return WaitFor(() => hostPlayer.LifeState == PlayerLifeState.Downed, 5, "the host to go down");
            yield return WaitFor(() => { var s = Read(); return s?.otherLifeState == (int)PlayerLifeState.Downed && s.otherRaise > 0.95f; }, 10, "the client to show the host downed and propped up");
            var hostSeen = Read();
            Clock($"client view of host: life={hostSeen?.otherLifeState} clip={hostSeen?.otherLifeClip} raise={hostSeen?.otherRaise:0.00}");
            Assert.That(hostView.IsOverridingCharacter, Is.False, "The host never animates its own hidden body");
            yield return Pause(1f);
            yield return GameViewShot("1P-downed-host-view.png");

            Vulnerable(clientPlayer);
            Hit(clientPlayer, clientPlayer.CurrentHealth + 40f, HitboxZone.Head, Vector3.back * 3f);
            yield return WaitFor(() => clientPlayer.LifeState == PlayerLifeState.Dead || clientPlayer.LifeState == PlayerLifeState.Spectating, 5, "the client to die outright");
            var hit = clientPlayer.LifeHit;
            string expected = set.ChooseDeath(hit).name;
            yield return WaitFor(() => clientView.IsOverridingCharacter && clientView.ActiveClipName == expected, 5, "the host to play the chosen Death clip");
            Clock($"death: hit={hit.region}/{hit.side}/{(hit.heavy ? "LG" : "SM")}/v{hit.variant} clip={clientView.ActiveClipName}");
            Assert.That((hit.region, hit.side, hit.heavy), Is.EqualTo((PlayerHitRegion.Head, PlayerHitSide.Back, true)));
            Assert.That(expected, Does.Contain("_Head_Back_LG"));
            yield return Pause(set.ChooseDeath(hit).length + 0.3f);
            float deadHead = BodyHeight(clientPlayer, HumanBodyBones.Head);
            Clock($"host view of dead client: head {deadHead:0.00} m");
            Assert.That(deadHead, Is.LessThan(0.5f), "The dead body lies on the ground");
            yield return ShotOf(clientPlayer.transform, new Vector3(2.2f, 1.6f, 1.2f), "3P-dead-head-back.png");
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
