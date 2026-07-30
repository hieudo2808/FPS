using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace FPS.Tests
{
    public class RubberBandingTests
    {
        [Test]
        public void RubberBandingSystem_HasIsEnabledFlag()
        {
            var field = typeof(RubberBandingSystem).GetField("isEnabled", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, "RubberBandingSystem should have a public isEnabled flag.");
            Assert.AreEqual(typeof(bool), field.FieldType, "isEnabled should be a boolean.");
        }
    }
}
