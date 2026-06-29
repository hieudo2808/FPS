using NUnit.Framework;
using UnityEngine;
using FPS;

namespace FPS.Tests
{
    public class InGameMenuUITests
    {
        [Test]
        public void InGameMenuUI_ExistsInNamespace()
        {
            var type = System.Type.GetType("FPS.InGameMenuUI, Assembly-CSharp");
            Assert.IsNotNull(type, "InGameMenuUI script should exist in FPS namespace.");
        }
    }
}
