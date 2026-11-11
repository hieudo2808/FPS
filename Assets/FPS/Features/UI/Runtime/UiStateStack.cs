using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FPS
{
    public enum UiScreen { Gameplay, Inventory, Document, Pause, Settings, Debrief }

    public static class UiStateStack
    {
        private sealed class Entry
        {
            public UiScreen screen;
            public GameObject root, selection;
            public Action close;
        }
        private static readonly List<Entry> entries = new();
        private static bool cursorVisible;
        private static CursorLockMode cursorLock;
        private static GameObject gameplaySelection;
        private static int backFrame = -1;
        private static bool changing, clearing;
        public static event Action Changed;
        public static UiScreen Top => entries.Count == 0 ? UiScreen.Gameplay : entries[^1].screen;
        public static bool Contains(UiScreen screen) => entries.Exists(e => e.screen == screen);
        public static bool BackConsumed => backFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (entries.Count > 0)
            {
                foreach (var entry in entries) if (entry.root != null) entry.root.SetActive(false);
                Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
            }
            entries.Clear(); Changed = null; backFrame = -1; changing = clearing = false;
            InputManager.GameplayInputBlocked = false;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }
        private static void OnSceneChanged(Scene before, Scene after) => Clear();

        public static void Push(UiScreen screen, GameObject root, Action close)
        {
            if (root == null || screen == UiScreen.Gameplay || changing || clearing) return;
            if (Contains(screen)) return;
            if ((screen is UiScreen.Inventory or UiScreen.Document) && (Top is UiScreen.Pause or UiScreen.Settings or UiScreen.Debrief)) return;
            bool wasOpen = entries.Count > 0;
            changing = true;
            if (screen == UiScreen.Debrief) Clear();
            if (screen is UiScreen.Inventory or UiScreen.Document)
            {
                if (Top is UiScreen.Inventory or UiScreen.Document) Pop();
            }
            if (entries.Count == 0 && !wasOpen)
            {
                cursorVisible = Cursor.visible; cursorLock = Cursor.lockState;
                gameplaySelection = EventSystem.current?.currentSelectedGameObject;
            }
            else if (entries.Count > 0) entries[^1].selection = EventSystem.current?.currentSelectedGameObject;
            root.SetActive(true);
            entries.Add(new Entry { screen = screen, root = root, close = close });
            changing = false;
            Apply();
        }

        public static void Remove(UiScreen screen)
        {
            int index = entries.FindIndex(e => e.screen == screen);
            if (index < 0) return;
            var entry = entries[index]; entries.RemoveAt(index);
            if (entry.root != null) entry.root.SetActive(false);
            Apply();
        }

        private static void Pop()
        {
            if (entries.Count == 0) return;
            bool previous = changing;
            changing = true;
            var entry = entries[^1]; entries.RemoveAt(entries.Count - 1);
            if (entry.root != null) entry.root.SetActive(false);
            try { if (entry.root != null) entry.close?.Invoke(); }
            finally { changing = previous; Apply(); }
        }

        public static bool TryBack()
        {
            if (BackConsumed) return true;
            if (Top == UiScreen.Gameplay) return false;
            backFrame = Time.frameCount;
            if (Top != UiScreen.Debrief) Pop();
            return true;
        }

        public static void Clear()
        {
            if (entries.Count == 0) { InputManager.GameplayInputBlocked = false; return; }
            bool previous = changing;
            clearing = changing = true;
            while (entries.Count > 0) Pop();
            changing = previous;
            Apply();
            clearing = false;
        }

        private static void Apply()
        {
            if (changing) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var root = entries[i].root;
                if (root == null) continue;
                var group = root.GetComponent<CanvasGroup>();
                if (group == null) group = root.AddComponent<CanvasGroup>();
                group.interactable = group.blocksRaycasts = i == entries.Count - 1;
            }
            bool open = entries.Count > 0;
            InputManager.GameplayInputBlocked = open;
            Cursor.visible = open || cursorVisible;
            Cursor.lockState = open ? CursorLockMode.None : cursorLock;
            var selection = open ? entries[^1].selection : gameplaySelection;
            if (selection == null || !selection.activeInHierarchy)
                selection = open ? FirstControl(entries[^1].root) : null;
            EventSystem.current?.SetSelectedGameObject(selection);
            Changed?.Invoke();
        }

        private static GameObject FirstControl(GameObject root)
        {
            if (root == null) return null;
            foreach (var control in root.GetComponentsInChildren<Selectable>())
                if (control.IsInteractable()) return control.gameObject;
            return null;
        }

        public static void RefreshFocus(UiScreen screen)
        {
            if (Top != screen || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(entries[^1].root.transform)) return;
            EventSystem.current.SetSelectedGameObject(FirstControl(entries[^1].root));
        }
    }
}
