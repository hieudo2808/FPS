using UnityEngine;
using System;

namespace FPS
{
    public class SettingsManager : Singleton<SettingsManager>
    {
        private const string MOUSE_SENSITIVITY_KEY = "MouseSensitivity";
        private const string GRAPHICS_QUALITY_KEY = "GraphicsQuality";

        private bool settingsLoaded;
        private float mouseSensitivity;
        private int graphicsQuality;

        public float MouseSensitivity
        {
            get
            {
                EnsureSettingsLoaded();
                return mouseSensitivity;
            }
            private set => mouseSensitivity = value;
        }

        public int GraphicsQuality
        {
            get
            {
                EnsureSettingsLoaded();
                return graphicsQuality;
            }
            private set => graphicsQuality = value;
        }

        public event Action<float> OnSensitivityChanged;
        public event Action<int> OnGraphicsQualityChanged;

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
                EnsureSettingsLoaded();
        }

        private void EnsureSettingsLoaded()
        {
            if (settingsLoaded) return;
            LoadSettings();
        }

        private void LoadSettings()
        {
            mouseSensitivity = PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, 2.0f);
            
            // Default quality is the current one in Unity settings if not saved
            int defaultQuality = QualitySettings.GetQualityLevel();
            graphicsQuality = PlayerPrefs.GetInt(GRAPHICS_QUALITY_KEY, defaultQuality);
            settingsLoaded = true;
            
            ApplyGraphicsQuality();
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            EnsureSettingsLoaded();
            mouseSensitivity = sensitivity;
            PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, sensitivity);
            PlayerPrefs.Save();
            OnSensitivityChanged?.Invoke(sensitivity);
        }

        public void SetGraphicsQuality(int qualityIndex)
        {
            EnsureSettingsLoaded();
            graphicsQuality = qualityIndex;
            PlayerPrefs.SetInt(GRAPHICS_QUALITY_KEY, qualityIndex);
            PlayerPrefs.Save();
            ApplyGraphicsQuality();
            OnGraphicsQualityChanged?.Invoke(qualityIndex);
        }

        private void ApplyGraphicsQuality()
        {
            if (QualitySettings.GetQualityLevel() != graphicsQuality)
            {
                QualitySettings.SetQualityLevel(graphicsQuality, true);
            }
        }
    }
}
