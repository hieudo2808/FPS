using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>Real NGO host/client transport in one process. Client physics copies are excluded.</summary>
    public sealed class SurvivalNetworkTests
    {
        const string Content = "Assets/FPS/Features/Survival/Content/Prefabs/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        NetworkManager host, client;
        PlayerHealth actor, teammate;
        SurvivalInventory serverInventory, clientInventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Tests/Fixtures/SurvivalRuntime.unity");
            AssetDatabase.SaveAssets();
            yield return new EnterPlayMode();
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
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tests/Fixtures/SurvivalProbe.prefab");
            manager.NetworkConfig.Prefabs = new NetworkPrefabs();
            foreach (var path in new[] { "Assets/Tests/Fixtures/SurvivalProbe.prefab", "Assets/Tests/Fixtures/SurvivalEnemyProbe.prefab",
                Content + "GrenadeProjectile.prefab", Content + "FireZone.prefab", Content + "MedkitPickup.prefab" })
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
            yield return new ExitPlayMode();
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
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
            enemyGo.AddComponent<CapsuleCollider>().center = Vector3.up;
            Physics.SyncTransforms();
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
