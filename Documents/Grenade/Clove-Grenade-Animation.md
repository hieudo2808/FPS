# Four-player grenade animation integration

Implemented for `ClovePlayer.prefab`, `BrimstonePlayer.prefab`, `GekkoPlayer.prefab` and `SagePlayer.prefab`:

- 1P uses `CloveGrenade1P.anim` on `CloveGrenade_FP.controller`.
- Clove/Gekko/Sage 3P reuse `CloveGrenade3P.anim` and the five existing Clove generic-path-bound gun controllers. Brimstone's five existing controllers use `BrimstoneGrenade3P.anim` and `BrimstoneGrenadeThrow.asset`.
- Both controllers have one upper-body `Grenade Throw` layer with a single `Throw` state. The 3P layer leaves locomotion/legs running.
- Server replicates one `GrenadeThrowState` containing sequence, grenade kind, start, release and end times. The projectile is spawned and the inventory count is reduced at the authored release time (0.333 s); the full action is 1.25 s.
- The grenade visual is held at the authored hand until release, then disappears. Both peers play from server time, so 1P owner and 3P observers stay in phase.
- Starting another grenade, weapon switching, shooting, reload and medical use are rejected while the throw is active. Sprint/death/respawn cancels before release without consuming a grenade.
- Frag and incendiary use the same throw animation; only the projectile behavior differs.
- All five weapon models are hidden during the action and restored afterward in both views. This includes the Operator, Odin and Bucky replacements that share the primary inventory slot.

## Brimstone bone axes

Matching hierarchy names alone is not sufficient. Brimstone's `CS_Sarge_S0_Skelmesh.ao` rig has different bone coordinate frames; merely replacing the Clove path prefix folded its torso upside down. The corrected clip converts all 61 retained upper-body bones using the body meshes' bind-pose matrices (427 position/quaternion curves, 24 fps). The absent optional shoulder-helper bone is omitted. Neither model nor locomotion clips were changed, and Blender was not required.

For each bone, `C = inverse(sourceBindRotation) * targetBindRotation` in Animator-root space. The baked local rotation is `inverse(C_parent) * sourceAnimatedRotation * C_bone`. Translation is the target rest offset plus the animated displacement converted through `inverse(C_parent)`. Quaternion signs are kept continuous. No pelvis, leg or root curves are added.

The 3P grenade grip also uses the Brimstone hand axes: offset approximately `(0, .05, -.01)` metres and rotation `(0, 90, 180)` degrees. 1P keeps its original grip; the other three 3P rigs retain `(0.05, -.01, 0)` and `(0, 0, 90)`.

The first retarget pass exposed a real stretch artifact, not just camera perspective: the imported clip translated `R_Elbow`/`R_Hand`, briefly increasing the forearm from about 0.274 m to 0.411 m. Both 3P clips now keep clavicle, elbow and hand local positions at their bind-pose values and retain only the authored rotations. Segment lengths are rigid across the full clip; no mesh, FBX or Blender edit was needed.

The selected source clips are:

- `1P/Anims/Grenade/FP_Core_Grenade_OverhandThrow.fbx`
- `3P/Anims/Grenade/TP_Core_Grenade_ThrowPrimary_UB_Montage.fbx`

The other 66 FBX grenade clips (idle, aim directions, crouch/run, underhand, secondary and duplicate montage variants) were unused by the project after the audit. They were archived with their `.meta` files before removal in `Grenade-unused-source-backup.zip`; `Archived-source-manifest.json` contains SHA-256 hashes for all 132 archived files.

Verification:

- The earlier Clove-only suite passed 6/6 (`Clove-Grenade-Tests.json`). The four-player tests are parameterized by character rather than copied into separate fixtures.
- Final four-player run: **12/12 passed** (four host-owned cases plus four client-owned five-weapon cases per player); XML is saved as `Documents/Grenade/FourPlayers-Final-Tests.xml`.
- `PlayersAuthoredGrenadeReleasesOnceAndRestoresBothRigs` checks host-owned frag/incendiary throws, release timing, duplicate requests, held visuals, recovery and sprint cancellation on all four prefabs.
- `ClientPlayersThrowWithAllFiveWeaponsAndPreserveLegs` checks client-owned throws with Vandal, Operator, Odin, Bucky and Classic: replicated count, owner 1P, host 3P, weapon hiding and restoration. It compares seven pelvis/leg bones with the throw layer on/off at two identical locomotion phases and six throw phases. The legs remain unchanged by the throw while continuing to animate between phases; the head must remain above the pelvis to catch the Brimstone axis regression.
- The client test waits for the replicated presentation to enter windup because NGO buffers the client's server clock; an initial fixed wall-clock assertion was unsuitable for this case.
- Captures: `Documents/UIUX/{Clove,Brimstone,Gekko,Sage}-Grenade-{1P,3P}-{Frag,Incendiary}-{windup,release}.png`.
- Saved-asset audit: all four prefabs have both Animator/hand references, one throw layer per controller, and no missing throw-curve paths. Shared release time is 0.333 s, duration 1.25 s.

The clip bake is intentionally generic-path based and does not require a Humanoid avatar. The 1P/3P animations currently share the overhand action; direction-specific aim and underhand throws are left out because the gameplay input is a single immediate throw, not a charge/aim selector.

The transport tests run host and client inside one Unity process. They do not establish two-machine/LAN behavior. Existing fixture warnings about the absent HUD and initialization of network variables remain in Editor.log. The procedural fallback remains for fixtures or future prefabs without an authored throw definition; all four production Player prefabs now use the authored animation.
