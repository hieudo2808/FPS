# Downloaded animation-source candidates — 2026-10-12

These files were acquired for source inspection on 2026-10-12. Updated 2026-10-13: FDDA Bandage was selected, retargeted and integrated into all four Unity player prefabs; see [integration and verification](../Integration/Bandage/README.md). The rejected VintageStory, BandageSpline and HYDRAV downloads and their inspection outputs were recycled during cleanup. The table and hashes below retain the acquisition history; FDDA source data, working Blend files and `Preview/retarget_gekko.py` remain available. Temporary inspection, verification, rendering and tuning scripts were also recycled after integration.

| Local file | Author/source | Inspection result |
| --- | --- | --- |
| `VintageStory-player-animation-patch.json` | Afroman726: https://raw.githubusercontent.com/Afroman726/bandageanimation/master/bandageanimation/assets/bandageanimation/patches/player-animation-patch.json | Three bandage motions with actual keyframes, block-character joints, no wrist/finger channels. |
| `BandageSpline.rar`, extracted `BandageSpline.blend` | Alexandr Prasolov's attachment: https://blenderartists.org/uploads/short-url/aNToa4XcndWSrUUsmd3U2QPaXZw.rar ; topic: https://blenderartists.org/t/mesh-twitsts-when-armature-follows-curve/1548651 | 34-bone bandage-strip rig, no human arms/hands; keyed armature channels do not vary. |
| `HYDRAV-fbxanitest.zip` | HYDRAV official free sample: https://hydrav.itch.io/standardanimationfbx | Archive list has hit/death, locomotion/jump and mannequin FBX; no medical motion. |
| `FDDA/` | FeelFried's public repository: https://github.com/FeelFried/FDDA-for-STALKER-Anomaly | Downloaded OMF medical hand/prop motions and basic prop OGF files. Confirmed actual changing arm/hand/finger key data. See `FDDA/ACQUISITION.md`. |

The recycled `BandageSpline-inspection.json` was produced by Blender 5.1.1 in a separate background process with `--disable-autoexec`, without saving the downloaded blend, and listed the rig, meshes and action/channel counts. `VintageStory-inspection.json` listed exact animation names, frame numbers and joint channels. `HYDRAV-fbxanitest-contents.txt` listed the sample archive contents; its individual FBX files were not imported.

No reuse license was established for these public candidates. Do not treat public downloadability as project integration approval or a verified commercial reuse license.

SHA-256:

```text
VintageStory-player-animation-patch.json
FAF946A0DDC2D8499A1113E4B0C977F036F34D220AE41CD848DDB063C8A5A691

BandageSpline.rar
E736A0250844DABAE3303DCFEE12F9955C97C5DB2F4D4679DFA3C16F4C0BBEA1

BandageSpline.blend
A9EF3DBE9427E0CBD9F1A07C812B46F65A99E1DC1449525BFBEBBF06437A5476

HYDRAV-fbxanitest.zip
951295D35998683EC56B347A30751C8888305B336E12B47C4C5932C13EAC4505
```
