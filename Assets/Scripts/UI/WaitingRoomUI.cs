using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FPS
{
    /// <summary>
    /// UI cho Waiting Room (LobbyScene).
    /// Gắn vào Canvas trong LobbyScene.
    /// </summary>
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

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI statusText;

        private bool isReady;

        private void Start()
        {
            // Button listeners
            if (copyCodeButton != null)
                copyCodeButton.onClick.AddListener(CopyJoinCode);

            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);

            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeaveClicked);

            // Join Code
            if (joinCodeText != null && NetworkGameManager.HasInstance)
                joinCodeText.text = NetworkGameManager.Instance.CurrentJoinCode;

            // Start button chỉ Host thấy
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsHosting);

            UpdateReadyButtonText();
            UpdateStartButton(false);
            UpdateStatus("Waiting for players...");
        }

        private void OnEnable()
        {
            // Đợi WaitingRoomManager spawn (có thể chậm hơn UI)
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

            // Unsubscribe trước để tránh duplicate
            Unsubscribe();

            WaitingRoomManager.Instance.OnPlayerListChanged += RefreshPlayerList;
            WaitingRoomManager.Instance.OnAllReadyChanged += UpdateStartButton;

            CancelInvoke(nameof(TrySubscribe));

            // Refresh ngay lần đầu
            RefreshPlayerList();
        }

        private void Unsubscribe()
        {
            if (WaitingRoomManager.Instance != null)
            {
                WaitingRoomManager.Instance.OnPlayerListChanged -= RefreshPlayerList;
                WaitingRoomManager.Instance.OnAllReadyChanged -= UpdateStartButton;
            }
        }

        // ==========================================
        // PLAYER LIST
        // ==========================================

        private void RefreshPlayerList()
        {
            if (WaitingRoomManager.Instance == null) return;

            // Xóa entries cũ
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

                if (playerEntryPrefab != null && playerListContainer != null)
                {
                    GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
                    var nameText = entry.GetComponentInChildren<TextMeshProUGUI>();
                    if (nameText != null)
                    {
                        string readyMark = data.isReady ? " ✓" : "";
                        string hostMark = (i == 0) ? " [HOST]" : "";
                        nameText.text = $"{data.playerName}{hostMark}{readyMark}";
                        nameText.color = data.isReady ? Color.green : Color.white;
                    }
                }
            }

            // Player count
            if (playerCountText != null)
                playerCountText.text = $"{count}/4 Players";

            // Update join code (có thể chưa set lúc Start)
            if (joinCodeText != null && NetworkGameManager.HasInstance)
                joinCodeText.text = NetworkGameManager.Instance.CurrentJoinCode;
        }

        // ==========================================
        // ACTIONS
        // ==========================================

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

        // ==========================================
        // UI HELPERS
        // ==========================================

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
