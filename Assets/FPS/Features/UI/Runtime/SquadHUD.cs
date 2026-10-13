using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    /// <summary>Three stable campaign teammate rows; the local player remains in the existing HUD.</summary>
    public sealed class SquadHUD : MonoBehaviour
    {
        private sealed class Row
        {
            public GameObject root;
            public TMP_Text portrait;
            public TMP_Text name;
            public TMP_Text character;
            public TMP_Text status;
            public Image health;
            public Image infection;
            public ulong playerId;
            public PlayerHealth player;
            public PlayerInfectionController infectionSource;
        }

        private readonly List<Row> rows = new(3);
        private CampaignMissionController campaign;
        private Canvas canvas;
        private Sprite barSprite;
        private float nextBindingRefresh;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (campaign != null) campaign.Changed -= RebuildRoster;
        }

        private void Update()
        {
            if (campaign == null && CampaignMissionController.Instance != null)
            {
                campaign = CampaignMissionController.Instance;
                campaign.Changed += RebuildRoster;
                RebuildRoster();
            }
            if (campaign == null || !campaign.IsSpawned || campaign.InsertionPartySize == 0)
            {
                canvas.enabled = false;
                return;
            }

            canvas.enabled = HudLayout.GameplayVisible;
            if (Time.unscaledTime >= nextBindingRefresh)
            {
                nextBindingRefresh = Time.unscaledTime + 1f;
                BindPlayers();
            }
            RenderRows();
        }

        private void RebuildRoster()
        {
            if (campaign == null || campaign.InsertionPartySize == 0) return;
            ulong localId = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>()?.StablePlayerId.Value ?? 0;
            int rowIndex = 0;
            for (int i = 0; i < campaign.InsertionPartySize && rowIndex < rows.Count; i++)
            {
                CampaignInsertionParticipant entry = campaign.InsertionParticipant(i);
                if (entry.playerId == localId) continue;
                Row row = rows[rowIndex++];
                row.root.SetActive(true);
                row.playerId = entry.playerId;
                row.name.text = CampaignCodename.For(entry.character);
                row.character.text = entry.character.ToString().ToUpperInvariant();
                row.portrait.text = entry.character.ToString()[0].ToString();
            }
            for (; rowIndex < rows.Count; rowIndex++)
            {
                rows[rowIndex].root.SetActive(false);
                rows[rowIndex].playerId = 0;
                rows[rowIndex].player = null;
            }
            BindPlayers();
        }

        private void BindPlayers()
        {
            if (campaign == null) return;
            foreach (Row row in rows)
            {
                if (row.playerId == 0) continue;
                PlayerHealth found = null;
                foreach (PlayerHealth player in campaign.Players)
                    if (player != null && player.StablePlayerId.Value == row.playerId) { found = player; break; }
                row.player = found;
                row.infectionSource = found != null ? found.GetComponent<PlayerInfectionController>() : null;
            }
        }

        private void RenderRows()
        {
            if (campaign == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (!row.root.activeSelf) continue;
                bool connected = false;
                for (int slot = 0; slot < campaign.InsertionPartySize; slot++)
                {
                    CampaignInsertionParticipant entry = campaign.InsertionParticipant(slot);
                    if (entry.playerId == row.playerId) { connected = entry.connected; break; }
                }
                if (!connected || row.player == null)
                {
                    row.status.text = "RECONNECTING";
                    row.status.color = TacticalUiTheme.Muted;
                    row.health.fillAmount = 0;
                    row.infection.fillAmount = 0;
                    continue;
                }

                float health = row.player.MaxHealth > 0 ? Mathf.Clamp01(row.player.CurrentHealth / row.player.MaxHealth) : 0;
                float infection = row.infectionSource != null ? Mathf.Clamp01(row.infectionSource.CurrentInfection / PlayerInfectionController.MaxInfection) : 0;
                row.health.fillAmount = health;
                row.infection.fillAmount = infection;
                row.health.color = health <= .25f ? TacticalUiTheme.Danger : health <= .5f ? new Color(.94f, .68f, .23f) : TacticalUiTheme.Success;
                row.infection.color = infection >= .7f ? TacticalUiTheme.Danger : TacticalUiTheme.Accent;

                string label;
                Color color = TacticalUiTheme.Text;
                switch (row.player.LifeState)
                {
                    case PlayerLifeState.Downed:
                        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(row.player.LifeStateDeadline - campaign.Now)));
                        label = Direction(row.player.transform.position) + " DOWNED · " + seconds + "s";
                        color = TacticalUiTheme.Danger;
                        break;
                    case PlayerLifeState.Dead: label = "DEAD"; color = TacticalUiTheme.Danger; break;
                    case PlayerLifeState.Spectating: label = "SPECTATING"; color = TacticalUiTheme.Muted; break;
                    default:
                        if (infection >= .01f) { label = $"INFECTED · {Mathf.CeilToInt(infection * 100)}%"; color = TacticalUiTheme.Accent; }
                        else if (health <= .5f) { label = "LOW HEALTH"; color = new Color(.94f, .68f, .23f); }
                        else label = "ALIVE";
                        break;
                }
                row.status.text = label;
                row.status.color = color;
            }
        }

        private static string Direction(Vector3 target)
        {
            var camera = Camera.main;
            if (camera == null) return "";
            var direction = Vector3.ProjectOnPlane(target - camera.transform.position, Vector3.up).normalized;
            float right = Vector3.Dot(camera.transform.right, direction);
            float forward = Vector3.Dot(camera.transform.forward, direction);
            return Mathf.Abs(right) > Mathf.Abs(forward) ? right >= 0 ? "RIGHT" : "LEFT" : forward >= 0 ? "AHEAD" : "BEHIND";
        }

        private void Build()
        {
            GameObject canvasObject = new("SquadHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = TacticalUiTheme.HudOrder + 1;
            HudLayout.ConfigureCanvas(canvas);
            RectTransform root = new GameObject("SquadPanel", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            root.SetParent(HudLayout.PeripheralRoot(canvas), false); HudLayout.Corner(root, Vector2.zero, new Vector2(28, 328), new Vector2(HudLayout.VitalsWidth, 304));
            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            barSprite = HudSkin.Get("Fill");
            for (int i = 0; i < 3; i++) rows.Add(BuildRow(root, i, barSprite));
            canvas.enabled = false;
        }

        private static Row BuildRow(Transform parent, int index, Sprite barSprite)
        {
            GameObject root = new($"SquadMember_{index + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            root.transform.SetParent(parent, false); HudSkin.Apply(root.GetComponent<Image>(), "Button"); root.GetComponent<LayoutElement>().preferredHeight = 96;
            TMP_Text portrait = Text(root.transform, "Portrait", new Vector2(16f / 350, 56f / 96), new Vector2(.14f, 84f / 96), 24, FontStyles.Bold); portrait.alignment = TextAlignmentOptions.Center;
            TMP_Text name = Text(root.transform, "Name", new Vector2(.17f, 56f / 96), new Vector2(.49f, 84f / 96), 21, FontStyles.Bold);
            TMP_Text character = Text(root.transform, "Character", new Vector2(.50f, 56f / 96), new Vector2(334f / 350, 84f / 96), 15); character.alignment = TextAlignmentOptions.Right;
            TMP_Text status = Text(root.transform, "Status", new Vector2(16f / 350, .25f), new Vector2(334f / 350, 56f / 96), 24, FontStyles.Bold);
            Image healthTrack = Image(root.transform, "HealthTrack", new Vector2(.17f, .18f), new Vector2(334f / 350, .23f)); healthTrack.sprite = barSprite; healthTrack.color = TacticalUiTheme.Control;
            Image health = Image(root.transform, "Health", new Vector2(.17f, .18f), new Vector2(334f / 350, .23f)); health.type = UnityEngine.UI.Image.Type.Filled; health.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; health.color = TacticalUiTheme.Success;
            Image infection = Image(root.transform, "Infection", new Vector2(.17f, .125f), new Vector2(334f / 350, .155f)); infection.type = UnityEngine.UI.Image.Type.Filled; infection.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; infection.color = TacticalUiTheme.Accent;
            health.sprite = infection.sprite = barSprite;
            root.SetActive(false);
            return new Row { root = root, portrait = portrait, name = name, character = character, status = status, health = health, infection = infection };
        }

        private static TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max, float size, FontStyles style = FontStyles.Normal)
        {
            TMP_Text text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>(); text.transform.SetParent(parent, false);
            RectTransform rect = text.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            text.fontSize = Mathf.Max(24, size); text.fontStyle = style; text.color = TacticalUiTheme.Text; text.raycastTarget = false; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }

        private static Image Image(Transform parent, string name, Vector2 min, Vector2 max)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); image.transform.SetParent(parent, false);
            RectTransform rect = image.rectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; image.raycastTarget = false; return image;
        }
    }
}
