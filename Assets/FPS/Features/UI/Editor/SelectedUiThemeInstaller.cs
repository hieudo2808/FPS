using UnityEditor;
using UnityEngine;
using TMPro;

namespace FPS.UI.Editor
{
    internal static class SelectedUiThemeInstaller
    {
        [MenuItem("Tools/FPS/UI/Selected Mockups/Install Theme")]
        private static void Install()
        {
            const string root = "Assets/FPS/Features/UI/Content/Resources/HudSkin/";
            foreach (string name in new[] { "Tracker", "Vitals", "Loadout", "ControlBezel", "ManifestBezel", "Module", "Screen", "ModuleHeader", "Sidebar", "PaperArea", "Paper", "Notice", "HoldButton", "LockedButton", "Warden", "Classic", "Frag", "Fire", "Medkit", "Antidote", "Divider", "Keycap", "ReturnKey", "DeviceHeader", "DeviceFooter" })
            {
                string path = root + "Selected/" + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = name switch
                {
                    "Tracker" => new Vector4(24, 28, 24, 24),
                    "Vitals" => new Vector4(24, 24, 24, 28),
                    "Loadout" => new Vector4(28, 24, 24, 28),
                    "ControlBezel" or "ManifestBezel" => new Vector4(68, 96, 68, 64),
                    "Keycap" => Vector4.one,
                    "Warden" or "Classic" or "Frag" or "Fire" or "Medkit" or "Antidote" or "Divider" or "ReturnKey" => Vector4.zero,
                    _ => new Vector4(2, 2, 2, 2)
                };
                importer.SaveAndReimport();
            }
            string themePath = root + "SelectedTheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<SelectedUiTheme>(themePath);
            if (theme == null) { theme = ScriptableObject.CreateInstance<SelectedUiTheme>(); AssetDatabase.CreateAsset(theme, themePath); }
            theme.LegacyBody = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            theme.BodyFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            theme.NumericFont = HudSkin.FontAsset(true);
            theme.Panel = AssetDatabase.LoadAssetAtPath<Sprite>(SurvivalUiArt.Root + "Surfaces/Panel.png");
            theme.Tracker = HudSkin.Get("Selected/Tracker"); theme.Vitals = HudSkin.Get("Selected/Vitals");
            theme.Loadout = HudSkin.Get("Selected/Loadout"); theme.Module = HudSkin.Get("Selected/Module");
            theme.ControlBezel = HudSkin.Get("Selected/ControlBezel"); theme.ManifestBezel = HudSkin.Get("Selected/ManifestBezel");
            theme.Screen = HudSkin.Get("Selected/Screen"); theme.ModuleHeader = HudSkin.Get("Selected/ModuleHeader");
            theme.Sidebar = HudSkin.Get("Selected/Sidebar"); theme.PaperArea = HudSkin.Get("Selected/PaperArea"); theme.PaperSurface = HudSkin.Get("Selected/Paper");
            theme.NoticeSurface = HudSkin.Get("Selected/Notice"); theme.HoldSurface = HudSkin.Get("Selected/HoldButton"); theme.LockedSurface = HudSkin.Get("Selected/LockedButton");
            theme.WardenIcon = HudSkin.Get("Selected/Warden");
            theme.Divider = HudSkin.Get("Selected/Divider"); theme.Keycap = HudSkin.Get("Selected/Keycap");
            theme.ReturnKey = HudSkin.Get("Selected/ReturnKey");
            theme.DeviceHeader = HudSkin.Get("Selected/DeviceHeader"); theme.DeviceFooter = HudSkin.Get("Selected/DeviceFooter");
            theme.PanelAlpha = 1 - Mathf.GammaToLinearSpace(1 - .90f);
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
        }
    }
}
