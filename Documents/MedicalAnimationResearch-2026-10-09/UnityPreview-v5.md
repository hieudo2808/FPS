# Self-injection V5 — Unity preview

- One Generic clip, 5 seconds at 24 fps, baked from Gekko source poses and an authored syringe grip.
- Right shoulder retracts during entrance and exit; needle contact and piston press retain the V4 pose.
- Authoring checks: wrist bend <= 25.001 degrees, forearm roll < 90 degrees, needle and thumb target errors < 1 micrometre. These are authoring measurements, not imported Unity contact tolerances.
- Unity preview uses native AnimationClipPlayable and SkinnedMeshRenderer, URP lighting and temporary preview materials.
- Camera framing uses the authored eye markers with Gekko prefab WeaponCamera FOV 43.38307 degrees and near plane 0.1 m. Eye placement remains authored for this prototype.
- Exported and visually reviewed 60 samples at 12 fps for each aspect: 16:9, 21:9, 4:3. Checked entrance and retreat closely at 21:9; the detached upper-arm fragment seen in V4 is absent in the reviewed V5 renders.
- Native deformation check confirms both imported skinned meshes change between entrance and injection; minimum bounds-centre motion is 0.3652231 m.
- GIF is made only from Unity-rendered PNGs: 60 frames, total 5000 ms.

The historical Unity preview window looped the clip and provided a timeline slider, FOV and aspect ratio controls. That temporary Editor tool was removed during cleanup on 2026-10-13; the rendered GIF and installed runtime assets are retained.

Required production asset: Assets/FPS/Features/Survival/Content/MedicalAnimation/SelfInjection.fbx. The obsolete prototype copy was deleted.

Preview: InjectionPrototype-v5/Unity/SelfInjection-Unity-v5.gif

The original preview above uses Gekko and flat preview colours. On 10 October the approved animation was integrated into the four player prefabs and checked in Play Mode with their original materials; see [runtime integration](Integration/README.md). Wider or different camera framing still needs another visual check.
