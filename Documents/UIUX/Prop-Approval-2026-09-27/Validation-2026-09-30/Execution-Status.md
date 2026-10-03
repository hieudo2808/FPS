# Gameplay prop replacement — execution status, 2026-10-02

## Latest result: saved with user approval

The user explicitly confirmed the save gate. GameScene was saved through Unity, then verified clean with 8 roots. Its SHA-256 is `8983F11C070E305B746948623F1E7DF0F267AF909021140D23BFED5E778DD47A` (previous disk hash `0F3FF0DE38A97AB7B5DA34C0850F1F8C0DD30C1B7A537C09D118D0A09896F1ED`).

Formal targeted Unity Test Runner on the saved final batch:

| Suite | Passed | Job |
|---|---:|---|
| ApprovedPropSceneTests | 10/10 | `55f1a934` |
| CampaignFileSceneTests | 1/1 | `71abad3a` |
| CampaignMorgueGeometryTests | 1/1 | `331e8a64` |
| Total | **12/12** | No failures or skipped tests |

Post-test Editor readback confirmed 71 LabEquipment roots with active replacement visuals and inactive legacy Visuals, 108 supplies, 8 scene roots and `isDirty=false`. Console error query: 0. Raw results: `Validation-2026-10-01/FinalLab-Saved-EditMode-2026-10-02.json`.

Current stage is `SAVED_EDITMODE_PASS_PENDING_RUNTIME`. This supersedes the unsaved/blocked-by-dirty-scene statements below. Full visual QA and PlayMode/independent-peer acceptance remain pending; save approval is not a waiver of those checks. Earlier dated sections are retained as history.

## Earlier outcome (historical, not final acceptance)

- 108 stable supplies staged with sourced visuals: 72 survival items, 27 fixed-weapon ammo caches, 9 MedicalCase rewards. Nine sourced supply tables support their footprints.
- Three objective devices corrected: AsylumFuse (K1), LabTrace (L2-R on C3), LabTransmit (R1).
- **43/71 LabEquipment instances replaced:** 9 ControlDesk, 7 LabBench, 2 FumeHood, 2 LabCart, 3 TransferTrolley and 20 TransitCase. **28 remain unchanged** pending an approved matching model/mapping.
- All old objective EquipmentPedestal/ControlFace and surplus SupplyWorktop objects are inactive. Replaced furniture's Visuals subtrees are inactive; root BoxColliders disabled; new mesh collision enabled.
- Gameplay owners, document sources, objective IDs, proxy target/nextTarget and supply IDs retained.
- Stage: `STAGED_LOCAL_CHECKS_PASS`. Final status remains unset until full acceptance. No group is claimed `REPLACED_AND_VALIDATED` merely because an Editor assertion passes.

## Earlier save state and authority

C2/C3 bench/fume-cupboard family and L2-R computer parts were approved. The user additionally approved C1 for 2 LabCart + 3 TransferTrolley and RPaciorek hard cases for 20 TransitCase. C4 and other new candidate mappings remain unapproved.

The scene was observed dirty on resumption. During later compilation it became clean with an on-disk SHA-256 of `E7DF55A9423EFA3B13BF85F5D0DBC0C0409D9BF6DE5E756E3B8750D670EBEA59`; the earlier baseline was `3CEDB4B2B443FBB1FB431DA2FD26B193ABBF5BF8AB0C834E0E64D8E836C67BA7`. This assistant issued **no scene-save command**; the external save's origin is not established.

On the latest resumption the scene was already clean, in Edit Mode with 8 roots and all 43 replacements present. Its on-disk SHA-256 was `0F3FF0DE38A97AB7B5DA34C0850F1F8C0DD30C1B7A537C09D118D0A09896F1ED`. The intervening save's origin is not established; no scene-save command was issued by this assistant. This continuation imports preview assets and fixes test setup, without applying unapproved replacements. Final save authority remains with the user. Unrelated working-tree changes are preserved.

## Corrected objective evidence

| Objective | Actual support | Interaction point | Reachable reader |
|---|---|---|---|
| AsylumFuse | SmallMetalicCase; four supported corners | (154.100, 1.015, 244.050) | (153.875, .093, 245.375) |
| LabTrace | C3 workbench, tabletop .78 m above floor; L2-R monitor/keyboard/mouse/tower | (180.190, -17.042, 244.085) | (180.640, -17.960, 245.600) |
| LabTransmit | Laboratory_2 Plane.001 MeshCollider, actual tabletop y=-17.354; clear of all 12 supplies | (168.915, -17.284, 301.885) | (170.240, -17.960, 301.885) |

The earlier radio at y=-17.182 floated because rendered bounds included the backsplash. The earlier LabTrace overlapped a homemade monitor. Those claims and images are withdrawn as acceptance evidence.

Current images:
- [AsylumFuse](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-09-30/Corrected-AsylumFuse.png)
- [LabTrace, latest](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/ControlDesk-05.png)
- [LabTransmit](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-09-30/Corrected-LabTransmit.png)
- [Furniture before/after](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/Before-After-1920x1080.png)

Do **not** use the older `*-Eye-Validated`, `*-Close-Validated`, `*-After-Close` images as proof. They remain on disk only as history.

## Furniture measurements and visual review

[Per-ID measurements](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/Furniture-After.csv) contains all 18 original GlobalObjectIds, paths, before/after bounds, approach positions and individual screenshots.

- ControlDesk: .78 m worktop; original 2.4 × .85 m footprint retained (axes exchanged for quarter-turn placements). The L2-R wooden desk/chair are excluded.
- LabBench: .92 m worktop preserves existing papers and devices; original footprint retained. Orientation faces reachable floor.
- FumeHood: uniformly fit to original bounds; approximately 1.332 × .946 × 2.271 m. Working fronts reachable.
- Computer parts have measured collider bounds, supported corners and separation from nearby document interaction boxes.
- No foundations, Tank arena, lift geometry, lighting or NavMesh bake changed.

Contact sheets: [1](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/Furniture-1-1920x1080.png), [2](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/Furniture-2-1920x1080.png), [3](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/Furniture-3-1920x1080.png). Each also has a 1280×720 version. These composite sizes are **not** evidence of runtime UI testing at those resolutions.

The original LabBench-C-Before image was occluded by a wall. Its comparison tile is explicitly labelled **LEGACY RECONSTRUCTED**: rendered from the preserved old mesh with temporary visibility changes restored in finally. It is not a historical screenshot. FumeHood full-height views supplement the close-up shots.

## Checks actually executed

Historical in-place checks: Unity compiled successfully, Console reported 0 errors, and the following nine methods passed by direct invocation (not Test Runner at that time):

1. DownloadedObjectiveDevices_HaveMeasuredSupportAndNoLegacyOverlap
2. ApprovedLabFurniture_PreservesFootprintsSupportAndInteraction (now covers all 18 furniture instances)
3. DownloadedRuntimeVisuals_HaveOnlySourcedActiveMeshesAndSeparateProjectileKinds
4. DownloadedSupplies_PreserveStableIdsMappingsGatesAndSupportedFootprints
5. SupplyCases_FitOriginalFootprintWithoutDuplicatingOrFloating
6. ReplacementDevices_HaveReachableBindingsAndNoActivePedestals
7. SharedDevice_AdvancesAndRollsBackWithAuthoritativeObjectiveState
8. EveryCatalogFile_HasUniqueVisibleSupportedReachablePhysicalSource (21 Files, including all 12 optional)
9. MorgueTank_RegisteredHiddenAnchorsHaveBodyClearRoutesToReaderAndLift (with required setup/teardown)

All passed. Supply checks cover 108 unique IDs, 432 supported corners, ammo capacity/mapping, survival gates 1/3, ammo gates 1/2/4, and retained chooseReward.

The continuation also passed the cart/case support/layer/material test and the microscope nonempty-mesh regression, for 11 directly invoked methods in total.

### Formal Test Runner, latest continuation

- Initial `ApprovedPropSceneTests`: 4/9 passed, 5 failed because Test Runner had no GameScene loaded. This was a test-fixture lifecycle defect, not evidence that placement failed.
- Fixed shared one-time setup/teardown to open GameScene additively only when absent, set it active for existing validators, restore the previous active scene and close only the test-owned scene. No save/reload of a pre-existing dirty scene.
- Rerun `ApprovedPropSceneTests`: **9/9 passed**, job `1c63f6f8`.
- `CampaignFileSceneTests`: **1/1 passed**, job `eff1d14f`.
- `CampaignMorgueGeometryTests`: **1/1 passed**, job `72cf1592`.
- Total: **11/11 targeted EditMode tests passed through Unity Test Runner**. This does not establish PlayMode or independent-peer acceptance.
- Raw results: `Validation-2026-10-01/EditMode-TestRunner-Results.json`. Final Console error query: 0. Final GameScene readback: clean Edit Mode, 8 roots, 108 supplies, no unapproved candidate placements. Scene SHA-256 unchanged from this continuation's starting hash `0F3FF0DE38A97AB7B5DA34C0850F1F8C0DD30C1B7A537C09D118D0A09896F1ED`.

An additional reapply check passed: same object identities, no transform-matrix drift above .1 mm, no duplicate replacement children, no moved Files and no changed supply IDs.

Authoring failures were rolled back as isolated Undo groups before success. Fixes:
- Unity's fake-null component reference requires `== null`, not `??`, when adding missing colliders/proxies.
- PhysX rays at exact mesh-triangle seams missed valid tabletops. The shared surface measurement now requires four diagonal neighbours within 2 mm per axis to hit a flat surface at agreeing heights; normal and corner-support checks remain. A prior hypothesis of a sink opening was not supported and was discarded.
- The first direct morgue invocation omitted NUnit setup; rerun with setup/teardown passed. That invocation error is not a scene failure.

The earlier SurvivalNetworkTests result (12/12) uses a real NGO host/client **inside one process**. It is historical evidence, not a new test of this scene and not four independent peers.

## Remaining blockers (historical pre-batch snapshot; see final addendum)

| Type | Unchanged instances | Missing gate |
|---|---:|---|
| ReagentCabinet | 9 | LAB_Cabinet (Naked Singularity, CC BY 4.0) mapping approved 2026-10-01; wait for all-28 batch |
| ColdStorage | 5 | KurtSteiner compact fridge imported/rendered; open-door 0.97 x 1.67 m footprint at 0.9 m height does not fit tall-storage role without review. Existing tall Asylum fridge remains an alternative |
| ServerRack | 7 | LAB_Rack (Spellkaze, CC BY 4.0) mapping approved 2026-10-01; wait for all-28 batch |
| Centrifuge | 3 | ProgressTH open/closed configurations rendered separately; 18,847 triangles and 164 renderers each. Pending approval; combine static source meshes before placement if chosen |
| LabStool | 1 | LAB_Stool (conndavis20, CC BY 4.0) mapping approved 2026-10-01; wait for all-28 batch |
| LabSink | 2 | Existing sourced Asylum Sink_V1 needs final family/fit decision |
| SpecimenChamber | 1 | Dominic Baker candidate rejected for visual/import issues. New moneii2706 CC BY cryopod imported/rendered: 99,794 triangles, 13 source meshes; optimization/fit/approval pending |
| **Total** | **28** | **17 mapping-approved, 11 pending review; all remain unchanged pending combined batch** |

[Per-ID blocker ledger](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/Remaining-Lab-Blocked-Current.csv) lists the current 28 GlobalObjectIds and original paths. The original 53-row ledger remains as historical evidence.

[Carts/cases audit](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-10-01/CartsCases-After.csv) records all 25 approved replacements, stable IDs, support and screenshots.

All 25 cart/case screenshots have now been reviewed in five contact sheets (`Carts-Cases-1` through `Carts-Cases-5`, each 1920x1080 and 1280x720). No obvious floating or stack gaps were visible from those views; this complements the 100 support probes, not full gameplay collision/occlusion acceptance.

Candidate images (not after-replacement evidence):

- `Candidate-Approval-1920x1080.png`: 9 reagent cabinets, 7 server racks and 1 stool, including temporary scene previews. User approved these mappings on 2026-10-01; historical PENDING labels in the image are superseded. Placement deferred until all 28 are reviewed, as requested.
- `Remaining-Candidate-Review-1920x1080.png`: compact/tall fridge alternatives, separate open/closed centrifuges, existing sink and C4 comparison. C4 is not recommended for the realistic device role.
- `Cryopod-Front.png`: moneii2706 replacement candidate, currently 99,794 triangles and simple materials. Imported candidate, not approved or optimized.
- Do not use `FridgeFront-Candidate.png` or `FridgeBack-Candidate.png`: those early previews incorrectly rotated the native Y-up model. `Fridge-Native0.png` is the corrected orientation.

The four Omax microscopes were repaired by disabling failing secondary-UV generation in the ModelImporter. The original source mesh was not changed; Unity now imports 57,457 vertices and all four render with nonzero bounds. Regression test passes; plain existing materials still need final visual acceptance.

## Asset/provenance limits

The original 17 downloaded packages plus seven new candidate packages are imported; import is not approval. [Manifest](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Provenance-Manifest.csv) records source URL, author, license, archive checksums, import paths and staging states.

- C2/C3: NightCandle, CC BY-NC 4.0, noncommercial only. C3's omitted 20 Icospheres and two default-material pipe pieces are excluded from the selected workbench derivative; full-set conversion is not accepted.
- L2-R: CR!STALLL, CC BY 4.0, computer parts only.
- K1 derivative: 126,296 → 5,682 triangles, approximately .018% bounds error.
- M1 uses green recolored markings; M2 uses a fictional BRT/T-9 label. Original textures retained.

Downloads cleanup completed on 2026-10-03: 25 validated asset archives (including the duplicate `m67-grenade.zip`) were moved to the Windows Recycle Bin after path/SHA-256/import validation. No unrelated Downloads files were touched. Receipt: `Download-Cleanup-2026-10-03.json`. The files are recoverable from the Recycle Bin.

## Earlier acceptance checklist (save/EditMode superseded above)

- PlayMode against safely preserved scene state; targeted EditMode Test Runner is now 11/11 passed.
- 1 host + 1 independent client, then four peers; disconnect/reconnect and latency/packet-loss matrix.
- Complete first-person/third-person/projectile gameplay visual checks.
- Runtime 1920×1080, 1280×720, 1024×768 and 2560×1080, HUD 100%/150%.
- The 28 remaining lab props and four repaired microscope visuals still need final visual acceptance.
- Final user save confirmation.

No scene save is requested as “all complete” while these remain.

### Latest approval-only continuation

Recorded approval for 17 cabinet/rack/stool instances without replacing any of the 28. Full remaining proposal is in Asset-Matrix.md section 10: A existing tall fridge x5, B ProgressTH open centrifuge x3, C existing steel sink x2, D moneii2706 cryopod x1. Actual isolated Unity previews are available for all four. A/C original pack provenance is not re-established; B/D need source optimization and all four still need final placement acceptance. No new code, scene change, save or test result is claimed in this approval-only update.

## Final 28 batch — pre-save snapshot 2026-10-02

All seven remaining mappings were approved, including user-confirmed permission for the existing Asylum Fridge and Sink_V1. The 28 replacements are staged in the open GameScene; prior statements that they are unchanged/pending mapping approval are historical. Stage: `STAGED_LOCAL_CHECKS_PASS`, not `REPLACED_AND_VALIDATED`.

- 9 ReagentCabinet, 7 ServerRack, 1 LabStool, 5 ColdStorage, 3 Centrifuge and 1 SpecimenChamber plus 2 LabSink are applied. Total LabEquipment replacement coverage is now 71/71 in memory.
- Centrifuge preserves 18,847 source triangles with 5 renderers instead of 164. Cryopod derivative is 29,927 triangles versus 99,794 source, approximately 0.005% bounds error; originals retained.
- Rechecked directly: 28 models, 112 support probes, uniform scales, contained footprints, source meshes, materials/layers and inactive legacy pass.
- Batch post-write verification preserved exact root matrices/GlobalObjectIds, all 108 supply serialized states and objective/file serialized states.
- Readback: Edit Mode, not compiling, 8 roots, dirty scene, Console error query zero. General scene validation: zero errors/warnings and 573 repeated-name information entries.
- Disk scene SHA-256 remains `0F3FF0DE38A97AB7B5DA34C0850F1F8C0DD30C1B7A537C09D118D0A09896F1ED`. No save was issued.
- Formal Test Runner rejected the dirty scene. Earlier 11/11 tests apply to the previous 43/71 state, not the final batch. Saving for further validation requires explicit user confirmation; this is not a claim that runtime/full acceptance is complete.

Evidence: `Validation-2026-10-01/FinalLab-After.csv`, 28 individual after images, and five `Final-Lab-*` contact sheets at 1920x1080 and 1280x720. All 28 rows in `Remaining-Lab-Blocked-Current.csv` now record the staged unsaved status. Centrifuge/stool images include occluded views; further visual inspection is still required, along with runtime/network checks listed above.
