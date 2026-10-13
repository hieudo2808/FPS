# Downloaded FDDA medical motion data

Source: https://github.com/FeelFried/FDDA-for-STALKER-Anomaly

Pinned commit: `c012623f7affac2a0a9973f4e44c743b85794fcf`.

Downloaded from the author's public GitHub repository. `download-manifest.json` records the original paths, sizes and SHA-256 hashes. The original files are preserved without modification.

## Verified motion contents

`meshes/anomaly_weapons/hud_hands_animation/zzzz_ea_a_main.omf` contains 117 motion chunks for a 42-bone first-person hand rig, including:

| Motion | Frames | Bones with varying rotation data |
| --- | --- | --- |
| `bint_hands_eat` | 171 | 40 |
| `bint_hands_eat_invtrans` | 171 | 41 |
| `bint_hands_eat_alt_1` | 171 | 40 |
| `bint_hands_eat_alt_1_invtrans` | 171 | 41 |
| `aptechka_hands_eat` | 261 | 38 |
| `aptechka_hands_eat_invtrans` | 261 | 39 |

These are actual packed per-frame quaternion/translation samples. Varying channels include left/right upper arms, forearms, forearm twist, hands and finger joints. This is neither video capture nor an animation-name-only script.

The supplied bandage configuration selects `bint_hands_eat_alt_1_invtrans` for the hands and `bint_wpn_eat` for the prop. The medkit configuration selects `aptechka_hands_eat_invtrans` and `aptechka_wpn_eat`. The downloaded prop OMF files have matching 171/261-frame actions, respectively. Basic bandage/medkit prop OGF files are also included.

Some names occur in multiple motion chunks. `motion-inspection.json` retains each chunk ID so a subsequent conversion must not overwrite actions merely because their names match.

## Verification and limits

`inspect_omf.py` reads the X-Ray OMF layout documented in https://github.com/PavelBlend/blender-xray/tree/master/io_scene_xray/formats/omf . It validates chunk boundaries, count consistency, contiguous bone IDs, supported flags, every bone track's exact byte span and presence of changing medical rotation tracks. Its valid/truncated chunk self-check passed, and every motion in the three OMF files parsed to its exact end.

The matching 42 deform bones and a textured hand mesh were subsequently obtained from https://github.com/firebreath1001/Stalker-Hand-Rig at commit `b073d271dba3583fb783b50c780cccc3aebf4c1e`. The downloaded `Preview/Stalker-ICHI-source.blend` remains unchanged. The working previews remove controller constraints and import each original OMF bone sample as location/quaternion keys using the blender-xray bind/coordinate conversion. First/middle/last-frame matrix errors are below `0.000001`; see `Preview/preview-verification.json`.

The Blender previews use bandage chunk 17 and medkit chunk 16, all 171/261 source frames, and the configuration's motion speed 2.0 (60 source frames per second). Duplicate chunks 117/106 were also imported and their three sample renders inspected; they did not show a visible difference at those samples. The chosen chunk IDs remain explicit. Game HUD positions are applied separately from the joint keys. Props are hidden for this hands-only motion review.

The review camera looks down 10 degrees to show the lower hand motion. Open shoulder sleeve rims are extended behind the camera in the working mesh to avoid exposed cuts. These are display adjustments, not changes to the original bone animation. Full-motion GIFs and replayable Blender files are in `Preview/`; source keys remain at 60 fps, GIFs sample every third frame at 20 fps. Gekko retargeting, runtime contacts and Unity framing remain unverified. The original bandage treats the right arm, so the eventual left-arm action needs retargeting/mirroring. No source medical motion is approved on appearance yet.

The repository has no declared license in GitHub metadata and its README links to the mod's main page without granting reuse rights. Downloaded for research; commercial/project redistribution permission remains unverified. No assets were bought, no runtime/prefab asset was changed, and no capture-based motion was created.
