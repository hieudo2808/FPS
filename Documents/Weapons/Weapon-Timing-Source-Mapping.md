# Weapon timing from ThongSo

Updated 2026-10-03 using the six HTML viewer exports in `Assets/FPS/Features/Weapons/Data/ThongSo/`.

## Source facts and mapping

These files contain model-viewer settings and animation metadata, not complete combat balance tables. The viewer sets each animation action's timeScale to imported clip duration / SequenceLength. FireRate, ReloadLength and EquipLength independently gate the next action. Consequently a clip can continue after gameplay is unlocked, or be interrupted by a new action.

| Weapon | FireRate (rounds/s) | FireInterval (s) | Equip lock (s) | Reload lock (s) | Equip clip (s) | Reload clip (s) |
|---|---:|---:|---:|---:|---:|---:|
| Bucky | 1.1 | 0.909091 | 0.75 | 1.75* | 1.333333 | 2.166667* |
| Classic | 6.75 | 0.148148 | 0.75 | 1.75 | 1 | 2.116667 |
| Odin | 12 | 0.083333 | 1.25 | 5 | 2.833333 | 6 |
| Operator | 0.6 | 1.666667 | 1.5 | 3.7 | 2.333333 | 4.5 |
| Vandal | 9.75 | 0.102564 | 1 | 2.5 | 1.65 | 2.933333 |
| Warden | 6.5 | 0.153846 | 1 | 2.5 | 2.333333 | 3.8 |

| Weapon | Idle FP (s) | Fire FP/GN (s) | Inspect FP (s) | Inspect gun |
|---|---:|---:|---:|---|
| Bucky | 2.733333 | 1.333333 | 4.25 | 4.25 s |
| Classic | 2.733333 | 0.333333 | 3.2 | Idle pose |
| Odin | 2.733333 | 0.5 | 7.5 | **7 s** (`SequenceLengthGN`) |
| Operator | 2.733333 | 1.8 | 4.333333 | Idle pose |
| Vandal | Static IdlePose; no SequenceLength | 0.316667 | 4.133333 | Idle pose |
| Warden | 2.733333 | 0.316667 | 4.166667 | Idle pose |

`WeaponData.FireInterval = 1 / FireRate`. Gameplay Equip/Reload durations store Settings. New serialized presentation durations store the Equip/Reload SequenceLengths; zero falls back to gameplay duration for legacy definitions. Existing automatic/single modes are retained; Odin, Vandal and Warden match AutomaticWeapon=true.

FP and gun state speed = actual Unity AnimationClip.length / target SequenceLength. Do not assume every imported clip needs speed 2.5. For example Warden FP Equip is 5.5 / 2.333333 = 2.357143, GN Equip is 5.833333 / 2.333333 = 2.5. FP Fire is 0.625 / 0.316667 = 1.973684, GN Fire is 0.791667 / 0.316667 = 2.5. Reload is driven by Motion Time; its runtime normalized clock now uses ReloadAnimationDuration rather than ReloadDuration.

## Gameplay integration and derived choices

Reload timeline uses immutable server start time and the infection timing multiplier for both gameplay and presentation. Receiving a delayed snapshot samples original elapsed time; it does not speed up the clip to fit the remaining lock. Natural completion retains the visual tail. Fire, Equip, Inspect, weapon disable and combat disable clear it. The existing server remains the authority for ammo and firing eligibility.

Third-person Equip/Reload controllers follow these presentation clocks through EquipNormalizedTime/ReloadNormalizedTime. Their different TP clips are sampled against the corresponding action durations for synchronization; the files do not supply separate TP SequenceLengths. Third-person Fire clips are retained. Odin retains its continuous feed/ejection loop: gun Fire uses the source duration, hands continue sampling its phase rather than restarting or independently advancing every shot.

Magazine commit frames were already authored in the project. They remain unchanged, converted using the new clip playback speed and clamped to the gameplay lock: Classic 0.9 s, Odin 2 s, Operator 1.616667 s, Vandal 1.35 s. Warden has no authored earlier marker and commits at 2.5 s. These commit moments are project choices, not supplied facts from ThongSo.

*Bucky remains PerShell. The source supplies one ReloadLength/SequenceLength, without a full per-shell schedule. The mapping uses the existing source-frame loop 13–41 of 130 frames: opening 0.216667 s, insert interval 0.466667 s, gameplay closing 1.066667 s. The visual closing lasts 1.483333 s. For N inserted rounds, gameplay duration is 1.75 + (N−1) × 0.466667 s; presentation duration is 2.166667 + (N−1) × 0.466667 s. The same multiplier applies to both. Thus shell commits stay aligned with the insert loop, gameplay is available before the final visual tail, and firing can still interrupt a partially filled reload. This is an explicit adaptation of the source to the project's existing per-shell design.

## Fields outside the supplied evidence

FireRateAlt is present for Classic (2.22) and Bucky (1.111), with an Alt Fire clip for Bucky. The current input/runtime uses right-click for ADS and has no alternate-fire action; these values are documented but not wired into the primary fire rate or input.

Held-model FOV, CameraPosition, OrbitTarget, ShadowHeight and Shadows configure the orbit viewer. They are not ADS, ballistic or weapon transform settings. First-person model FOV is 43.383067; Warden's earlier Clove viewmodel camera setup already uses it. Damage, magazine capacity, reserve ammo, bullet speed/lifetime, spread, recoil and falloff were preserved because the files contain no corresponding gameplay values.

Original source FBX assets and the Warden attachment/hierarchy correction were preserved. Runtime and asset backups for this timing task are in `Temp/WeaponTimingBefore/`. Serialized before/after data and controller timing audit are in `Temp/Weapon*Audit*.json` and `Temp/WeaponTimingChanges.json`.

## Verification

Final targeted EditMode run: **23 passed, 0 failed** (19 WeaponTimingTests cases plus 4 existing hidden third-person presentation cases for Brimstone, Clove, Gekko and Sage). Covers the six saved definitions, legacy fallbacks, independent reload clocks, delayed snapshots and infection multiplier, Bucky inserts/cancellation, natural visual tails, interruption/disable, third-person timing and Odin's separate Inspect durations. NUnit XML: `WeaponTimingTests.xml`.

Unity compilation completed successfully with 0 Console errors. Saved-asset readback verified all six definitions and 124 animation states across 32 related controllers; serialized comparisons confirmed unrelated weapon properties were unchanged. The four player prefabs reference the updated shared CloveGrenade_FP arms controller, and their 21 weapon instances reference the matching data assets. The GameScene remained clean. This verification exercises timing rules and Editor presentation; it does not represent a multiplayer PlayMode session.
