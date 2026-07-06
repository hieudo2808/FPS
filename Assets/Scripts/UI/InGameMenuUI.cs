using UnityEngine;
using UnityEngine.UI;

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
        public static bool IsMenuOpen { get; private set; }

        private void Start()
        {
            // Ẩn tất cả khi mới vào game
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (leaveMatchButton != null) leaveMatchButton.onClick.AddListener(LeaveMatch);
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
            SetGameplayInputBlocked(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
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
            if (isPaused)
            {
                TogglePauseMenu();
            }
        }

        private void OpenSettings()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetGameplayInputBlocked(true);
        }

        private void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetGameplayInputBlocked(true);
        }

        private void OnDestroy()
        {
            SetGameplayInputBlocked(false);
        }

        private static void SetGameplayInputBlocked(bool blocked)
        {
            IsMenuOpen = blocked;
            InputManager.GameplayInputBlocked = blocked;
        }

        private void LeaveMatch()
        {
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
