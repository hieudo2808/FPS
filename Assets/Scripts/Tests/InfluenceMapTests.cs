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
        // Test 3: GetBestSpawnPosition tra ve Vector3.zero khi cache rong
        //         (khong NavMesh trong EditMode => cache rong => fallback dung)
        // -------------------------------------------------------
        [Test]
        public void TestInfluenceMap_GetBestSpawnPosition_ReturnsFallbackWhenCacheEmpty()
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

            // GetBestSpawnPosition phai tra ve Vector3.zero khi cache rong
            Vector3 result = mgr.GetBestSpawnPosition();
            Assert.AreEqual(Vector3.zero, result,
                "GetBestSpawnPosition phai tra ve Vector3.zero khi cachedNavMeshPoints rong");

            Object.DestroyImmediate(go);
        }
    }
}
