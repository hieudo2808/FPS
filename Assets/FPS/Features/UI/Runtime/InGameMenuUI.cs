using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FPS
{
    public class InGameMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaveMatchButton;
        [SerializeField] private Button closeSettingsButton;

        private bool initialized;
        public static bool IsMenuOpen => UiStateStack.Contains(UiScreen.Pause) || UiStateStack.Contains(UiScreen.Settings);

        private void Start()
        {
            // Ẩn tất cả khi mới vào game
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            RegisterUiListeners();
            UiStateStack.Remove(UiScreen.Settings);
            UiStateStack.Remove(UiScreen.Pause);
            initialized = true;
        }

        private void OnEnable()
        {
            if (initialized)
                RegisterUiListeners();
        }

        private void OnDisable()
        {
            UnregisterUiListeners();
            UiStateStack.Remove(UiScreen.Settings);
            UiStateStack.Remove(UiScreen.Pause);
        }

        private void RegisterUiListeners()
        {
            resumeButton?.onClick.AddListener(ResumeGame);
            settingsButton?.onClick.AddListener(OpenSettings);
            leaveMatchButton?.onClick.AddListener(LeaveMatch);
            closeSettingsButton?.onClick.AddListener(CloseSettings);
        }

        private void UnregisterUiListeners()
        {
            resumeButton?.onClick.RemoveListener(ResumeGame);
            settingsButton?.onClick.RemoveListener(OpenSettings);
            leaveMatchButton?.onClick.RemoveListener(LeaveMatch);
            closeSettingsButton?.onClick.RemoveListener(CloseSettings);
        }

        private void Update()
        {
            if (settingsPanel != null && settingsPanel.activeSelf && settingsPanel.TryGetComponent<SettingsUI>(out var settings) && settings.BlocksBack) return;
            if (InputManager.Instance != null && InputManager.Instance.GetPauseInputDown())
            {
                if (!UiStateStack.TryBack()) TogglePauseMenu();
            }
        }

        private void TogglePauseMenu()
        {
            if (IsMenuOpen) ResumeGame();
            else if (UiStateStack.Top == UiScreen.Gameplay)
                UiStateStack.Push(UiScreen.Pause, pausePanel, ResumeGame);
        }

        public void ResumeGame()
        {
            UiStateStack.Remove(UiScreen.Settings);
            UiStateStack.Remove(UiScreen.Pause);
        }

        private void OpenSettings()
        {
            if (!UiStateStack.Contains(UiScreen.Pause)) return;
            UiStateStack.Push(UiScreen.Settings, settingsPanel, CloseSettings);
        }

        private void CloseSettings() => UiStateStack.Remove(UiScreen.Settings);

        private void OnDestroy()
        {
            UnregisterUiListeners();
            UiStateStack.Remove(UiScreen.Settings);
            UiStateStack.Remove(UiScreen.Pause);
        }

        private static void ClearUiSelection()
        {
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void LeaveMatch()
        {
            UiStateStack.Clear();
            ClearUiSelection();
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.Disconnect();
            }
            else
            {
                Debug.LogWarning("NetworkGameManager instance not found. Cannot disconnect.");
            }
        }
    }
}
