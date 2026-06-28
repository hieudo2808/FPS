using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace FPS.Tests
{
    /// <summary>
    /// Regression tests cho RubberBandingSystem.
    /// Dam bao toi uu hoa chuyen UpdateCatchUpSpeed sang Coroutine
    /// khong lam hong logic tang toc zombie dang o phia sau Player.
    /// </summary>
    public class RubberBandingTests
    {
        [Test]
        public void TestCatchUpSpeed_ZombieBehindPlayer_GetsSpeedBoost()
        {
            // Kiem tra UpdateCatchUpSpeed van ton tai sau khi refactor
            var updateMethod = typeof(RubberBandingSystem).GetMethod(
                "UpdateCatchUpSpeed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(updateMethod,
                "UpdateCatchUpSpeed phai ton tai nhu private method tren RubberBandingSystem");

            // Kiem tra CatchUpSpeedLoop Coroutine da duoc them sau Task 8
            var coroutineMethod = typeof(RubberBandingSystem).GetMethod(
                "CatchUpSpeedLoop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(coroutineMethod,
                "CatchUpSpeedLoop Coroutine phai ton tai sau khi toi uu hoa Task 8");
        }

        [Test]
        public void TestRubberBanding_CatchUpSpeedLoop_ReturnsIEnumerator()
        {
            // Dam bao CatchUpSpeedLoop la IEnumerator (Coroutine hop le)
            var coroutineMethod = typeof(RubberBandingSystem).GetMethod(
                "CatchUpSpeedLoop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(coroutineMethod,
                "Sau toi uu Task 8, CatchUpSpeedLoop phai ton tai nhu private IEnumerator method");

            Assert.AreEqual(
                typeof(System.Collections.IEnumerator),
                coroutineMethod.ReturnType,
                "CatchUpSpeedLoop phai tra ve IEnumerator");
        }
    }
}
