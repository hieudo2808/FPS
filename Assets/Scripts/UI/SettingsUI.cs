using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace FPS
{
    public class SettingsUI : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button graphicsTabButton;
        [SerializeField] private Button inputTabButton;

        [Header("Tab Panels")]
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject graphicsPanel;
        [SerializeField] private GameObject inputPanel;

        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Graphics Settings")]
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Mouse Settings")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TextMeshProUGUI sensitivityValueText;

        [Header("Keybindings")]
        [SerializeField] private Button fireKeyBtn;
        [SerializeField] private Button aimKeyBtn;
        [SerializeField] private Button reloadKeyBtn;
        [SerializeField] private Button weapon1KeyBtn;
        [SerializeField] private Button weapon2KeyBtn;

        private string actionToRebind = null;
        private Button currentRebindBtn = null;
        private TextMeshProUGUI currentRebindText = null;

        private void Start()
        {
            InitializeTabs();
            InitializeAudioSettings();
            InitializeGraphicsSettings();
            InitializeMouseSettings();
            InitializeKeybindings();
        }

        private void InitializeTabs()
        {
            if (audioTabButton != null) audioTabButton.onClick.AddListener(() => SwitchTab(audioPanel));
            if (graphicsTabButton != null) graphicsTabButton.onClick.AddListener(() => SwitchTab(graphicsPanel));
            if (inputTabButton != null) inputTabButton.onClick.AddListener(() => SwitchTab(inputPanel));

            // Mở tab Audio mặc định
            SwitchTab(audioPanel);
        }

        private void SwitchTab(GameObject targetPanel)
        {
            if (audioPanel != null) audioPanel.SetActive(audioPanel == targetPanel);
            if (graphicsPanel != null) graphicsPanel.SetActive(graphicsPanel == targetPanel);
            if (inputPanel != null) inputPanel.SetActive(inputPanel == targetPanel);
        }

        private void InitializeAudioSettings()
        {
            if (AudioManager.Instance != null)
            {
                if (masterVolumeSlider)
                {
                    masterVolumeSlider.value = AudioManager.Instance.GetMasterVolume();
                    masterVolumeSlider.onValueChanged.AddListener(AudioManager.Instance.SetMasterVolume);
                }
                if (musicVolumeSlider)
                {
                    musicVolumeSlider.value = AudioManager.Instance.GetMusicVolume();
                    musicVolumeSlider.onValueChanged.AddListener(AudioManager.Instance.SetMusicVolume);
                }
                if (sfxVolumeSlider)
                {
                    sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
                    sfxVolumeSlider.onValueChanged.AddListener(AudioManager.Instance.SetSFXVolume);
                }
            }
        }

        private void InitializeGraphicsSettings()
        {
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                var options = new System.Collections.Generic.List<string>(QualitySettings.names);
                qualityDropdown.AddOptions(options);

                int currentQuality = SettingsManager.Instance != null ? SettingsManager.Instance.GraphicsQuality : QualitySettings.GetQualityLevel();
                qualityDropdown.value = currentQuality;
                qualityDropdown.RefreshShownValue();

                qualityDropdown.onValueChanged.AddListener((val) =>
                {
                    SettingsManager.Instance?.SetGraphicsQuality(val);
                });
            }
        }

        private void InitializeMouseSettings()
        {
            if (sensitivitySlider != null && SettingsManager.Instance != null)
            {
                sensitivitySlider.value = SettingsManager.Instance.MouseSensitivity;
                UpdateSensitivityText(SettingsManager.Instance.MouseSensitivity);

                sensitivitySlider.onValueChanged.AddListener((val) =>
                {
                    SettingsManager.Instance.SetMouseSensitivity(val);
                    UpdateSensitivityText(val);
                });
            }
        }

        private void UpdateSensitivityText(float val)
        {
            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = val.ToString("F1");
            }
        }

        private void InitializeKeybindings()
        {
            SetupRebindButton(fireKeyBtn, "Fire");
            SetupRebindButton(aimKeyBtn, "Aim");
            SetupRebindButton(reloadKeyBtn, "Reload");
            SetupRebindButton(weapon1KeyBtn, "Weapon1");
            SetupRebindButton(weapon2KeyBtn, "Weapon2");
        }

        private void SetupRebindButton(Button btn, string actionName)
        {
            if (btn == null || InputManager.Instance == null) return;

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = InputManager.Instance.GetKeyForAction(actionName).ToString();
            }

            btn.onClick.AddListener(() => StartRebinding(actionName, btn, txt));
        }

        private void StartRebinding(string actionName, Button btn, TextMeshProUGUI txt)
        {
            if (actionToRebind != null) return; // Đang rebind nút khác

            actionToRebind = actionName;
            currentRebindBtn = btn;
            currentRebindText = txt;

            if (txt != null) txt.text = "Press Any Key...";
        }

        private void Update()
        {
            if (actionToRebind != null)
            {
                if (Input.anyKeyDown)
                {
                    foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (Input.GetKeyDown(keyCode))
                        {
                            // Bỏ qua phím Esc để có thể cancel
                            if (keyCode == KeyCode.Escape)
                            {
                                CancelRebind();
                                break;
                            }

                            // Lưu phím mới
                            InputManager.Instance.RebindKey(actionToRebind, keyCode);
                            
                            // Cập nhật UI
                            if (currentRebindText != null)
                            {
                                currentRebindText.text = keyCode.ToString();
                            }

                            // Reset state
                            actionToRebind = null;
                            currentRebindBtn = null;
                            currentRebindText = null;
                            break;
                        }
                    }
                }
            }
        }

        private void CancelRebind()
        {
            if (actionToRebind != null && currentRebindText != null && InputManager.Instance != null)
            {
                currentRebindText.text = InputManager.Instance.GetKeyForAction(actionToRebind).ToString();
            }
            actionToRebind = null;
            currentRebindBtn = null;
            currentRebindText = null;
        }

        private void OnDisable()
        {
            // Lưu thiết lập khi đóng bảng Settings
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SaveSettings();
            }
            CancelRebind();
        }
    }
}
