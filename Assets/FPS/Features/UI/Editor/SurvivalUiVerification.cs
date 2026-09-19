using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.UI.Editor
{
    /// <summary>Renders the real runtime SurvivalHotbar at the three supported HUD sizes.</summary>
    public static class SurvivalUiVerification
    {
        public static void CaptureAll()
        {
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                Capture(size.x, size.y);
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        }

        private static void Capture(int width, int height)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null; Texture2D image = null;
            Camera camera = null;
            try
            {
                var canvasGo = new GameObject("Survival HUD Preview", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasGo, preview);
                SetLayerRecursive(canvasGo.transform, 5);
                var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 20;
                var rect = (RectTransform)canvasGo.transform; rect.sizeDelta = new Vector2(width, height);
                rect.localScale = Vector3.one / Mathf.Min(width / 1920f, height / 1080f);
                rect.position = Vector3.zero;
                var inventoryGo = new GameObject("Preview Inventory"); SceneManager.MoveGameObjectToScene(inventoryGo, preview);
                var inventory = inventoryGo.AddComponent<SurvivalInventory>();
                var hotbarGo = new GameObject("SurvivalHotbar", typeof(RectTransform)); hotbarGo.transform.SetParent(canvasGo.transform, false);
                SetLayerRecursive(hotbarGo.transform, 5);
                hotbarGo.AddComponent<SurvivalHotbar>().Bind(inventory);
                SetLayerRecursive(canvasGo.transform, 5);
                var cameraGo = new GameObject("HUD Preview Camera"); SceneManager.MoveGameObjectToScene(cameraGo, preview);
                camera = cameraGo.AddComponent<Camera>(); camera.scene = preview; camera.orthographic = true; camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .04f, .035f); camera.cullingMask = ~0;
                target = new RenderTexture(width, height, 24); camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("Documents/UIUX");
                File.WriteAllBytes($"Documents/UIUX/SurvivalHotbar-{width}x{height}.png", image.EncodeToPNG());
                Debug.Log($"SURVIVAL_UI_CAPTURE {width}x{height}");
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                RenderTexture.active = null;
                if (target != null) Object.DestroyImmediate(target);
                if (image != null) Object.DestroyImmediate(image);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayerRecursive(child, layer);
        }
    }
}
