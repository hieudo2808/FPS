# Gekko FDDA healing retarget — Blender research preview

Updated 2026-10-13. This folder retains the Blender research candidates. Bandage was subsequently selected and integrated into all four Unity player prefabs; see [Unity integration](../Integration/Bandage/README.md) for the installed assets and native verification. The candidate Blend files and original hand assets remain unchanged.

## Review files

| Clip | Timing | Blender file | Historical FBX | Full preview |
| --- | --- | --- | --- | --- |
| Bandage | 171 source frames at 60 fps; 2.85 s | [Blend](Bandage/SelfHeal_FDDA_Bandage.blend) | Recycled | [GIF](Bandage/Gekko-FDDA-Bandage-Blender.gif) |
| Medkit | 261 source frames at 60 fps; 4.35 s | [Blend](Medkit/SelfHeal_FDDA_Medkit.blend) | Recycled | [GIF](Medkit/Gekko-FDDA-Medkit-Blender.gif) |

GIFs sample every third frame at 20 fps without retiming. Both candidates include both actions; the corresponding action is selected in each file. Props, bandage rolls and applied dressings are absent. Bandage is mirrored to treat the left wrist; Medkit already treats the left arm. Open the Blend and press Space over the timeline to play.

## Placement and motion

The original Hand.fbx object transforms, mesh coordinates, topology, bone rest matrices and lengths, and skin weights are retained exactly. The camera uses the inverse of the GekkoPlayer prefab hand placement: Unity position (0.09, -1.69, 0.11), rotation xyzw (-0.012340715, 0.7069991, -0.012340715, 0.7069991), scale (1, 1, 1). Vertical FOV is 43.383068 degrees; near clip is 0.1 m. Animation pose offsets are distinct from this unchanged object placement.

Retargeting maps source palms, phalanges and forearms to Gekko's rest anatomy. Forearm twist is distributed through the existing twist bones. The source camera alignment is compensated by -12 degrees about model Y. The treated wrist path is placed 0.20 m left of the camera with 0.45 lateral variation; pose depth is adjusted 0.15 m forward, and height by 0.04 m for Bandage / 0.08 m for Medkit. These are baked into the candidate actions only.

Gekko's upper-arm sleeves require a different elbow/shoulder fit from the source rig. Upper arms retain their original lengths and point toward fixed body hints below the camera. Bandage's working forearm direction blends toward its body hint with weight 0.35. The wrapping pass uses a smooth 0.08 m axial range near the treated wrist, with transitions at frames 48–60 and 138–150, to avoid wrapping through the upper-arm sleeve. Working-wrist radial scale is 1.60 with 0.18 m clearance in quadrature. Medkit's middle finger has a 12 mm directional clearance target around frame 84, faded over frames 77–91; this correction leaves both wrists fixed.

## Validation and limits

- `rig-integrity-verification.json`: exact comparison against original Hand.fbx, including object transforms, rest bone lengths/matrices, geometry, topology and weights. Original source SHA256: `11fb61c303cdd4446edd223bf812d06b7796faf8af03a2ccf6637f8b9a552e7e`.
- `contact-summary.json` and each clip's `contact-check.json`: evaluated opposing-arm mesh BVH checks at all 172 / 262 baked frames. No intersections within the 21:9 camera frustum; 16:9 and 4:3 are subsets. Bandage still has six intersection frames outside this frustum. A different camera/FOV can expose them.
- `export-verification.json`: FBX reimport joint positions agree within approximately 0.002 mm; no potentially visible proximal cap faces at any checked frame in 21:9.
- Historical `Blender`, `Blender-aspect-1`, `Blender-aspect-2` exports contained full 16:9 samples plus five 21:9 and five 4:3 samples per clip. Their raw frames were recycled during cleanup; GIFs and `blender-preview-verification.json` retain the preview and timing evidence. This is Blender rendering, not native Unity/runtime verification.

The Blender research here previews Gekko. Native Unity previews and runtime verification for Brimstone, Clove, Gekko and Sage are recorded separately in `../Integration/Bandage`. Old experiments, `Before-source-elbow-fix`, Blend backups, render caches, redundant FBX exports and temporary verification/tuning/render scripts were recycled on 2026-10-13. Final Blend files, GIFs, validation reports and the retarget authoring script remain available.

## Sources and reproduction

Source motion: [FDDA for STALKER Anomaly](https://github.com/FeelFried/FDDA-for-STALKER-Anomaly), commit `c012623f7affac2a0a9973f4e44c743b85794fcf`, OMF chunks 17 (Bandage) and 16 (Medkit). Source rig: [Stalker Hand Rig](https://github.com/firebreath1001/Stalker-Hand-Rig), commit `b073d271dba3583fb783b50c780cccc3aebf4c1e`. Reuse and redistribution rights are not verified. These outputs are research candidates, not cleared distribution assets.

Open the final Bandage or Medkit Blend directly to edit/play the baked motion. The original retarget authoring script is retained as `../SourceCandidates/FDDA/Preview/retarget_gekko.py`; historical checks and renders are documented in the retained JSON reports, and their temporary scripts were recycled. The native working files have the original DF texture and camera viewport configured for review.
