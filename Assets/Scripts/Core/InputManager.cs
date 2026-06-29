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
                { "Fire", LoadKey("Fire", KeyCode.Mouse0) },
                { "Reload", LoadKey("Reload", KeyCode.R) },
                { "Aim", LoadKey("Aim", KeyCode.Mouse1) },
                { "Weapon1", LoadKey("Weapon1", KeyCode.Alpha1) },
                { "Weapon2", LoadKey("Weapon2", KeyCode.Alpha2) }
            };
        }

        private KeyCode LoadKey(string action, KeyCode defaultKey)
        {
            string savedKey = PlayerPrefs.GetString("Input_" + action, "");
            if (string.IsNullOrEmpty(savedKey))
            {
                return defaultKey;
            }
            if (System.Enum.TryParse(savedKey, out KeyCode parsedKey))
            {
                return parsedKey;
            }
            return defaultKey;

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
            
            PlayerPrefs.SetString("Input_" + action, newKey.ToString());
            PlayerPrefs.Save();
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

        public bool GetReloadInput()
        {
            return Input.GetKey(GetKeyForAction("Reload"));
        }

        public bool GetAimInput()
        {
            return Input.GetKey(GetKeyForAction("Aim"));
        }

        public bool GetWeapon1InputDown()
        {
            return Input.GetKeyDown(GetKeyForAction("Weapon1"));
        }

        public bool GetWeapon2InputDown()
        {
            return Input.GetKeyDown(GetKeyForAction("Weapon2"));
        }
    }
}
