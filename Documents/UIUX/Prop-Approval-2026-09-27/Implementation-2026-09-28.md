# Approved props — implementation status, updated 29 September 2026

This supersedes the proposal-only status in Approval.md and Combat-Items-Addendum.md. The approval boards remain the source for visual choices, not evidence of completed import.

## Saved in GameScene

| Approval | Implementation |
|---|---|
| A1 | AsylumAccess uses the existing Guard Room CRT. |
| A2-A | AsylumInstall and AsylumPower use one wall-mounted Electric_box_v2. The existing interaction proxy switches to the power step only after installation is recorded in campaign state; checkpoint rollback switches it back. No extra device or interaction system. |
| A3/A4 | Patient and mortuary papers use existing Exam Room / Morgue tables. |
| L1 | LabPower uses the existing electrical cabinet. |
| F1 | LabIndex uses the existing paper asset on the clear left end of the C Research bench. The old pedestal/control face are inactive. |
| L3 | LabArchive uses PC_Monitor + Keyboard from the Asylum pack, on the right end of the existing E Containment bench. The paper transcript is on its clear left end. This is L3, **not the still-pending L2 replacement**. |
| K3 | RPaciorek black hard cases for Factory/Lab. The Lab case stands on the floor beside the archive bench, not inside its lab instruments; the Factory case rests on its existing floor. |
| S2 | 27 RPaciorek silver equipment cases, fitted within the original supply footprints. These remain mixed campaign supply caches, not the five weapon-specific ammo models. |

Old objective primitives and original case visuals are retained inactive for recovery. No objective/file/supply IDs, weapon distribution, reward quantities, checkpoint masks or server-authority rules were changed by this placement pass.

### Repairs discovered during verification

- The previous equipment-case import used a blanket x100 scale, producing cases over 6 m long. The authoring command now fits the full renderer bounds into the original footprint, aligns the bottom, reuses existing instances and disables obsolete trim.
- Both evidence cases had 100 m-wide colliders despite small visible meshes. Their colliders now match the imported mesh bounds (approximately 0.62 x 0.48 x 0.32 m). The correction is serialized and checked after scene reload.
- Two affected Lab benches now use their actual static mesh collision instead of coarse boxes covering the empty space above the desktop. Existing meshes/materials are unchanged.
- Correction: `AsylumTransfer` must share the mortuary paper's real `TableWhite` location. The old hidden console binding was a regression, not an intended fallback. The authoring helper now disables the old console and binds the objective to the table; the geometry test checks the actual document reader as well as the corridor/lift route points.

## Real Unity captures

- [Shared Asylum cabinet](Asylum-ServiceCabinet-after.png)
- [Archive PC, transcript and case](Lab-Archive-PC-after.png)
- [Index on existing desktop](Lab-Index-after.png)
- [Supply cases at corrected scale](Supply-Cases-scale-fixed.png)
- [Mortuary document at the real reachable table](Morgue-Table-reader-after.png)

These are offscreen renders of the saved Editor scene, not multiplayer gameplay captures. Old grenade/medical visuals visible in the supply image are still pending replacement.

## Provenance

- PC_Monitor, Keyboard, Paper and Electric_box_v2: reused project assets at the paths listed in Approval.md (Keyboard is in the same AsylumFacility/Prefabs directory). No new license claim or redistribution permission inferred.
- RPaciorek Hard Cases: https://github.com/dragons-labs/BlenderAssets ; imported files and the repository license are in `Assets/ThirdParty/ApprovedProps/RPaciorekHardCases/`. Original Sketchfab source/author metadata is retained in `Previews/`. Meshes/materials are reused; only placement, scale and collision were changed in this pass.

## Still pending — do not mark integrated

L2-R workstation, G1/G2 grenades, M1/M2 medical items, AM1–AM5 weapon-specific ammo boxes, K2 keycard and R1 radio still need their approved original model files. The prior download attempt hit Sketchfab's login requirement; no bypass was attempted. No matching new downloads were found in the user's Downloads folder in this continuation. K1 fuse remains on technical hold; A2-B is not installed because A2-A is used.

Typed ammo code and the earlier targeted ammo tests are retained. The 27 mixed campaign caches still have no `ammoWeapon` mapping: `_0/_1/_2` encode party-size variants, not weapon categories. No arbitrary weapon assignment was made while the real ammo models and placement mapping are outstanding.

## Verification

- `ApprovedPropSceneTests`: 3/3 passed after scene reload on 29 September (`92a18a42`).
- `CampaignFileSceneTests`: 1/1 passed on 29 September (`064cf848`).
- `CampaignMorgueGeometryTests`: 1/1 passed on 29 September after save/reload (`343e9568`). Checks both concealed Tank anchors, body clearance, spawn distance and routes to the actual reader plus the retained corridor/lift targets. The strengthened test first failed against the hidden-console scene (`966f4dc1`), confirming it catches that regression.
- `CampaignRulesTests`: earlier continuation, 11/11 passed (not rerun for this placement-only correction).
- `AmmoPickupRulesTests`: earlier continuation, 1/1 passed.
- Typed network ammo test: earlier continuation, 1/1 passed; no new multiplayer acceptance claim.
- Unity Console errors: 0; GameScene: saved and clean; Edit Mode; not compiling.

These are targeted Editor/PlayMode checks, not a claim of host/client or four-peer campaign acceptance.
