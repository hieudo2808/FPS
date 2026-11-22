using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// The 3P Death set (Humanoid, shared by every player character) indexed by where the finishing hit
    /// landed and which side it came from. Built by Tools/FPS/Player Life/Build Animation Set.
    /// </summary>
    [CreateAssetMenu(menuName = "FPS/Player Life Animation Set")]
    public sealed class PlayerLifeAnimationSet : ScriptableObject
    {
        [Serializable]
        public struct DeathClip
        {
            public PlayerHitRegion region;
            public PlayerHitSide side;
            public bool heavy;
            public AnimationClip clip;
        }

        [Tooltip("Valorant-style deaths: the body falls away from the side that was hit.")]
        public DeathClip[] deaths = Array.Empty<DeathClip>();
        [Tooltip("Extra variants for heavy hits to the head from the front.")]
        public AnimationClip[] headshots = Array.Empty<AnimationClip>();
        [Tooltip("Going down: legs give way and the player lands on the back, close to where they stood.")]
        public AnimationClip downedFall;
        [Header("Downed first-person view")]
        [Tooltip("Eye height above the ground while lying propped up.")]
        [Range(0.2f, 1f)] public float downedEyeHeight = 0.45f;
        [Tooltip("The head lies behind the capsule: the feet point where the player looks.")]
        [Range(0f, 1f)] public float downedHeadBack = 0.55f;
        [Tooltip("How far left/right of the direction they fell a downed player can turn (cannot look behind).")]
        [Range(20f, 120f)] public float downedYawHalfArc = 60f;
        [Range(-80f, 0f)] public float downedPitchUp = -55f;
        [Range(0f, 60f)] public float downedPitchDown = 20f;
        [Range(0f, 25f)] public float downedRoll = 10f;

        readonly List<AnimationClip> scratch = new();

        /// <summary>Same hit, same clip on every peer.</summary>
        public AnimationClip ChooseDeath(PlayerLifeHit hit)
        {
            scratch.Clear();
            Collect(hit.region, hit.side, hit.heavy, exactWeight: true);
            if (scratch.Count == 0) Collect(hit.region, hit.side, hit.heavy, exactWeight: false);
            if (scratch.Count == 0) Collect(PlayerHitRegion.Body, hit.side, hit.heavy, exactWeight: false);
            if (hit.region == PlayerHitRegion.Head && hit.side == PlayerHitSide.Front && hit.heavy)
                foreach (var clip in headshots) if (clip != null) scratch.Add(clip);
            return scratch.Count == 0 ? downedFall : scratch[hit.variant % scratch.Count];
        }

        void Collect(PlayerHitRegion region, PlayerHitSide side, bool heavy, bool exactWeight)
        {
            foreach (var death in deaths)
                if (death.clip != null && death.region == region && death.side == side && (!exactWeight || death.heavy == heavy))
                    scratch.Add(death.clip);
        }
    }
}
