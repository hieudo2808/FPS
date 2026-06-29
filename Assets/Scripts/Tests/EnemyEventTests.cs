using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace FPS.Tests
{
    public class EnemyEventTests
    {
        [Test]
        public void EnemyHealth_HasOnDeathServerEvent()
        {
            var eventInfo = typeof(EnemyHealth).GetEvent("OnDeathServer");
            Assert.IsNotNull(eventInfo, "EnemyHealth should have OnDeathServer event to decouple from EnemyAI.");
        }
    }
}
