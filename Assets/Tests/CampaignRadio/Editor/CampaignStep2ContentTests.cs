using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class CampaignStep2ContentTests
    {
        private const string CatalogPath = "Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset";

        [Test]
        public void HintTier_UsesShortened25_45_75Thresholds()
        {
            Assert.That(CampaignRules.HintTier(0f), Is.EqualTo(0));
            Assert.That(CampaignRules.HintTier(24.9f), Is.EqualTo(0));
            Assert.That(CampaignRules.HintTier(25f), Is.EqualTo(1));
            Assert.That(CampaignRules.HintTier(44.9f), Is.EqualTo(1));
            Assert.That(CampaignRules.HintTier(45f), Is.EqualTo(2));
            Assert.That(CampaignRules.HintTier(74.9f), Is.EqualTo(2));
            Assert.That(CampaignRules.HintTier(75f), Is.EqualTo(3));
        }

        [Test]
        public void ChapterLabels_MatchStandardizedEnglishLore()
        {
            Assert.That(CampaignRules.ChapterName(CampaignChapter.Factory), Is.EqualTo("FACTORY"));
            Assert.That(CampaignRules.ChapterName(CampaignChapter.Asylum), Is.EqualTo("NORTHBRIDGE"));
            Assert.That(CampaignRules.ChapterName(CampaignChapter.Laboratory), Is.EqualTo("LABORATORY B2"));

            Assert.That(CampaignRules.ChapterGoal(CampaignChapter.Factory), Is.EqualTo("Recover the T9-17 case"));
            Assert.That(CampaignRules.ChapterGoal(CampaignChapter.Asylum), Is.EqualTo("Trace P046 & reach B2"));
            Assert.That(CampaignRules.ChapterGoal(CampaignChapter.Laboratory), Is.EqualTo("Transmit complete E-02"));
        }

        [Test]
        public void TextHighlight_FormatsKeywordsIdempotentlyAndStripsCleanly()
        {
            var theme = ScriptableObject.CreateInstance<SelectedUiTheme>();
            try
            {
                CampaignTextHighlight.ResolveHexColors(theme, false, out string accentHex, out string dangerHex);
                string formatted = CampaignTextHighlight.Format("Check **AL-04 / K6** and watch for Tank.", theme);
                Assert.That(formatted, Does.Contain($"<color=#{accentHex}>AL-04 / K6</color>"));
                Assert.That(formatted, Does.Contain($"<color=#{dangerHex}>Tank</color>"));
                Assert.That(CampaignTextHighlight.StripRichText(formatted), Is.EqualTo("Check AL-04 / K6 and watch for Tank."));

                // Idempotent when applied twice
                string twice = CampaignTextHighlight.Format(formatted, theme);
                Assert.That(twice, Is.EqualTo(formatted));

                // Paper mode adjusts contrast against light background
                CampaignTextHighlight.ResolveHexColors(theme, true, out string paperAccentHex, out _);
                Assert.That(paperAccentHex, Is.Not.EqualTo(accentHex));
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void Catalog_HasStandardizedFilesKeyDataAndObjectives()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.files.Select(f => (int)f.id), Is.EquivalentTo(Enumerable.Range(1, 21)));
            Assert.That(catalog.keyItems.Select(k => (int)k.id), Is.EquivalentTo(Enumerable.Range(1, 4)));
            Assert.That(catalog.objectives.Select(o => (int)o.id), Is.EquivalentTo(Enumerable.Range(1, 20)));
            Assert.That(catalog.dialogue.Select(d => (int)d.id), Is.EquivalentTo(Enumerable.Range(1, 28)));

            foreach (var file in catalog.files)
            {
                Assert.That(file.summary, Is.Not.Null.And.Not.Empty, $"Missing KEY DATA on file {file.id}");
                Assert.That(file.body, Is.Not.Null.And.Not.Empty, $"Missing BODY on file {file.id}");
                Assert.That(file.summary.Trim(), Is.Not.EqualTo(file.body.Trim()), $"File {file.id} summary should be distinct KEY DATA");
                string formatted = CampaignTextHighlight.FormatFileRecord(file);
                Assert.That(formatted, Does.StartWith("KEY DATA\n"));
                Assert.That(formatted, Does.Contain("\n\nFULL RECORD\n"));
            }

            foreach (var obj in catalog.objectives)
            {
                Assert.That(obj.title, Is.Not.Null.And.Not.Empty);
                Assert.That(obj.instructions, Is.Not.Null.And.Not.Empty);
                Assert.That(obj.hint, Is.Not.Null.And.Not.Empty);
                Assert.That(obj.hint2, Is.Not.Null.And.Not.Empty);
                Assert.That(obj.hint3, Is.Not.Null.And.Not.Empty);
            }
        }
    }
}
