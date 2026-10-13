# Bandage heal HP — installed Unity verification

2026-10-13. Installed on Brimstone, Clove, Gekko and Sage player prefabs.

## Runtime behavior

Owner self-use of a medkit plays the selected FDDA Bandage motion with empty hands. The 2.85-second source is sampled by the existing server progress over the existing 4-second heal duration. Healing still restores 50 HP and consumes one medkit only on completion. Cancellation, damage, disable and despawn restore the original hand presentation. Injection uses its existing approved clip and viewmodel. Remote observers and teammate treatment retain their existing presentation.

`SelfInjectionAnimation` now shares its existing manual PlayableGraph playback with healing. Each player receives a separate `*SelfHeal.prefab` and `*SelfHeal.anim` in `Assets/FPS/Features/Survival/Content/MedicalAnimation`. Those viewmodels reference the original character mesh and materials, retaining the player prefab hand placement. Only animation poses are retargeted; original hand FBX files, bind poses, geometry, weights, controllers and injection content are unchanged. Sage sleeve accessory bones follow their mapped forearm helpers through animation curves.

## Verification

- `runtime-heal-test.json`: PlayMode test passed for all four authored player prefabs. Covers original mesh/material identity, empty hands, hidden guns, remote observer exclusion, cancellation at 2/50/98 percent, interruption by damage, full four-second completion, 50 HP restoration, one-item consumption, switching heal → injection → heal, and disable cleanup.
- `runtime-injection-test.json`: existing PlayMode regression passed for all four characters, including cancellation and completion.
- `runtime-authority-test.json`: existing host/client authority, interruption and inventory persistence test passed.
- `runtime-presentation-test.json`: updated authored-player presentation test passed, including treatment audio, complete healing, subsequent injection and incendiary collision behavior.
- `native-surface-summary.json`: Unity `BakeMesh` output at all 172 source sample times for each character. Opposing-arm triangle BVH checks found zero potentially visible intersections in the prefab weapon camera's 21:9 frustum (vertical FOV 43.383068°, near plane 0.1 m). 16:9 and 4:3 are narrower subsets. The proximal ends, including open edges, are outside this frustum at every sample.
- Each character was exported as 80 Unity renders per aspect ratio, three aspect ratios, at 20 fps over four seconds. `Bandage-AllPlayers-Unity.gif` and the four aspect contact sheets are retained; raw sequences and redundant individual GIFs were recycled during cleanup.
- `../original-asset-integrity.json`: SHA256 comparison confirms 24 original hand/injection files are unchanged.
- Initial integration verification reported no Unity console errors. Authored active `GameScene` is checked with `isDirty=false` after the Save Gate. Untitled and temporary Test Runner scenes are never saved.

### Standalone verification (2026-10-13)

`standalone-build-report.json` records a successful Windows64 Mono non-Development build: zero errors, 32 existing compiler warnings. `Standalone-2026-10-13/complete.txt` records passing checks for all four shipped player prefabs loaded from the real `PlayerPrefabCatalog`, using a local NGO host in an isolated runtime scene. The Player exited normally with code 0 after 38.25 seconds, rendering through Direct3D11 at 1920x1080.

Each character passed original mesh/material identity, empty hands, original hands/weapon hiding and restoration, cancellation without healing or consumption, interruption by damage, a real four-second completion restoring 50 HP and consuming exactly one medkit, injection-to-bandage switching, component-disable cleanup and despawn cleanup. Sixteen Player screenshots were captured; their combined `standalone-contact.png` is retained after cleanup. The Player log contains no exception, assertion, or error entries. These controlled checks complement the earlier host/client PlayMode tests; they do not exercise a full multiplayer campaign.

`production-original-asset-integrity.json` confirms the same 24 original hand/injection files remain byte-identical. The standalone validation harness was temporarily copied into `Assets/FPS/Prototypes/MedicalAnimation`, then deleted through the Editor before the final build. Its research C#/Python copies were recycled during the final tooling cleanup; runtime regression tests remain in `Assets/Tests/PlayMode/SurvivalNetworkTests.cs`.

The clean production build is `Builds/MedicalAnimation/Release-2026-10-13/FPS.exe`. `release-build-report.json` records `Succeeded`, zero errors, 16 pre-existing warnings, 1,941,742,195 bytes, Windows64 Mono, non-Development, and all three authored scenes (MainMenu, LobbyScene, GameScene). All 19 medical content files stayed unchanged between standalone validation and the clean release (`production-content-hashes.json`).

`Release-Smoke-2026-10-13/result.json` confirms the clean executable started D3D11/Mono and remained running after 12 seconds. The launcher then deliberately terminated it; exit code 1 comes from that termination and does not establish normal application shutdown. No runtime exception/assertion/crash was observed in this startup smoke check. The shipped FPS assembly contains the medical runtime and excludes both the standalone validation and performance benchmark harnesses; the medical Editor assembly is absent from the build.

`production-final-scene-state.json` confirms active authored GameScene has `isDirty=false` after the final Save Gate, and `production-final-console-errors.json` reports zero errors. The installed native Bandage preview was reviewed and later closed before its Editor tooling was removed. One earlier UnitySkills request was aborted during script-domain reload; it was retried after compilation completed, with no remaining final console errors.

The historical installation/export menus and native surface/preview scripts were removed after integration was verified. Runtime assets are self-contained and do not need them. Editable motion remains in `../../Retarget-FDDA-v1/Bandage/SelfHeal_FDDA_Bandage.blend`. During installation, curve key reduction verified reconstruction error within 0.01 mm for local position components and 0.0001 for quaternion components before saving.

## Cleanup (2026-10-13)

Rejected prototypes, duplicate preview outputs, raw render sequences, `surface.bin`/`surface.json` caches and the standalone verification build were moved to the Windows Recycle Bin. Combined previews, aspect contact sheets, logs, reports, source Blend/FBX files and the final release are retained. `cleanup-recycled.json` and `cleanup-regenerated-cache-recycled.json` record the exact recycled paths.

Unused `Assets/FPS/Prototypes/MedicalAnimation/SelfHeal.fbx` and `SelfInjection.fbx` were deleted through Unity after dependency checks. Obsolete candidate Healing/Medkit/import menus were removed; the injection preview used the production FBX before the temporary Editor tools were removed. `cleanup-integrity-after.json` confirms all 57 protected files unchanged, and `cleanup-dependencies-after.json` confirms unchanged dependencies for all four player prefabs and three build scenes. Injection and installed Bandage native preview exports were checked after the tooling cleanup, then their regenerated caches were recycled.

A subsequent final cleanup removed the entire medical preview/install Editor folder and asmdef, together with the unused production `SelfHeal_FDDA_Bandage.fbx` (zero asset references). Production `SelfInjection.fbx` is retained because runtime prefabs reference it. Three duplicate research FBX exports and 26 temporary C#/Python scripts were recycled. `cleanup-final-tools-*.json` records these removals, the remaining protected-file hashes and final dependency/Editor checks. The final Bandage/injection Blend sources, two authoring scripts, production `.anim`/prefab assets and shared runtime regression tests are retained.

## Limits and provenance

The intersection check records 6 frames outside the checked frustum for Brimstone, 0 for Clove, 6 for Gekko and 9 for Sage. A changed camera/FOV may expose these. Surface checks sample the animation at 60 Hz; they do not prove every continuous-time pose. Standalone verification covers Windows64 Mono with the prefab camera setup; IL2CPP and other platforms have not been checked.

Source motion: https://github.com/FeelFried/FDDA-for-STALKER-Anomaly at `c012623f7affac2a0a9973f4e44c743b85794fcf`; source rig: https://github.com/firebreath1001/Stalker-Hand-Rig at `b073d271dba3583fb783b50c780cccc3aebf4c1e`. Reuse and redistribution rights remain unverified. Retained source candidates are under `../../Retarget-FDDA-v1`.
