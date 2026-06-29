using NUnit.Framework;
using UnityEngine;
using FPS;
using System.Reflection;

namespace FPS.Tests
{
    public class WaitingRoomUITests
    {
        [Test]
        public void WaitingRoomUI_HasDifficultyDropdownReference()
        {
            var field = typeof(WaitingRoomUI).GetField("difficultyDropdown", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "WaitingRoomUI should have a difficultyDropdown field.");
        }
    }
}
