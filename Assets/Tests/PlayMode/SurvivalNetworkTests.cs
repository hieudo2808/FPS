#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>Real NGO host/client transport in one process. Client physics copies are excluded.</summary>
    public sealed partial class SurvivalNetworkTests
    {
        const string Content = "Assets/FPS/Features/Survival/Content/Prefabs/";
        const string PlayerPrefab = "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab";
        static readonly string[] FeedbackEnemies = {
            "Assets/FPS/Features/Characters/Content/Enemies/ZombieRig/Prefabs/ZombieRig.prefab",
            "Assets/FPS/Features/Characters/Content/Enemies/Infector/Prefabs/Infector.prefab",
            "Assets/FPS/Features/Characters/Content/Enemies/Tanker/Prefabs/Tank.prefab" };
        static readonly string[] PlayerNames = { "Clove", "Brimstone", "Gekko", "Sage" };
        static string PlayerPath(string character) => $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        NetworkManager host, client, observer;
        NetworkSimulator recoilSimulator;
        static readonly int[] RecoilNetworkDelays = { 0, 50, 100 };
        static readonly string[] RecoilProfileNames = { "Vandal", "Classic", "Odin", "Operator", "Bucky" };
        static readonly int[] RecoilFrameRates = { 30, 60, 144 };
        PlayerHealth actor, teammate;
        SurvivalInventory serverInventory, clientInventory;
        UnityEngine.AI.NavMeshDataInstance feedbackNavigation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("SurvivalRuntime");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            yield return null;
            host = CreateManager("Survival test host");
            client = CreateManager("Survival test client");
            host.SetSingleton();
            if (TestContext.CurrentContext.Test.MethodName != nameof(WardenRecoilPredictsAndReconcilesAcrossRealOwnerRpc)
                && TestContext.CurrentContext.Test.MethodName != nameof(ProfileRecoilPredictsAndReconcilesAcrossRealOwnerRpc))
                yield return StartNetwork();
        }

        IEnumerator StartNetwork(int delay = -1)
        {
            if (delay >= 0)
            {
                recoilSimulator = new GameObject("Warden network conditions").AddComponent<NetworkSimulator>();
                recoilSimulator.ConnectionPreset = NetworkSimulatorPreset.Create("Warden " + delay,
                    packetDelayMs: delay, packetJitterMs: delay == 50 ? 10 : delay == 100 ? 20 : 0,
                    packetLossPercent: delay == 100 ? 2 : 0);
            }
            Assert.That(host.StartHost(), Is.True);
            Assert.That(client.StartClient(), Is.True);
            if (recoilSimulator != null)
            {
                // Configure latency before the handshake so the client's initial RTT is representative.
                var hostDriver = host.GetComponent<UnityTransport>().GetNetworkDriver();
                Assert.That(hostDriver.CurrentSettings.TryGet<SimulatorUtility.Parameters>(out var simulation), Is.True);
                simulation.PacketDelayMs = simulation.PacketJitterMs = simulation.PacketDropInterval = 0;
                simulation.PacketDropPercentage = 0;
                hostDriver.ModifySimulatorStageParameters(simulation);
            }
            yield return Until(() => client.IsConnectedClient && host.ConnectedClients.Count == 2, "client connection");
            yield return Until(() => client.LocalClient.PlayerObject != null, "client player spawn");
            actor = host.ConnectedClients[client.LocalClientId].PlayerObject.GetComponent<PlayerHealth>();
            teammate = host.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            serverInventory = actor.GetComponent<SurvivalInventory>();
            clientInventory = client.LocalClient.PlayerObject.GetComponent<SurvivalInventory>();
            actor.transform.position = Vector3.zero;
            teammate.transform.position = new Vector3(2, 0, 0);
            DisableClientColliders();
            Physics.SyncTransforms();
        }

        NetworkManager CreateManager(string name)
        {
            var go = new GameObject(name);
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 17841, "127.0.0.1");
            manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tests/Fixtures/SurvivalProbe.prefab");
            manager.NetworkConfig.Prefabs = new NetworkPrefabs();
            foreach (var path in new[] { "Assets/Tests/Fixtures/SurvivalProbe.prefab", "Assets/Tests/Fixtures/SurvivalEnemyProbe.prefab",
                Content + "GrenadeProjectile.prefab", Content + "FireZone.prefab", Content + "MedkitPickup.prefab" }.Concat(PlayerNames.Select(PlayerPath)).Concat(FeedbackEnemies))
                manager.AddNetworkPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            return manager;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (feedbackNavigation.valid) feedbackNavigation.Remove();
            if (recoilSimulator != null)
            {
                recoilSimulator.ConnectionPreset = NetworkSimulatorPresets.None;
                Object.Destroy(recoilSimulator.gameObject);
            }
            if (observer != null) observer.Shutdown();
            if (client != null) client.Shutdown();
            if (host != null) host.Shutdown();
            yield return null;
            if (observer != null) Object.Destroy(observer.gameObject);
            observer = null;
            if (client != null) Object.Destroy(client.gameObject);
            if (host != null) Object.Destroy(host.gameObject);
            yield return null;
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("SurvivalRuntime");
            if (scene.IsValid()) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }

        IEnumerator Until(Func<bool> condition, string label, float timeout = 8)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            { DisableClientColliders(); yield return null; }
            Assert.That(condition(), Is.True, label);
        }

        void DisableClientColliders()
        {
            DisableFor(client);
            DisableFor(observer);
            void DisableFor(NetworkManager peer)
            {
                if (peer == null || peer.SpawnManager == null) return;
                foreach (var obj in peer.SpawnManager.SpawnedObjectsList)
                    foreach (var collider in obj.GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
        }

        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        void FinishSoon() => Field<NetworkVariable<double>>(serverInventory, "useDeadline").Value = serverInventory.ServerNow;

        [UnityTest]
        public IEnumerator WardenCameraHandlesDelayedKickCompensationAndClamp()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            var look = player.GetComponent<MouseMovement>();
            look.enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            var manager = player.GetComponent<WeaponManager>();
            manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Warden);
            manager.SetEquippedWeaponServer(0);
            yield return Until(() => !player.GetComponent<WeaponFireHandler>().IsServerEquipping, "camera fixture equip");
            var weapon = manager.GetWeapon(0);
            weapon.enabled = false;
            var data = Object.Instantiate(weapon.Data);
            var profile = Object.Instantiate(data.recoilPattern);
            data.recoilPattern = profile;
            profile.snappiness = 10000;
            SetField(weapon, "weaponData", data);
            var recoil = player.GetComponent<RecoilController>();
            recoil.enabled = false;
            try
            {
                var state = new WeaponStability();
                state.Fire(profile, weapon.StabilityTime - .2, data.FireInterval,
                    new WeaponStabilityContext { grounded = true }, 1);
                weapon.ReconcileStability(new WeaponOwnerState { stability = state, hasResolvedFireSequence = true });
                recoil.Bind(weapon, false);
                Invoke(recoil, "Update");
                Assert.That(-look.XRotation, Is.GreaterThan(.1f), "200 ms late first frame must retain its remaining kick");
                recoil.Release(weapon);
                Assert.That(look.XRotation, Is.EqualTo(0).Within(.0001));

                CheckDelta(.3f, .1f, .05f, -.05f);
                CheckDelta(.3f, .1f, 0f, 0f);
                CheckDelta(.4f, -.6f, .1f, -.7f);
                CheckDelta(-.4f, .6f, -.1f, .7f);
                CheckDelta(.4f, 0f, .1f, -.1f); // Rejected prediction removes only uncompensated kick.

                SetField(look, "xRotation", -89.95f);
                SetField(recoil, "currentPattern", profile);
                SetField(recoil, "targetRecoil", new Vector2(.6f, 0));
                Invoke(recoil, "Update");
                Assert.That(Field<Vector2>(recoil, "recoverableRecoil").x, Is.EqualTo(.05f).Within(.0001));
                SetField(recoil, "targetRecoil", Vector2.zero);
                Invoke(recoil, "Update");
                Assert.That(look.XRotation, Is.EqualTo(-89.95f).Within(.0001), "clamped kick cannot return un-applied degrees");

                void CheckDelta(float before, float after, float debt, float expected)
                {
                    SetField(look, "xRotation", -debt);
                    SetField(recoil, "currentPattern", profile);
                    SetField(recoil, "currentRecoil", new Vector2(before, 0));
                    SetField(recoil, "targetRecoil", new Vector2(after, 0));
                    SetField(recoil, "recoverableRecoil", new Vector2(debt, 0));
                    SetField(recoil, "timeSinceLastShot", 0f);
                    Invoke(recoil, "Update");
                    Assert.That(look.XRotation, Is.EqualTo(-(debt + expected)).Within(.0001), $"model {before}->{after}, debt={debt}");
                    Invoke(recoil, "Clear");
                }
            }
            finally
            {
                Invoke(recoil, "Clear");
                player.GetComponent<NetworkObject>().Despawn();
                Object.Destroy(profile);
                Object.Destroy(data);
            }
        }

        [UnityTest]
        public IEnumerator JumpFireUsesPostMoveContextAndLandingRestoresGrounding()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(100, -.5f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")), new Vector3(100, 5, 0), Quaternion.identity);
            var movement = player.GetComponent<PlayerMovement>();
            movement.enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            var controller = player.GetComponent<CharacterController>();
            try
            {
                Physics.SyncTransforms();
                controller.Move(Vector3.down * 10);
                Assert.That(controller.isGrounded, Is.True, "fixture must contact floor before jumping");
                var input = new PlayerInputPayload { sequence = 17, tick = 100, jumpPressed = true,
                    yaw = 32, pitch = -7, move = new Vector2(.5f, 0) };
                Invoke(movement, "SimulateTick", input, 1f / 60);
                Assert.That(movement.StabilityContext.grounded, Is.False, "jump-start tick must be airborne after Move");
                var profile = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Warden/Warden.asset").recoilPattern;
                Assert.That(profile.GetSpread(0, movement.StabilityContext), Is.EqualTo(10).Within(.0001));
                Invoke(movement, "RecordServerAim", input, 100);
                Field<NetworkTimer>(movement, "networkTimer").CurrentTick = 101;
                Field<PlayerInputPayload[]>(movement, "inputBuffer")[100] = input;
                Field<PlayerStatePayload[]>(movement, "stateBuffer")[100] =
                    (PlayerStatePayload)typeof(PlayerMovement).GetMethod("CaptureState", Private).Invoke(movement, new object[] { 100 });
                SetField(movement, "hasTickedOnce", true);
                SetField(movement, "aimHeld", true); // Live state must not replace the chosen historical sample.
                Assert.That(movement.TryGetPredictedFireAim(17, out var ownerAim, out var ownerAds, out var ownerContext), Is.True);
                Assert.That(movement.TryBuildServerAim(100, 17, out _, out var serverAim, out var serverAds, out var serverContext), Is.True);
                Assert.That(ownerAds || serverAds || ownerContext.aimed || serverContext.aimed, Is.False);
                Assert.That(ownerContext.grounded || serverContext.grounded, Is.False);
                Assert.That(ownerContext.normalizedSpeed, Is.EqualTo(serverContext.normalizedSpeed).Within(.0001));
                Assert.That(Vector3.Distance(ownerAim, serverAim), Is.LessThan(.0001));

                float bottomOffset = controller.bounds.min.y - player.transform.position.y;
                controller.enabled = false;
                player.transform.position = new Vector3(100, .05f - bottomOffset, 0);
                controller.enabled = true;
                SetField(movement, "verticalVelocity", -10f);
                Physics.SyncTransforms();
                Invoke(movement, "SimulateTick", new PlayerInputPayload(), .02f);
                Assert.That(controller.isGrounded, Is.True, "fixture lands in this Move");
                Assert.That(movement.StabilityContext.grounded, Is.True, "landing must be captured on the same tick");

                // Replay can re-simulate an older sprint command after infection disables sprinting.
                var infection = player.GetComponent<PlayerInfectionController>();
                infection.SetInfectionServer(100);
                Assert.That(infection.CanSprint, Is.False);
                input.sprint = true;
                input.jumpPressed = false;
                Field<PlayerInputPayload[]>(movement, "inputBuffer")[100] = input;
                Invoke(movement, "ReplayFrom", 100);
                Invoke(movement, "RecordServerAim", input, 100);
                infection.SetInfectionServer(0); // Reading history must not substitute the live permission.
                Assert.That(movement.TryGetPredictedFireAim(17, out _, out _, out ownerContext), Is.True);
                Assert.That(movement.TryBuildServerAim(100, 17, out _, out _, out _, out serverContext), Is.True);
                Assert.That(serverContext.sprinting, Is.False, "server simulated the sprint restriction");
                Assert.That(ownerContext.sprinting, Is.False, "owner history must retain the effective replayed sprint context");
            }
            finally
            {
                player.GetComponent<NetworkObject>().Despawn();
                Object.Destroy(floor);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WardenReconnectRestoresYawTransitionAcrossARealDisconnect()
            => ReconnectRecoil("Warden");

        [UnityTest]
        public IEnumerator ProfileReconnectRestoresRecoilAcrossARealDisconnect(
            [Values("Vandal", "Odin", "Operator", "Bucky")] string name)
            => ReconnectRecoil(name);

        IEnumerator ReconnectRecoil(string name)
        {
            var source = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/FPS/Features/Weapons/Content/{name}/{name}.asset");
            var profile = Object.Instantiate(source.recoilPattern);
            bool transition = profile.yawSwitchChance > 0;
            if (transition) profile.yawSwitchChance = 1;
            ushort shotCount = (ushort)(transition ? profile.protectedShots + 1 : 1);
            var primary = (PrimaryWeaponId)Enum.Parse(typeof(PrimaryWeaponId), name);
            var data = Object.Instantiate(source);
            data.recoilPattern = profile;
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            var serverState = new WeaponServerState();
            serverState.EnsureInitialized(1, data);
            yield return Until(() => host.ServerTime.Time > shotCount * source.FireInterval, "reconnect clock warmup");
            double transitionStart = host.ServerTime.Time;
            for (ushort shot = 1; shot <= shotCount; shot++)
                Assert.That(serverState.TryConsumeFire(data, transitionStart - (shotCount - shot) * (double)data.FireInterval,
                    shot, true, new WeaponStabilityContext { grounded = true }, shot), Is.True);
            serverState.RecordFireResult(shotCount);
            var snapshot = actor.CaptureRuntimeSnapshot();
            snapshot.primaryWeaponId = primary;
            snapshot.equippedWeaponSlot = 0;
            snapshot.weaponSlot0 = serverState.Capture(0, source);
            snapshot.revision = 42;
            var expected = snapshot.weaponSlot0.stability;
            Assert.That(expected.acceptedShots, Is.EqualTo(shotCount));
            if (transition) Assert.That(expected.yawFromSign, Is.Not.EqualTo(expected.yawToSign));
            host.DisconnectClient(client.LocalClientId);
            client.Shutdown(discardMessageQueue: true);
            yield return Until(() => host.ConnectedClients.Count == 1, "real client disconnect");
            Object.Destroy(client.gameObject);
            yield return null;
            client = CreateManager("reconnected recoil client");
            Assert.That(client.StartClient(), Is.True);
            yield return Until(() => client.IsConnectedClient && host.ConnectedClients.Count == 2, "new peer reconnect");
            player.GetComponent<PlayerHealth>().PrepareReconnect(snapshot);
            player.GetComponent<NetworkObject>().SpawnWithOwnership(client.LocalClientId);
            ulong id = player.GetComponent<NetworkObject>().NetworkObjectId;
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(id), "restored owner replica");
            var owner = client.SpawnManager.SpawnedObjects[id];
            owner.GetComponent<PlayerMovement>().enabled = false;
            owner.GetComponent<MouseMovement>().enabled = false;
            var manager = owner.GetComponent<WeaponManager>();
            yield return Until(() => manager.ActivePrimaryWeaponId == primary
                && manager.GetWeapon(0).PredictedStability.Equals(expected)
                && owner.GetComponent<PlayerHealth>().IsInputReady, "snapshot and restore acknowledgement");
            double restoreElapsed = host.ServerTime.Time - transitionStart;
            var weapon = manager.GetWeapon(0);
            System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/network-clock.txt",
                $"reconnect host={host.ServerTime.Time:F4} client={client.ServerTime.Time:F4} weapon={weapon.StabilityTime:F4} bound={Field<WeaponFireHandler>(weapon, "cachedFireHandler") != null}\n");
            yield return Until(() => weapon.StabilityTime > expected.yawSwitchStartTime + .001,
                "buffered owner clock reaches the transition start");
            weapon.enabled = false;
            Assert.That(player.GetComponent<WeaponFireHandler>().CaptureWeaponSnapshot(0).stability.Equals(expected), Is.True);
            Assert.That(Field<int>(weapon, "pendingServerShots"), Is.Zero);
            if (transition)
            {
                // Reconnecting can outlast the transition; its original absolute phase must survive either way.
                double restoredTime = weapon.StabilityTime;
                float phase = Mathf.Clamp01((float)((restoredTime - expected.yawSwitchStartTime) / profile.yawSwitchDuration));
                float direction = Mathf.Lerp(expected.yawFromSign, expected.yawToSign, phase);
                Assert.That(weapon.PredictedStability.YawDirectionAt(source.recoilPattern, restoredTime),
                    Is.EqualTo(direction).Within(.0001), "reconnect must continue or finish the original transition, never restart it");
            }
            foreach (double offset in new[] { 0.0, .15, .3, .45, .6 })
            {
                double time = expected.yawSwitchStartTime + offset;
                Assert.That(weapon.PredictedStability.YawDirectionAt(source.recoilPattern, time),
                    Is.EqualTo(expected.YawDirectionAt(profile, time)).Within(.0001));
                Assert.That(weapon.PredictedStability.TargetAt(source.recoilPattern, time, data.FireInterval),
                    Is.EqualTo(expected.TargetAt(profile, time, data.FireInterval)));
            }
            var recoil = owner.GetComponent<RecoilController>();
            recoil.Bind(weapon, false);
            yield return null;
            yield return Until(() => Mathf.Abs(owner.GetComponent<MouseMovement>().XRotation) < .01f,
                "reconnected camera settles", 2);
            recoil.Release(weapon);
            Assert.That(owner.GetComponent<MouseMovement>().XRotation, Is.EqualTo(0).Within(.01));
            System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/reconnect.txt",
                $"profile={name}; restore elapsed={restoreElapsed:F4}s; settled elapsed={host.ServerTime.Time - transitionStart:F4}s; transitionStart={expected.yawSwitchStartTime:F6}; state/ACK matched\n");
            Object.Destroy(profile);
            Object.Destroy(data);
        }

        [UnityTest]
        public IEnumerator WardenCameraCadenceAndLifecycleStayClean([ValueSource(nameof(RecoilFrameRates))] int fps)
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            var look = player.GetComponent<MouseMovement>();
            look.enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            var manager = player.GetComponent<WeaponManager>();
            manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Warden);
            manager.SetEquippedWeaponServer(0);
            yield return Until(() => !player.GetComponent<WeaponFireHandler>().IsServerEquipping, "cadence fixture equip");
            var weapon = manager.GetWeapon(0);
            weapon.enabled = false;
            var recoil = player.GetComponent<RecoilController>();
            recoil.enabled = false;
            float previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / fps;
            // Use the presentation clock for deterministic cadence; real RPC timing has its own matrix test.
            SetField(weapon, "cachedFireHandler", null);
            try
            {
                yield return null;
                foreach (bool aimed in new[] { false, true })
                foreach (int shots in new[] { 1, 3, 15 })
                {
                    var state = new WeaponStability();
                    double nextShot = Time.timeAsDouble;
                    int fired = 0;
                    float peak = 0;
                    while (fired < shots)
                    {
                        while (fired < shots && Time.timeAsDouble >= nextShot)
                        {
                            state.Fire(weapon.Data.recoilPattern, nextShot, weapon.Data.FireInterval,
                                new WeaponStabilityContext { grounded = true, aimed = aimed }, (uint)++fired);
                            weapon.ReconcileStability(new WeaponOwnerState { stability = state });
                            recoil.Bind(weapon, aimed);
                            nextShot += weapon.Data.FireInterval;
                        }
                        Invoke(recoil, "Update");
                        peak = Mathf.Max(peak, -look.XRotation);
                        Assert.That(float.IsFinite(look.XRotation) && float.IsFinite(look.YRotation), Is.True);
                        yield return null;
                    }
                    Assert.That(peak, Is.GreaterThan(.01f), $"{fps} FPS aimed={aimed} shots={shots} visible kick");
                    yield return Settle();
                    recoil.Release(weapon);
                }

                Kick();
                var beforeSwitch = weapon.PredictedStability;
                weapon.enabled = true;
                manager.SetEquippedWeaponServer(1);
                AssertClean("weapon switch");
                Assert.That(weapon.PredictedStability.Equals(beforeSwitch), Is.True, "switch preserves the burst model");
                manager.SetEquippedWeaponServer(0);
                weapon.enabled = false;
                SetField(weapon, "cachedFireHandler", null);
                Kick();
                weapon.SetLocalAmmoState(14, 30, true);
                yield return Settle();
                weapon.SetLocalAmmoState(15, 29, false);
                AssertClean("reload");
                Kick();
                Invoke(player.GetComponent<PlayerHealth>(), "Die");
                AssertClean("death");
                player.GetComponent<PlayerHealth>().Respawn(Vector3.zero, Quaternion.identity);
                AssertClean("respawn");
                System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/camera-cadence.txt",
                    $"{fps} FPS cadence: hip/ADS x 1/3/15 shots; switch/reload/death/respawn passed\n");

                void Kick()
                {
                    var state = new WeaponStability();
                    state.Fire(weapon.Data.recoilPattern, Time.timeAsDouble, weapon.Data.FireInterval,
                        new WeaponStabilityContext { grounded = true }, 1);
                    weapon.ReconcileStability(new WeaponOwnerState { stability = state });
                    recoil.Bind(weapon, false);
                    Invoke(recoil, "Update");
                    Assert.That(-look.XRotation, Is.GreaterThan(.01f));
                }
                IEnumerator Settle()
                {
                    for (int frame = 0; frame < 2 * fps; frame++)
                    {
                        yield return null;
                        Invoke(recoil, "Update");
                    }
                    AssertClean("recovery within two seconds");
                }
                void AssertClean(string phase)
                {
                    Assert.That(Mathf.Abs(look.XRotation), Is.LessThan(.01f), phase + " pitch");
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, look.YRotation)), Is.LessThan(.01f), phase + " yaw");
                }
            }
            finally
            {
                Time.captureDeltaTime = previousCapture;
                recoil.Release(weapon);
            }
        }

        [UnityTest]
        public IEnumerator WardenRecoilPredictsAndReconcilesAcrossRealOwnerRpc([ValueSource(nameof(RecoilNetworkDelays))] int delay)
            => RecoilAcrossRealOwnerRpc("Warden", delay);

        [UnityTest]
        public IEnumerator ProfileRecoilPredictsAndReconcilesAcrossRealOwnerRpc(
            [ValueSource(nameof(RecoilProfileNames))] string name, [ValueSource(nameof(RecoilNetworkDelays))] int delay)
            => RecoilAcrossRealOwnerRpc(name, delay);

        IEnumerator RecoilAcrossRealOwnerRpc(string name, int delay)
        {
            yield return StartNetwork(delay);
            observer = CreateManager("recoil observer");
            Assert.That(observer.StartClient(), Is.True);
            yield return Until(() => observer.IsConnectedClient && host.ConnectedClients.Count == 3, "observer connection");
            int jitter = delay == 50 ? 10 : delay == 100 ? 20 : 0;
            int loss = delay == 100 ? 2 : 0;
            Assert.That(recoilSimulator.IsAvailable && recoilSimulator.IsConnected, Is.True, "installed simulator must control the real transport");
            actor.transform.position = Vector3.right * 50;
            teammate.transform.position = Vector3.left * 50;
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            var serverMovement = player.GetComponent<PlayerMovement>();
            serverMovement.enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            player.GetComponent<NetworkObject>().SpawnWithOwnership(client.LocalClientId);
            ulong id = player.GetComponent<NetworkObject>().NetworkObjectId;
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(id), "recoil owner spawn");
            yield return Until(() => observer.SpawnManager.SpawnedObjects.ContainsKey(id), "recoil observer spawn");
            var owner = client.SpawnManager.SpawnedObjects[id];
            var ownerMovement = owner.GetComponent<PlayerMovement>();
            ownerMovement.enabled = false;
            var look = owner.GetComponent<MouseMovement>();
            look.enabled = false;
            var serverManager = player.GetComponent<WeaponManager>();
            var ownerManager = owner.GetComponent<WeaponManager>();
            int slot = name == "Classic" ? 1 : 0;
            var primary = name == "Classic" ? PrimaryWeaponId.Warden : (PrimaryWeaponId)Enum.Parse(typeof(PrimaryWeaponId), name);
            Assert.That(serverManager.TryReplacePrimaryWeaponServer(primary), Is.True);
            serverManager.SetEquippedWeaponServer(slot);
            var serverFire = player.GetComponent<WeaponFireHandler>();
            var ownerFire = owner.GetComponent<WeaponFireHandler>();
            yield return Until(() => ownerManager.ActivePrimaryWeaponId == primary
                && ownerManager.CurrentWeaponIndex == slot && !serverFire.IsServerEquipping, name + " equip");
            yield return new WaitForSeconds(.15f);
            var weapon = ownerManager.GetWeapon(slot);
            yield return Until(() => !weapon.IsEquipPresentationActive, "owner equip deadline");
            // Runtime RTT changes need time for NGO's local/server clock offsets to converge.
            yield return Until(() => Math.Abs(client.LocalTime.Time - client.ServerTime.Time
                - client.NetworkTimeSystem.LocalBufferSec - client.NetworkTimeSystem.ServerBufferSec
                - client.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId) / 2000d)
                < 1d / client.NetworkConfig.TickRate, "clock offsets after latency change", 15);
            Assert.That(weapon.Data.recoilPattern.useStabilityCurves, Is.True);
            weapon.enabled = false;
            var recoil = owner.GetComponent<RecoilController>();
            SetField(serverMovement, "isGrounded", true);
            SetField(ownerMovement, "isGrounded", true);
            Assert.That(owner.GetComponent<PlayerHealth>().CanUseCombat, Is.True);
            Assert.That(ownerFire.IsOwner && !ownerFire.IsServer, Is.True);

            // Freeze locomotion and supply one real aim sample; fire, RPC, rejection, NV and camera remain live.
            uint inputSequence = 1;
            int tick = client.ServerTime.Tick;
            var input = new PlayerInputPayload { sequence = inputSequence, tick = tick,
                yaw = look.YRotation, pitch = look.XRotation };
            Invoke(serverMovement, "RecordServerAim", input, tick);
            SetField(ownerMovement, "localCommandSequence", inputSequence);
            SetField(ownerMovement, "hasTickedOnce", true);
            Field<NetworkTimer>(ownerMovement, "networkTimer").CurrentTick = tick + 1;
            Field<PlayerInputPayload[]>(ownerMovement, "inputBuffer")[tick % 2048] = input;
            Field<PlayerStatePayload[]>(ownerMovement, "stateBuffer")[tick % 2048] = new PlayerStatePayload { grounded = true };
            Field<NetworkTimer>(serverMovement, "networkTimer").CurrentTick = tick;

            Assert.That(Field<bool>(weapon, "combatInputBlocked"), Is.False, "owner combat input");
            Assert.That(weapon.IsEquipPresentationActive, Is.False, "owner equip clock");
            int initialAmmo = weapon.Data.magazineSize;
            Assert.That(weapon.CurrentAmmo, Is.EqualTo(initialAmmo), "owner ammo");
            Assert.That(look.BodyCam.isActiveAndEnabled, Is.True, "owner aim camera");
            Assert.That(Field<WeaponFireHandler>(weapon, "cachedFireHandler"), Is.EqualTo(ownerFire), "owner binding");
            double fireStarted = Time.realtimeSinceStartupAsDouble;
            ownerMovement.TryGetLatestFireReference(out _, out int actionTick);
            Field<NetworkVariable<WeaponOwnerState>>(serverFire, "ownerWeaponState").OnValueChanged += (_, value) =>
                System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/network-clock.txt",
                    $"delay={delay} receive sequence={value.resolvedFireSequence} tick={host.ServerTime.Tick} realAge={Time.realtimeSinceStartupAsDouble - fireStarted:F4} frameDelta={Time.unscaledDeltaTime:F4} rtt={host.NetworkConfig.NetworkTransport.GetCurrentRtt(client.LocalClientId)} clientRtt={client.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId)} result={value.lastFireResult}\n");
            System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/network-clock.txt",
                $"delay={delay} before hostTick={host.ServerTime.Tick} clientTick={client.ServerTime.Tick} localTick={client.LocalTime.Tick} actionTick={actionTick} inputTick={tick} rate={client.NetworkConfig.TickRate} hostTime={host.ServerTime.Time:F4} clientTime={client.ServerTime.Time:F4} localTime={client.LocalTime.Time:F4} rtt={host.NetworkConfig.NetworkTransport.GetCurrentRtt(client.LocalClientId)}\n");
            Invoke(weapon, "FireBullet");
            Invoke(weapon, "FireBullet");
            System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/network-clock.txt",
                $"delay={delay} FireBullet cost={Time.realtimeSinceStartupAsDouble - fireStarted:F4}s\n");
            Assert.That(weapon.PredictedStability.index, Is.EqualTo(Mathf.Min(2, weapon.Data.recoilPattern.MaximumStabilityIndex)).Within(.001));
            Invoke(recoil, "Update");
            if (name != "Operator" && name != "Bucky")
                Assert.That(Mathf.Abs(look.XRotation), Is.GreaterThan(.01f), "predicted recoil applies to canonical aim");
            yield return Until(() => Field<NetworkVariable<WeaponOwnerState>>(ownerFire, "ownerWeaponState").Value
                .resolvedFireSequence == 2, "coalesced accepted/rejected acknowledgement");
            var authoritative = Field<NetworkVariable<WeaponOwnerState>>(serverFire, "ownerWeaponState").Value;
            System.IO.File.AppendAllText("Documents/Warden/Recoil-v2/Evidence/network-clock.txt",
                $"delay={delay} ACK hostTick={host.ServerTime.Tick} receiveTick={authoritative.authoritativeShotTick} clientTick={client.ServerTime.Tick} result={authoritative.lastFireResult} index={authoritative.stability.index} rtt={host.NetworkConfig.NetworkTransport.GetCurrentRtt(client.LocalClientId)}\n");
            int accepted = authoritative.stability.hasShot ? 1 : 0;
            if (delay == 0) Assert.That(authoritative.lastFireResult, Is.EqualTo(FireRejectReason.CooldownOrAmmo));
            else Assert.That(authoritative.lastFireResult == FireRejectReason.CooldownOrAmmo
                || authoritative.lastFireResult == FireRejectReason.InvalidTick, Is.True, "only a valid timing rejection is allowed");
            if (accepted == 0)
            {
                Assert.That(authoritative.lastFireResult, Is.EqualTo(FireRejectReason.InvalidTick));
                Assert.That(authoritative.stability.Equals(default), Is.True, "rejected shots cannot advance model or RNG");
            }
            Assert.That(authoritative.stability.index, Is.EqualTo(accepted).Within(.001));
            Assert.That(weapon.PredictedStability.Equals(authoritative.stability), Is.True);
            Assert.That(Field<int>(weapon, "pendingServerShots"), Is.Zero);
            Assert.That(serverFire.ServerMagazineAmmo, Is.EqualTo(initialAmmo - accepted));
            var observed = observer.SpawnManager.SpawnedObjects[id];
            var observedManager = observed.GetComponent<WeaponManager>();
            yield return Until(() => observedManager.ActivePrimaryWeaponId == primary
                && observedManager.CurrentWeaponIndex == slot, "observer weapon selection");
            Assert.That(observed.GetComponent<WeaponFireHandler>().IsOwner, Is.False);
            Assert.That(observedManager.GetWeapon(slot).PredictedStability.hasShot, Is.False,
                "observer effects must not run owner prediction");
            Assert.That(observed.GetComponent<MouseMovement>().XRotation, Is.Zero);

            // The model recovers first; exponential camera smoothing has a separate settling tail.
            double recoveryEnd = authoritative.stability.lastShotTime + weapon.Data.FireInterval
                + weapon.Data.recoilPattern.Evaluate(weapon.Data.recoilPattern.recoveryTime, authoritative.stability.index);
            yield return Until(() => weapon.StabilityTime >= recoveryEnd && Mathf.Abs(look.XRotation) < .01f,
                "camera recovery including smoothing tail", 2f);
            Assert.That(Mathf.Abs(look.XRotation), Is.LessThan(.01), "recovery returns only applied recoil");
            SetField(look, "xRotation", -89.9f);
            var applied = look.ApplyRecoil(5, 0);
            Assert.That(applied.x, Is.EqualTo(.1f).Within(.001));
            look.ApplyRecoil(-applied.x, 0);
            Assert.That(look.XRotation, Is.EqualTo(-89.9f).Within(.001));
            SetField(look, "xRotation", 0f);
            look.SetCosmeticRecoil(2f);
            Invoke(look, "RenderRotation");
            Assert.That(look.XRotation, Is.Zero, "cosmetic pop cannot enter input aim");
            Assert.That(Vector3.Angle(look.AimDirection, look.BodyCam.transform.forward), Is.EqualTo(2).Within(.01));
            look.SetCosmeticRecoil(0);
            recoil.Release(weapon);

            // A delayed command from another slot cannot acknowledge this weapon's predictions.
            var ready = Field<NetworkVariable<bool>>(player.GetComponent<PlayerHealth>(), "networkInputReady");
            ready.Value = false;
            try
            {
                Invoke(serverFire, "TryProcessRuntimeFire", new FireCommand { weaponSlot = (byte)(1 - slot), sequence = 102 }, client.LocalClientId);
                var afterWrongSlot = Field<NetworkVariable<WeaponOwnerState>>(serverFire, "ownerWeaponState").Value;
                Assert.That(afterWrongSlot.resolvedFireSequence, Is.EqualTo(2));
                Assert.That(afterWrongSlot.lastFireResult, Is.EqualTo(FireRejectReason.WrongWeapon));
            }
            finally { ready.Value = true; }

            if (weapon.Data.fireMode == FireMode.Auto)
            {
                double deadline = weapon.StabilityTime;
                for (ushort sequence = 3; sequence <= 12; sequence++)
                {
                    while (weapon.StabilityTime < deadline) yield return null;
                    tick = client.ServerTime.Tick;
                    inputSequence++;
                    input = new PlayerInputPayload { sequence = inputSequence, tick = tick,
                        yaw = look.YRotation, pitch = look.XRotation };
                    Invoke(serverMovement, "RecordServerAim", input, tick);
                    SetField(ownerMovement, "localCommandSequence", inputSequence);
                    Field<NetworkTimer>(ownerMovement, "networkTimer").CurrentTick = tick + 1;
                    Field<PlayerInputPayload[]>(ownerMovement, "inputBuffer")[tick % 2048] = input;
                    Field<PlayerStatePayload[]>(ownerMovement, "stateBuffer")[tick % 2048] = new PlayerStatePayload { grounded = true };
                    SetField(weapon, "continuousAutoFire", sequence > 3);
                    Invoke(weapon, "FireBullet");
                    Assert.That(Field<ushort>(weapon, "fireSequence"), Is.EqualTo(sequence));
                    deadline = Field<double>(weapon, "autoNextFireTime");
                    yield return null;
                }
                yield return Until(() => Field<NetworkVariable<WeaponOwnerState>>(ownerFire, "ownerWeaponState").Value.resolvedFireSequence == 12,
                    "held continuation acknowledgement drains");
                var heldState = Field<NetworkVariable<WeaponOwnerState>>(serverFire, "ownerWeaponState").Value;
                Assert.That(weapon.PredictedStability.Equals(heldState.stability), Is.True,
                    "held bit, original samples and pending replay must converge after jitter/rejections");
                Assert.That(Field<int>(weapon, "pendingServerShots"), Is.Zero);
                Assert.That(weapon.CurrentAmmo, Is.EqualTo(serverFire.ServerMagazineAmmo));
                System.IO.File.AppendAllText("Reports/Recoil-Implementation-2026-11-03/Evidence/r5-held-network.csv",
                    $"{name},{delay},{host.NetworkConfig.NetworkTransport.GetCurrentRtt(client.LocalClientId)},{heldState.stability.index},{initialAmmo-serverFire.ServerMagazineAmmo},{heldState.lastFireResult},0,true\n");
            }

            ulong rtt = host.NetworkConfig.NetworkTransport.GetCurrentRtt(client.LocalClientId);
            const string matrix = "Reports/Recoil-Implementation-2026-11-03/Evidence/network-matrix.csv";
            if (!System.IO.File.Exists(matrix)) System.IO.File.WriteAllText(matrix,
                "profile,delay,jitter,loss,rtt,hostClock,ownerClock,accepted,lastResult,pending,stateMatches\n");
            System.IO.File.AppendAllText(matrix, FormattableString.Invariant(
                $"{name},{delay},{jitter},{loss},{rtt},{host.ServerTime.Time:F6},{client.ServerTime.Time:F6},{accepted},{authoritative.lastFireResult},{Field<int>(weapon, "pendingServerShots")},{weapon.PredictedStability.Equals(authoritative.stability)}\n"));
            Debug.Log($"RECOIL_OWNER_RPC_PASS profile={name} delay={delay} jitter={jitter} loss={loss} RTT={rtt} ms");
        }

        [UnityTest]
        public IEnumerator DirectionalDamageUsesRealOwnerRpcForEveryDamageSource()
        {
            var settings = UnityEngine.AI.NavMesh.GetSettingsByIndex(0);
            var floor = new UnityEngine.AI.NavMeshBuildSource { shape = UnityEngine.AI.NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(Vector3.down * .1f, Quaternion.identity, Vector3.one), size = new Vector3(30, .2f, 30) };
            feedbackNavigation = UnityEngine.AI.NavMesh.AddNavMeshData(UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,
                new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildSource> { floor }, new Bounds(Vector3.zero, Vector3.one * 40), Vector3.zero, Quaternion.identity));
            var received = new System.Collections.Generic.List<Vector3>();
            var ownerReplica = client.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            int nonOwnerEvents = 0;
            ownerReplica.DirectionalDamageReceived += received.Add;
            actor.DirectionalDamageReceived += _ => nonOwnerEvents++;
            teammate.DirectionalDamageReceived += _ => nonOwnerEvents++;
            teammate.transform.position = Vector3.right * 50;
            int expected = 0;
            foreach (var path in FeedbackEnemies)
            {
                actor.ResetHealth();
                var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), Vector3.back * 1.5f, Quaternion.identity);
                var ai = enemy.GetComponent<EnemyAI>(); ai.enabled = false;
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent != null) agent.enabled = false;
                enemy.GetComponent<NetworkObject>().Spawn();
                yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(ai.NetworkObjectId), "feedback enemy replica");
                // The animation impact is invoked directly; damage and RPC still run on real spawned behaviours.
                if (ai is SI_Infector infector)
                {
                    SetField(infector, "currentTargetTransform", actor.transform);
                    SetField(infector, "currentTargetProfile", new PlayerProfile { playerTransform = actor.transform, cachedHealth = actor });
                    Invoke(infector, "ApplyImplantImpact");
                }
                else if (ai is SI_Tank tank)
                {
                    DisableClientColliders(); Physics.SyncTransforms();
                    Invoke(tank, "ExecuteHeavySwingHit");
                    expected++;
                    yield return Until(() => received.Count == expected, "Tank swing owner event");
                    Assert.That(received.Last(), Is.EqualTo(enemy.transform.position));
                    actor.ResetHealth();
                    Invoke(tank, "ExecuteSlamAoE");
                }
                else
                {
                    var attack = (EnemyMeleeAttack)typeof(EnemyAI).GetField("meleeAttack", Private).GetValue(ai);
                    attack.Configure(3, 5, 0, 0, 0, 0, 180);
                    Assert.That(attack.TryBegin(enemy.transform, actor.transform, Time.time), Is.True);
                    typeof(EnemyAI).GetMethod("ProcessPendingAttackDamage", Private).Invoke(ai, new object[] { true });
                }
                expected++;
                yield return Until(() => received.Count == expected, path + " owner event");
                Assert.That(received.Last(), Is.EqualTo(enemy.transform.position));
                enemy.GetComponent<NetworkObject>().Despawn();
            }
            foreach (bool fire in new[] { false, true })
            {
                actor.ResetHealth(); DisableClientColliders(); Physics.SyncTransforms();
                var center = new Vector3(0, 1, -1);
                SurvivalDamage.Apply(center, 3, teammate.OwnerClientId, fire);
                expected++;
                yield return Until(() => received.Count == expected, fire ? "fire owner event" : "blast owner event");
                Assert.That(received.Last(), Is.EqualTo(center));
            }
            actor.ResetHealth();
            actor.TakeInfectionDamage(1);
            actor.TakeDamage(1);
            actor.TakeDamage(new DamageInfo(1, sourcePosition: new Vector3(float.NaN, 0, 0)));
            actor.TakeDamage(new DamageInfo(0, sourcePosition: Vector3.right));
            actor.TakeDamage(new DamageInfo(float.PositiveInfinity, sourcePosition: Vector3.right));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(received.Count, Is.EqualTo(expected), "sepsis, unattributed, invalid source, and rejected damage have no directional RPC");
            Assert.That(nonOwnerEvents, Is.Zero, "Host observer and other player never receive victim feedback");
        }

        [UnityTest]
        public IEnumerator BothWeaponDamagePathsConfirmOnlyAcceptedDamageAndKeepServerSource()
        {
            teammate.transform.position = Vector3.right * 50;
            var shooter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), Vector3.back * 5, Quaternion.identity);
            shooter.GetComponent<PlayerMovement>().enabled = false;
            shooter.GetComponent<MouseMovement>().enabled = false;
            shooter.GetComponent<NetworkObject>().SpawnWithOwnership(host.LocalClientId);
            var handler = shooter.GetComponent<WeaponFireHandler>();
            yield return null;
            var data = shooter.GetComponent<WeaponManager>().GetWeapon(0).Data;
            var ownerReplica = client.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            int events = 0; Vector3 source = default;
            ownerReplica.DirectionalDamageReceived += p => { events++; source = p; };
            var methods = typeof(WeaponFireHandler).GetMethods(Private).Where(m => m.Name == "TryApplyDamage").ToArray();
            DisableClientColliders(); Physics.SyncTransforms();
            Assert.That(Physics.Raycast(actor.transform.position + Vector3.up + Vector3.back * 2, Vector3.forward, out RaycastHit hit, 5), Is.True);
            Assert.That(hit.collider.GetComponentInParent<PlayerHealth>(), Is.EqualTo(actor));
            int expected = 0;
            foreach (var method in methods)
            {
                actor.ResetHealth();
                object resolvedHit = method.GetParameters()[1].ParameterType == typeof(RaycastHit) ? hit
                    : new LagCompensatedHit(null, actor, actor.transform.position + Vector3.up, 5, HitboxZone.Head, 1);
                var args = new object[] { data, resolvedHit, host.LocalClientId, default(DamageInfo) };
                Assert.That((bool)method.Invoke(handler, args), Is.True);
                Assert.That(((DamageInfo)args[3]).sourcePosition, Is.EqualTo(shooter.transform.position));
                expected++;
                yield return Until(() => events == expected, "gun victim owner feedback");
                Assert.That(source, Is.EqualTo(shooter.transform.position));
                actor.TakeDamage(1000);
                Assert.That((bool)method.Invoke(handler, args), Is.False, "dead target does not confirm a hit");
            }
            actor.ResetHealth();
            var filter = actor.gameObject.AddComponent<DamageFilter>();
            SetField(filter, "acceptedTypes", DamageType.Environment); SetField(filter, "acceptUnspecified", false);
            var rayMethod = methods.Single(m => m.GetParameters()[1].ParameterType == typeof(RaycastHit));
            Assert.That((bool)rayMethod.Invoke(handler, new object[] { data, hit, host.LocalClientId, default(DamageInfo) }), Is.False);
            Assert.That(actor.CurrentHealth, Is.EqualTo(actor.MaxHealth));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(events, Is.EqualTo(expected));
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tests/Fixtures/SurvivalEnemyProbe.prefab")).GetComponent<EnemyHealth>();
            enemy.NetworkObject.Spawn();
            var confirm = typeof(WeaponFireHandler).GetMethod("ApplyConfirmedDamage", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((bool)confirm.Invoke(null, new object[] { enemy, new DamageInfo(1000) }), Is.True);
            Assert.That((bool)confirm.Invoke(null, new object[] { enemy, new DamageInfo(10) }), Is.False);
        }

        [UnityTest]
        public IEnumerator PlayersAuthoredGrenadeReleasesOnceAndRestoresBothRigs([ValueSource(nameof(PlayerNames))] string character)
        {
            actor.transform.position = Vector3.right * 50;
            teammate.transform.position = Vector3.left * 50;
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath(character)), Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerMovement>().enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            player.GetComponent<NetworkObject>().SpawnWithOwnership(0);
            var inventory = player.GetComponent<SurvivalInventory>();
            var animation = player.GetComponent<GrenadeThrowAnimation>();
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(inventory.NetworkObjectId), character + " remote copy");
            var remote = client.SpawnManager.SpawnedObjects[inventory.NetworkObjectId];
            var remoteInventory = remote.GetComponent<SurvivalInventory>();
            var remoteAnimation = remote.GetComponent<GrenadeThrowAnimation>();
            var fp = Field<Animator>(animation, "firstPersonAnimator");
            var tp = Field<Animator>(remoteAnimation, "thirdPersonAnimator");
            int fpLayer = fp.GetLayerIndex(GrenadeThrowDefinition.LayerName);
            int tpLayer = tp.GetLayerIndex(GrenadeThrowDefinition.LayerName);
            Assert.That(fpLayer, Is.GreaterThan(0)); Assert.That(tpLayer, Is.GreaterThan(0));
            yield return new WaitForSeconds(1.5f);
            DisableClientColliders();
            int slot = player.GetComponent<WeaponManager>().CurrentWeaponIndex;

            foreach (var kind in new[] { ThrowableKind.Frag, ThrowableKind.Incendiary })
            {
                if (kind == ThrowableKind.Incendiary) inventory.CycleThrowable();
                inventory.RequestThrow(Vector3.forward);
                Assert.That(inventory.IsThrowing, Is.True);
                Assert.That(inventory.Count(kind == ThrowableKind.Frag ? PickupType.FragGrenade : PickupType.IncendiaryGrenade), Is.EqualTo(3), "reserved, not consumed before release");
                Assert.That(host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), Is.False, "no early projectile");
                uint sequence = inventory.ThrowState.Sequence;
                Invoke(inventory, "RequestThrowServerRpc", kind, Vector3.forward, sequence);
                player.GetComponent<WeaponManager>().RequestEquipWeaponServerRpc(slot == 0 ? 1 : 0);
                inventory.RequestUse(ConsumableKind.Medkit);
                Assert.That(player.GetComponent<WeaponManager>().CurrentWeaponIndex, Is.EqualTo(slot));
                Assert.That(inventory.IsUsingItem, Is.False);
                yield return new WaitForSeconds(.18f);
                DisableClientColliders();
                Assert.That(remoteInventory.IsThrowing, Is.True);
                Assert.That(remoteInventory.ThrowState.ReleaseAt, Is.EqualTo(inventory.ThrowState.ReleaseAt));
                Assert.That(fp.GetLayerWeight(fpLayer), Is.GreaterThan(.5f));
                Assert.That(tp.GetLayerWeight(tpLayer), Is.GreaterThan(.5f));
                AssertHeldGrenade(animation, true);
                AssertHeldGrenade(remoteAnimation, false);
                CapturePlayer(player, character + "-Grenade-1P-" + kind + "-windup");
                CaptureRemoteGrenade(remote.gameObject, character + "-Grenade-3P-" + kind + "-windup");
                yield return Until(() => inventory.Count(kind == ThrowableKind.Frag ? PickupType.FragGrenade : PickupType.IncendiaryGrenade) == 2, "authored release consumes one");
                yield return null;
                Assert.That(Field<GameObject>(animation, "heldGrenade").activeSelf, Is.False);
                Assert.That(host.SpawnManager.SpawnedObjectsList.Count(o => o.GetComponent<SurvivalProjectile>() != null), Is.EqualTo(1));
                yield return Until(() => !Field<GameObject>(remoteAnimation, "heldGrenade").activeSelf, "observer hand visual released");
                CapturePlayer(player, character + "-Grenade-1P-" + kind + "-release");
                CaptureRemoteGrenade(remote.gameObject, character + "-Grenade-3P-" + kind + "-release");
                yield return Until(() => !inventory.IsThrowing && !remoteInventory.IsThrowing, "recovery ends on both peers");
                yield return null;
                Assert.That(fp.GetLayerWeight(fpLayer), Is.Zero);
                Assert.That(tp.GetLayerWeight(tpLayer), Is.Zero);
                var visibleWeapon = player.GetComponent<WeaponManager>().CurrentWeapon;
                Assert.That(visibleWeapon.GetComponentsInChildren<Renderer>().All(r => !r.forceRenderingOff), Is.True);
                yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "grenade expires");
            }
            player.GetComponent<PlayerHealth>().Respawn(Vector3.zero, Quaternion.identity);
            inventory.RequestThrow(Vector3.forward);
            Assert.That(inventory.IsThrowing, Is.True);
            int beforeCancel = inventory.IncendiaryGrenadeCount.Value;
            SetField(player.GetComponent<PlayerMovement>(), "serverSprinting", true);
            yield return Until(() => !inventory.IsThrowing, "sprint cancels windup");
            SetField(player.GetComponent<PlayerMovement>(), "serverSprinting", false);
            Assert.That(inventory.IncendiaryGrenadeCount.Value, Is.EqualTo(beforeCancel));
            Assert.That(host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), Is.False);
            Debug.Log(character + "_AUTHORED_GRENADE_PASS");
        }

        [UnityTest]
        public IEnumerator ClientPlayersThrowWithAllSixWeaponsAndPreserveLegs([ValueSource(nameof(PlayerNames))] string character)
        {
            actor.transform.position = Vector3.right * 50;
            teammate.transform.position = Vector3.left * 50;
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath(character)), Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerMovement>().enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            player.GetComponent<NetworkObject>().SpawnWithOwnership(client.LocalClientId);
            var inventory = player.GetComponent<SurvivalInventory>();
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(inventory.NetworkObjectId), "client-owned " + character + " spawn");
            var owner = client.SpawnManager.SpawnedObjects[inventory.NetworkObjectId];
            owner.GetComponent<PlayerMovement>().enabled = false;
            owner.GetComponent<MouseMovement>().enabled = false;
            var ownerInventory = owner.GetComponent<SurvivalInventory>();
            var ownerAnimation = owner.GetComponent<GrenadeThrowAnimation>();
            var observerAnimation = player.GetComponent<GrenadeThrowAnimation>();
            var manager = player.GetComponent<WeaponManager>();
            var ownerManager = owner.GetComponent<WeaponManager>();
            Assert.That(ownerInventory.IsOwner, Is.True);
            Assert.That(ownerInventory.IsServer, Is.False);

            for (int weapon = 0; weapon < 6; weapon++)
            {
                var primary = (PrimaryWeaponId)Mathf.Min(weapon, 4);
                int slot = weapon == 5 ? 1 : 0;
                string label = weapon == 5 ? "Classic" : primary.ToString();
                Assert.That(manager.TryReplacePrimaryWeaponServer(primary), Is.True, label);
                manager.SetEquippedWeaponServer(slot);
                yield return Until(() => ownerManager.ActivePrimaryWeaponId == primary && ownerManager.CurrentWeaponIndex == slot,
                    label + " equip replicated");
                yield return Until(() => !player.GetComponent<WeaponFireHandler>().IsServerEquipping, label + " equip completes");
                yield return new WaitForSeconds(.15f);
                var fp = Field<Animator>(ownerAnimation, "firstPersonAnimator");
                var tp = Field<Animator>(observerAnimation, "thirdPersonAnimator");
                var thirdPersonWeapon = player.GetComponent<PlayerVisibilityController>().ThirdPersonWeaponPresentations
                    .Single(p => p.WeaponData == manager.CurrentWeapon.GetComponent<Weapon>().Data).WeaponObject;
                var thirdPersonRenderers = thirdPersonWeapon.GetComponentsInChildren<Renderer>(true);
                Assert.That(thirdPersonRenderers.Length, Is.GreaterThan(0), label);
                Assert.That(tp.runtimeAnimatorController.name, Does.Contain(label), "observer controller follows gun");
                AssertGrenadeLeavesLocomotionLegs(tp, label);
                inventory.FragGrenadeCount.Value = 3;
                yield return Until(() => ownerInventory.FragGrenadeCount.Value == 3, label + " stock replicated");
                ownerInventory.RequestThrow(Vector3.forward);
                yield return Until(() => inventory.IsThrowing && ownerInventory.IsThrowing, label + " client request accepted");
                // NGO buffers the client's server clock; wait for both presentations
                // to enter windup instead of assuming equal wall-clock progress.
                yield return Until(() => fp.GetLayerWeight(fp.GetLayerIndex(GrenadeThrowDefinition.LayerName)) > .5f
                    && tp.GetLayerWeight(tp.GetLayerIndex(GrenadeThrowDefinition.LayerName)) > .5f, label + " both rigs in windup", 1);
                Assert.That(inventory.FragGrenadeCount.Value, Is.EqualTo(3), label + " stock retained in windup"
                    + $"; hostElapsed={inventory.ServerNow - inventory.ThrowState.StartedAt:F4}"
                    + $" ownerElapsed={ownerInventory.ServerNow - ownerInventory.ThrowState.StartedAt:F4}"
                    + $" release={inventory.ThrowState.ReleaseAt - inventory.ThrowState.StartedAt:F4}"
                    + $" frame={Time.unscaledDeltaTime:F4}");
                Assert.That(fp.GetLayerWeight(fp.GetLayerIndex(GrenadeThrowDefinition.LayerName)), Is.GreaterThan(.5f), label + " owner 1P");
                Assert.That(tp.GetLayerWeight(tp.GetLayerIndex(GrenadeThrowDefinition.LayerName)), Is.GreaterThan(.5f), label + " host 3P");
                AssertHeldGrenade(ownerAnimation, true);
                AssertHeldGrenade(observerAnimation, false);
                Assert.That(ownerManager.CurrentWeapon.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff), Is.True, label + " 1P gun hidden while throwing");
                Assert.That(thirdPersonRenderers.All(r => r.forceRenderingOff), Is.True, label + " 3P gun hidden while throwing");
                yield return Until(() => inventory.FragGrenadeCount.Value == 2 && ownerInventory.FragGrenadeCount.Value == 2,
                    label + " release replicated");
                Assert.That(host.SpawnManager.SpawnedObjectsList.Count(o => o.GetComponent<SurvivalProjectile>() != null), Is.EqualTo(1));
                yield return Until(() => !inventory.IsThrowing && !ownerInventory.IsThrowing, label + " recovery");
                yield return null;
                Assert.That(fp.GetLayerWeight(fp.GetLayerIndex(GrenadeThrowDefinition.LayerName)), Is.Zero, label);
                Assert.That(tp.GetLayerWeight(tp.GetLayerIndex(GrenadeThrowDefinition.LayerName)), Is.Zero, label);
                var renderers = ownerManager.CurrentWeapon.GetComponentsInChildren<Renderer>();
                Assert.That(renderers.Length, Is.GreaterThan(0));
                Assert.That(renderers.All(r => !r.forceRenderingOff), Is.True, label + " gun restored");
                Assert.That(thirdPersonRenderers.All(r => !r.forceRenderingOff), Is.True, label + " 3P gun restored");
                yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), label + " projectile expires");
            }
            Debug.Log(character + "_CLIENT_SIX_GUNS_LEGS_PASS");
        }

        static void AssertHeldGrenade(GrenadeThrowAnimation animation, bool firstPerson)
        {
            var held = Field<GameObject>(animation, "heldGrenade");
            Assert.That(held, Is.Not.Null);
            Assert.That(held.activeInHierarchy, Is.True);
            Assert.That(held.GetComponentsInChildren<NetworkObject>(true), Is.Empty, "hand visual must not spawn a second network projectile");
            Assert.That(held.GetComponentsInChildren<Collider>(true), Is.Empty, "hand visual must not affect physics");
            Assert.That(held.GetComponentsInChildren<Rigidbody>(true), Is.Empty, "hand visual must not simulate a projectile");
            int layer = LayerMask.NameToLayer(firstPerson ? "FirstPerson" : "ThirdPerson");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0));
            Assert.That(held.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == layer), Is.True);
            Transform hand = Field<Transform>(animation, firstPerson ? "firstPersonHand" : "thirdPersonHand");
            Vector3 grip = Field<Vector3>(animation, firstPerson ? "firstPersonGripOffset" : "thirdPersonGripOffset");
            Assert.That(Vector3.Distance(held.transform.position, hand.position + hand.rotation * grip), Is.LessThan(.002f), "grenade follows the correct animated hand");
            Vector3 rotation = Field<Vector3>(animation, firstPerson ? "gripRotation" : "thirdPersonGripRotation");
            Assert.That(Quaternion.Angle(held.transform.rotation, hand.rotation * Quaternion.Euler(rotation)), Is.LessThan(.1f), "grenade uses this rig's grip axes");
        }

        static void AssertGrenadeLeavesLocomotionLegs(Animator animator, string label)
        {
            // Evaluate the real controller twice at identical locomotion phases. Only
            // the grenade layer changes; a second phase proves the legs are moving.
            var legs = animator.GetComponentsInChildren<Transform>(true)
                .Where(t => new[] { "Pelvis", "L_Hip", "R_Hip", "L_Knee", "R_Knee", "L_Foot", "R_Foot" }.Contains(t.name)).ToArray();
            Assert.That(legs.Length, Is.EqualTo(7), label);
            var hand = animator.GetComponentsInChildren<Transform>(true).Single(t => t.name == "R_Hand");
            var head = animator.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Head");
            var pelvis = legs.Single(t => t.name == "Pelvis");
            int layer = animator.GetLayerIndex(GrenadeThrowDefinition.LayerName);
            var weights = Enumerable.Range(0, animator.layerCount).Select(animator.GetLayerWeight).ToArray();
            float speed = animator.speed, movement = animator.GetFloat("Speed");
            bool grounded = animator.GetBool("Grounded"), falling = animator.GetBool("FreeFall");
            var culling = animator.cullingMode;
            Quaternion[] firstPhase = null;
            try
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 0;
                animator.SetFloat("Speed", 3);
                animator.SetBool("Grounded", true);
                animator.SetBool("FreeFall", false);
                for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0);
                foreach (float phase in new[] { .17f, .47f })
                {
                    animator.SetLayerWeight(layer, 0);
                    animator.Play("Locomotion.Locomotion", 0, phase);
                    animator.Update(0);
                    var positions = legs.Select(t => t.localPosition).ToArray();
                    var rotations = legs.Select(t => t.localRotation).ToArray();
                    Quaternion handBefore = hand.localRotation;
                    float standingHeight = Vector3.Dot(head.position - pelvis.position, animator.transform.up);
                    Assert.That(standingHeight, Is.GreaterThan(.3f), label + " baseline torso");
                    animator.SetLayerWeight(layer, 1);
                    foreach (float throwPhase in new[] { 0f, .17f, .27f, .47f, .8f, 1f })
                    {
                        animator.Play("Grenade Throw.Throw", layer, throwPhase);
                        animator.Update(0);
                        for (int i = 0; i < legs.Length; i++)
                        {
                            Assert.That(Vector3.Distance(positions[i], legs[i].localPosition), Is.LessThan(.00001f), label + " " + legs[i].name);
                            Assert.That(Quaternion.Angle(rotations[i], legs[i].localRotation), Is.LessThan(.1f), label + " " + legs[i].name);
                        }
                        // Matching bone paths is insufficient: Brimstone uses different bind axes.
                        Assert.That(Vector3.Dot(head.position - pelvis.position, animator.transform.up),
                            Is.GreaterThan(standingHeight * .5f), label + " torso folded at throw phase " + throwPhase);
                    }
                    Assert.That(Quaternion.Angle(handBefore, hand.localRotation), Is.GreaterThan(1), label + " throw moves hand");
                    if (firstPhase != null)
                        Assert.That(rotations.Select((rotation, i) => Quaternion.Angle(firstPhase[i], rotation)).Max(), Is.GreaterThan(.5f), label + " legs animate across phases");
                    firstPhase = rotations;
                }
            }
            finally
            {
                for (int i = 0; i < weights.Length; i++) animator.SetLayerWeight(i, weights[i]);
                animator.speed = speed; animator.cullingMode = culling;
                animator.SetFloat("Speed", movement); animator.SetBool("Grounded", grounded); animator.SetBool("FreeFall", falling);
            }
        }

        void CaptureRemoteGrenade(GameObject player, string name)
        {
            var hidden = Object.FindObjectsByType<Renderer>().Where(r => !r.transform.IsChildOf(player.transform)).ToArray();
            var original = hidden.Select(r => r.forceRenderingOff).ToArray();
            foreach (var renderer in hidden) renderer.forceRenderingOff = true;
            var camera = new GameObject("Third-person grenade capture").AddComponent<Camera>();
            var light = new GameObject("Grenade capture light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(30, -35, 0);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .12f, .12f);
            camera.transform.position = player.transform.position + new Vector3(2, 1.8f, 3);
            camera.transform.LookAt(player.transform.position + Vector3.up * 1.2f); camera.fieldOfView = 38;
            var target = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes("Documents/UIUX/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.Destroy(camera.gameObject); Object.Destroy(light.gameObject); Object.Destroy(target); Object.Destroy(image);
                for (int i = 0; i < hidden.Length; i++) if (hidden[i] != null) hidden[i].forceRenderingOff = original[i];
            }
        }

        [UnityTest]
        public IEnumerator SelfHealUsesAllFourCharactersAndRestoresPresentation()
        {
            actor.transform.position = Vector3.right * 50;
            teammate.transform.position = Vector3.left * 50;
            foreach (var character in PlayerNames)
            {
                var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath(character)));
                player.GetComponent<PlayerMovement>().enabled = false;
                player.GetComponent<MouseMovement>().enabled = false;
                player.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                yield return new WaitForSeconds(.2f);
                var inventory = player.GetComponent<SurvivalInventory>();
                var health = player.GetComponent<PlayerHealth>();
                var medical = player.GetComponent<SelfInjectionAnimation>();
                var presentation = player.GetComponent<SurvivalPresentation>();
                var hands = Field<Renderer[]>(medical, "originalHands");
                var hidden = hands.Select(r => r.forceRenderingOff).ToArray();
                health.TakeDamage(70);
                foreach (var fraction in new[] { .02f, .5f, .98f })
                {
                    inventory.RequestUse(ConsumableKind.Medkit);
                    yield return Until(() => medical.IsPlaying, character + " heal starts");
                    Assert.That(Field<bool>(medical, "playingHeal"), Is.True);
                    var now = inventory.ServerNow;
                    Field<NetworkVariable<double>>(inventory, "useStarted").Value = now - 4 * fraction;
                    Field<NetworkVariable<double>>(inventory, "useDeadline").Value = now + 4 * (1 - fraction);
                    Invoke(presentation, "LateUpdate");
                    var model = Field<GameObject>(medical, "viewmodel");
                    var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                    Assert.That(skin.sharedMesh, Is.SameAs(((SkinnedMeshRenderer)hands[0]).sharedMesh), character + " original mesh and bind poses");
                    Assert.That(skin.sharedMaterials, Is.EqualTo(hands[0].sharedMaterials));
                    Assert.That(model.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(1), "empty hands, no props");
                    Assert.That(Field<GameObject>(presentation, "item") == null || !Field<GameObject>(presentation, "item").activeSelf, Is.True);
                    Assert.That(hands.All(r => r.forceRenderingOff), Is.True);
                    Assert.That(Field<Renderer[]>(presentation, "weaponRenderers").All(r => r == null || r.forceRenderingOff), Is.True);
                    var remote = client.SpawnManager.SpawnedObjects[player.GetComponent<NetworkObject>().NetworkObjectId];
                    Assert.That(remote.GetComponent<SelfInjectionAnimation>().IsPlaying, Is.False, "owner-only first-person clip");
                    CapturePlayer(player, "SelfHeal-" + character + "-" + fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    inventory.CancelUse();
                    yield return null; yield return null;
                    Assert.That(medical.IsPlaying, Is.False);
                    Assert.That(model.activeSelf, Is.False);
                    Assert.That(hands.Select(r => r.forceRenderingOff), Is.EqualTo(hidden));
                    Assert.That(inventory.MedkitCount.Value, Is.EqualTo(2), "cancel never consumes");
                    Assert.That(health.CurrentHealth, Is.EqualTo(30), "cancel never heals");
                }
                inventory.RequestUse(ConsumableKind.Medkit);
                yield return Until(() => medical.IsPlaying, "heal before damage");
                health.TakeDamage(1);
                yield return Until(() => !inventory.IsUsingItem && !medical.IsPlaying, "damage cancels and restores");
                Assert.That(inventory.MedkitCount.Value, Is.EqualTo(2));
                inventory.RequestUse(ConsumableKind.Medkit);
                yield return Until(() => medical.IsPlaying, "full four-second heal");
                Assert.That(Field<NetworkVariable<double>>(inventory, "useDeadline").Value - Field<NetworkVariable<double>>(inventory, "useStarted").Value,
                    Is.EqualTo(4).Within(.001));
                yield return Until(() => !inventory.IsUsingItem && !medical.IsPlaying, "heal completion", 6);
                Assert.That(health.CurrentHealth, Is.EqualTo(79));
                Assert.That(inventory.MedkitCount.Value, Is.EqualTo(1));
                Assert.That(hands.Select(r => r.forceRenderingOff), Is.EqualTo(hidden));
                player.GetComponent<PlayerInfectionController>().SetInfectionServer(65);
                inventory.RequestUse(ConsumableKind.Antidote);
                yield return Until(() => medical.IsPlaying && !Field<bool>(medical, "playingHeal"), "switch from heal to injection");
                inventory.CancelUse(); yield return null; yield return null;
                inventory.RequestUse(ConsumableKind.Medkit);
                yield return Until(() => medical.IsPlaying && Field<bool>(medical, "playingHeal"), "switch back from injection to heal");
                medical.enabled = false;
                Assert.That(medical.IsPlaying, Is.False);
                Assert.That(hands.Select(r => r.forceRenderingOff), Is.EqualTo(hidden), "disable restores visibility");
                inventory.CancelUse();
                player.GetComponent<NetworkObject>().Despawn(true);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SelfInjectionUsesAllFourCharactersAndRestoresPresentation()
        {
            actor.transform.position = Vector3.right * 50;
            teammate.transform.position = Vector3.left * 50;
            foreach (var character in PlayerNames)
            {
                var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath(character)), Vector3.zero, Quaternion.identity);
                player.GetComponent<PlayerMovement>().enabled = false;
                player.GetComponent<MouseMovement>().enabled = false;
                player.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                yield return new WaitForSeconds(.2f);
                var inventory = player.GetComponent<SurvivalInventory>();
                var injection = player.GetComponent<SelfInjectionAnimation>();
                var presentation = player.GetComponent<SurvivalPresentation>();
                var infection = player.GetComponent<PlayerInfectionController>();
                Assert.That(injection, Is.Not.Null, character);
                var hands = Field<Renderer[]>(injection, "originalHands");
                var originalHidden = hands.Select(r => r.forceRenderingOff).ToArray();
                infection.SetInfectionServer(65);
                inventory.RequestUse(ConsumableKind.Antidote);
                yield return Until(() => injection.IsPlaying, character + " self-injection starts");
                var viewmodel = Field<GameObject>(injection, "viewmodel");
                Assert.That(viewmodel.activeInHierarchy, Is.True, character);
                var skin = viewmodel.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(skin.sharedMaterials, Is.EqualTo(hands[0].sharedMaterials), character + " keeps character materials");
                Assert.That(skin.bones.All(b => b != null), Is.True, character + " binds every skin bone");
                foreach (var seconds in new[] { .5f, 2.5f, 4.25f })
                {
                    var now = inventory.ServerNow;
                    Field<NetworkVariable<double>>(inventory, "useStarted").Value = now - seconds;
                    Field<NetworkVariable<double>>(inventory, "useDeadline").Value = now + 5 - seconds;
                    yield return null;
                    CapturePlayer(player, "SelfInjection-" + character + "-" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    CapturePlayer(player, "SelfInjection-" + character + "-wide-" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), true);
                    Assert.That(hands.All(r => r.forceRenderingOff), Is.True, character + " hides original hands");
                    Assert.That(Field<Renderer[]>(presentation, "weaponRenderers").All(r => r == null || r.forceRenderingOff), Is.True, character + " hides guns");
                }
                inventory.CancelUse();
                yield return null;
                yield return null;
                Assert.That(injection.IsPlaying, Is.False, character);
                Assert.That(viewmodel.activeSelf, Is.False, character);
                Assert.That(hands.Select(r => r.forceRenderingOff), Is.EqualTo(originalHidden), character + " restores original hand visibility");
                Assert.That(inventory.AntidoteCount.Value, Is.EqualTo(2), character + " cancellation keeps the antidote");

                foreach (var fraction in new[] { .02f, .5f, .98f })
                {
                    inventory.RequestUse(ConsumableKind.Antidote);
                    var now = inventory.ServerNow;
                    Field<NetworkVariable<double>>(inventory, "useStarted").Value = now - 5 * fraction;
                    Field<NetworkVariable<double>>(inventory, "useDeadline").Value = now + 5 * (1 - fraction);
                    Invoke(presentation, "LateUpdate");
                    Assert.That(injection.IsPlaying, Is.True, character + " restart");
                    inventory.CancelUse();
                    yield return null;
                    yield return null;
                    Assert.That(injection.IsPlaying, Is.False, character + " cancels at " + fraction);
                    Assert.That(inventory.AntidoteCount.Value, Is.EqualTo(2), character);
                }
                inventory.RequestUse(ConsumableKind.Antidote);
                yield return Until(() => injection.IsPlaying, character + " completion starts");
                Field<NetworkVariable<double>>(inventory, "useDeadline").Value = inventory.ServerNow;
                yield return Until(() => !inventory.IsUsingItem, character + " server completes");
                yield return null;
                Assert.That(injection.IsPlaying, Is.False, character);
                Assert.That(inventory.AntidoteCount.Value, Is.EqualTo(1), character + " consumes once on completion");
                Assert.That(infection.CurrentInfection, Is.EqualTo(25).Within(1), character);
                Assert.That(hands.Select(r => r.forceRenderingOff), Is.EqualTo(originalHidden), character);
                player.GetComponent<NetworkObject>().Despawn(true);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AuthoredPlayerMedicalPresentationAndIncendiaryCollision()
        {
            actor.transform.position = Vector3.right * 50;
            teammate.transform.position = Vector3.left * 50;
            var realPlayer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), Vector3.zero, Quaternion.identity);
            realPlayer.GetComponent<PlayerMovement>().enabled = false;
            realPlayer.GetComponent<MouseMovement>().enabled = false;
            realPlayer.GetComponent<NetworkObject>().SpawnWithOwnership(0);
            yield return new WaitForSeconds(.3f);
            DisableClientColliders();
            var health = realPlayer.GetComponent<PlayerHealth>();
            var inventory = realPlayer.GetComponent<SurvivalInventory>();
            health.TakeDamage(55);
            inventory.RequestUse(ConsumableKind.Medkit);
            yield return Until(() => inventory.IsUsingItem, "authored player treatment starts");
            yield return new WaitForSeconds(.8f);
            var presentation = realPlayer.GetComponent<SurvivalPresentation>();
            Assert.That(realPlayer.GetComponent<SelfInjectionAnimation>().IsPlaying, Is.True);
            Assert.That(Field<GameObject>(presentation, "item") == null || !Field<GameObject>(presentation, "item").activeSelf, Is.True);
            Assert.That(Field<AudioSource>(presentation, "treatmentAudio").isPlaying, Is.True);
            CapturePlayer(realPlayer, "Survival-Medkit-runtime");
            yield return Until(() => !inventory.IsUsingItem, "authored player heals", 6);
            Assert.That(health.CurrentHealth, Is.EqualTo(95));
            // Presentation consumes server state in LateUpdate, after the test coroutine resumes.
            yield return null;
            Assert.That(realPlayer.GetComponent<SelfInjectionAnimation>().IsPlaying, Is.False);
            var infection = realPlayer.GetComponent<PlayerInfectionController>();
            infection.SetInfectionServer(65);
            inventory.RequestUse(ConsumableKind.Antidote);
            yield return Until(() => inventory.IsUsingItem, "authored antidote starts");
            yield return new WaitForSeconds(.8f);
            CapturePlayer(realPlayer, "Survival-Antidote-runtime");
            inventory.CancelUse();
            yield return null;
            Assert.That(inventory.AntidoteCount.Value, Is.EqualTo(2));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1.5f, 2);
            wall.transform.localScale = new Vector3(4, 3, .2f);
            wall.AddComponent<Rigidbody>().isKinematic = true;
            Physics.SyncTransforms();
            inventory.CycleThrowable();
            inventory.RequestThrow(Vector3.forward);
            yield return Until(() => host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalFireZone>() != null), "incendiary collision creates fire", 4);
            Assert.That(inventory.IncendiaryGrenadeCount.Value, Is.EqualTo(2));
            Assert.That(inventory.FragGrenadeCount.Value, Is.EqualTo(3));
            yield return new WaitForSeconds(.6f);
            CapturePlayer(realPlayer, "Survival-Fire-runtime");
            Debug.Log("SURVIVAL_AUTHORED_PRESENTATION_PASS");
        }

        void CapturePlayer(GameObject player, string name, bool wide = false)
        {
            // Both peers share a scene in this transport fixture. Hide only the client copy
            // during capture so its remote body cannot overlap the host's first-person camera.
            var clientRenderers = client.SpawnManager.SpawnedObjectsList
                .SelectMany(o => o.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var hidden = clientRenderers.Select(r => r.forceRenderingOff).ToArray();
            foreach (var renderer in clientRenderers) renderer.forceRenderingOff = true;
            var mouse = player.GetComponent<MouseMovement>();
            var presentation = player.GetComponent<SurvivalPresentation>();
            var heldItem = Field<GameObject>(presentation, "item");
            if (heldItem != null && heldItem.activeSelf)
            {
                Vector3 screen = mouse.WeaponCam.WorldToViewportPoint(heldItem.transform.position);
                Assert.That(screen.z, Is.GreaterThan(mouse.WeaponCam.nearClipPlane));
                Assert.That(screen.x, Is.InRange(.1f, .9f), "item within horizontal view");
                Assert.That(screen.y, Is.InRange(.1f, .7f), "item clear of HUD and crosshair");
            }
            var width = wide ? 1680 : 1280;
            var target = new RenderTexture(width, 720, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(width, 720, TextureFormat.RGB24, false);
            try
            {
                var light = new GameObject("Presentation test key light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 2;
                light.transform.rotation = Quaternion.Euler(25, -30, 0);
                mouse.BodyCam.targetTexture = target; mouse.BodyCam.Render();
                mouse.WeaponCam.targetTexture = target; mouse.WeaponCam.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, 720), 0, 0); image.Apply();
                System.IO.Directory.CreateDirectory("Documents/UIUX");
                System.IO.File.WriteAllBytes("Documents/UIUX/" + name + ".png", image.EncodeToPNG());
                Object.Destroy(light.gameObject);
            }
            finally
            {
                for (int i = 0; i < clientRenderers.Length; i++)
                    if (clientRenderers[i] != null) clientRenderers[i].forceRenderingOff = hidden[i];
                mouse.BodyCam.targetTexture = null; mouse.WeaponCam.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(target); Object.Destroy(image);
            }
        }

        [UnityTest]
        public IEnumerator MedicalAuthorityCancellationAndPersistence()
        {
            actor.TakeDamage(70);
            yield return Until(() => clientInventory.GetComponent<PlayerHealth>().CurrentHealth == 30, "health replication");
            clientInventory.GetComponent<PlayerHealth>().Heal(50);
            Assert.That(clientInventory.TryAdd(PickupType.Medkit, 1), Is.False);
            Assert.That(clientInventory.MedkitCount.CanClientWrite(client.LocalClientId), Is.False);
            Assert.That(actor.CurrentHealth, Is.EqualTo(30));

            clientInventory.RequestUse(ConsumableKind.Medkit);
            yield return Until(() => serverInventory.IsUsingItem && clientInventory.IsUsingItem, "medical start replicated");
            Assert.That(serverInventory.MedkitCount.Value, Is.EqualTo(2), "item reserved until completion");
            yield return Until(() => !serverInventory.IsUsingItem, "four-second self heal", 6);
            Assert.That(actor.CurrentHealth, Is.EqualTo(80));
            Assert.That(serverInventory.MedkitCount.Value, Is.EqualTo(1));
            yield return Until(() => clientInventory.MedkitCount.Value == 1 && !clientInventory.IsUsingItem, "count completion replicated");

            // Interrupt at early, middle and near-complete progress; no lost item.
            foreach (float fraction in new[] { .02f, .5f, .98f })
            {
                clientInventory.RequestUse(ConsumableKind.Medkit);
                yield return Until(() => serverInventory.IsUsingItem, "restart medical action");
                double now = serverInventory.ServerNow;
                Field<NetworkVariable<double>>(serverInventory, "useStarted").Value = now - 4 * fraction;
                Field<NetworkVariable<double>>(serverInventory, "useDeadline").Value = now + 4 * (1 - fraction);
                actor.TakeDamage(1);
                yield return Until(() => !serverInventory.IsUsingItem, "damage cancellation");
                Assert.That(serverInventory.MedkitCount.Value, Is.EqualTo(1));
            }
            clientInventory.RequestUse(ConsumableKind.Medkit);
            yield return Until(() => serverInventory.IsUsingItem, "sprint test starts");
            SetField(actor.GetComponent<PlayerMovement>(), "serverSprinting", true);
            yield return Until(() => !serverInventory.IsUsingItem, "sprint cancellation");
            SetField(actor.GetComponent<PlayerMovement>(), "serverSprinting", false);
            Assert.That(serverInventory.MedkitCount.Value, Is.EqualTo(1));

            clientInventory.RequestUse(ConsumableKind.Medkit);
            yield return Until(() => serverInventory.IsUsingItem, "clamped healing starts");
            FinishSoon();
            yield return Until(() => !serverInventory.IsUsingItem, "clamped healing completes");
            Assert.That(actor.CurrentHealth, Is.EqualTo(actor.MaxHealth));
            Assert.That(serverInventory.MedkitCount.Value, Is.Zero);
            Assert.That(serverInventory.CanTreat(serverInventory, ConsumableKind.Medkit), Is.False);

            var infection = actor.GetComponent<PlayerInfectionController>();
            infection.SetInfectionServer(100);
            clientInventory.GetComponent<PlayerInfectionController>().TreatInfectionServer(100);
            Assert.That(infection.CurrentInfection, Is.EqualTo(100));
            clientInventory.RequestUse(ConsumableKind.Antidote);
            yield return Until(() => serverInventory.IsUsingItem, "sepsis treatment starts");
            actor.TakeInfectionDamage(5);
            yield return null;
            Assert.That(serverInventory.IsUsingItem, Is.True, "infection drain must not starve antidote");
            yield return Until(() => !serverInventory.IsUsingItem, "five-second antidote", 7);
            Assert.That(infection.CurrentInfection, Is.EqualTo(60));
            Assert.That(infection.IsSepsis, Is.False);
            Assert.That(infection.SepsisTimeRemaining, Is.Zero);
            infection.SetInfectionServer(30);
            clientInventory.RequestUse(ConsumableKind.Antidote);
            yield return Until(() => serverInventory.IsUsingItem, "second antidote");
            infection.AddInfectionServer(20);
            FinishSoon();
            yield return Until(() => !serverInventory.IsUsingItem, "current infection reduction");
            Assert.That(infection.CurrentInfection, Is.EqualTo(10));
            Assert.That(serverInventory.AntidoteCount.Value, Is.Zero);

            Assert.That(serverInventory.TryAdd(PickupType.Medkit, 1), Is.True);
            teammate.TakeDamage(20);
            var target = teammate.GetComponent<SurvivalInventory>();
            Assert.That(serverInventory.CanTreat(target, ConsumableKind.Medkit), Is.True);
            teammate.transform.position = Vector3.right * 4;
            Physics.SyncTransforms();
            Assert.That(serverInventory.CanTreat(target, ConsumableKind.Medkit), Is.False, "range");
            teammate.transform.position = Vector3.right * 2;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(1, 1, 0); wall.transform.localScale = new Vector3(.2f, 3, 3);
            Physics.SyncTransforms();
            Assert.That(serverInventory.CanTreat(target, ConsumableKind.Medkit), Is.False, "line of sight");
            Object.Destroy(wall); yield return null; Physics.SyncTransforms();
            var clientTarget = client.SpawnManager.SpawnedObjects[teammate.NetworkObjectId].GetComponent<SurvivalInventory>();
            clientInventory.RequestUse(ConsumableKind.Medkit, clientTarget);
            yield return Until(() => serverInventory.IsUsingItem, "assist starts");
            Assert.That(Field<NetworkVariable<double>>(serverInventory, "useDeadline").Value - Field<NetworkVariable<double>>(serverInventory, "useStarted").Value, Is.EqualTo(2.5).Within(.001));
            FinishSoon(); yield return Until(() => !serverInventory.IsUsingItem, "assist completion");
            Assert.That(teammate.CurrentHealth, Is.EqualTo(100));
            Assert.That(serverInventory.MedkitCount.Value, Is.Zero);

            var pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Content + "MedkitPickup.prefab")).GetComponent<PickupItem>();
            pickup.NetworkObject.Spawn();
            Assert.That(pickup.TryClaimServer(actor.NetworkObject), Is.EqualTo(PickupResultCode.Accepted));
            Assert.That(serverInventory.MedkitCount.Value, Is.EqualTo(1));
            Assert.That(pickup.TryClaimServer(actor.NetworkObject), Is.Not.EqualTo(PickupResultCode.Accepted));
            var snapshot = actor.CaptureRuntimeSnapshot();
            var saved = CampaignPlayerSave.Capture(snapshot).Restore(snapshot.sessionPlayerId);
            serverInventory.MedkitCount.Value = 0;
            serverInventory.RestoreServer(saved);
            actor.Respawn(Vector3.zero, Quaternion.identity);
            Assert.That(serverInventory.MedkitCount.Value, Is.EqualTo(1), "checkpoint and respawn preserve inventory");
            yield return Until(() => clientInventory.MedkitCount.Value == 1, "restored count replicated");
            Debug.Log("SURVIVAL_NETWORK_MEDICAL_PASS");
        }

        [UnityTest]
        public IEnumerator TypedAmmoRejectsWithoutConsumingAndRefillsOnlyOwnedSlot()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), Vector3.up * 5, Quaternion.identity);
            player.GetComponent<PlayerMovement>().enabled = false;
            player.GetComponent<MouseMovement>().enabled = false;
            player.GetComponent<NetworkObject>().SpawnWithOwnership(client.LocalClientId);
            var manager = player.GetComponent<WeaponManager>();
            var fire = player.GetComponent<WeaponFireHandler>();
            var health = player.GetComponent<PlayerHealth>();
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(fire.NetworkObjectId), "armed client replica");
            var remoteManager = client.SpawnManager.SpawnedObjects[fire.NetworkObjectId].GetComponent<WeaponManager>();
            var primary = manager.GetWeapon(0).Data;
            var secondary = manager.GetWeapon(1).Data;
            Assert.That(manager.TryGetPrimaryCandidate(PrimaryWeaponId.Bucky, out var bucky), Is.True);
            var wrongWeapon = bucky.GetComponent<Weapon>().Data;
            Assert.That(wrongWeapon, Is.Not.EqualTo(primary));

            // Reuse a registered network prefab; only the server-side grant configuration matters.
            var pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Content + "MedkitPickup.prefab")).GetComponent<PickupItem>();
            SetField(pickup, "pickupType", PickupType.Ammo);
            SetField(pickup, "ammoAmount", 5);
            SetField(pickup, "ammoWeapon", wrongWeapon);
            SetField(pickup, "despawnDelay", 1f);
            pickup.NetworkObject.Spawn();
            var ledger = new PickupTransactionService(20);
            uint sequence = 0;
            PickupResultCode Claim() => ledger.Execute(client.LocalClientId, ++sequence, pickup.NetworkObjectId,
                host.ServerTime.Time, () => pickup.TryClaimServer(health.NetworkObject)).Code;
            Assert.That(Claim(), Is.EqualTo(PickupResultCode.AmmoUnavailable), "unowned candidate must not receive ammo");
            Assert.That(pickup.CanInteract && pickup.IsSpawned, Is.True);
            Assert.That(ledger.IsClaimed(pickup.NetworkObjectId), Is.False);
            SetField(pickup, "ammoWeapon", null);
            Assert.That(Claim(), Is.EqualTo(PickupResultCode.AmmoUnavailable), "missing target cannot refill equipped gun");
            SetField(pickup, "ammoWeapon", secondary);
            Assert.That(Claim(), Is.EqualTo(PickupResultCode.AmmoUnavailable), "full reserve leaves box");

            var snapshot = health.CaptureRuntimeSnapshot();
            snapshot.weaponSlot1.reserveAmmo = secondary.ReserveCapacity - 5;
            fire.RestoreServerSnapshot(snapshot);
            int primaryBefore = fire.CaptureWeaponSnapshot(0).reserveAmmo;
            Assert.That(manager.CurrentWeaponIndex, Is.Zero);
            Assert.That(Claim(), Is.EqualTo(PickupResultCode.Accepted));
            Assert.That(Claim(), Is.EqualTo(PickupResultCode.AlreadyClaimed));
            Assert.That(fire.CaptureWeaponSnapshot(0).reserveAmmo, Is.EqualTo(primaryBefore));
            Assert.That(fire.CaptureWeaponSnapshot(1).reserveAmmo, Is.EqualTo(secondary.ReserveCapacity));
            Assert.That(manager.CurrentWeaponIndex, Is.Zero, "pickup does not switch guns");
            remoteManager.RequestEquipWeaponServerRpc(1);
            yield return Until(() => manager.CurrentWeaponIndex == 1 && remoteManager.CurrentWeaponIndex == 1, "switch to refilled slot");
            yield return Until(() => Field<int>(remoteManager.GetWeapon(1), "reservedAmmo") == secondary.ReserveCapacity, "owner receives refilled reserve");
            Debug.Log("TYPED_AMMO_NETWORK_PASS");
        }

        [UnityTest]
        public IEnumerator AssistInterruptionSoloAndDenseHazards()
        {
            var target = teammate.GetComponent<SurvivalInventory>();
            var clientTarget = client.SpawnManager.SpawnedObjects[teammate.NetworkObjectId].GetComponent<SurvivalInventory>();
            var infection = teammate.GetComponent<PlayerInfectionController>();
            infection.SetInfectionServer(70);
            clientInventory.RequestUse(ConsumableKind.Antidote, clientTarget);
            yield return Until(() => serverInventory.IsUsingItem, "team antidote starts");
            teammate.transform.position = Vector3.right * 4;
            yield return Until(() => !serverInventory.IsUsingItem, "leaving range cancels assist");
            Assert.That(serverInventory.AntidoteCount.Value, Is.EqualTo(2));
            teammate.transform.position = Vector3.right * 2;
            Physics.SyncTransforms();
            clientInventory.RequestUse(ConsumableKind.Antidote, clientTarget);
            yield return Until(() => serverInventory.IsUsingItem, "team antidote restarts");
            Assert.That(Field<NetworkVariable<double>>(serverInventory, "useDeadline").Value - Field<NetworkVariable<double>>(serverInventory, "useStarted").Value, Is.EqualTo(3).Within(.001));
            yield return Until(() => !serverInventory.IsUsingItem, "three-second team antidote", 5);
            Assert.That(infection.CurrentInfection, Is.EqualTo(30));
            Assert.That(serverInventory.AntidoteCount.Value, Is.EqualTo(1));
            Assert.That(target.AntidoteCount.Value, Is.EqualTo(2), "helper pays for treatment");
            clientInventory.RequestUse(ConsumableKind.Antidote, clientTarget);
            yield return Until(() => serverInventory.IsUsingItem, "assist before target death");
            teammate.TakeDamage(1000);
            yield return Until(() => !serverInventory.IsUsingItem, "dead target cancels assist");
            Assert.That(serverInventory.AntidoteCount.Value, Is.EqualTo(1));
            teammate.Respawn(new Vector3(20, 0, 0), Quaternion.identity);

            client.Shutdown();
            yield return Until(() => host.ConnectedClients.Count == 1, "solo host after disconnect");
            teammate.TakeDamage(25);
            target.RequestUse(ConsumableKind.Medkit);
            yield return Until(() => target.IsUsingItem, "solo medical use");
            Field<NetworkVariable<double>>(target, "useDeadline").Value = target.ServerNow;
            yield return Until(() => !target.IsUsingItem, "solo heal completion");
            Assert.That(teammate.CurrentHealth, Is.EqualTo(100));

            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = new Vector3(20, 1.5f, 2);
            obstacle.transform.localScale = new Vector3(3, 3, .2f);
            var body = obstacle.AddComponent<Rigidbody>();
            body.useGravity = false; body.mass = 100; body.linearVelocity = Vector3.right;
            Physics.SyncTransforms();
            target.RequestThrow(Vector3.forward);
            Assert.That(target.FragGrenadeCount.Value, Is.EqualTo(2));
            yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "moving obstacle detonates before fuse", 1.5f);
            Object.Destroy(obstacle); yield return null;
            teammate.Respawn(new Vector3(20, 0, 0), Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.transform.position = new Vector3(20, .25f * (i + 1), 2 + i);
                step.transform.localScale = new Vector3(3, .5f * (i + 1), 1);
            }
            yield return new WaitForSeconds(.55f);
            Physics.SyncTransforms();
            target.RequestThrow((Vector3.forward - Vector3.up * .5f).normalized);
            Assert.That(target.FragGrenadeCount.Value, Is.EqualTo(1));
            yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "stairs detonate before fuse", 1.5f);

            var enemies = new System.Collections.Generic.List<EnemyHealth>();
            for (int i = 0; i < 24; i++)
            {
                Vector3 offset = Quaternion.Euler(0, i * 15, 0) * Vector3.forward * 2.5f;
                var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tests/Fixtures/SurvivalEnemyProbe.prefab"), new Vector3(40, 0, 0) + offset, Quaternion.identity);
                go.GetComponent<NetworkObject>().Spawn();
                enemies.Add(go.GetComponent<EnemyHealth>());
            }
            Physics.SyncTransforms();
            var victims = new System.Collections.Generic.HashSet<Component>();
            SurvivalDamage.Apply(new Vector3(40, 1, 0), 3, 0, true, victims);
            SurvivalDamage.Apply(new Vector3(40, 1, 0), 3, 0, true, victims);
            Assert.That(victims.Count, Is.EqualTo(24));
            foreach (var enemy in enemies) Assert.That(enemy.CurrentHealth, Is.EqualTo(88), "dense victims each damaged only once");
            Debug.Log("SURVIVAL_EDGE_CASES_PASS");
        }

        [UnityTest]
        public IEnumerator BothGrenadesLaunchOnServerAndCompleteLongLobsBeforeFuse()
        {
            teammate.transform.position = Vector3.right * 50;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -.5f, 50);
            floor.transform.localScale = new Vector3(200, 1, 200);
            Physics.SyncTransforms();
            foreach (var kind in new[] { ThrowableKind.Frag, ThrowableKind.Incendiary })
            {
                if (kind == ThrowableKind.Incendiary)
                {
                    SetField(serverInventory, "throwDefinition", AssetDatabase.LoadAssetAtPath<GrenadeThrowDefinition>(
                        "Assets/FPS/Features/Weapons/Content/Grenade/CloveGrenadeThrow.asset"));
                    Assert.That(serverInventory.ThrowDefinition.IsValid, Is.True, "Also exercise the animated release path.");
                    clientInventory.CycleThrowable();
                    yield return Until(() => clientInventory.SelectedThrowable.Value == kind, "incendiary selected");
                }
                clientInventory.RequestThrow(Quaternion.Euler(-35f, 0, 0) * Vector3.forward);
                yield return Until(() => host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "server launch");
                var projectile = host.SpawnManager.SpawnedObjectsList.Single(o => o.GetComponent<SurvivalProjectile>() != null);
                Vector3 velocity = Field<Vector3>(projectile.GetComponent<SurvivalProjectile>(), "launchVelocity");
                Assert.That(velocity.magnitude, Is.EqualTo(28f).Within(.001f));
                Assert.That(velocity.y, Is.EqualTo(velocity.z).Within(.001f), "35 degree aim plus 10 degree overhand elevation.");
                Assert.That(projectile.GetComponent<Rigidbody>().isKinematic, Is.False);
                ulong id = projectile.NetworkObjectId;
                yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(id), "observer projectile");
                Assert.That(client.SpawnManager.SpawnedObjects[id].GetComponent<Rigidbody>().isKinematic, Is.True);
                double launchedAt = serverInventory.ServerNow;
                float furthest = projectile.transform.position.z;
                while (host.SpawnManager.SpawnedObjects.ContainsKey(id) && serverInventory.ServerNow - launchedAt < 7)
                {
                    furthest = Mathf.Max(furthest, projectile.GetComponent<Rigidbody>().position.z);
                    DisableClientColliders();
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(host.SpawnManager.SpawnedObjects.ContainsKey(id), Is.False);
                Assert.That(furthest, Is.InRange(75f, 85f), kind + " lands after a long ballistic arc");
                Assert.That(serverInventory.ServerNow - launchedAt, Is.LessThan(5.5), "Impact completes before the six-second fuse.");
                Assert.That(kind == ThrowableKind.Frag ? serverInventory.FragGrenadeCount.Value : serverInventory.IncendiaryGrenadeCount.Value, Is.EqualTo(2));
                TestContext.WriteLine($"{kind}: authoritative range {furthest:F2} m, flight {serverInventory.ServerNow - launchedAt:F2} s.");
            }
            Object.Destroy(floor);
        }

        [UnityTest]
        public IEnumerator GrenadeAuthorityOcclusionAndFireNonStacking()
        {
            teammate.transform.position = Vector3.right * 50;
            // Duplicate client RPC after cooldown must still consume exactly once.
            Invoke(clientInventory, "RequestThrowServerRpc", ThrowableKind.Frag, Vector3.forward, (uint)1);
            yield return Until(() => serverInventory.FragGrenadeCount.Value == 2, "server grenade acceptance");
            Assert.That(host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), Is.True);
            yield return Until(() => client.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "projectile replicated");
            yield return new WaitForSeconds(.6f);
            Invoke(clientInventory, "RequestThrowServerRpc", ThrowableKind.Frag, Vector3.forward, (uint)1);
            yield return new WaitForSeconds(.2f);
            Assert.That(serverInventory.FragGrenadeCount.Value, Is.EqualTo(2), "duplicate packet");
            yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "fuse despawn", 8);

            var enemyGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tests/Fixtures/SurvivalEnemyProbe.prefab"), new Vector3(20, 0, 2), Quaternion.identity);
            enemyGo.GetComponent<NetworkObject>().Spawn();
            var enemy = enemyGo.GetComponent<EnemyHealth>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(20, 1, 1); wall.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms();
            Vector3 blast = new Vector3(20, 1, 0);
            SurvivalDamage.Apply(blast, 4, client.LocalClientId, false);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100), "wall blocks blast");
            Object.Destroy(wall); yield return null; DisableClientColliders(); Physics.SyncTransforms();
            float distance = Vector3.Distance(blast, enemyGo.GetComponent<Collider>().ClosestPoint(blast));
            // Multiple hurt colliders still take one hit.
            var extraCollider = enemyGo.AddComponent<CapsuleCollider>();
            extraCollider.center = Vector3.up - Vector3.forward * .5f; extraCollider.radius = .3f; extraCollider.height = 1.8f;
            Physics.SyncTransforms();
            distance = Mathf.Min(distance, Vector3.Distance(blast, extraCollider.ClosestPoint(blast)));
            SurvivalDamage.Apply(blast, 4, client.LocalClientId, false);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(100 - SurvivalRules.FragDamage(distance)).Within(.1), "radial falloff and collider deduplication");
            enemy.ResetHealth();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Content + "FireZone.prefab").GetComponent<SurvivalFireZone>();
            SurvivalFireZone.CreateOrExtend(prefab, blast, client.LocalClientId, host);
            yield return Until(() => host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalFireZone>() != null), "fire spawn");
            var zone = host.SpawnManager.SpawnedObjectsList.Select(o => o.GetComponent<SurvivalFireZone>()).First(z => z != null);
            double firstExpiry = Field<NetworkVariable<double>>(zone, "expires").Value;
            yield return new WaitForSeconds(.55f);
            SurvivalFireZone.CreateOrExtend(prefab, blast + Vector3.right, 0, host);
            Assert.That(host.SpawnManager.SpawnedObjectsList.Count(o => o.GetComponent<SurvivalFireZone>() != null), Is.EqualTo(1), "overlapping throwers share fire zone");
            Assert.That(Field<NetworkVariable<double>>(zone, "expires").Value, Is.GreaterThan(firstExpiry));
            float before = enemy.CurrentHealth;
            yield return new WaitForSeconds(.51f);
            Assert.That(before - enemy.CurrentHealth, Is.EqualTo(12).Within(.01), "one fire hit per half-second");
            Field<NetworkVariable<double>>(zone, "expires").Value = host.ServerTime.Time;
            yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalFireZone>() != null), "fire expiration");
            Debug.Log("SURVIVAL_NETWORK_GRENADE_PASS");
        }
    }
}

#endif
