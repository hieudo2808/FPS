using NUnit.Framework;
using UnityEngine;
using FPS;

namespace FPS.Tests
{
    public class SettingsUITests
    {
        [Test]
        public void SettingsUI_ExistsInNamespace()
        {
            var type = System.Type.GetType("FPS.SettingsUI, Assembly-CSharp");
            Assert.IsNotNull(type, "SettingsUI script should exist in FPS namespace.");
        }
    }
}
