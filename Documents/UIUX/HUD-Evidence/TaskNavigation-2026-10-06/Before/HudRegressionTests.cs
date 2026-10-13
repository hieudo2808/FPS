using System.Collections.Generic;
using FPS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class HudRegressionTests
    {
        [Test]
        public void TrackingUsesNearestCompleteRouteThenKeepsManualSelection()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
            var lengths = new Dictionary<CampaignObjectiveId, float?>
            { [ids[0]] = 30, [ids[1]] = null, [ids[2]] = 12 };
            var chosen = CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, id => lengths[id]);
            Assert.That(chosen, Is.EqualTo(ids[2]));
            lengths[ids[0]] = 1;
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, chosen, id => lengths[id]), Is.EqualTo(chosen));
            state.completed |= CampaignState.Bit(chosen); // Includes a teammate's authoritative completion.
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, chosen, id => lengths[id]), Is.EqualTo(ids[0]));
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => null), Is.EqualTo(ids[0]));
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => float.NaN), Is.EqualTo(ids[0]));
        }

        [Test]
        public void TrackingInvalidatesOnChapterPhaseAndCheckpointRollback()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, completed = CampaignState.Bit(CampaignObjectiveId.FactoryIsolate) };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup };
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[1], _ => 1), Is.EqualTo(ids[1]));
            state.completed = 0; // Restore a checkpoint before the prerequisite.
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[1], _ => 1), Is.EqualTo(ids[0]));
            state.chapter = CampaignChapter.Asylum;
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, ids[0], _ => 1), Is.EqualTo(CampaignObjectiveId.None));
            state.phase = CampaignPhase.Preparing;
            Assert.That(CampaignObjectiveSelection.Resolve(state, new[] { CampaignObjectiveId.AsylumAccess }, CampaignObjectiveId.AsylumAccess, _ => 1), Is.EqualTo(CampaignObjectiveId.None));
        }

        [Test]
        public void ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner()
        {
            var bounds = new Rect(100, 80, 1080, 560);
            var center = new Vector2(640, 360);
            Assert.That(HudMarkerProjection.Project(new Vector3(700, 400, 1), center, bounds, out var edge), Is.EqualTo(new Vector2(700, 400)));
            Assert.That(edge, Is.False);
            var behind = HudMarkerProjection.Project(new Vector3(900, 360, -1), center, bounds, out edge);
            Assert.That(edge, Is.True);
            Assert.That(behind.x, Is.EqualTo(bounds.xMin));
            var adjusted = HudMarkerProjection.AvoidCornerHud(new Vector2(100, 90), bounds, new Rect(0, 0, 400, 300), 40);
            Assert.That(adjusted.y, Is.GreaterThanOrEqualTo(340));
            Assert.That(adjusted.x, Is.EqualTo(bounds.xMin));
            var directlyBehind = HudMarkerProjection.Project(new Vector3(640, 360, -1), center, bounds, out edge);
            Assert.That(float.IsNaN(directlyBehind.x) || float.IsInfinity(directlyBehind.y), Is.False);
            Assert.That(directlyBehind.y, Is.EqualTo(bounds.yMin));
            var crowded = new[] { new Rect(0, 0, 400, 570), new Rect(0, 644, 500, 220) };
            var safe = HudMarkerProjection.AvoidHud(new Vector2(130, 580), new Rect(130, 100, 1276, 684), crowded, 60);
            foreach (var area in crowded)
            {
                var expanded = new Rect(area.xMin - 60, area.yMin - 60, area.width + 120, area.height + 120);
                Assert.That(expanded.Contains(safe), Is.False, "Avoid all corner groups together at the 125% fixture.");
            }
        }

        [Test]
        public void PromptsFollowOverridesAndReset()
        {
            var root = new GameObject("HUD binding regression");
            var saved = new Dictionary<string, string>();
            InputManager input = null;
            try
            {
                input = root.AddComponent<InputManager>();
                foreach (var action in input.ActionsAsset)
                    Save("Input_" + action.name);
                Save("InputBindings");
                int updates = 0;
                input.PromptBindingsChanged += () => updates++;
                input.RebindKey("Grenade", KeyCode.H);
                Assert.That(input.GetPromptDisplayName("Grenade"), Is.EqualTo("H").IgnoreCase);
                Assert.That(updates, Is.GreaterThan(0));
                input.ResetBindingsToDefault();
                Assert.That(input.GetPromptDisplayName("Grenade"), Is.EqualTo("G").IgnoreCase);
                Assert.That(input.GetPromptDisplayName("CycleGrenade"), Does.Contain("3"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var pair in saved)
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key); else PlayerPrefs.SetString(pair.Key, pair.Value);
                PlayerPrefs.Save();
            }
            void Save(string key) => saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }
    }
}
