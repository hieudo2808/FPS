using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class PlayerLifeRulesTests
    {
        const string SetPath = "Assets/FPS/Features/Characters/Animation/Content/3P/PlayerLifeAnimationSet.asset";

        [Test]
        public void LifeHitSurvivesTheReplicatedByte()
        {
            foreach (PlayerHitRegion region in System.Enum.GetValues(typeof(PlayerHitRegion)))
            foreach (PlayerHitSide side in System.Enum.GetValues(typeof(PlayerHitSide)))
            foreach (bool heavy in new[] { false, true })
            for (byte variant = 0; variant < 8; variant++)
            {
                var hit = new PlayerLifeHit(region, side, heavy, variant);
                var back = PlayerLifeHit.Unpack(hit.Pack());
                Assert.That((back.region, back.side, back.heavy, back.variant), Is.EqualTo((region, side, heavy, variant)));
            }
        }

        [TestCase(0f, 0f, 5f, PlayerHitSide.Front)]
        [TestCase(0f, 0f, -5f, PlayerHitSide.Back)]
        [TestCase(5f, 0f, 0f, PlayerHitSide.Right)]
        [TestCase(-5f, 0f, 0f, PlayerHitSide.Left)]
        [TestCase(3f, 2f, 3.2f, PlayerHitSide.Front)]
        public void SideIsWhereTheAttackCameFromRelativeToTheFacing(float x, float y, float z, PlayerHitSide expected)
        {
            Assert.That(PlayerLifeHit.SideOf(Vector3.zero, Vector3.forward, new Vector3(x, y, z)), Is.EqualTo(expected));
            // Same answer when the victim is turned: it is relative to the victim, not the world.
            var turn = Quaternion.Euler(0f, 130f, 0f);
            Assert.That(PlayerLifeHit.SideOf(Vector3.one, turn * Vector3.forward, Vector3.one + turn * new Vector3(x, y, z)), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownSourceAndRegionsHaveSafeDefaults()
        {
            Assert.That(PlayerLifeHit.SideOf(Vector3.zero, Vector3.forward, null), Is.EqualTo(PlayerHitSide.Front));
            Assert.That(PlayerLifeHit.RegionOf(HitboxZone.Chest), Is.EqualTo(PlayerHitRegion.Body));
            Assert.That(PlayerLifeHit.RegionOf(HitboxZone.Arm), Is.EqualTo(PlayerHitRegion.Body));
            Assert.That(PlayerLifeHit.From(Vector3.zero, Vector3.forward, null, HitboxZone.Body, 12f, DamageType.Explosion, 0).heavy, Is.True);
            Assert.That(PlayerLifeHit.From(Vector3.zero, Vector3.forward, null, HitboxZone.Body, 12f, DamageType.Melee, 0).heavy, Is.False);
        }

        [Test]
        public void EveryHitPicksADeathFromTheMatchingRegionAndSide()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlayerLifeAnimationSet>(SetPath);
            Assert.That(set, Is.Not.Null, "Tools/FPS/Player Life/Build Animation Set");
            Assert.That(set.downedFall, Is.Not.Null);
            Assert.That(set.deaths.Length, Is.EqualTo(24));
            Assert.That(set.deaths.All(d => d.clip != null && d.clip.isHumanMotion), Is.True, "Every Death clip retargets (Humanoid)");
            string[] bodyWords = { "_Body_", "_Belly_", "_Lumbar_" };
            foreach (PlayerHitRegion region in System.Enum.GetValues(typeof(PlayerHitRegion)))
            foreach (PlayerHitSide side in System.Enum.GetValues(typeof(PlayerHitSide)))
            foreach (bool heavy in new[] { false, true })
            for (byte variant = 0; variant < 8; variant++)
            {
                var hit = new PlayerLifeHit(region, side, heavy, variant);
                var clip = set.ChooseDeath(hit);
                Assert.That(clip, Is.Not.Null);
                Assert.That(set.ChooseDeath(hit), Is.SameAs(clip), "Same hit, same clip on every peer");
                string name = clip.name;
                if (name.Contains("Headshot")) { Assert.That(region == PlayerHitRegion.Head && side == PlayerHitSide.Front && heavy); continue; }
                Assert.That(name, Does.Contain("_" + side + "_"), $"{region}/{side}/{heavy} -> {name}");
                if (region == PlayerHitRegion.Head) Assert.That(name, Does.Contain("_Head_"));
                else if (region == PlayerHitRegion.Leg) Assert.That(name, Does.Contain("_Leg_"));
                else Assert.That(bodyWords.Any(name.Contains), Is.True, name);
                if (region != PlayerHitRegion.Leg) Assert.That(name.EndsWith(heavy ? "_LG" : "_SM"), Is.True, name);
            }
        }

        [Test]
        public void OnlyTheSidearmIsUsableWhileDowned()
        {
            string[] guids = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/FPS/Features/Weapons/Content" });
            var marked = guids.Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null && w.usableWhileDowned).Select(w => w.weaponName).ToArray();
            Assert.That(marked, Is.EqualTo(new[] { "Classic" }));
        }
    }
}
