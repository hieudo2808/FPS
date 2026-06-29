using NUnit.Framework;
using UnityEngine;
using FPS;
using System.Reflection;

namespace FPS.Tests
{
    public class InfluenceMapCoroutineTests
    {
        [Test]
        public void BakeNavMeshCache_ReturnsIEnumerator()
        {
            var method = typeof(InfluenceMapManager).GetMethod("BakeNavMeshCache", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "BakeNavMeshCache should exist.");
            Assert.AreEqual(typeof(System.Collections.IEnumerator), method.ReturnType, "BakeNavMeshCache should return IEnumerator to prevent main thread blocking.");
        }
    }
}
