# Warden first-person setup for Brimstone, Gekko and Sage

Applied 2026-10-03 to BrimstonePlayer.prefab, GekkoPlayer.prefab and SagePlayer.prefab. All three now use Warden as the default primary in slot 0 and Classic in slot 1. Vandal, Operator, Odin and Bucky remain selectable primary candidates; the list contains five candidates including Warden.

Each prefab instantiates the existing corrected Warden.prefab under its own first-person R_WeaponMaster socket. Wrapper local position is zero, rotation is (0,270,0), scale is .01. Shared Warden data, gun controller, corrected scope/magazines/bullet and all timing values are reused. Each Weapon references that player's own arms Animator, its own gun Animator/muzzle/scope, and its own compatible Vandal_BulletPool using VandalOdinBullet. No object references point into ClovePlayer. The player visibility primary slot also references Warden. WeaponCamera FOV is 43.383067, matching the Clove setup and the FP model settings; the body camera remains unchanged.

Brimstone retains FP_Sarge hands, Gekko retains FP_AggroBot hands, and Sage retains FP_Thorne hands. Their rigs share the Warden layer in CloveGrenade_FP.controller. The original models, materials and other player systems were preserved.

Verification from saved assets:

- Correct default, two owned slots, five primary candidates and exactly one active first-person weapon.
- Zero unresolved Warden hand curve paths (5,150 bindings checked per rig) and zero unresolved gun paths.
- Corrected attachment transforms match the source Warden prefab.
- Sampled 21 poses: Idle, Equip, Fire, Inspect and Reload at .25/.45/.70 for each player. Idle and Fire barrel directions align with camera forward (dot product at least .98); wrappers retain the corrected rotation.
- Rendered and visually inspected Idle and Reload using each player's actual skinned hands, with FBX scale compensated in the temporary render meshes.
- Offline slot switching Warden → Classic → Warden gives layers 7 → 1 → 7. Primary replacement Vandal → Warden gives layers 2 → 7. Exactly one FP weapon remains active throughout.
- Existing player presentation regressions: 4 passed, 0 failed. Final Unity Console has 0 errors and GameScene remains clean. The offline switch probe emits the expected warnings about writing NetworkVariables before a NetworkObject is spawned.

The first preview render had RenderTexture cleanup errors; cleanup was corrected and subsequent renders completed successfully. Historical diagnostics were preserved in Temp/WardenOtherPlayers/EditorBeforeFinalVerification.log before the clean final verification.

This task changes first-person presentation. Existing third-person fallback behavior is retained. Multiplayer spawning was not tested in PlayMode.

## Previews

| Player | Idle | Reload |
|---|---|---|
| Brimstone | [Idle](Brimstone-Warden-1P-Idle.png) | [Reload](Brimstone-Warden-1P-Reload.png) |
| Gekko | [Idle](Gekko-Warden-1P-Idle.png) | [Reload](Gekko-Warden-1P-Reload.png) |
| Sage | [Idle](Sage-Warden-1P-Idle.png) | [Reload](Sage-Warden-1P-Reload.png) |

Pre-operation copies of the three player prefabs and the integration/pose/test records are in Temp/WardenOtherPlayers/. No permanent runtime scripts, source FBX assets or shared controllers were changed by this task.
