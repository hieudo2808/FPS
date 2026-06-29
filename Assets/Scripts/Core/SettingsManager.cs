using UnityEngine;
using System;

namespace FPS
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string MOUSE_SENSITIVITY_KEY = "MouseSensitivity";
        private const string GRAPHICS_QUALITY_KEY = "GraphicsQuality";

        public float MouseSensitivity { get; private set; }
        public int GraphicsQuality { get; private set; }

        public event Action<float> OnSensitivityChanged;
        public event Action<int> OnGraphicsQualityChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void LoadSettings()
        {
            MouseSensitivity = PlayerPrefs.GetFloat(MOUSE_SENSITIVITY_KEY, 2.0f);
            
            // Default quality is the current one in Unity settings if not saved
            int defaultQuality = QualitySettings.GetQualityLevel();
            GraphicsQuality = PlayerPrefs.GetInt(GRAPHICS_QUALITY_KEY, defaultQuality);
            
            ApplyGraphicsQuality();
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            MouseSensitivity = sensitivity;
            PlayerPrefs.SetFloat(MOUSE_SENSITIVITY_KEY, sensitivity);
            PlayerPrefs.Save();
            OnSensitivityChanged?.Invoke(sensitivity);
        }

        public void SetGraphicsQuality(int qualityIndex)
        {
            GraphicsQuality = qualityIndex;
            PlayerPrefs.SetInt(GRAPHICS_QUALITY_KEY, qualityIndex);
            PlayerPrefs.Save();
            ApplyGraphicsQuality();
            OnGraphicsQualityChanged?.Invoke(qualityIndex);
        }

        private void ApplyGraphicsQuality()
        {
            if (QualitySettings.GetQualityLevel() != GraphicsQuality)
            {
                QualitySettings.SetQualityLevel(GraphicsQuality, true);
            }
        }
    }
}
