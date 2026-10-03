# Outbreak Protocol — implementation handoff

Updated 2026-09-27. This is an evidence/status record, not the narrative canon. `Outbreak-Protocol-Master-Canon.md` remains the English content source.

## Delivered in the Tank / physical Files continuation

- Saved GameScene now has two Factory cold-storage Tank anchors and two Asylum basement anchors. The morgue was extended 10 m east; its connecting doorway is approximately 4 m wide and 3.34 m high. Original meshes and the disabled old door remain recoverable.
- A concealed receiving partition, local navigation-clearance volumes and two copied existing lights support the Asylum Tank staging. The mortuary objective moved with its required-file child; its stable objective/file IDs were preserved.
- Only the Asylum NavMesh was rebuilt. The scene currently references `Campaign/MorgueArena/AsylumNavigation 8.asset`. Earlier generated diagnostic bakes are retained and are not the active scene reference. Original pre-expansion navigation assets were not overwritten.
- All 21 Files have registered physical sources and existing paper-prefab visuals: 9 required plus 12 optional (Factory 3, Asylum 4, Lab 5). Required-file console fallback remains available. Optional files have no console or automatic objective grant.
- Pickup hides only the paper child and its interaction collider. Its owner/console persists. Restoring the checkpoint mask restores paper visibility through the existing refresh path.
- Editor authoring is under `Tools/FPS/Campaign/Morgue/` and `Tools/FPS/Campaign/Files/`. It refuses preexisting dirty scenes, uses Undo and does not save GameScene automatically. File placement skips already-authored visuals.

## Verification evidence

| Check | Result / limits |
|---|---|
| CampaignRulesTests | 11/11 passed, job `9b27feda`; includes file visibility/rollback ownership |
| CampaignMorgueGeometryTests | 1/1 passed after scene reload and document placement, job `b09e4e56`; two hidden anchors, body clearance, routes to reader and lift, retained mortuary file link |
| CampaignMorgueNavigationTests | 1/1 passed, job `4263c526`; four live NavMesh routes using Tank prefab agent settings on the scene-referenced bake |
| CampaignLifecycleTests | Earlier continuation: 2/2 passed, job `ed0c1388`; isolated server lifecycle/dialogue checks |
| CampaignFileSceneTests | 1/1 passed after desktop correction and saved-scene reload, job `e87137e3`; all 21 sources, real F-ray hits, support, correct Lab mesh surfaces and all four player prefab interaction masks/ranges |

The navigation probe does not instantiate a combat Tank or prove attack behavior, moving-target pursuit, network replication, four-player avoidance or full campaign completion. Static geometry and live NavMesh checks are complementary, not substitutes for real co-op playthroughs.

Visual inspection caught papers sitting above Lab desktops because a coarse collider enclosed each desk and monitor. The correction uses exact render-mesh collision on the two affected desks and moves three papers onto the visible surface. No global collider replacement or runtime placement system is added.

Saved Scene View evidence: [expanded morgue](UIUX/Campaign-Morgue-Arena.png), [Asylum desk](UIUX/Campaign-Files-Asylum.png), [corrected Lab desktop](UIUX/Campaign-Files-Lab.png). These are Editor views, not multiplayer gameplay captures. Re-running `Place Missing Documents` after scene reload creates no duplicates and leaves the scene clean. Source compilation reported no errors; Console was clear after verification.

## Remaining plan work

1. Complete asset-backed TMP/UGUI prefab presentation for Team Inventory, Files reader, Squad HUD and Debrief; current runtime-built implementations are not the final UI acceptance result.
2. Finish event-driven roster binding, portrait presentation, stable disconnect/reconnect behavior and character-lock verification.
3. Verify full downed/revive/death/spectator, extraction/boarding, chapter transitions, retry and attempt-summary flow with real peers.
4. Play through all three Tank encounters with actual AI. Check safe-spawn pending behavior, player avoidance, Asylum evasion and Lab transmission under pressure.
5. Run host + client, then four real peers; duplicate File interaction, latency/loss, reconnect and checkpoint rollback. No host migration is in scope.
6. Run requested resolution, HUD-scale, high-contrast, safe-area, controller-focus and long-text/Unicode QA. Existing Scene View captures are not final HUD-resolution acceptance screenshots.

The full campaign plan is not yet complete. Preserve the unrelated dirty Git worktree; no broad reset, prefab overwrite or repository cleanup was performed.
