using System;
using UnityEngine;

namespace FPS
{
    public sealed class SettingsManager : Singleton<SettingsManager>
    {
        private const string MouseSensitivityKey = "MouseSensitivity";
        private const string GraphicsQualityKey = "GraphicsQuality";
        private const string ResolutionWidthKey = "ResolutionWidth";
        private const string ResolutionHeightKey = "ResolutionHeight";
        private const string FullscreenKey = "Fullscreen";

        public const float MinMouseSensitivity = 0.1f;
        public const float MaxMouseSensitivity = 10f;
        public const float DefaultMouseSensitivity = 2f;

        private bool settingsLoaded;
        private float mouseSensitivity;
        private int graphicsQuality;
        private int resolutionWidth;
        private int resolutionHeight;
        private bool fullscreen;

        public float MouseSensitivity
        {
            get
            {
                EnsureSettingsLoaded();
                return mouseSensitivity;
            }
        }

        public int GraphicsQuality
        {
            get
            {
                EnsureSettingsLoaded();
                return graphicsQuality;
            }
        }

        public int ResolutionWidth
        {
            get
            {
                EnsureSettingsLoaded();
                return resolutionWidth;
            }
        }

        public int ResolutionHeight
        {
            get
            {
                EnsureSettingsLoaded();
                return resolutionHeight;
            }
        }

        public bool Fullscreen
        {
            get
            {
                EnsureSettingsLoaded();
                return fullscreen;
            }
        }

        public event Action<float> OnSensitivityChanged;
        public event Action<int> OnGraphicsQualityChanged;
        public event Action<int, int, bool> OnResolutionChanged;

        protected override void Awake()
        {
            base.Awake();
            if (Instance == this)
                EnsureSettingsLoaded();
        }

        private void EnsureSettingsLoaded()
        {
            if (settingsLoaded)
                return;

            LoadSettings();
        }

        private void LoadSettings()
        {
            float storedSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity);
            mouseSensitivity = NormalizeSensitivity(storedSensitivity);

            int qualityCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;
            int defaultQuality = qualityCount > 0 ? QualitySettings.GetQualityLevel() : 0;
            graphicsQuality = qualityCount > 0
                ? Mathf.Clamp(PlayerPrefs.GetInt(GraphicsQualityKey, defaultQuality), 0, qualityCount - 1)
                : 0;

            Resolution current = Screen.currentResolution;
            resolutionWidth = Mathf.Max(1, PlayerPrefs.GetInt(ResolutionWidthKey, current.width));
            resolutionHeight = Mathf.Max(1, PlayerPrefs.GetInt(ResolutionHeightKey, current.height));
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            settingsLoaded = true;

            PlayerPrefs.SetFloat(MouseSensitivityKey, mouseSensitivity);
            ApplyGraphicsQuality();
            ApplyResolution();
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            EnsureSettingsLoaded();
            float normalized = NormalizeSensitivity(sensitivity);
            if (Mathf.Approximately(mouseSensitivity, normalized))
                return;

            mouseSensitivity = normalized;
            PlayerPrefs.SetFloat(MouseSensitivityKey, mouseSensitivity);
            PlayerPrefs.Save();
            OnSensitivityChanged?.Invoke(mouseSensitivity);
        }

        public void SetGraphicsQuality(int qualityIndex)
        {
            EnsureSettingsLoaded();
            int qualityCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;
            if (qualityCount == 0)
                return;

            int clamped = Mathf.Clamp(qualityIndex, 0, qualityCount - 1);
            bool qualityChanged = graphicsQuality != clamped;
            graphicsQuality = clamped;
            PlayerPrefs.SetInt(GraphicsQualityKey, graphicsQuality);
            PlayerPrefs.Save();
            // Persisting the user's choice is required even when it already
            // matches the current engine index. SetQualityLevel itself remains
            // guarded inside ApplyGraphicsQuality so we do not trigger an
            // expensive quality rebuild for a no-op selection.
            if (qualityChanged || QualitySettings.GetQualityLevel() != clamped)
                ApplyGraphicsQuality();
            OnGraphicsQualityChanged?.Invoke(graphicsQuality);
        }

        public void SetResolution(int width, int height, bool isFullscreen)
        {
            EnsureSettingsLoaded();
            resolutionWidth = Mathf.Max(1, width);
            resolutionHeight = Mathf.Max(1, height);
            fullscreen = isFullscreen;
            PlayerPrefs.SetInt(ResolutionWidthKey, resolutionWidth);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolutionHeight);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            ApplyResolution();
            OnResolutionChanged?.Invoke(resolutionWidth, resolutionHeight, fullscreen);
        }

        public static float NormalizeSensitivity(float value)
        {
            if (value >= MinMouseSensitivity && value <= MaxMouseSensitivity)
                return value;

            // Convert values saved by the previous 10..500 runtime scale.
            if (value > MaxMouseSensitivity)
            {
                float normalized = Mathf.InverseLerp(10f, 500f, value);
                return Mathf.Lerp(MinMouseSensitivity, MaxMouseSensitivity, normalized);
            }

            return Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        }

        private void ApplyGraphicsQuality()
        {
            if (QualitySettings.names == null || QualitySettings.names.Length == 0)
                return;

            graphicsQuality = Mathf.Clamp(graphicsQuality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != graphicsQuality)
                QualitySettings.SetQualityLevel(graphicsQuality, applyExpensiveChanges: true);

            QualitySettings.resolutionScalingFixedDPIFactor = 1f;
            if (string.Equals(QualitySettings.names[graphicsQuality], "Ultra", StringComparison.OrdinalIgnoreCase))
            {
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.lodBias = 2f;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                QualitySettings.antiAliasing = 4;
            }
        }

        private void ApplyResolution()
        {
            if (resolutionWidth <= 0 || resolutionHeight <= 0)
                return;

            FullScreenMode mode = fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(resolutionWidth, resolutionHeight, mode);
        }
    }
}
