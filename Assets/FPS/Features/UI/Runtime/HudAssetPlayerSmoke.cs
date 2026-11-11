#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    // Opt-in Development Player fixture; uses the shipped UI and resource loading paths.
    public sealed class HudAssetPlayerSmoke : MonoBehaviour
    {
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-hudAssetSmoke");
            if (index < 0 || index + 1 >= args.Length) return;
            var smoke = new GameObject("HUD asset Player smoke").AddComponent<HudAssetPlayerSmoke>();
            smoke.output = Path.GetFullPath(args[index + 1]);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            if (HUDManager.HasInstance || CampaignMissionController.Instance != null)
            { Fail("Launch the fixture from the configured MainMenu startup scene"); yield break; }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "player-mainmenu.png"));
            yield return new WaitForSecondsRealtime(.5f);
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) canvas.enabled = false;
            var root = new GameObject("Player smoke Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasRoot = root.GetComponent<Canvas>(); canvasRoot.renderMode = RenderMode.ScreenSpaceOverlay;
            HudLayout.ConfigureCanvas(canvasRoot);
            var hud = root.AddComponent<HUDManager>();
            var campaign = root.AddComponent<CampaignMissionController>(); campaign.enabled = false;
            campaign.settings = ScriptableObject.CreateInstance<CampaignSettings>();
            campaign.State.phase = CampaignPhase.Exploring;
            // Match GameScene: inventory owns a top-level Canvas under a non-Canvas manager.
            var inventory = new GameObject("Player smoke Inventory").AddComponent<TeamInventoryUI>();
            yield return null;
            inventory.Toggle();
            Canvas.ForceUpdateCanvases();
            var labels = inventory.GetComponentsInChildren<TMP_Text>();
            var inventoryCanvas = inventory.GetComponentInChildren<Canvas>();
            if (!TeamInventoryUI.IsOpen || labels.Length == 0 || inventoryCanvas == null
                || inventoryCanvas.GetComponent<RectTransform>().rect.width * inventoryCanvas.scaleFactor < Screen.width - 1)
            { Fail("Inventory did not open at viewport size"); yield break; }
            foreach (var label in labels)
            {
                label.ForceMeshUpdate();
                if (label.font != HudSkin.FontAsset() && label.font != HudSkin.FontAsset(true))
                { Fail("Inventory did not use Teko"); yield break; }
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "player-inventory.png"));
            yield return new WaitForSecondsRealtime(.5f);
            inventory.Close();
            hud.SetAimHudVisible(true, false, null, true);
            Canvas.ForceUpdateCanvases();
            var reticle = root.transform.Find("ADSReflexReticle") as RectTransform;
            var sprite = HudSkin.Get("ADSReflexDot");
            if (reticle == null || reticle.GetComponent<Image>().sprite != sprite)
            { Fail("Imported ADS sprite did not resolve"); yield break; }
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, reticle.TransformPoint(reticle.rect.center));
            float centerError = Vector2.Distance(center, new Vector2(Screen.width, Screen.height) * .5f);
            float width = reticle.rect.width * canvasRoot.scaleFactor;
            if (centerError > .5f || Mathf.Abs(width - 10f) > .1f)
            { Fail("Reticle center or physical size is incorrect"); yield break; }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "player-reticle.png"));
            yield return new WaitForSecondsRealtime(.5f);
            var background = new GameObject("Synthetic reticle contrast background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(root.transform, false); background.transform.SetAsFirstSibling();
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.raycastTarget = false;
            foreach (bool light in new[] { false, true })
            {
                background.color = light ? Color.white : Color.black;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, light ? "player-reticle-light.png" : "player-reticle-dark.png"));
                yield return new WaitForSecondsRealtime(.5f);
            }
            Destroy(inventory.gameObject); Destroy(root);
            yield return null;
            if (sprite == null) { Fail("HUD cleanup destroyed shared artwork"); yield break; }
            File.WriteAllText(Path.Combine(output, "player-smoke.json"),
                "{\"status\":\"pass\",\"fixture\":\"Development Player; real inventory and ADS API; synthetic campaign state\",\"width\":" + Screen.width
                + ",\"height\":" + Screen.height + ",\"centerErrorPixels\":" + centerError.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"reticleWidthPixels\":" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            Debug.Log("HUD_ASSET_SMOKE_PASS"); Application.Quit(0);
        }

        private void Fail(string reason)
        {
            File.WriteAllText(Path.Combine(output, "player-smoke-failure.txt"), reason);
            Debug.LogError("HUD_ASSET_SMOKE_FAIL: " + reason); Application.Quit(1);
        }
    }
}
#endif
