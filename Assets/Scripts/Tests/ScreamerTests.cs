using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class ScreamerTests
    {
        private GameObject gameObject;
        private SI_Screamer screamer;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Screamer");
            screamer = gameObject.AddComponent<SI_Screamer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ScreamRoutine_YieldsAndSetsScreamingState()
        {
            screamer.UseAbility();

            var method = typeof(SI_Screamer).GetMethod("ScreamRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var coroutine = (IEnumerator)method.Invoke(screamer, null);
            bool hasNext = coroutine.MoveNext();

            Assert.True(screamer.IsScreaming);
            Assert.True(hasNext, "ScreamRoutine must yield during the telegraph/scream window.");
        }

        [Test]
        public void Screamer_DoesNotHaveEmptyUpdateOverride()
        {
            var method = typeof(SI_Screamer).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.Null(method, "SI_Screamer should use the server-authoritative base update loop, not a duplicate local brain.");
        }
    }
}
