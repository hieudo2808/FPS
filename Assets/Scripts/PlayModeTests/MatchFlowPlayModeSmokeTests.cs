using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class MatchFlowPlayModeSmokeTests
    {
        private static int sceneCounter;

        private readonly List<Object> objectsToDestroy = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Fixture trước (GameScenePlayModeSmokeTests) để GameScene load lại, trong đó
            // có NetworkMatchStateManager (Lobby) và geometry chặn ray. Chạy trên scene
            // rỗng để test không phụ thuộc thứ tự fixture.
            Scene fresh = SceneManager.CreateScene($"MatchFlowSmokeScene_{sceneCounter++}");
            SceneManager.SetActiveScene(fresh);

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != fresh && scene.isLoaded)
                    SceneManager.UnloadSceneAsync(scene);
            }

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (objectsToDestroy[i] != null)
                    Object.Destroy(objectsToDestroy[i]);
            }

            objectsToDestroy.Clear();
            InputManager.MatchInputBlocked = false;
        }

        [UnityTest]
        public IEnumerator Warmup_AutoTransitionsToPlaying_AndUnblocksInput()
        {
            GameObject managerGo = CreateGameObject("MatchStateRuntime");
            managerGo.AddComponent<NetworkObject>();
            NetworkMatchStateManager manager = managerGo.AddComponent<NetworkMatchStateManager>();

            yield return null;

            // Đặt warmup gần hết hạn để test không phải chờ đủ WarmupSeconds thật.
            manager.SetStateForTests(
                NetworkMatchState.Warmup,
                Time.timeAsDouble - (NetworkGameplayPolicy.WarmupSeconds - 0.2));

            Assert.False(NetworkMatchStateManager.IsGameplayActive,
                "Warmup must block gameplay.");
            Assert.True(InputManager.GameplayInputBlocked,
                "Warmup must block gameplay input.");

            float timeout = Time.realtimeSinceStartup + 3f;
            while (manager.State != NetworkMatchState.Playing && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.AreEqual(NetworkMatchState.Playing, manager.State,
                "Server Update loop must auto-advance Warmup -> Playing after WarmupSeconds.");
            Assert.True(NetworkMatchStateManager.IsGameplayActive);
            Assert.False(InputManager.GameplayInputBlocked,
                "Playing must unblock gameplay input.");
        }

        [UnityTest]
        public IEnumerator LagCompensatedFire_HitsRewoundTarget_AndClampRejectsStaleShots()
        {
            WeaponFireHandler fireHandler = CreateRuntimeWeaponRig(out WeaponData data);
            data.magazineSize = 10;
            data.totalAmmo = 10;
            data.fireRate = 0f;
            data.damage = 10f;
            data.damageType = DamageType.Bullet;
            data.hitMask = Physics.DefaultRaycastLayers;
            fireHandler.InitializeServerWeaponStateForTests(10, 0);

            Vector3 oldPosition = new Vector3(0f, 0f, 5f);
            Vector3 newPosition = new Vector3(50f, 0f, 5f);

            GameObject targetGo = CreateGameObject("RewoundTarget");
            AttributedDamageReceiver receiver = targetGo.AddComponent<AttributedDamageReceiver>();
            HitboxSegment segment = targetGo.AddComponent<HitboxSegment>();
            typeof(HitboxSegment)
                .GetField("zone", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(segment, HitboxZone.Head);
            targetGo.AddComponent<BoxCollider>();
            LagCompensatedTarget lagTarget = targetGo.AddComponent<LagCompensatedTarget>();

            targetGo.transform.position = oldPosition;
            Physics.SyncTransforms();
            lagTarget.RefreshSegments();

            // Ghi history tường minh: mục tiêu ở vị trí cũ 100ms trước, hiện tại đã sang vị trí mới.
            double now = LagCompensationManager.GetServerTime();
            lagTarget.SampleForTests(now - 0.1);

            targetGo.transform.position = newPosition;
            Physics.SyncTransforms();
            lagTarget.SampleForTests(now);

            // Shot đến trễ ~100ms (ping ~100-150ms): server phải rewind và công nhận headshot.
            Assert.IsTrue(fireHandler.ProcessFireServerForTests(
                Vector3.zero, Vector3.forward, clientShotLocalTime: now - 0.1));
            Assert.AreEqual(1, receiver.Hits.Count,
                "Server rewind must confirm a shot aimed at the target's old position.");
            Assert.AreEqual(HitboxZone.Head, receiver.Hits[0].hitZone);
            Assert.AreEqual(20f, receiver.Hits[0].amount, 0.001f,
                "Head hit must apply the x2 multiplier server-side.");

            yield return null;

            // Shot quá cũ: mục tiêu đã rời vị trí cũ hơn MaxRewindSeconds, history quanh
            // mốc rewind clamp chỉ còn vị trí mới nên phát bắn không được ăn hit.
            now = LagCompensationManager.GetServerTime();
            lagTarget.SampleForTests(now - 0.5);
            targetGo.transform.position = newPosition;
            Physics.SyncTransforms();
            lagTarget.SampleForTests(now - 0.2);
            lagTarget.SampleForTests(now - 0.1);
            lagTarget.SampleForTests(now);

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(
                Vector3.zero, Vector3.forward, clientShotLocalTime: now - 0.5),
                "Stale shots still consume ammo server-side.");
            Assert.AreEqual(1, receiver.Hits.Count,
                "Shots older than MaxRewindSeconds must not hit a position the target left long ago.");
        }

        private WeaponFireHandler CreateRuntimeWeaponRig(out WeaponData weaponData)
        {
            GameObject player = CreateGameObject("MatchFlowWeaponPlayer");
            player.AddComponent<NetworkObject>();
            WeaponManager weaponManager = player.AddComponent<WeaponManager>();
            WeaponFireHandler fireHandler = player.AddComponent<WeaponFireHandler>();
            SetNetworkServer(weaponManager, true);
            SetNetworkServer(fireHandler, true);

            GameObject weaponObject = CreateGameObject("MatchFlowWeapon");
            weaponObject.transform.SetParent(player.transform);
            Weapon weapon = weaponObject.AddComponent<Weapon>();
            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(weaponData);

            SetPrivateField(weapon, "weaponData", weaponData);
            SetPrivateField(weaponManager, "weapons", new List<GameObject> { weaponObject });

            return fireHandler;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }

        private static void SetNetworkServer(NetworkBehaviour behaviour, bool isServer)
        {
            PropertyInfo property = typeof(NetworkBehaviour).GetProperty(
                "IsServer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            property.SetValue(behaviour, isServer);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private sealed class AttributedDamageReceiver : MonoBehaviour, IAttributedDamageable
        {
            public readonly List<DamageInfo> Hits = new List<DamageInfo>();

            public bool IsDead => false;

            public void TakeDamage(float amount)
            {
                Hits.Add(new DamageInfo(amount));
            }

            public void TakeDamage(DamageInfo damageInfo)
            {
                Hits.Add(damageInfo);
            }
        }
    }
}
