using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.UI.Editor
{
    /// <summary>Explicit HUD review commands; never runs automatically on scene load.</summary>
    public static class HudReviewTools
    {
        [MenuItem("Tools/FPS/UI/HUD Review/Capture Baseline")]
        public static void CaptureBaseline()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Capture authored baseline in Edit Mode.");
            TacticalUiWorkshop.SaveAuthoredScenes();
            UiVisualVerification.Capture("GameScene", "survival", 1920, 1080);
            Directory.CreateDirectory("Documents/UIUX/HUD-Evidence");
            File.Copy("Documents/UIUX/GameScene-survival-1920x1080.png", "Documents/UIUX/HUD-Evidence/baseline-authored.png", true);
            Debug.Log("HUD baseline saved. Authored UI preview; excludes campaign runtime canvases.");
        }

        [MenuItem("Tools/FPS/UI/HUD Review/Start Local Host")]
        public static void StartLocalHost()
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode in MainMenu first.");
            var network = Unity.Netcode.NetworkManager.Singleton;
            if (network == null) throw new System.InvalidOperationException("NetworkManager is missing.");
            if (network.IsListening) throw new System.InvalidOperationException("Use a fresh Play Mode session for local review.");
            // Review-only loopback session. These settings are never saved to authored assets.
            network.NetworkConfig.ConnectionApproval = false;
            network.NetworkConfig.PlayerPrefab = null;
            using var socket = new System.Net.Sockets.UdpClient(0);
            ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            socket.Close();
            network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", port, "127.0.0.1");
            if (!network.StartHost()) throw new System.InvalidOperationException("Local host failed to start.");
            network.SceneManager.OnLoadEventCompleted += SpawnReviewPlayer;
            network.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }

        private static void SpawnReviewPlayer(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> completed, System.Collections.Generic.List<ulong> timedOut)
        {
            if (sceneName != "GameScene") return;
            var network = Unity.Netcode.NetworkManager.Singleton;
            network.SceneManager.OnLoadEventCompleted -= SpawnReviewPlayer;
            var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
            if (catalog == null || !catalog.TryGetPrefab(PlayerCharacterId.Brimstone, out var prefab))
                throw new System.InvalidOperationException("Review player is missing.");
            NetworkSpawnManager.Instance.TryGetNextSpawnPose(out var position, out var rotation);
            var player = Object.Instantiate(prefab, position, rotation);
            player.GetComponent<PlayerHealth>().PrepareInitialSpawn(new SessionPlayerId(1));
            player.GetComponent<Unity.Netcode.NetworkObject>().SpawnAsPlayerObject(network.LocalClientId);
            NetworkMatchStateManager.Instance.EnterWarmup();
            Debug.Log("HUD review local host is ready in GameScene.");
        }

        [MenuItem("Tools/FPS/UI/HUD Review/Resolution/1280x720")]
        public static void Resolution720() => SetResolution(1280, 720);
        [MenuItem("Tools/FPS/UI/HUD Review/Resolution/1920x1080")]
        public static void Resolution1080() => SetResolution(1920, 1080);
        [MenuItem("Tools/FPS/UI/HUD Review/Resolution/3440x1440")]
        public static void ResolutionUltrawide() => SetResolution(3440, 1440);

        private static void SetResolution(int width, int height)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kind = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = System.Activator.CreateInstance(sizeType, System.Enum.ToObject(kind, 1), width, height, "HUD review");
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
            view.Repaint();
            Debug.Log($"HUD fixture resolution requested: {width}x{height}");
        }

        private static float fixtureScale = 1f;
        [MenuItem("Tools/FPS/UI/HUD Review/Scale/100%")]
        public static void Scale100() => SetFixtureScale(1f);
        [MenuItem("Tools/FPS/UI/HUD Review/Scale/125%")]
        public static void Scale125() => SetFixtureScale(1.25f);
        private static void SetFixtureScale(float scale)
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Scale fixture is Play Mode only.");
            fixtureScale = scale;
            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
                if (scaler.GetComponent<Canvas>().isRootCanvas)
                    scaler.referenceResolution = TacticalUiTheme.ReferenceResolution / scale;
        }

        [MenuItem("Tools/FPS/UI/HUD Review/Toggle Inventory")]
        public static void ToggleInventory() => TeamInventoryUI.Instance.Toggle();

        [MenuItem("Tools/FPS/UI/HUD Review/Open Missions")]
        public static void OpenMissions()
        {
            if (!TeamInventoryUI.IsOpen) TeamInventoryUI.Instance.Toggle();
            var button = TeamInventoryUI.Instance.GetComponentsInChildren<Button>(true).First(b => b.name == "MissionsTab");
            button.onClick.Invoke();
        }

        [MenuItem("Tools/FPS/UI/HUD Review/Capture Layout Matrix")]
        public static async void CaptureLayoutMatrix()
        {
            try
            {
                if (!EditorApplication.isPlaying || CampaignMissionController.Instance?.State.phase != CampaignPhase.Exploring)
                    throw new System.InvalidOperationException("Start a local review and wait for Exploring first.");
                foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(3440, 1440) })
                    foreach (float scale in new[] { 1f, 1.25f })
                    {
                        if (!EditorApplication.isPlaying) return;
                        TeamInventoryUI.Instance.Close();
                        SetResolution(resolution.x, resolution.y);
                        SetFixtureScale(scale);
                        await System.Threading.Tasks.Task.Delay(800);
                        CaptureGameplay();
                        await System.Threading.Tasks.Task.Delay(800);
                        OpenMissions();
                        await System.Threading.Tasks.Task.Delay(800);
                        CaptureGameplay();
                        await System.Threading.Tasks.Task.Delay(800);
                    }
                TeamInventoryUI.Instance.Close();
                SetFixtureScale(1f);
                SetResolution(1920, 1080);
                Debug.Log("HUD layout matrix captured: three resolutions, two scales, gameplay and Missions.");
            }
            catch (System.Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Tools/FPS/UI/HUD Review/Capture Gameplay")]
        public static void CaptureGameplay()
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Gameplay evidence requires Play Mode.");
            string phase = TeamInventoryUI.IsOpen ? "inventory" : CampaignHUD.IsDocumentOpen ? "document" : CampaignMissionController.Instance?.State.phase.ToString() ?? "unbound";
            string path = $"Documents/UIUX/HUD-Evidence/{phase}-{Screen.width}x{Screen.height}-{fixtureScale:0.00}";
            Directory.CreateDirectory("Documents/UIUX/HUD-Evidence");
            ScreenCapture.CaptureScreenshot(path + ".png");
            var lines = new System.Collections.Generic.List<string>
            {
                $"Actual Game View: {Screen.width}x{Screen.height}; fixture scale: {fixtureScale}; phase: {phase}",
                $"Tracked objective: {CampaignHUD.Instance?.TrackedObjectiveId}; input blocked: {InputManager.GameplayInputBlocked}",
                $"Cursor: {Cursor.lockState}, visible={Cursor.visible}"
            };
            if (HUDManager.Instance != null)
            { HUDManager.Instance.ValidateCrosshairAlignment(out float error); lines.Add($"Reticle center error: {error:F3}px"); }
            foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>())
                if (text.isActiveAndEnabled && text.canvas != null && text.canvas.renderMode != RenderMode.WorldSpace && !string.IsNullOrEmpty(text.text))
                { text.ForceMeshUpdate(); lines.Add($"TMP {text.name}: {text.text.Replace('\n', '/')} | {text.fontSize * (text.canvas != null ? text.canvas.scaleFactor : 1):F1}px | overflow={text.isTextOverflowing}"); }
            foreach (var text in Object.FindObjectsByType<Text>())
                if (text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text))
                    lines.Add($"Text {text.name}: {text.text.Replace('\n', '/')} | {text.fontSize * text.canvas.scaleFactor:F1}px | preferred={text.preferredHeight:F1}, available={text.rectTransform.rect.height:F1}");
            var groups = new System.Collections.Generic.Dictionary<string, Rect>();
            string[] names = { "MissionTracker", "SquadPanel", "HealthPanel", "InfectionPanel", "WeaponPanel", "SurvivalHotbar", "InteractionPrompt", "Interaction", "Feedback", "MandatorySubtitles" };
            foreach (var rect in Object.FindObjectsByType<RectTransform>())
            {
                if (!names.Contains(rect.name)) continue;
                bool present = false;
                Rect bounds = default;
                foreach (var graphic in rect.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled || graphic.canvas == null || !graphic.canvas.enabled) continue;
                    if (graphic is TMPro.TMP_Text tmp && string.IsNullOrEmpty(tmp.text) || graphic is Text text && string.IsNullOrEmpty(text.text)) continue;
                    var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
                    var area = Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
                    bounds = present ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, area.xMin), Mathf.Min(bounds.yMin, area.yMin), Mathf.Max(bounds.xMax, area.xMax), Mathf.Max(bounds.yMax, area.yMax)) : area;
                    present = true;
                }
                if (present) { groups[rect.name] = bounds; lines.Add($"GROUP {rect.name}: {bounds}"); }
            }
            foreach (var a in groups)
                foreach (var b in groups)
                    if (string.CompareOrdinal(a.Key, b.Key) < 0 && a.Value.Overlaps(b.Value)) lines.Add($"OVERLAP: {a.Key} / {b.Key}");
            File.WriteAllLines(path + ".txt", lines);
            Debug.Log("HUD gameplay capture queued: " + path);
        }
    }
}
