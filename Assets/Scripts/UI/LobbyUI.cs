using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FPS
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject playPopup;
        [SerializeField] private GameObject settingsPopup;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button openPlayBtn;
        [SerializeField] private Button openSettingsBtn;
        [SerializeField] private Button quitBtn;

        [Header("Play Popup Elements")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button closePlayPopupBtn;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings Popup Elements")]
        [SerializeField] private Button closeSettingsPopupBtn;
        [SerializeField] private Button saveNameBtn;
        [SerializeField] private TMP_InputField playerNameInput;

        private void Start()
        {
            // Initial Window State
            OpenMainMenu();

            // Main Menu Buttons
            if (openPlayBtn != null) openPlayBtn.onClick.AddListener(OpenPlayPopup);
            if (openSettingsBtn != null) openSettingsBtn.onClick.AddListener(OpenSettingsPopup);
            if (quitBtn != null) quitBtn.onClick.AddListener(Application.Quit);

            // Play Popup Buttons
            if (closePlayPopupBtn != null) closePlayPopupBtn.onClick.AddListener(OpenMainMenu);
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);

            // Settings Popup Buttons
            if (closeSettingsPopupBtn != null) closeSettingsPopupBtn.onClick.AddListener(OpenMainMenu);
            if (saveNameBtn != null) saveNameBtn.onClick.AddListener(SavePlayerName);

            // Load Player Name from Prefs
            if (playerNameInput != null)
            {
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            }

            // Đăng ký callback MỘT LẦN DUY NHẤT
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnHostStarted += HandleHostStarted;
                NetworkGameManager.Instance.OnClientConnected += HandleClientConnected;
                NetworkGameManager.Instance.OnClientDisconnected += HandleClientDisconnected;
                NetworkGameManager.Instance.OnConnectionFailed += HandleConnectionFailed;
            }

            UpdateStatus("MAIN MENU READY");
        }

        private void OnDestroy()
        {
            if (NetworkGameManager.HasInstance)
            {
                NetworkGameManager.Instance.OnHostStarted -= HandleHostStarted;
                NetworkGameManager.Instance.OnClientConnected -= HandleClientConnected;
                NetworkGameManager.Instance.OnClientDisconnected -= HandleClientDisconnected;
                NetworkGameManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
            }
        }

        private void HandleHostStarted()
        {
            UpdateStatus($"SERVER ESTABLISHED. JOIN CODE: {NetworkGameManager.Instance.CurrentJoinCode}");
        }

        private void HandleClientConnected()
        {
            UpdateStatus("CONNECTED. ENTERING MATCH...");
        }

        private void HandleClientDisconnected()
        {
            UpdateStatus("CONNECTION LOST");
            SetButtonsInteractable(true);
        }

        private void HandleConnectionFailed(string msg)
        {
            UpdateStatus($"FAILED: {msg}");
            SetButtonsInteractable(true);
        }

        private void OpenMainMenu()
        {
            if (mainPanel) mainPanel.SetActive(true);
            if (playPopup) playPopup.SetActive(false);
            if (settingsPopup) settingsPopup.SetActive(false);
            UpdateStatus("MAIN MENU READY");
        }

        private void OpenPlayPopup()
        {
            if (mainPanel) mainPanel.SetActive(true); // Keep background
            if (playPopup) playPopup.SetActive(true);
            if (settingsPopup) settingsPopup.SetActive(false);
            UpdateStatus("WAITING FOR ACTION");
        }

        private void OpenSettingsPopup()
        {
            if (mainPanel) mainPanel.SetActive(true);
            if (playPopup) playPopup.SetActive(false);
            if (settingsPopup) settingsPopup.SetActive(true);
        }

        private void SavePlayerName()
        {
            if (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
            {
                string newName = playerNameInput.text;
                PlayerPrefs.SetString("PlayerName", newName);
                PlayerPrefs.Save();
                UpdateStatus($"Name saved as {newName}");
            }
        }

        private void OnHostClicked()
        {
            if (NetworkGameManager.Instance == null)
            {
                UpdateStatus("Error: NetworkGameManager not found!");
                return;
            }

            UpdateStatus("CREATING SERVER THROUGH RELAY...");
            SetButtonsInteractable(false);
            NetworkGameManager.Instance.StartHostGame();
        }

        private void OnJoinClicked()
        {
            if (NetworkGameManager.Instance == null)
            {
                UpdateStatus("Error: NetworkGameManager not found!");
                return;
            }

            string code = joinCodeInput != null ? joinCodeInput.text : "";
            if (string.IsNullOrEmpty(code))
            {
                UpdateStatus("ERROR: ENTER A JOIN CODE");
                return;
            }

            UpdateStatus($"CONNECTING TO ROOM {code}...");
            SetButtonsInteractable(false);
            NetworkGameManager.Instance.JoinGame(code);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (hostButton != null) hostButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
            if (closePlayPopupBtn != null) closePlayPopupBtn.interactable = interactable;
            if (joinCodeInput != null) joinCodeInput.interactable = interactable;
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}

