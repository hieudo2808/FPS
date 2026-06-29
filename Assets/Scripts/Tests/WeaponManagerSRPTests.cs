using NUnit.Framework;
using UnityEngine;
using System;
using System.Reflection;

namespace FPS.Tests
{
    public class WeaponManagerSRPTests
    {
        [Test]
        public void WeaponManager_ShouldNotHave_UpdateMethod()
        {
            var updateMethod = typeof(WeaponManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNull(updateMethod, "WeaponManager should not have Update method (SRP Violation). It should be moved to WeaponInputHandler.");
        }

        [Test]
        public void WeaponFireHandler_Exists_AndHasRPCs()
        {
            Type fireHandlerType = Type.GetType("FPS.WeaponFireHandler, Assembly-CSharp") ?? Type.GetType("FPS.WeaponFireHandler, FPS");
            Assert.IsNotNull(fireHandlerType, "WeaponFireHandler class is missing.");

            var requestFireMethod = fireHandlerType.GetMethod("RequestFireServerRpc", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(requestFireMethod, "WeaponFireHandler should contain RequestFireServerRpc.");
        }

        [Test]
        public void WeaponInputHandler_Exists()
        {
            Type inputHandlerType = Type.GetType("FPS.WeaponInputHandler, Assembly-CSharp") ?? Type.GetType("FPS.WeaponInputHandler, FPS");
            Assert.IsNotNull(inputHandlerType, "WeaponInputHandler class is missing.");
        }
    }
}
