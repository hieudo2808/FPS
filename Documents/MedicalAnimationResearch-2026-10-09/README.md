# Medical animation sources and verification

Updated 2026-10-13 after production integration and cleanup.

- Production Unity assets: `Assets/FPS/Features/Survival/Content/MedicalAnimation`. Bandage healing is installed on Brimstone, Clove, Gekko and Sage; the approved injection is retained.
- Editable Bandage source: [Blender](Retarget-FDDA-v1/Bandage/SelfHeal_FDDA_Bandage.blend). The retarget authoring script and original FDDA source data remain under `SourceCandidates/FDDA`; production playback uses the four baked `.anim` assets.
- Editable injection source: [Blender](InjectionPrototype-v5/SelfInjection_Prototype.blend) and `create_injection_preview_v5.py`. Its required production FBX remains in Unity.
- Native Unity evidence and runtime/build reports: [Bandage integration](Integration/Bandage/README.md), [combined preview](Integration/Bandage/Bandage-AllPlayers-Unity.gif), and the four `Integration/Bandage/*-aspects.png` contact sheets. Injection evidence remains under `InjectionPrototype-v5` and `Integration`.
- Final Windows64 Mono release: `Builds/MedicalAnimation/Release-2026-10-13/FPS.exe`, relative to the project root.

Rejected experiments, duplicate exports, render/surface caches and the standalone verification build were moved to the Windows Recycle Bin. Two unused prototype FBXs were deleted through Unity; obsolete candidate preview/import menus were removed. Final sources, runtime, player prefabs, original hand assets and the release executable remained byte-identical. The Medkit research Blend files were retained, including the files open in Blender.

The final tooling cleanup removed the three medical preview/install Editor scripts and their asmdef, plus the unreferenced production Bandage FBX. Three redundant research FBX exports and 26 temporary verification, rendering and tuning scripts were recycled. Shared regression tests are retained. The final Blend sources and two authoring scripts remain available; the medical Editor menus are no longer installed.

Exact cleanup lists, protected SHA256 checks, dependency comparisons and preview verification are recorded in `Integration/Bandage/cleanup-*.json`. Reuse and redistribution rights for the downloaded FDDA motion/rig remain unverified; provenance is retained in the source and integration notes.
