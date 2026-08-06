using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
        [SerializeField] private TMP_Dropdown characterDropdown;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI statusText;

        private bool isReady;
        private bool initialized;

        private static readonly PlayerCharacterId[] CharacterOptions =
        {
            PlayerCharacterId.Clove,
            PlayerCharacterId.Brimstone,
            PlayerCharacterId.Sage,
            PlayerCharacterId.Gekko
        };

        private void Start()
        {
            AttachButtonResetters();

            if (joinCodeText != null && NetworkGameManager.HasInstance)
                joinCodeText.text = NetworkGameManager.Instance.CurrentJoinCode;

            bool isHost = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsHosting;

            if (startGameButton != null)
                startGameButton.gameObject.SetActive(isHost);

            if (difficultyDropdown != null)
            {
                DropdownTemplateUtility.Normalize(difficultyDropdown);
                difficultyDropdown.interactable = isHost;

            }

            if (characterDropdown != null)
            {
                DropdownTemplateUtility.Normalize(characterDropdown);
                characterDropdown.ClearOptions();
                var options = new System.Collections.Generic.List<string>();
                for (int i = 0; i < CharacterOptions.Length; i++)
                    options.Add(CharacterOptions[i].ToString());
                characterDropdown.AddOptions(options);
            }

            RegisterUiListeners();

            UpdateReadyButtonText();
            UpdateStartButton(false);
            UpdateStatus("Waiting for players...");
            initialized = true;
        }

        private void OnEnable()
        {
            if (initialized)
                RegisterUiListeners();
            InvokeRepeating(nameof(TrySubscribe), 0.1f, 0.5f);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(TrySubscribe));
            UnregisterUiListeners();
            Unsubscribe();
        }

        private void RegisterUiListeners()
        {
            copyCodeButton?.onClick.AddListener(CopyJoinCode);
            readyButton?.onClick.AddListener(OnReadyClicked);
            startGameButton?.onClick.AddListener(OnStartGameClicked);
            leaveButton?.onClick.AddListener(OnLeaveClicked);

            bool isHost = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsHosting;
            if (isHost)
                difficultyDropdown?.onValueChanged.AddListener(OnDifficultyDropdownChanged);
            characterDropdown?.onValueChanged.AddListener(OnCharacterDropdownChanged);
        }

        private void UnregisterUiListeners()
        {
            copyCodeButton?.onClick.RemoveListener(CopyJoinCode);
            readyButton?.onClick.RemoveListener(OnReadyClicked);
            startGameButton?.onClick.RemoveListener(OnStartGameClicked);
            leaveButton?.onClick.RemoveListener(OnLeaveClicked);
            difficultyDropdown?.onValueChanged.RemoveListener(OnDifficultyDropdownChanged);
            characterDropdown?.onValueChanged.RemoveListener(OnCharacterDropdownChanged);
        }

        private void OnDestroy()
        {
            UnregisterUiListeners();
        }

        private void AttachButtonResetters()
        {
            UiButtonSelectionResetter.Attach(copyCodeButton);
            UiButtonSelectionResetter.Attach(readyButton);
            UiButtonSelectionResetter.Attach(startGameButton);
            UiButtonSelectionResetter.Attach(leaveButton);
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

        private void OnCharacterDropdownChanged(int value)
        {
            if (value < 0 || value >= CharacterOptions.Length || WaitingRoomManager.Instance == null)
                return;

            WaitingRoomManager.Instance.SetCharacterServerRpc(CharacterOptions[value]);
            EventSystem.current?.SetSelectedGameObject(null);
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

            if (NetworkManager.Singleton != null)
            {
                bool localPlayerFound = false;
                for (int i = 0; i < count; i++)
                {
                    if (players[i].clientId != NetworkManager.Singleton.LocalClientId)
                        continue;

                    localPlayerFound = true;
                    isReady = players[i].isReady;
                    if (characterDropdown != null)
                    {
                        int selected = Array.IndexOf(CharacterOptions, (PlayerCharacterId)players[i].characterId);
                        if (selected >= 0)
                            characterDropdown.SetValueWithoutNotify(selected);
                    }
                    break;
                }

                if (characterDropdown != null)
                    characterDropdown.interactable = localPlayerFound;
            }
            else if (characterDropdown != null)
            {
                characterDropdown.interactable = false;
            }

            for (int i = 0; i < count; i++)
            {
                var data = players[i];
                PlayerCharacterId character = Enum.IsDefined(typeof(PlayerCharacterId), data.characterId)
                    ? (PlayerCharacterId)data.characterId
                    : PlayerCharacterId.Clove;
                CreatePlayerEntry(data.playerName.ToString(), i == 0, data.isReady, character);
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

        private void CreatePlayerEntry(string playerName, bool isHost, bool ready, PlayerCharacterId character)
        {
            if (playerEntryPrefab == null || playerListContainer == null) return;

            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            TextMeshProUGUI nameText = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText == null) return;

            string hostMark = isHost ? " [HOST]" : "";
            string readyMark = ready ? " [READY]" : " [NOT READY]";
            nameText.text = $"{playerName} [{character}]{hostMark}{readyMark}";
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
            // The authoritative list refresh updates the button. Never predict
            // ready state locally, otherwise a rejected RPC leaves the UI stale.
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
