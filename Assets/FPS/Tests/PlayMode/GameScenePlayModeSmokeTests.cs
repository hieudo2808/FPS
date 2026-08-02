using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class GameScenePlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator BuildSettingsScenes_LoadInPlayModeWithoutRuntimeErrors()
        {
            yield return LoadAndAssertScene("Assets/FPS/Scenes/MainMenu.unity", "MainMenu");
            yield return LoadAndAssertScene("Assets/FPS/Scenes/LobbyScene.unity", "LobbyScene");
            yield return LoadAndAssertScene("Assets/FPS/Scenes/GameScene.unity", "GameScene");

            Assert.NotNull(Object.FindFirstObjectByType<AttackSlotManager>(FindObjectsInactive.Include),
                "GameScene should contain AttackSlotManager so enemy slot coordination can run in gameplay.");
            Assert.NotNull(Object.FindFirstObjectByType<NetworkGameManager>(FindObjectsInactive.Include),
                "GameScene should contain NetworkGameManager so network/session flow can own player spawning.");
        }

        private static IEnumerator LoadAndAssertScene(string path, string expectedName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            Assert.NotNull(load, $"Could not start loading scene at {path}.");

            while (!load.isDone)
                yield return null;

            yield return null;

            Scene activeScene = SceneManager.GetActiveScene();
            Assert.AreEqual(expectedName, activeScene.name);
            Assert.IsTrue(activeScene.isLoaded);
        }
    }
}
