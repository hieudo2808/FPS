using TMPro;
using UnityEngine;

namespace FPS
{
    public sealed class PlayerRosterEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private TMP_Text readiness;

        public void SetPlayer(string name, bool host, bool ready, PlayerCharacterId character)
        {
            playerName.text = name;
            playerName.color = TacticalUiTheme.Text;
            detail.text = character + (host ? "  /  SQUAD LEADER" : "  /  OPERATOR");
            readiness.text = ready ? "READY" : "NOT READY";
            readiness.color = ready ? TacticalUiTheme.Success : TacticalUiTheme.Muted;
        }

        public void SetEmpty(int slot)
        {
            playerName.text = "Open slot " + slot.ToString("00");
            playerName.color = TacticalUiTheme.Muted;
            detail.text = "Share the room code to invite a teammate.";
            readiness.text = "AWAITING PLAYER";
            readiness.color = TacticalUiTheme.Muted;
        }
    }
}
