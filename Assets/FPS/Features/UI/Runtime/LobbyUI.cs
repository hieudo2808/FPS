using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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

        private bool initialized;

        private void Start()
        {
            AttachButtonResetters();
            // Initial Window State
            OpenMainMenu();

            // Main Menu Buttons
            RegisterUiListeners();

            // Play Popup Buttons

            // Settings Popup Buttons

            // Load Player Name from Prefs
            if (playerNameInput != null)
            {
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            }

            // Đăng ký callback MỘT LẦN DUY NHẤT
            SubscribeNetworkCallbacks();

            UpdateStatus("MAIN MENU READY");
            initialized = true;
        }

        private void OnEnable()
        {
            if (initialized)
            {
                RegisterUiListeners();
                SubscribeNetworkCallbacks();
            }
        }

        private void OnDisable()
        {
            UnregisterUiListeners();
            UnsubscribeNetworkCallbacks();
        }

        private void RegisterUiListeners()
        {
            openPlayBtn?.onClick.AddListener(OpenPlayPopup);
            openSettingsBtn?.onClick.AddListener(OpenSettingsPopup);
            quitBtn?.onClick.AddListener(QuitApplication);
            closePlayPopupBtn?.onClick.AddListener(OpenMainMenu);
            hostButton?.onClick.AddListener(OnHostClicked);
            joinButton?.onClick.AddListener(OnJoinClicked);
            closeSettingsPopupBtn?.onClick.AddListener(OpenMainMenu);
            saveNameBtn?.onClick.AddListener(SavePlayerName);
            playerNameInput?.onEndEdit.AddListener(OnPlayerNameEndEdit);
            playerNameInput?.onValueChanged.AddListener(OnPlayerNameValueChanged);
        }

        private void UnregisterUiListeners()
        {
            openPlayBtn?.onClick.RemoveListener(OpenPlayPopup);
            openSettingsBtn?.onClick.RemoveListener(OpenSettingsPopup);
            quitBtn?.onClick.RemoveListener(QuitApplication);
            closePlayPopupBtn?.onClick.RemoveListener(OpenMainMenu);
            hostButton?.onClick.RemoveListener(OnHostClicked);
            joinButton?.onClick.RemoveListener(OnJoinClicked);
            closeSettingsPopupBtn?.onClick.RemoveListener(OpenMainMenu);
            saveNameBtn?.onClick.RemoveListener(SavePlayerName);
            playerNameInput?.onEndEdit.RemoveListener(OnPlayerNameEndEdit);
            playerNameInput?.onValueChanged.RemoveListener(OnPlayerNameValueChanged);
        }

        private void SubscribeNetworkCallbacks()
        {
            if (NetworkGameManager.Instance == null)
                return;

            NetworkGameManager.Instance.OnHostStarted += HandleHostStarted;
            NetworkGameManager.Instance.OnClientConnected += HandleClientConnected;
            NetworkGameManager.Instance.OnClientDisconnected += HandleClientDisconnected;
            NetworkGameManager.Instance.OnConnectionFailed += HandleConnectionFailed;
        }

        private void UnsubscribeNetworkCallbacks()
        {
            if (!NetworkGameManager.HasInstance)
                return;

            NetworkGameManager.Instance.OnHostStarted -= HandleHostStarted;
            NetworkGameManager.Instance.OnClientConnected -= HandleClientConnected;
            NetworkGameManager.Instance.OnClientDisconnected -= HandleClientDisconnected;
            NetworkGameManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
        }

        private void AttachButtonResetters()
        {
            Button[] buttons =
            {
                openPlayBtn, openSettingsBtn, quitBtn, hostButton, joinButton,
                closePlayPopupBtn, closeSettingsPopupBtn, saveNameBtn
            };
            foreach (Button button in buttons)
                UiButtonSelectionResetter.Attach(button);
        }

        private void OnDestroy()
        {
            UnregisterUiListeners();
            UnsubscribeNetworkCallbacks();
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
            PersistPlayerName(showStatus: true);
        }

        private void OnPlayerNameEndEdit(string _)
        {
            PersistPlayerName(showStatus: false);
        }

        private void OnPlayerNameValueChanged(string _)
        {
            PersistPlayerName(showStatus: false);
        }

        private static void QuitApplication()
        {
            Application.Quit();
        }

        private string PersistPlayerName(bool showStatus)
        {
            if (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
            {
                string newName = SanitizePlayerName(playerNameInput.text);
                playerNameInput.SetTextWithoutNotify(newName);
                PlayerPrefs.SetString("PlayerName", newName);
                PlayerPrefs.Save();
                if (showStatus)
                {
                    UpdateStatus($"Name saved as {newName}");
                }

                return newName;
            }

            return PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
        }

        private static string SanitizePlayerName(string rawName)
        {
            string name = string.IsNullOrWhiteSpace(rawName) ? "Player" : rawName.Trim();
            return name.Length > 24 ? name.Substring(0, 24) : name;
        }

        private void OnHostClicked()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            PersistPlayerName(showStatus: false);

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
            EventSystem.current?.SetSelectedGameObject(null);
            PersistPlayerName(showStatus: false);

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
