using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class WaitingRoomUI : MonoBehaviour
    {
        [Header("Join Code")]
        [SerializeField] private TextMeshProUGUI joinCodeText;
        [SerializeField] private Button copyCodeButton;

        [Header("Player List")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Actions")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveButton;

        [Header("Settings")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI statusText;

        private bool isReady;

        private void Start()
        {
            if (copyCodeButton != null)
                copyCodeButton.onClick.AddListener(CopyJoinCode);

            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeaveClicked);

            if (joinCodeText != null && NetworkGameManager.HasInstance)
                joinCodeText.text = NetworkGameManager.Instance.CurrentJoinCode;

            bool isHost = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsHosting;

            if (startGameButton != null)
                startGameButton.gameObject.SetActive(isHost);

            if (difficultyDropdown != null)
            {
                DropdownTemplateUtility.Normalize(difficultyDropdown);
                difficultyDropdown.interactable = isHost;

                if (isHost)
                {
                    difficultyDropdown.onValueChanged.AddListener(OnDifficultyDropdownChanged);
                }
            }

            UpdateReadyButtonText();
            UpdateStartButton(false);
            UpdateStatus("Waiting for players...");
        }

        private void OnEnable()
        {
            InvokeRepeating(nameof(TrySubscribe), 0.1f, 0.5f);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(TrySubscribe));
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (WaitingRoomManager.Instance == null) return;

            Unsubscribe();

            WaitingRoomManager.Instance.OnPlayerListChanged += RefreshPlayerList;
            WaitingRoomManager.Instance.OnAllReadyChanged += UpdateStartButton;
            WaitingRoomManager.Instance.LobbyDifficulty.OnValueChanged += HandleDifficultyChanged;

            CancelInvoke(nameof(TrySubscribe));
            RefreshPlayerList();
        }

        private void Unsubscribe()
        {
            if (WaitingRoomManager.Instance != null)
            {
                WaitingRoomManager.Instance.OnPlayerListChanged -= RefreshPlayerList;
                WaitingRoomManager.Instance.OnAllReadyChanged -= UpdateStartButton;
                WaitingRoomManager.Instance.LobbyDifficulty.OnValueChanged -= HandleDifficultyChanged;
            }
        }

        private void OnDifficultyDropdownChanged(int value)
        {
            if (WaitingRoomManager.Instance != null)
            {
                WaitingRoomManager.Instance.SetDifficultyServerRpc((DifficultyLevel)value);
            }
        }

        private void RefreshPlayerList()
        {
            if (WaitingRoomManager.Instance == null) return;

            if (playerListContainer != null)
            {
                for (int i = playerListContainer.childCount - 1; i >= 0; i--)
                    Destroy(playerListContainer.GetChild(i).gameObject);
            }

            var players = WaitingRoomManager.Instance.Players;
            int count = players.Count;

            for (int i = 0; i < count; i++)
            {
                var data = players[i];
                CreatePlayerEntry(data.playerName.ToString(), i == 0, data.isReady);
            }

            for (int i = count; i < 4; i++)
            {
                CreateEmptyEntry(i + 1);
            }

            if (playerCountText != null)
                playerCountText.text = $"{count}/4 Players";

            if (joinCodeText != null && NetworkGameManager.HasInstance)
                joinCodeText.text = NetworkGameManager.Instance.CurrentJoinCode;
        }

        private void CreatePlayerEntry(string playerName, bool isHost, bool ready)
        {
            if (playerEntryPrefab == null || playerListContainer == null) return;

            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            TextMeshProUGUI nameText = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText == null) return;

            string hostMark = isHost ? " [HOST]" : "";
            string readyMark = ready ? " [READY]" : " [NOT READY]";
            nameText.text = $"{playerName}{hostMark}{readyMark}";
            nameText.color = ready ? new Color(0.54f, 1.0f, 0.72f, 1.0f) : Color.white;
        }

        private void CreateEmptyEntry(int slotNumber)
        {
            if (playerEntryPrefab == null || playerListContainer == null) return;

            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            TextMeshProUGUI nameText = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText == null) return;

            nameText.text = $"EMPTY SLOT {slotNumber}";
            nameText.color = new Color(0.48f, 0.56f, 0.62f, 1.0f);
        }

        private void HandleDifficultyChanged(DifficultyLevel previousValue, DifficultyLevel newValue)
        {
            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)newValue);
            }
        }

        private void OnReadyClicked()
        {
            if (WaitingRoomManager.Instance == null) return;

            WaitingRoomManager.Instance.ToggleReadyServerRpc();
            isReady = !isReady;
            UpdateReadyButtonText();
        }

        private void OnStartGameClicked()
        {
            if (WaitingRoomManager.Instance == null) return;

            WaitingRoomManager.Instance.StartMatch();
            UpdateStatus("Starting match...");

            if (startGameButton != null)
                startGameButton.interactable = false;
        }

        private void OnLeaveClicked()
        {
            NetworkGameManager.Instance?.Disconnect();
        }

        private void CopyJoinCode()
        {
            if (NetworkGameManager.HasInstance && !string.IsNullOrEmpty(NetworkGameManager.Instance.CurrentJoinCode))
            {
                GUIUtility.systemCopyBuffer = NetworkGameManager.Instance.CurrentJoinCode;
                UpdateStatus("Join code copied!");
            }
        }

        private void UpdateReadyButtonText()
        {
            if (readyButtonText != null)
                readyButtonText.text = isReady ? "CANCEL READY" : "READY";
        }

        private void UpdateStartButton(bool allReady)
        {
            if (startGameButton != null && NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsHosting)
                startGameButton.interactable = allReady;
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
