using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

namespace FPS
{
    public class SettingsUI : MonoBehaviour
    {
        private const string MouseSensitivityKey = "MouseSensitivity";
        private const string GraphicsQualityKey = "GraphicsQuality";
        private static readonly Color ActiveTabTextColor = Color.white;
        private static readonly Color InactiveTabTextColor = new Color(0.88f, 0.92f, 0.95f, 1.0f);

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
        [SerializeField] private TextMeshProUGUI masterVolumeValueText;
        [SerializeField] private TextMeshProUGUI musicVolumeValueText;
        [SerializeField] private TextMeshProUGUI sfxVolumeValueText;

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
        [SerializeField] private Button interactKeyBtn;
        [SerializeField] private Button grenadeKeyBtn;
        [SerializeField] private TextMeshProUGUI rebindStatusText;

        [Header("Footer")]
        [SerializeField] private Button resetDefaultsButton;

        private string actionToRebind = null;
        private Button currentRebindBtn = null;
        private TextMeshProUGUI currentRebindText = null;
        private bool initialized;

        private void Start()
        {
            InitializeUi();
            initialized = true;
        }

        private void OnEnable()
        {
            if (initialized)
                InitializeUi();
        }

        private void InitializeUi()
        {
            UnregisterRuntimeListeners();
            AttachButtonResetters();
            InitializeTabs();
            InitializeAudioSettings();
            InitializeGraphicsSettings();
            InitializeMouseSettings();
            InitializeKeybindings();

            if (resetDefaultsButton != null)
            {
                resetDefaultsButton.onClick.AddListener(ResetDefaults);
            }

            if (rebindStatusText != null)
            {
                rebindStatusText.text = "Changes apply instantly.";
            }
        }

        private void UnregisterRuntimeListeners()
        {
            audioTabButton?.onClick.RemoveAllListeners();
            graphicsTabButton?.onClick.RemoveAllListeners();
            inputTabButton?.onClick.RemoveAllListeners();
            fireKeyBtn?.onClick.RemoveAllListeners();
            aimKeyBtn?.onClick.RemoveAllListeners();
            reloadKeyBtn?.onClick.RemoveAllListeners();
            weapon1KeyBtn?.onClick.RemoveAllListeners();
            weapon2KeyBtn?.onClick.RemoveAllListeners();
            interactKeyBtn?.onClick.RemoveAllListeners();
            grenadeKeyBtn?.onClick.RemoveAllListeners();
            resetDefaultsButton?.onClick.RemoveAllListeners();
            masterVolumeSlider?.onValueChanged.RemoveAllListeners();
            musicVolumeSlider?.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
            qualityDropdown?.onValueChanged.RemoveAllListeners();
            sensitivitySlider?.onValueChanged.RemoveAllListeners();
        }

        private void AttachButtonResetters()
        {
            Button[] buttons =
            {
                audioTabButton, graphicsTabButton, inputTabButton,
                fireKeyBtn, aimKeyBtn, reloadKeyBtn, weapon1KeyBtn,
                weapon2KeyBtn, interactKeyBtn, grenadeKeyBtn, resetDefaultsButton
            };
            foreach (Button button in buttons)
                UiButtonSelectionResetter.Attach(button);
        }

        private void InitializeTabs()
        {
            ConfigureTabButton(audioTabButton);
            ConfigureTabButton(graphicsTabButton);
            ConfigureTabButton(inputTabButton);
            if (audioTabButton != null) audioTabButton.onClick.AddListener(() => SwitchTab(audioPanel));
            if (graphicsTabButton != null) graphicsTabButton.onClick.AddListener(() => SwitchTab(graphicsPanel));
            if (inputTabButton != null) inputTabButton.onClick.AddListener(() => SwitchTab(inputPanel));

            // Mở tab Audio mặc định
            SwitchTab(audioPanel);
        }

        private static void ConfigureTabButton(Button button)
        {
            if (button != null)
                button.transition = Selectable.Transition.None;
        }

        private void SwitchTab(GameObject targetPanel)
        {
            if (audioPanel != null) audioPanel.SetActive(audioPanel == targetPanel);
            if (graphicsPanel != null) graphicsPanel.SetActive(graphicsPanel == targetPanel);
            if (inputPanel != null) inputPanel.SetActive(inputPanel == targetPanel);

            SetTabSelected(audioTabButton, audioPanel == targetPanel);
            SetTabSelected(graphicsTabButton, graphicsPanel == targetPanel);
            SetTabSelected(inputTabButton, inputPanel == targetPanel);
        }

        private void SetTabSelected(Button button, bool selected)
        {
            if (button == null) return;

            Color textColor = selected ? ActiveTabTextColor : InactiveTabTextColor;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = textColor;
            }
        }

        private void InitializeAudioSettings()
        {
            masterVolumeValueText = masterVolumeValueText != null ? masterVolumeValueText : FindSliderValueText(masterVolumeSlider);
            musicVolumeValueText = musicVolumeValueText != null ? musicVolumeValueText : FindSliderValueText(musicVolumeSlider);
            sfxVolumeValueText = sfxVolumeValueText != null ? sfxVolumeValueText : FindSliderValueText(sfxVolumeSlider);

            InitializeAudioSlider(masterVolumeSlider, masterVolumeValueText,
                AudioManager.Instance != null ? AudioManager.Instance.GetMasterVolume() : PlayerPrefs.GetFloat("master", GetSliderValue(masterVolumeSlider, 1f)),
                val => ApplyAudioValue("master", val, audio => audio.SetMasterVolume(val)));

            InitializeAudioSlider(musicVolumeSlider, musicVolumeValueText,
                AudioManager.Instance != null ? AudioManager.Instance.GetMusicVolume() : PlayerPrefs.GetFloat("music", GetSliderValue(musicVolumeSlider, 1f)),
                val => ApplyAudioValue("music", val, audio => audio.SetMusicVolume(val)));

            InitializeAudioSlider(sfxVolumeSlider, sfxVolumeValueText,
                AudioManager.Instance != null ? AudioManager.Instance.GetSFXVolume() : PlayerPrefs.GetFloat("sfx", GetSliderValue(sfxVolumeSlider, 1f)),
                val => ApplyAudioValue("sfx", val, audio => audio.SetSFXVolume(val)));
        }

        private void ApplyAudioValue(string key, float value, Action<AudioManager> apply)
        {
            value = Mathf.Clamp01(value);
            if (AudioManager.Instance != null)
            {
                apply(AudioManager.Instance);
                return;
            }

            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        private float GetSliderValue(Slider slider, float fallback)
        {
            return slider != null ? slider.value : fallback;
        }

        private void InitializeAudioSlider(Slider slider, TextMeshProUGUI valueText, float initialValue, Action<float> onChanged)
        {
            if (slider == null) return;

            slider.SetValueWithoutNotify(initialValue);
            UpdateValueText(valueText, initialValue);

            slider.onValueChanged.AddListener((val) =>
            {
                onChanged?.Invoke(val);
                UpdateValueText(valueText, val);
            });
        }

        private void UpdateValueText(TextMeshProUGUI target, float val)
        {
            if (target != null)
            {
                target.text = $"{Mathf.RoundToInt(Mathf.Clamp01(val) * 100f)}%";
            }
        }

        private TextMeshProUGUI FindSliderValueText(Slider slider)
        {
            if (slider == null || slider.transform.parent == null) return null;

            Transform valueText = slider.transform.parent.Find("ValueText");
            return valueText != null ? valueText.GetComponent<TextMeshProUGUI>() : null;
        }

        private void InitializeGraphicsSettings()
        {
            if (qualityDropdown != null)
            {
                DropdownTemplateUtility.Normalize(qualityDropdown);
                qualityDropdown.ClearOptions();
                var options = new List<string>(QualitySettings.names);
                qualityDropdown.AddOptions(options);

                int savedQuality = PlayerPrefs.GetInt(GraphicsQualityKey, QualitySettings.GetQualityLevel());
                int currentQuality = SettingsManager.Instance != null ? SettingsManager.Instance.GraphicsQuality : savedQuality;
                currentQuality = Mathf.Clamp(currentQuality, 0, Mathf.Max(0, options.Count - 1));
                qualityDropdown.SetValueWithoutNotify(currentQuality);
                qualityDropdown.RefreshShownValue();

                qualityDropdown.onValueChanged.AddListener((val) =>
                {
                    if (SettingsManager.Instance != null)
                    {
                        SettingsManager.Instance.SetGraphicsQuality(val);
                    }
                    else
                    {
                        PlayerPrefs.SetInt(GraphicsQualityKey, val);
                        PlayerPrefs.Save();
                        QualitySettings.SetQualityLevel(val, true);
                    }
                });
            }
        }

        private void InitializeMouseSettings()
        {
            if (sensitivitySlider != null)
            {
                sensitivityValueText = sensitivityValueText != null ? sensitivityValueText : FindSliderValueText(sensitivitySlider);

                float sensitivity = SettingsManager.Instance != null
                    ? SettingsManager.Instance.MouseSensitivity
                    : PlayerPrefs.GetFloat(MouseSensitivityKey, sensitivitySlider.value);

                sensitivitySlider.SetValueWithoutNotify(sensitivity);
                UpdateSensitivityText(sensitivity);

                sensitivitySlider.onValueChanged.AddListener((val) =>
                {
                    if (SettingsManager.Instance != null)
                    {
                        SettingsManager.Instance.SetMouseSensitivity(val);
                    }
                    else
                    {
                        PlayerPrefs.SetFloat(MouseSensitivityKey, val);
                        PlayerPrefs.Save();
                    }

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
            SetupRebindButton(interactKeyBtn, "Interact");
            SetupRebindButton(grenadeKeyBtn, "Grenade");
        }

        private void SetupRebindButton(Button btn, string actionName)
        {
            if (btn == null || InputManager.Instance == null) return;

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = InputManager.Instance.GetBindingDisplayName(actionName);

            btn.onClick.AddListener(() => StartRebinding(actionName, btn, txt));
        }

        private void RefreshKeybindingText(Button btn, string actionName)
        {
            if (btn == null || InputManager.Instance == null) return;

            TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = InputManager.Instance.GetBindingDisplayName(actionName);
        }

        private void ResetDefaults()
        {
            AudioManager.Instance?.ResetToDefault();

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetMouseSensitivity(2.0f);
            }
            else
            {
                PlayerPrefs.SetFloat(MouseSensitivityKey, 2.0f);
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.SetValueWithoutNotify(2.0f);
                UpdateSensitivityText(2.0f);
            }

            SetSliderWithoutNotify(masterVolumeSlider, masterVolumeValueText, 0.5f);
            SetSliderWithoutNotify(musicVolumeSlider, musicVolumeValueText, 0.5f);
            SetSliderWithoutNotify(sfxVolumeSlider, sfxVolumeValueText, 0.5f);

            if (InputManager.Instance != null)
            {
                InputManager.Instance.RebindKey("Fire", KeyCode.Mouse0);
                InputManager.Instance.RebindKey("Aim", KeyCode.Mouse1);
                InputManager.Instance.RebindKey("Reload", KeyCode.R);
                InputManager.Instance.RebindKey("Weapon1", KeyCode.Alpha1);
                InputManager.Instance.RebindKey("Weapon2", KeyCode.Alpha2);
                InputManager.Instance.RebindKey("Jump", KeyCode.Space);
                InputManager.Instance.RebindKey("Interact", KeyCode.F);
                InputManager.Instance.RebindKey("Grenade", KeyCode.G);
            }

            RefreshKeybindingText(fireKeyBtn, "Fire");
            RefreshKeybindingText(aimKeyBtn, "Aim");
            RefreshKeybindingText(reloadKeyBtn, "Reload");
            RefreshKeybindingText(weapon1KeyBtn, "Weapon1");
            RefreshKeybindingText(weapon2KeyBtn, "Weapon2");
            RefreshKeybindingText(interactKeyBtn, "Interact");
            RefreshKeybindingText(grenadeKeyBtn, "Grenade");

            if (rebindStatusText != null)
            {
                rebindStatusText.text = "Defaults restored.";
            }

            PlayerPrefs.Save();
        }

        private void SetSliderWithoutNotify(Slider slider, TextMeshProUGUI valueText, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }

            UpdateValueText(valueText, value);
        }

        private void StartRebinding(string actionName, Button btn, TextMeshProUGUI txt)
        {
            if (actionToRebind != null) return; // Đang rebind nút khác

            actionToRebind = actionName;
            currentRebindBtn = btn;
            currentRebindText = txt;

            if (txt != null) txt.text = "Press a key...";
            if (rebindStatusText != null) rebindStatusText.text = "Press a key. ESC cancels.";

            if (!InputManager.Instance.StartInteractiveRebind(actionName, CompleteRebind))
                CancelRebind();
        }

        private void CompleteRebind(bool accepted)
        {
            if (actionToRebind != null && accepted)
            {
                if (currentRebindText != null && InputManager.Instance != null)
                    currentRebindText.text = InputManager.Instance.GetBindingDisplayName(actionToRebind);
            }

            actionToRebind = null;
            currentRebindBtn = null;
            currentRebindText = null;
            EventSystem.current?.SetSelectedGameObject(null);
            if (rebindStatusText != null) rebindStatusText.text = accepted
                ? "Changes apply instantly."
                : "Rebind cancelled.";
        }

        private void CancelRebind()
        {
            if (actionToRebind != null && currentRebindText != null && InputManager.Instance != null)
            {
                currentRebindText.text = InputManager.Instance.GetBindingDisplayName(actionToRebind);
            }
            InputManager.Instance?.CancelInteractiveRebind();
            actionToRebind = null;
            currentRebindBtn = null;
            currentRebindText = null;
            EventSystem.current?.SetSelectedGameObject(null);
            if (rebindStatusText != null) rebindStatusText.text = "Changes apply instantly.";
        }

        private void OnDisable()
        {
            UnregisterRuntimeListeners();
            // Lưu thiết lập khi đóng bảng Settings
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SaveSettings();
            }
            CancelRebind();
        }
    }
}
