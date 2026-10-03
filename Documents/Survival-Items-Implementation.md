# Survival items — implementation and verification

Updated: 2026-09-20. Unity 6000.5.6f1, Built-in render pipeline, NGO 2.13.1.

## Delivered behavior

| Input (rebindable) | Action |
| --- | --- |
| G | Immediately throw the selected grenade |
| 3 | Switch frag / incendiary |
| 4 | Use a medkit on yourself |
| 5 | Use an antidote on yourself |
| F | Pick up an available item or assist a teammate under the interaction reticle |

Inventory starts at, and is capped at, 3 frag, 3 incendiary, 2 medkits and 2 antidotes. The grenade limit is interpreted per type. HUD counts come from replicated inventory; no fixed grenade count remains. Existing saves without the survival inventory version receive the initial loadout; new checkpoints and respawns retain actual counts and selected grenade.

Frag: collision or 2.2 s fuse, 4 m radius, linear 100–25 damage, server raycast occlusion. Multiple victim colliders use the nearest visible collider and receive one hit. Incendiary: 3 m hazard for 6 s, one hit per victim per 0.5 s world tick (12 enemy / 4 player damage). Overlapping unobstructed zones extend the existing zone's duration; they do not expand its footprint or multiply damage.

Medkit: +50 HP, capped at maximum, 4 s self / 2.5 s assist. Antidote: −40 from the infection value at completion, clamped to zero, 5 s self / 3 s assist. Server validates alive/combat state, stock, teammate distance ≤3 m, sightline and action state. Combat damage to either participant, sprint, lost range/sightline or death cancels use without consuming stock. Counts decrease only on completion. Reloading blocks starting a consumable or throw; medical use blocks weapon input/aim/reload. Walking slows to 55% speed.

Periodic Sepsis drain deliberately does not cancel treatment: otherwise the 5 s antidote can be repeatedly prevented by the infection it treats. Attacks, gunfire and fire hazards still interrupt. Falling below Sepsis resets its timer/cadence. There is no player-facing free infection cure or separate H-key medicine reserve.

## Content and presentation

- Four pickup prefabs, network projectile and fire zone registered; Brimstone, Clove, Gekko and Sage have inventory/presentation components and first-/third-person arm references.
- Actual supplied grenade mesh, textures, HealEffect source and audio are reused. Medkit pouch and antidote vial are basic local geometry; VirusSample supplied only a 2D image, not a usable vial model.
- Hotbar uses transparent thumbnails rendered from the four actual item models. Selection, unavailable stock, rebind labels and medical progress are supported. Minimum hotbar width is 320 screen pixels. No slogan or match-phase label was added.
- Medical poses and the throw gesture are procedural two-bone arm overlays. Weapon rendering is restored when the action ends. These are not bespoke authored animation clips.
- Pooled flash, dust, fragments, shock ring, scorch, medical completion, flames, embers, smoke and sound. Flame sprite sheet is from the supplied folder. Local heat distortion is capped at 1.5 pixels and draws before the weapon/UI. Its named GrabPass shares one background copy per camera; assess this cost on low-end target hardware.
- 72 campaign survival supplies across 18 rows: 36 available to 1–2 players, another 36 for 3–4 players. Each has a stable claim ID. All were positioned against actual scene colliders, including the Factory row formerly below the road. Existing ordinary campaign supplies were preserved.

## Verification evidence

| Verification | Result |
| --- | --- |
| SurvivalRulesTests, EditMode, job e7f317a5 | 6/6 passed |
| SurvivalNetworkTests, PlayMode, job 52db19e3 | 4/4 passed |
| HUD and Controls captures at 1920×1080, 1280×720, 1024×768 | 6 captures, 0 text/bounds issues; visually inspected |
| Character/catalog/network prefab validation | Four characters, twelve arm references each, six network prefabs |
| Pickup surface queries | 72 adjusted, 0 missing surfaces |

The four PlayMode tests use actual localhost NGO host/client transports **in one Unity process**, with client physics/render copies excluded from host queries/captures. Coverage includes client authority rejection, repeated grenade RPCs, counts replicated, actual 4/5 s self treatment and 3 s teammate antidote, medkit assist, interruptions at early/middle/near-complete progress, sprint, current-value infection reduction, Sepsis, wall occlusion, unequal collider falloff, fire merge/tick/expiry, pickup idempotency, checkpoint/respawn, solo after disconnect, moving obstacle/stair collisions and 24 enemy-health targets receiving one hazard hit per tick.

Real Brimstone prefab capture checks item visibility, audio, completion cleanup and incendiary collision. Visual captures use a test scene; HUD resolution captures use copies of the authored UI. These do not substitute for a complete two-machine co-op campaign walkthrough, build validation, latency/loss testing, animated zombie-horde playthrough or performance profiling. Clove/Gekko/Sage references were validated but their medical poses were not individually visually exercised.

During verification a temporary screenshot helper released its RenderTexture before its camera. The helper was corrected and rerun successfully. Final Console checks are performed after the corrected run; compiler warnings elsewhere in the project remain.

## Images

- `UIUX/GameScene-survival-1920x1080.png`, `1280x720.png`, `1024x768.png`
- `UIUX/MainMenu-controls-1920x1080.png`, `1280x720.png`, `1024x768.png`
- `UIUX/Survival-Medkit-runtime.png`, `Survival-Antidote-runtime.png`
- `UIUX/Survival-FireLoop-visual.png`, `Survival-FragBlast-visual.png`
- `UIUX/Survival-Pickups-map.png` (Edit Mode view also shows reserve row)

## Imported source provenance

The following files were copied from the user's supplied library; no additional download was required in this completion pass. Their supplied provenance is recorded here without asserting a distribution license.

- `E:\ProjectSettings\Assets\Mesh\HE_Grenade_3PV.asset` → `Assets\FPS\Features\Survival\Content\Meshes\FragGrenade.asset`
- `E:\ProjectSettings\Assets\Texture2D\HE_Granade_albedo.png` → `Assets\FPS\Features\Survival\Content\Textures\FragGrenadeAlbedo.png`
- `E:\ProjectSettings\Assets\Texture2D\HE_Granade_Normal.png` → `Assets\FPS\Features\Survival\Content\Textures\FragGrenadeNormal.png`
- `E:\ProjectSettings\Assets\GameObject\HealEffect.prefab` → `Assets\FPS\Features\Survival\Content\Effects\Source\HealEffect.prefab`
- `E:\ProjectSettings\Assets\AudioClip\DE_K_exploze_handgrenade.wav` → `Assets\FPS\Features\Survival\Content\Audio\FragExplosion.wav`
- `E:\ProjectSettings\Assets\AudioClip\pickup_ammo_DE.wav` → `Assets\FPS\Features\Survival\Content\Audio\PickupFrag.wav`
- `E:\ProjectSettings\Assets\AudioClip\pickup_shells_DE2.wav` → `Assets\FPS\Features\Survival\Content\Audio\PickupFire.wav`
- `E:\ProjectSettings\Assets\AudioClip\pickup_loot_DE2.wav` → `Assets\FPS\Features\Survival\Content\Audio\PickupMedkit.wav`
- `E:\ProjectSettings\Assets\AudioClip\pickup_ipad_DE2.wav` → `Assets\FPS\Features\Survival\Content\Audio\PickupAntidote.wav`
- `E:\ProjectSettings\Assets\AudioClip\healing_chamber_DE.wav` → `Assets\FPS\Features\Survival\Content\Audio\Treatment.wav`
- `E:\ProjectSettings\Assets\AudioClip\DE_paralyzer_burn01.wav` → `Assets\FPS\Features\Survival\Content\Audio\FireLoop.wav`
- `E:\ProjectSettings\Assets\AudioClip\Carabine_reload_out.ogg` → `Assets\FPS\Features\Survival\Content\Audio\Throw.ogg`
- `E:\ProjectSettings\Assets\Texture2D\FlameParticles01.png` → `Assets\FPS\Features\Survival\Content\Textures\FlameFlipbook.png`

## Tooling notes

Unity assets/scenes were queried and authored through the Editor. The Codebase Memory executable was unavailable because its security identity check rejected the cache-directory DACL; no security check was bypassed. Narrowed source reads were used with the known implementation paths.

Final Editor verification: MainMenu active, Edit Mode, `isDirty=false`, `playing=false`, `compiling=false`. Asset validation passed, and the Unity Semantic Graph export completed through its Editor menu (the calling tool timed out while export continued, then the completion was verified in Editor.log).

Final Console query returned 0 errors. Existing compiler warnings remain; no claim of a warning-free project is made.
