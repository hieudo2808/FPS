using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Campaign-only downed, phase, extraction and debrief presentation.</summary>
    public sealed class CampaignStatusUI : MonoBehaviour
    {
        private CampaignMissionController campaign;
        private PlayerHealth owner;
        private Canvas canvas;
        private TMP_Text phaseText;
        private TMP_Text lifeText;
        private TMP_Text debriefText;
        private GameObject debriefPanel;

        private void Awake() => Build();

        private void Update()
        {
            campaign ??= CampaignMissionController.Instance;
            owner ??= Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>();
            if (campaign == null || owner == null) { canvas.enabled = false; return; }
            canvas.enabled = !InGameMenuUI.IsMenuOpen && !TeamInventoryUI.IsOpen;
            if (!canvas.enabled) return;

            float remaining = campaign.Remaining;
            phaseText.gameObject.SetActive(campaign.State.phase is CampaignPhase.Preparing or CampaignPhase.Encounter or CampaignPhase.AwaitingParty);
            phaseText.text = campaign.State.phase switch
            {
                CampaignPhase.Preparing => $"PREPARE  ·  READY {campaign.ReadyCount}/{campaign.ActivePlayerCount}",
                CampaignPhase.Encounter => $"EXTRACTION  ·  {Mathf.CeilToInt(remaining)}s",
                CampaignPhase.AwaitingParty => "BOARDING  ·  WAIT FOR THE SQUAD",
                _ => ""
            };

            bool downed = owner.LifeState == PlayerLifeState.Downed;
            bool dead = owner.LifeState is PlayerLifeState.Dead or PlayerLifeState.Spectating;
            lifeText.gameObject.SetActive(downed || dead);
            if (downed)
                lifeText.text = $"DOWNED\nBLEED OUT IN {Mathf.Max(0, Mathf.CeilToInt((float)(owner.LifeStateDeadline - campaign.Now)))}\nWAIT FOR REVIVE";
            else if (dead)
                lifeText.text = campaign.State.phase == CampaignPhase.Failed ? "MISSION FAILED" : "SPECTATING\nFIRE / AIM  ·  CHANGE OPERATOR";

            bool finished = campaign.State.phase is CampaignPhase.Completed or CampaignPhase.Failed;
            debriefPanel.SetActive(finished);
            if (finished)
            {
                CampaignRunSummary summary = campaign.RunSummary;
                if (summary == null)
                {
                    debriefPanel.SetActive(false);
                    return;
                }
                int optionalTotal = summary.optionalFilesTotal > 0 ? summary.optionalFilesTotal
                    : campaign.ContentCatalog?.files?.Count(f => f != null && !f.required) ?? 0;
                debriefText.text = $"MISSION { (summary.result == CampaignPhase.Failed ? "FAILED" : "COMPLETE") }\n\n"
                    + $"TIME  {FormatTime(summary.durationSeconds)}\n"
                    + $"SURVIVORS  {summary.survivors}/{summary.rosterSize}\n"
                    + $"TEAM DOWNS  {summary.teamDowns}\n"
                    + $"ZOMBIES ELIMINATED  {summary.zombieKills}\n\n"
                    + $"DISCOVERY\nOPTIONAL FILES  {summary.optionalFilesFound}/{optionalTotal}\n"
                    + $"FACTORY CASE  {(summary.factoryCaseSecured ? "SECURED" : "NOT SECURED")}\n"
                    + $"LAB CASE  {(summary.labCaseSecured ? "SECURED" : "NOT SECURED")}";
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static int CountFiles(ulong mask)
        {
            int count = 0;
            while (mask != 0) { count += (int)(mask & 1); mask >>= 1; }
            return count;
        }

        private void Build()
        {
            GameObject canvasObject = new("CampaignStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = TacticalUiTheme.CampaignOrder + 1;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = TacticalUiTheme.ReferenceResolution;
            phaseText = Text(canvasObject.transform, "Phase", new Vector2(.32f, .87f), new Vector2(.68f, .95f), 24, FontStyles.Bold); phaseText.alignment = TextAlignmentOptions.Center;
            lifeText = Text(canvasObject.transform, "LifeState", new Vector2(.22f, .38f), new Vector2(.78f, .63f), 32, FontStyles.Bold); lifeText.alignment = TextAlignmentOptions.Center; lifeText.color = TacticalUiTheme.Danger;
            debriefPanel = new GameObject("Debrief", typeof(RectTransform), typeof(Image)); debriefPanel.transform.SetParent(canvasObject.transform, false); RectTransform panel = debriefPanel.GetComponent<RectTransform>(); panel.anchorMin = new Vector2(.29f, .19f); panel.anchorMax = new Vector2(.71f, .81f); panel.offsetMin = panel.offsetMax = Vector2.zero; panel.GetComponent<Image>().color = new Color(.03f, .04f, .04f, .96f);
            debriefText = Text(panel, "Summary", new Vector2(.08f, .08f), new Vector2(.92f, .92f), 25, FontStyles.Normal); debriefText.alignment = TextAlignmentOptions.Center;
            phaseText.gameObject.SetActive(false); lifeText.gameObject.SetActive(false); debriefPanel.SetActive(false);
        }

        private static TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max, float size, FontStyles style)
        {
            TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>(); text.transform.SetParent(parent, false); RectTransform rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; text.fontSize = size; text.fontStyle = style; text.color = TacticalUiTheme.Text; text.raycastTarget = false; text.enableWordWrapping = true; return text;
        }
    }
}
