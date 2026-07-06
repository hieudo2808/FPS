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
            Assert.AreEqual("FPS", typeof(SettingsUI).Namespace);
        }
    }
}
