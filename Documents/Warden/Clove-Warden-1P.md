# Clove — Warden first-person integration

Completed on 2026-10-03. ClovePlayer now uses Warden in primary slot 0, retains Classic in slot 1, and retains Vandal/Operator/Odin/Bucky as selectable primary candidates.

## Hierarchy and animation

Warden is nested under the existing first-person `Skeleton/Root/MasterWeapon/WeaponGameOverride/R_WeaponMaster` socket. Its Unity wrapper uses local rotation `(0,270,0)` and scale `.01`. The Three.js viewer defaults its GLB wrapper to `(0,90,0)`, but that angle cannot be copied directly onto this imported FBX hierarchy. The saved Unity pose was checked by rendering Clove's hands and measuring the barrel direction: Idle/Fire point approximately `(0.02,0.03,1.00)`, along the camera's positive Z direction. Using `(0,90,0)` on the Unity wrapper points the barrel backwards. Weapon references point to Clove's shared arms Animator, the Warden gun Animator, Warden data, and the gun's MuzzlePoint. The existing CloveGrenade_FP controller keeps its other weapon and grenade layers; Warden uses layer 7.

The five hand actions use FP_Warden.fbx: IdleAdd, BattleRifle Equip, AK Fire (the source clip's actual name), BattleRifle Inspect, and BattleRifle Reload. The gun uses GN_Warden.fbx for Equip/Fire/Reload. Inspect uses the gun's rest Idle pose, as specified in warden.txt.

The timing correction separates gameplay locks from full clip durations, following the viewer code. Gameplay: Equip 1 second, fire interval 1/6.5 second, Reload 2.5 seconds. Presentation: Equip 2.333333 seconds, Fire 0.316667 seconds, Reload 3.8 seconds, Inspect 4.166667 seconds, hand Idle 2.733333 seconds. Each FP/GN state uses its own imported clip length divided by the target duration; trimmed clips therefore need different speeds. Both Reload states use ReloadNormalizedTime sampled against the presentation duration. Natural reload completion unlocks gameplay while the visual tail continues; a new action cancels that tail. Ammo commits at gameplay reload completion; no earlier commit frame is supplied by warden.txt. See ../Weapons/Weapon-Timing-Source-Mapping.md for the six-weapon mapping and validation.

The WeaponCamera FOV is 43.383067, taken from the first-person model settings. The held-model viewer FOV 25 was not used as an ADS gameplay setting.

## Attachments

Magazine attaches to Magazine, extra magazine to Magazine_Extra, scope to Reflex, and bullet to Bullet1. The four attachment wrappers have local position zero, rotation identity, and scale one under their respective imported bones. The previous extra local `(0,0,180)` rotated the scope, magazine and bullet backwards. Removing it also moves the scope's offset geometry to the correct side of Reflex, so its foot sits on the top rail behind the magazine well. Bullet mesh geometry has its pointed end along local negative X; the corrected bone transform maps that to the gun's positive X barrel direction.

Three derived mesh assets under Warden/Presentation remove baked attachment origin offsets before parenting to animated bones. Their geometry and the original FBX files were preserved during the attachment correction. Warden's existing authored materials replace corresponding embedded FBX materials.

The live page at https://kingdomarchives.com/modelviewer/clove-fp?held=warden was fetched on 2026-10-03. Its held settings exactly match `warden.txt`; its `modelviewer-Dfne7uos.js` loader matches the supplied code. `ObjectToBone` uses `bone.add(object)` and then replaces local position and rotation, rather than preserving the object's previous world rotation. These offsets are GLB/Three.js values, not raw Unity FBX Transform values.

GN_Warden's imported Idle clips contain zero curve bindings. `Warden_GN_Idle_Rest.anim` therefore records only the transforms whose paths occur in GN_Warden's action clips, ensuring animated parts return to rest after actions. It has 240 bindings and no static attachment bindings. The previous 340 static bindings were removed because they would restore the reversed attachment transforms on returning to Idle. A Stat_TrackSocket matching the source animation hierarchy completes gun clip bindings.

## Verification

- Editor asset readback: Warden default primary, five primary candidates, slot 0 Warden, correct Animator/muzzle references, every gun renderer enabled.
- All FP and GN animation curve paths resolve: zero missing bindings.
- Targeted offline switches Warden → Classic → Warden show exactly one first-person weapon and layers 7 → 1 → 7.
- Targeted primary replacement Vandal → Warden shows exactly one weapon and layers 2 → 7.
- Restoring Vandal sets the prepared selection guard; the default spawn selection will not overwrite the prepared primary.
- Final saved asset readback: Clove wrapper `(0,270,0)`, four attachment wrappers identity. The nested prefab rotation override was explicitly persisted and reloaded.
- Idle, Equip, Fire, Inspect, and Reload were sampled and rendered from the saved Clove prefab without transform corrections in the preview. Reload was checked at normalized times .25, .45 and .70; the .25 preview shows the left hand holding the spare magazine. Scope remains on the rail and the bullet points toward the muzzle. All four static attachment rotations stay identity through every sampled action.
- Unity compilation ready and Console error count 0 after verification. Offline NetworkVariable tests emitted the expected warning about modifying an unspawned NetworkObject.
- Temporary preview scenes were removed; GameScene and the existing Warden Prefab Stage remain clean, and the Editor is stopped.

Multiplayer host/client spawning and reconnect were not exercised in Play mode. Third-person Warden presentation is outside this first-person task and retains the existing fallback behavior.

## Preview images

- `Warden-Final-Side.png` compares the final saved attachment arrangement with the supplied reference.
- All `Clove-Warden-1P-*.png` images were regenerated after the final corrections. The preview explicitly bakes sampled skinned meshes with the FBX scale compensated, so the hands show their sampled animation pose.

Pre-operation asset copies are available under Temp/WardenIntegrationBackup; runtime source copies are under Temp/WardenIntegration/RuntimeBefore. These local backup folders are not intended as shipped assets.

The attachment correction backup is under Temp/WardenIntegration/BeforeAttachmentFix and includes Warden, ClovePlayer, derived meshes and the previous rest clip. Live viewer responses are stored under Temp/WardenIntegration for diagnosis only.
