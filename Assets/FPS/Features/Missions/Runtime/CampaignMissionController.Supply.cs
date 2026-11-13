using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Server-owned supply visibility. One replicated bit per supply of the current chapter says whether it is on the
    /// table; clients only display it. A bit flips while no operator has a line of sight to the supply, so nothing
    /// appears or vanishes in front of anyone. Taking a supply is the one change players are meant to see.
    /// </summary>
    public sealed partial class CampaignMissionController
    {
        public const float SupplySightRange = 60f;
        public const double ReserveReleaseCooldown = 45.0;
        private const double SupplyEvaluationInterval = .5;

        private readonly NetworkVariable<ulong> revealedSupplyMask = new();
        private CampaignSupply[] chapterSupplies = Array.Empty<CampaignSupply>();
        private CampaignChapter supplyIndexChapter;
        private bool supplyIndexBuilt;
        private byte releasedReserveStations;
        private ushort supplyDownBaseline;
        private double nextSupplyEvaluation, nextReserveReleaseAt, lastSquadDownAt = -1;
        private readonly List<PlayerHealth> supplyWatchers = new(4);

        public byte ReleasedReserveStations => releasedReserveStations;

        public bool IsSupplyRevealed(CampaignSupply supply)
        {
            EnsureSupplyIndex();
            int index = Array.IndexOf(chapterSupplies, supply);
            return index >= 0 && (revealedSupplyMask.Value & (1UL << index)) != 0;
        }

        // Bit order must match on every peer: the same scene array, filtered by chapter, in ordinal id order.
        private void EnsureSupplyIndex()
        {
            if (supplyIndexBuilt && supplyIndexChapter == state.chapter) return;
            chapterSupplies = (supplies ?? Array.Empty<CampaignSupply>())
                .Where(s => s != null && s.chapter == state.chapter)
                .OrderBy(s => s.supplyId, StringComparer.Ordinal).Take(64).ToArray();
            supplyIndexChapter = state.chapter;
            supplyIndexBuilt = true;
            if (!IsServer) return;
            releasedReserveStations = 0;
            nextReserveReleaseAt = 0;
            lastSquadDownAt = -1;
            supplyDownBaseline = state.teamDownedCount;
        }

        private void UpdateSupplies()
        {
            EnsureSupplyIndex();
            if (Now < nextSupplyEvaluation) return;
            nextSupplyEvaluation = Now + SupplyEvaluationInterval;

            supplyWatchers.Clear();
            foreach (var player in Players)
                if (player.LifeState is PlayerLifeState.Alive or PlayerLifeState.Downed) supplyWatchers.Add(player);

            if (state.teamDownedCount > supplyDownBaseline) lastSquadDownAt = Now;
            supplyDownBaseline = state.teamDownedCount;
            if (state.phase is CampaignPhase.Exploring or CampaignPhase.Preparing or CampaignPhase.Encounter
                && Now >= nextReserveReleaseAt && SquadNeedsReserve())
                TryReleaseReserve();

            ulong mask = revealedSupplyMask.Value;
            for (int i = 0; i < chapterSupplies.Length; i++)
            {
                CampaignSupply supply = chapterSupplies[i];
                ulong bit = 1UL << i;
                bool shown = (mask & bit) != 0;
                bool claimed = IsSupplyClaimed(supply.supplyId);
                bool wanted = !claimed && CampaignRules.SupplyWanted(supply.IsDirectorReserve, supply.minimumPartySize,
                    state.supplyPartySize, IsReserveStationReleased(CampaignRules.SupplyStation(supply.supplyId)));
                if (wanted == shown) continue;
                if (claimed || !IsSupplyObserved(supply)) mask ^= bit;
            }
            revealedSupplyMask.Value = mask;
        }

        private bool IsReserveStationReleased(int station) => station >= 0 && (releasedReserveStations & (1 << station)) != 0;

        private bool SquadNeedsReserve()
        {
            float health = 0, ammo = 0; int count = 0, downed = 0;
            foreach (var player in supplyWatchers)
            {
                if (player.LifeState == PlayerLifeState.Downed) { downed++; continue; }
                if (!player.IsInputReady) continue;
                health += player.MaxHealth > 0 ? Mathf.Clamp01(player.CurrentHealth / player.MaxHealth) : 1f;
                var weapons = player.GetComponent<WeaponFireHandler>();
                ammo += weapons != null ? weapons.ServerCarriedReserveFraction : 1f;
                count++;
            }
            if (count == 0) return false; // nobody standing could reach a station anyway
            return CampaignRules.SquadNeedsReserve(health / count, downed,
                lastSquadDownAt < 0 ? -1f : (float)(Now - lastSquadDownAt), ammo / count);
        }

        // Opens the nearest station whose reserve nobody can currently see. If every candidate is in view, wait.
        private void TryReleaseReserve()
        {
            Vector3 centre = Vector3.zero; int standing = 0;
            foreach (var player in supplyWatchers)
                if (player.LifeState == PlayerLifeState.Alive) { centre += player.transform.position; standing++; }
            if (standing == 0) return;
            centre /= standing;

            int best = -1; float bestDistance = float.MaxValue;
            for (int station = 0; station < 8; station++)
            {
                if (IsReserveStationReleased(station)) continue;
                bool any = false, observed = false; Vector3 position = default;
                foreach (var supply in chapterSupplies)
                {
                    if (!supply.IsDirectorReserve || CampaignRules.SupplyStation(supply.supplyId) != station || IsSupplyClaimed(supply.supplyId)) continue;
                    any = true; position = supply.transform.position;
                    if (IsSupplyObserved(supply)) { observed = true; break; }
                }
                if (!any || observed) continue;
                float distance = (position - centre).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = station; }
            }
            if (best < 0) return;
            releasedReserveStations |= (byte)(1 << best);
            nextReserveReleaseAt = Now + ReserveReleaseCooldown;
            int released = best;
            GameLog.Info(() => $"[CampaignSupply] Director released reserve at {state.chapter} station {released}");
        }

        /// <summary>True when any living or downed operator within range has a clear line from eye height to the supply.</summary>
        public bool IsSupplyObserved(CampaignSupply supply)
        {
            Vector3 target = supply.transform.position;
            foreach (var player in supplyWatchers)
            {
                Vector3 eye = player.transform.position + Vector3.up * 1.55f;
                if ((target - eye).sqrMagnitude > SupplySightRange * SupplySightRange) continue;
                if (!Physics.Linecast(eye, target, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || hit.transform == supply.transform || hit.transform.IsChildOf(supply.transform)
                    || hit.transform == player.transform || hit.transform.IsChildOf(player.transform))
                    return true;
            }
            return false;
        }
    }
}
