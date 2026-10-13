# FDDA source animation previews

Cleanup note (2026-10-13): the C#/Python inspection, verification and render helpers mentioned below are historical and were recycled after Bandage integration. Source Blend files, GIFs, JSON evidence and `retarget_gekko.py` remain available. Native Unity verification is recorded in `../../../Integration/Bandage/README.md`.

Open `FDDA-Bandage-Source-Preview.gif` or `FDDA-Medkit-Source-Preview.gif` to review the complete motion. These are textured STALKER source hands, not Gekko or Unity runtime previews. Props are hidden. Bandage treats the right arm in the original action.

For native playback, open the corresponding `FDDA-Bandage-Preview.blend` or `FDDA-Medkit-Preview.blend` in Blender and press Space over the timeline. Each working file includes both original actions. Bone keys are preserved at every source frame; playback is 60 fps, reflecting OMF speed 2.0. The looping GIFs sample at 20 fps without retiming: 2.85 seconds for bandage, 4.35 seconds for medkit.

The historical `create_source_preview.py` import used the packed OMF data, blender-xray coordinate/bind conversion and checks for all 42 matching bones. The historical render/GIF helpers are recycled; their results remain in `preview-verification.json` and the retained source GIFs. Downloaded source files are unchanged.

Blender 5.1.1 was used in an isolated background process. The open Unity/Blender scenes and Unity runtime/prefabs were not edited. No claim of Gekko compatibility or approved animation quality is made. FDDA and hand-rig reuse/redistribution permissions remain unverified; these files are research previews.

Source provenance and license limits: see `../ACQUISITION.md` and `../download-manifest.json`.
