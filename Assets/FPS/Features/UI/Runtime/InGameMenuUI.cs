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

        private bool isPaused = false;
        private bool wasCursorVisible = false;
        private CursorLockMode previousCursorLockMode;
        private bool initialized;
        public static bool IsMenuOpen { get; private set; }

        private void Start()
        {
            // Ẩn tất cả khi mới vào game
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            RegisterUiListeners();
            SetGameplayInputBlocked(false);
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
            SetGameplayInputBlocked(false);
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
            if (InputManager.Instance != null && InputManager.Instance.GetPauseInputDown())
            {
                if (settingsPanel != null && settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
                else
                {
                    TogglePauseMenu();
                }
            }
        }

        private void TogglePauseMenu()
        {
            isPaused = !isPaused;

            if (pausePanel != null)
            {
                pausePanel.SetActive(isPaused);
            }

            if (isPaused)
            {
                // Lưu lại trạng thái chuột trước khi pause
                wasCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;

                // Mở khóa chuột để click UI
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                SetGameplayInputBlocked(true);
            }
            else
            {
                // Khôi phục trạng thái chuột
                Cursor.visible = wasCursorVisible;
                Cursor.lockState = previousCursorLockMode;
                SetGameplayInputBlocked(false);
            }
        }

        public void ResumeGame()
        {
            ClearUiSelection();
            if (isPaused)
            {
                TogglePauseMenu();
            }
        }

        private void OpenSettings()
        {
            ClearUiSelection();
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetGameplayInputBlocked(true);
        }

        private void CloseSettings()
        {
            ClearUiSelection();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetGameplayInputBlocked(true);
        }

        private void OnDestroy()
        {
            UnregisterUiListeners();
            SetGameplayInputBlocked(false);
        }

        private static void ClearUiSelection()
        {
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private static void SetGameplayInputBlocked(bool blocked)
        {
            IsMenuOpen = blocked;
            InputManager.GameplayInputBlocked = blocked;
        }

        private void LeaveMatch()
        {
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
