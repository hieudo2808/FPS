using NUnit.Framework;
using UnityEngine;
using FPS;
using System.Reflection;
using Unity.Netcode;

namespace FPS.Tests
{
    public class LobbyDifficultyTests
    {
        [Test]
        public void NetworkGameManager_HasSelectedDifficultyProperty()
        {
            var prop = typeof(NetworkGameManager).GetProperty("SelectedDifficulty");
            Assert.IsNotNull(prop, "NetworkGameManager should have SelectedDifficulty property to pass data from Lobby to Game.");
            Assert.AreEqual(typeof(DifficultyLevel), prop.PropertyType, "SelectedDifficulty should be of type DifficultyLevel.");
        }

        [Test]
        public void WaitingRoomManager_HasLobbyDifficultyNetworkVariable()
        {
            var field = typeof(WaitingRoomManager).GetProperty("LobbyDifficulty");
            Assert.IsNotNull(field, "WaitingRoomManager should have LobbyDifficulty property.");
            Assert.AreEqual(typeof(NetworkVariable<DifficultyLevel>), field.PropertyType, "LobbyDifficulty should be a NetworkVariable<DifficultyLevel>.");
        }
    }
}
