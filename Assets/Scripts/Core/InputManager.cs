using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        private Dictionary<string, KeyCode> keyBindings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDefaultBindings();
        }

        private void InitializeDefaultBindings()
        {
            keyBindings = new Dictionary<string, KeyCode>
            {
                { "Fire", KeyCode.Mouse0 },
                { "Reload", KeyCode.R },
                { "Aim", KeyCode.Mouse1 }
            };
        }

        public KeyCode GetKeyForAction(string action)
        {
            if (keyBindings == null) InitializeDefaultBindings(); // Safe-guard for tests where Awake might not run immediately
            
            if (keyBindings.TryGetValue(action, out KeyCode key))
            {
                return key;
            }
            return KeyCode.None;
        }

        public void RebindKey(string action, KeyCode newKey)
        {
            if (keyBindings == null) InitializeDefaultBindings();

            if (keyBindings.ContainsKey(action))
            {
                keyBindings[action] = newKey;
            }
            else
            {
                keyBindings.Add(action, newKey);
            }
        }

        public bool GetFireInput()
        {
            return Input.GetKey(GetKeyForAction("Fire"));
        }

        public bool GetFireInputDown()
        {
            return Input.GetKeyDown(GetKeyForAction("Fire"));
        }

        public bool GetReloadInputDown()
        {
            return Input.GetKeyDown(GetKeyForAction("Reload"));
        }

        public bool GetAimInput()
        {
            return Input.GetKey(GetKeyForAction("Aim"));
        }
    }
}
