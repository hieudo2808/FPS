using FPS;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS.Tests
{
    public sealed class HudAssetReplacementTests
    {
        [Test]
        public void SelectedThemeContainsTheApprovedFramesAndThreeInventoryTabs()
        {
            var theme = SelectedUiTheme.Current;
            Assert.That(theme, Is.Not.Null);
            Assert.That(typeof(SelectedUiTheme).GetFields().Count(field => field.FieldType == typeof(Color)), Is.EqualTo(4),
                "All HUD surfaces must derive from the four editable theme colors.");
            Assert.That(new[] { theme.Panel, theme.Tracker, theme.Vitals, theme.Loadout, theme.ControlBezel,
                theme.ManifestBezel, theme.Module, theme.HoldSurface, theme.WardenIcon, theme.Divider, theme.Keycap,
                theme.ReturnKey, theme.DeviceHeader, theme.DeviceFooter }, Has.None.Null);
            Assert.That(theme.BodyFont.name, Is.EqualTo("LiberationSans SDF"));
            Assert.That(theme.NumericFont.name, Is.EqualTo("Teko-Bold SDF"));
            var root = new GameObject("Three tab inventory fixture");
            try
            {
                var inventory = root.AddComponent<TeamInventoryUI>();
                typeof(TeamInventoryUI).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(inventory, null);
                var tabs = root.GetComponentsInChildren<Button>(true);
                Assert.That(System.Array.Exists(tabs, button => button.name == "JournalTab"), Is.False);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Count(item => item.name.EndsWith("Tab")), Is.EqualTo(3));
                Assert.That(System.Array.Exists(tabs, button => button.name == "FilesTab"), Is.True);
                Assert.That(System.Array.Exists(tabs, button => button.name == "MissionsTab"), Is.True);
                Assert.That(System.Array.Exists(tabs, button => button.name == "KeyItemsTab"), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReticleUsesImportedArtworkAndFullRect()
        {
            var sprite = HudSkin.Get("ADSReflexDot");
            string path = AssetDatabase.GetAssetPath(sprite);
            Assert.That(path, Does.EndWith("/ADSReflexDot.png"));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(64, 64)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(32, 32)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importer.mipmapEnabled || importer.isReadable, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test]
        public void RuntimeFontsResolveAndContainCampaignGlyphs()
        {
            const string sample = "FACTORY AN LAC B2 0123456789 / []%: — · Tiếng Việt: nhiệm vụ, tài liệu, đồng đội, hồi sinh, thất bại, hoàn thành";
            foreach (bool bold in new[] { false, true })
            {
                var font = HudSkin.Font(bold);
                Assert.That(AssetDatabase.GetAssetPath(font), Does.Contain("/Resources/HudSkin/Fonts/"));
                foreach (char letter in sample) Assert.That(font.HasCharacter(letter), Is.True, font.name + ": " + letter);
                var sdf = HudSkin.FontAsset(bold);
                Assert.That(AssetDatabase.GetAssetPath(sdf), Does.EndWith(font.name + " SDF.asset"));
                Assert.That(sdf.atlasTexture, Is.Not.Null);
                Assert.That(sdf.HasCharacters(sample, out uint[] missing, searchFallbacks: true, tryAddCharacter: true), Is.True,
                    "Missing SDF glyphs: " + string.Join(",", missing ?? new uint[0]));
            }
        }


    }
}
