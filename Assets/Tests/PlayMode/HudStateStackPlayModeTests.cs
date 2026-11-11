using System.Collections;
using FPS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FPS.Tests
{
    public sealed class HudStateStackPlayModeTests
    {
        [UnityTest]
        public IEnumerator BackCallbackCannotReopenAndSubsystemResetReleasesInput()
        {
            UiStateStack.Clear();
            var root = new GameObject("Stack cleanup fixture");
            var pause = new GameObject("Pause"); pause.transform.SetParent(root.transform);
            var settings = new GameObject("Settings"); settings.transform.SetParent(root.transform);
            try
            {
                UiStateStack.Push(UiScreen.Pause, pause, () => UiStateStack.Push(UiScreen.Settings, settings, () => {}));
                yield return null;
                Assert.That(UiStateStack.TryBack(), Is.True);
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Gameplay));
                Assert.That(UiStateStack.TryBack(), Is.True, "The same frame cannot reopen Pause");
                UiStateStack.Push(UiScreen.Pause, pause, () => {});
                typeof(UiStateStack).GetMethod("Reset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Gameplay));
                Assert.That(InputManager.GameplayInputBlocked, Is.False);
                Assert.That(pause.activeSelf, Is.False);
            }
            finally { UiStateStack.Clear(); Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator DisabledHudClearsFeedbackAndHidesAssetReticle()
        {
            var root = new GameObject("HUD disable fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var hud = root.AddComponent<HUDManager>();
                var campaignHud = root.AddComponent<CampaignHUD>();
                campaignHud.ShowMessage("Transient message");
                campaignHud.enabled = false; campaignHud.enabled = true;
                campaignHud.enabled = false;
                Assert.That(campaignHud.transform.Find("CampaignCanvas").GetComponent<Canvas>().enabled, Is.False);
                campaignHud.enabled = true;
                var feedback = campaignHud.GetComponentsInChildren<Text>(true);
                foreach (var label in feedback) if (label.name == "Feedback") Assert.That(label.text, Is.Empty);
                hud.SetAimHudVisible(true, false, null, true);
                var reticle = root.transform.Find("ADSReflexReticle").GetComponent<Image>();
                Assert.That(reticle.sprite, Is.EqualTo(HudSkin.Get("ADSReflexDot")));
                hud.enabled = false;
                Assert.That(reticle.enabled, Is.False);
                Assert.That(HudSkin.Get("ADSReflexDot"), Is.Not.Null);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneCleanupSkipsDestroyedRootsAndReleasesInput()
        {
            var pause = new GameObject("Destroyed scene UI");
            UiStateStack.Push(UiScreen.Pause, pause, () => throw new MissingReferenceException("Destroyed root callback must be skipped"));
            Object.Destroy(pause);
            yield return null;
            UiStateStack.Clear();
            Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Gameplay));
            Assert.That(InputManager.GameplayInputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator StackReplacementAndReentrantDebriefPreserveFocusAndCursor()
        {
            UiStateStack.Clear();
            bool visible = Cursor.visible; var cursorLock = Cursor.lockState;
            var root = new GameObject("Stack fixture");
            var eventRoot = new GameObject("Stack EventSystem", typeof(EventSystem));
            yield return null;
            var previousEventSystem = EventSystem.current;
            EventSystem.current = eventRoot.GetComponent<EventSystem>();
            var gameplay = new GameObject("Gameplay selection");
            var inventory = new GameObject("Inventory", typeof(RectTransform), typeof(Image), typeof(Button));
            var document = new GameObject("Document", typeof(RectTransform), typeof(Image), typeof(Button));
            var debrief = new GameObject("Debrief", typeof(RectTransform), typeof(Image), typeof(Button));
            foreach (var item in new[] { eventRoot, gameplay, inventory, document, debrief }) item.transform.SetParent(root.transform);
            System.Action reentrant = () => { if (UiStateStack.Top != UiScreen.Debrief) UiStateStack.Push(UiScreen.Debrief, debrief, () => {}); };
            try
            {
                EventSystem.current.SetSelectedGameObject(gameplay);
                UiStateStack.Push(UiScreen.Inventory, inventory, () => {});
                UiStateStack.Push(UiScreen.Document, document, () => {});
                Assert.That(UiStateStack.Contains(UiScreen.Inventory), Is.False);
                UiStateStack.Changed += reentrant;
                UiStateStack.Push(UiScreen.Debrief, debrief, () => {});
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Debrief));
                UiStateStack.Clear();
                Assert.That(UiStateStack.Top, Is.EqualTo(UiScreen.Gameplay));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(gameplay));
                Assert.That(Cursor.visible, Is.EqualTo(visible));
                Assert.That(Cursor.lockState, Is.EqualTo(cursorLock));
            }
            finally
            {
                UiStateStack.Changed -= reentrant; UiStateStack.Clear(); Object.Destroy(root);
                if (previousEventSystem != null && previousEventSystem != eventRoot.GetComponent<EventSystem>()) EventSystem.current = previousEventSystem;
            }
        }
    }
}
