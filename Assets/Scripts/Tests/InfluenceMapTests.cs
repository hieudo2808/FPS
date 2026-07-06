using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace FPS.Tests
{
    /// <summary>
    /// Regression tests cho Task 10: Caching NavMesh points trong InfluenceMapManager.
    /// Dam bao GetBestSpawnPosition() khong goi NavMesh.SamplePosition trong vong lap moi lan duoc goi.
    /// </summary>
    public class InfluenceMapTests
    {
        // -------------------------------------------------------
        // Test 1: cachedNavMeshPoints field ton tai
        // -------------------------------------------------------
        [Test]
        public void TestInfluenceMap_CachedNavMeshPoints_FieldExists()
        {
            var field = typeof(InfluenceMapManager).GetField(
                "cachedNavMeshPoints",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                "InfluenceMapManager phai co private field 'cachedNavMeshPoints' " +
                "de luu cache cac diem NavMesh hop le (Task 10)");

            // Phai la List<Vector3>
            Assert.AreEqual(typeof(List<Vector3>), field.FieldType,
                "cachedNavMeshPoints phai la List<Vector3>");
        }

        // -------------------------------------------------------
        // Test 2: BakeNavMeshCache method ton tai (duoc goi tu InitializeGrid/Start)
        // -------------------------------------------------------
        [Test]
        public void TestInfluenceMap_BakeNavMeshCache_MethodExists()
        {
            var method = typeof(InfluenceMapManager).GetMethod(
                "BakeNavMeshCache",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "InfluenceMapManager phai co method 'BakeNavMeshCache()' " +
                "duoc goi mot lan tai Start() de dien cachedNavMeshPoints (Task 10)");
        }

        // -------------------------------------------------------
        // Test 3: TryGetBestSpawnPosition bao loi ro khi cache rong
        //         (khong NavMesh trong EditMode => cache rong => caller phai skip/fallback co y thuc)
        // -------------------------------------------------------
        [Test]
        public void TestInfluenceMap_TryGetBestSpawnPosition_ReturnsFalseWhenCacheEmpty()
        {
            var go = new GameObject("InfluenceMapManager");
            var mgr = go.AddComponent<InfluenceMapManager>();

            // Khoi tao grid thu cong (Start khong chay trong EditMode test)
            var initMethod = typeof(InfluenceMapManager).GetMethod(
                "InitializeGrid",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(initMethod, "InitializeGrid phai ton tai");
            initMethod.Invoke(mgr, null);

            // Goi BakeNavMeshCache - se tra ve list rong vi EditMode khong co NavMesh
            var bakeMethod = typeof(InfluenceMapManager).GetMethod(
                "BakeNavMeshCache",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(bakeMethod, "BakeNavMeshCache phai ton tai");
            bakeMethod.Invoke(mgr, null);

            bool found = mgr.TryGetBestSpawnPosition(out Vector3 result);
            Assert.False(found, "TryGetBestSpawnPosition phai tra ve false khi cachedNavMeshPoints rong");
            Assert.AreEqual(Vector3.zero, result, "Failed TryGetBestSpawnPosition must not leak a fallback position.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryGetFairPressurePositionNearPlayer_UsesFairDistanceBand()
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = Vector3.zero;
            player.transform.forward = Vector3.forward;

            var profilerGo = new GameObject("PlayerProfiler");
            var profiler = profilerGo.AddComponent<PlayerProfiler>();
            typeof(PlayerProfiler)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(profiler, null);

            var profilesField = typeof(PlayerProfiler).GetField("playerProfiles", BindingFlags.Instance | BindingFlags.NonPublic);
            profilesField.SetValue(profiler, new List<PlayerProfile>
            {
                new PlayerProfile
                {
                    playerTransform = player.transform,
                    playerIndex = 0,
                    lookDirection = Vector3.forward,
                    currentHealth = 100f
                }
            });

            var go = new GameObject("InfluenceMapManager");
            var mgr = go.AddComponent<InfluenceMapManager>();
            typeof(InfluenceMapManager)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(mgr, null);

            typeof(InfluenceMapManager)
                .GetField("cachedNavMeshPoints", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mgr, new List<Vector3>
                {
                    new Vector3(0f, 0f, -20f),
                    new Vector3(0f, 0f, -35f),
                    new Vector3(0f, 0f, -80f)
                });

            var method = typeof(InfluenceMapManager).GetMethod(
                "TryGetFairPressurePositionNearPlayer",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method, "InfluenceMapManager should expose a fair pressure API without behindOnly.");

            object[] args = { 0, Vector3.zero };
            bool found = (bool)method.Invoke(mgr, args);
            Vector3 position = (Vector3)args[1];

            Assert.True(found, "A fair near-player pressure point should be selected from cached candidates.");
            float distance = Vector3.Distance(Vector3.zero, position);
            Assert.GreaterOrEqual(distance, 30f, "Pressure spawn must be outside the minimum fair distance plus margin.");
            Assert.LessOrEqual(distance, 50f, "Pressure spawn should stay near enough to apply pressure instead of falling back globally.");

            var instanceProp = typeof(PlayerProfiler).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            instanceProp.SetValue(null, null);
            var mapInstanceProp = typeof(InfluenceMapManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            mapInstanceProp.SetValue(null, null);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(profilerGo);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void InfluenceMap_DoesNotExposeBehindOnlySpawnApi()
        {
            Assert.Null(typeof(InfluenceMapManager).GetMethod(
                    "GetSpawnPositionNearPlayer",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(bool) },
                    null),
                "Behind-only near-player spawning must not remain as a public live API.");

            Assert.Null(typeof(InfluenceMapManager).GetMethod(
                    "TryGetSpawnPositionNearPlayer",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(bool), typeof(Vector3).MakeByRefType() },
                    null),
                "Callers should use TryGetFairPressurePositionNearPlayer instead of behindOnly APIs.");
        }
    }
}
