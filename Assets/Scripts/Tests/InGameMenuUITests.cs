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
            Assert.AreEqual("FPS", typeof(InGameMenuUI).Namespace);
        }
    }
}
