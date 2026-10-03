#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>Real NGO host/client transport in one process. Client physics copies are excluded.</summary>
    public sealed class SurvivalNetworkTests
    {
        const string Content = "Assets/FPS/Features/Survival/Content/Prefabs/";
        const string PlayerPrefab = "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab";
        static readonly string[] PlayerNames = { "Clove", "Brimstone", "Gekko", "Sage" };
        static string PlayerPath(string character) => $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        NetworkManager host, client;
        PlayerHealth actor, teammate;
        SurvivalInventory serverInventory, clientInventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("SurvivalRuntime");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            yield return null;
            host = CreateManager("Survival test host");
            client = CreateManager("Survival test client");
            host.SetSingleton();
            Assert.That(host.StartHost(), Is.True);
            Assert.That(client.StartClient(), Is.True);
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
                Content + "GrenadeProjectile.prefab", Content + "FireZone.prefab", Content + "MedkitPickup.prefab" }.Concat(PlayerNames.Select(PlayerPath)))
                manager.AddNetworkPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            return manager;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (client != null) client.Shutdown();
            if (host != null) host.Shutdown();
            yield return null;
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
            if (client == null || client.SpawnManager == null) return;
            foreach (var obj in client.SpawnManager.SpawnedObjectsList)
                foreach (var collider in obj.GetComponentsInChildren<Collider>()) collider.enabled = false;
        }

        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        void FinishSoon() => Field<NetworkVariable<double>>(serverInventory, "useDeadline").Value = serverInventory.ServerNow;

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
        public IEnumerator ClientPlayersThrowWithAllFiveWeaponsAndPreserveLegs([ValueSource(nameof(PlayerNames))] string character)
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

            for (int weapon = 0; weapon < 5; weapon++)
            {
                var primary = (PrimaryWeaponId)Mathf.Min(weapon, 3);
                int slot = weapon == 4 ? 1 : 0;
                string label = weapon == 4 ? "Classic" : primary.ToString();
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
                Assert.That(inventory.FragGrenadeCount.Value, Is.EqualTo(3), label + " stock retained in windup");
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
            Debug.Log(character + "_CLIENT_FIVE_GUNS_LEGS_PASS");
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
            Assert.That(Field<GameObject>(presentation, "item").activeInHierarchy, Is.True);
            Assert.That(Field<AudioSource>(presentation, "treatmentAudio").isPlaying, Is.True);
            CapturePlayer(realPlayer, "Survival-Medkit-runtime");
            yield return Until(() => !inventory.IsUsingItem, "authored player heals", 6);
            Assert.That(health.CurrentHealth, Is.EqualTo(95));
            // Presentation consumes server state in LateUpdate, after the test coroutine resumes.
            yield return null;
            Assert.That(Field<GameObject>(presentation, "item").activeSelf, Is.False);
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

        void CapturePlayer(GameObject player, string name)
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
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                var light = new GameObject("Presentation test key light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 2;
                light.transform.rotation = Quaternion.Euler(25, -30, 0);
                mouse.BodyCam.targetTexture = target; mouse.BodyCam.Render();
                mouse.WeaponCam.targetTexture = target; mouse.WeaponCam.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
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
            yield return Until(() => !host.SpawnManager.SpawnedObjectsList.Any(o => o.GetComponent<SurvivalProjectile>() != null), "fuse despawn", 4);

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
