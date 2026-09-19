using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FPS.UI.Editor
{
    /// <summary>Curated third-party artwork. All runtime references are serialized by the workshop.</summary>
    internal static class SurvivalUiArt
    {
        internal const string Root = "Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/";

        internal static Sprite Get(string key)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + key + ".png");
            if (sprite == null) throw new InvalidOperationException("Missing curated UI sprite: " + key);
            return sprite;
        }

        internal static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.spriteBorder = Border(path);
                importer.SaveAndReimport();
            }
        }

        static Vector4 Border(string path)
        {
            if (path.EndsWith("/Panel.png") || path.EndsWith("/Control.png")) return new Vector4(12, 12, 12, 12);
            if (path.EndsWith("/MenuFocus.png")) return new Vector4(24, 16, 24, 16);
            if (path.EndsWith("/HealthFrame.png")) return new Vector4(22, 10, 22, 10);
            if (path.EndsWith("/HealthTrack.png")) return new Vector4(8, 4, 8, 4);
            return Vector4.zero;
        }

        internal static void Assign(Image image, string key, Color color, bool sliced = false)
        {
            image.sprite = Get(key);
            image.color = color;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1;
        }

        internal static bool IsSurface(string name)
        {
            return name == "SettingsShell" || name == "PlayOnlineModal" || name == "PauseShell"
                || name == "RosterPanel" || name == "MissionPanel" || name == "LobbyHeader";
        }
    }
}
