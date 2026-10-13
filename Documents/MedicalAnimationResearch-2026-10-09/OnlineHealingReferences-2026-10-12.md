# Online healing animation research — 2026-10-12

## Current status

V7 is rejected by the user. Its hand retraces a semicircle instead of completing a directional wrap; the cropped proximal arm/sleeve remains visible and deformed. Passing contact and joint checks do not establish a believable wrapping action. Do not deliver V7 as approved or integrate it into gameplay.

Requested result: show and lower a medkit, then mime wrapping around the **left wrist** with empty hands. No visible roll, no dressing attached to the arm. Movement should come primarily from the elbow, with anatomical wrist motion and no exposed arm ends. Gekko preview only. Approved injection and the four player prefabs remain outside this correction.

## Sources actually inspected

### 1. Polydin — First Person Healing Interactions

- Product: https://www.fab.com/listings/dbc813e9-8c51-41e6-b12d-f884293e64a1
- Publisher demo: https://www.youtube.com/watch?v=ctbwl1_u_yg
- Publisher announcement: https://forums.unrealengine.com/t/polydin-3d-first-person-healing-interactions/2624923
- Publisher states 33 hand-animated first-person interactions and 13 rigged props. The announcement explicitly lists **five bandage interactions**. The demo description says the animations are optimized for the player camera.
- Native browser screenshots inspected at 2.5, 5, 8, 9 and 12 seconds. The opening section is the bandage category, including labeled interactions 01, 02 and 03. Interaction 02 visibly presents a bandage roll; props would need to be omitted for the requested empty-hand version.
- Best candidate pack to investigate for a first-person motion base. This is a paid product: the publisher's demo links to "Buy this pack on Fab". No purchase was made and no animation source was obtained. Fab returned a security challenge, so current price, file formats, skeleton and license details were not verified. Do not claim ready compatibility with Unity/Gekko.

### 2. FC4 Bandage Animation recreation — legendhavoc175

- Video: https://www.youtube.com/watch?v=OnWxx0x2mEM
- Native frames inspected at 1.5, 2.5 and 3.2 seconds. At 1.5 seconds the left forearm is horizontal and the right hand holds the roll above it. At 2.5 seconds the bandage crosses the upper wrist; at 3.2 seconds the right hand and roll are below the wrist. This is a useful visible sequence of **above → around → below**, unlike V7's retraced arc.
- Saved comparison image: `OnlineReferences/FC4-wrist-wrap-reference.png`.
- This is an artist's recreation, not verified original Far Cry keyframes. No downloadable animation file or reuse license was found in the inspected video description. Use as a visual reference, not as an acquired FBX.

### 3. nikoff — Survival Medical & Healing Animations — Mocap Pack

- Product: https://www.fab.com/listings/5ac1b71a-a4ec-4cf0-b140-5fc50dcb1827
- Publisher announcement: https://forums.unrealengine.com/t/nikoff-survival-medical-healing-animations-mocap-pack/2720718
- Demo linked by publisher: https://youtu.be/8csizz4dZCo
- Publisher animation list: https://docs.google.com/spreadsheets/d/18daQPYlye7ZG9mvWZeCW2Ww1IyV9AZDCVMBzzh27goo/edit
- The public animation list was read directly. It includes `AS_Med_Bandage_Arm_In`, `AS_Med_Bandage_Arm_Hand_Out` (tears off the bandage by hand), and `AS_Med_Bandage_Arm_Teeth_Out` (tears it by teeth).
- Potential alternative for a complete start/finish motion. Its announcement covers characters and NPCs; first-person framing was not verified and its video was not inspected. No source file, price, format or license was verified. Do not call it a ready first-person replacement.

### Other checked results

- Hunter MacInnes, "Bandage Animation": https://www.youtube.com/watch?v=Kzg-VuqhLZs — description only says "Low Poly FPS Healing Anima."; no source download link.
- Sketchfab public animated-model searches for bandage, bandaging, healing animation and medkit animation did not yield a suitable downloadable wrist-wrapping motion in the returned results. Several results were static props or unrelated animations. This is limited search coverage, not proof that no free asset exists.
- Raisecreation "Healing Animation Pack": https://assetstore.unity.com/packages/3d/animations/healing-animation-pack-293747 — store page shows USD 10 and Standard Unity Asset Store EULA. The retrieved page did not establish that it contains the required first-person wrist wrap, so it was not selected.

## Implementation decision and remaining work

The follow-up source search confirmed actual FBX availability from nikoff and FBX/MotionBuilder source from Vanilla Loop (details below). The subsequent download request also obtained FDDA's original packed medical hand-motion data from its author's GitHub repository, with actual varying arm/wrist/finger samples. Prefer evaluating this source before reconstructing motion from video. Polydin remains a promising first-person pack, but its downloadable source format has not been verified. No retarget or visual approval of the acquired FDDA motions is complete. The FC4 recreation remains a visual reference if source conversion proves unavailable.

Gekko's hand asset uses a Generic rig, so an external skeleton is not a drop-in clip. Preserve its natural finger poses unless the source provides a verified compatible replacement. A motion asset also cannot by itself repair the exposed cropped arm mesh: remove the V7 folded sleeve extension and use a properly skinned continuation or authored framing that hides the proximal ends throughout the clip, including entry and exit at 16:9, 21:9 and 4:3.

Unity-Skills rechecked `Assets/FPS/Features/Characters/Content/Players/Gekko/Models/Hand.fbx`: `animationType=Generic`, `avatarSetup=NoAvatar`, `isHuman=false`. At the end of this research, the active authored scene was `Assets/FPS/Scenes/GameScene.unity`, `isDirty=false`; `isPlaying=false` and `isCompiling=false`. Only read-only Editor queries were used in this research.

Next validation must include a visually continuous wrap in one direction, implied bandage tension and a credible finishing gesture, contact clearance, elbow-driven motion, natural wrists/fingers, and no exposed arm ends. Then inspect native Unity frames at all three aspect ratios before delivering a preview. Current work is research only; no new animation, runtime or prefab change was made.

## Tool limitations

Jina search/reader returned HTTP 402; Firecrawl refused keyless access. Google, DuckDuckGo and Brave presented challenges. Yahoo yielded some usable search pages but other queries failed with HTTP 500. Publisher YouTube pages, Unreal forum posts and the public animation spreadsheet supplied the inspected evidence. Blender MCP was disconnected at localhost:9876; no Blender state was changed.

## Follow-up: real keyframes before video capture

User instruction: find a complete animation/keyframe source first; only then consider video capture. This follow-up researched source acquisition. It did not rebuild the heal clip or derive motion from video.

### Publisher-confirmed paid source

**nikoff — Survival Medical & Healing Animations — Mocap Pack**

- Exact publisher product page inspected: https://nikoff.cc/product/survival-medical-healing-animations
- The product-specific page explicitly says **UE5 Project**, **FBX Available**, **18 Animations**, and **From $19.99 on FAB**. This establishes a supplied animation format, unlike a demo-only result. No free healing sample/download is linked on the inspected page.
- Relevant clip names: `AS_Med_Bandage_Arm_In`, `AS_Med_Bandage_Arm_Hand_Out`, `AS_Med_Bandage_Arm_Teeth_Out`. The publisher page and earlier spreadsheet disagree about which finish uses teeth versus hands; use the actual source/demo when selecting, not the inconsistent description.
- Native publisher demo inspected at 2, 4 and 8 seconds: https://www.youtube.com/watch?v=8csizz4dZCo . The opening group includes a character holding a bent left forearm across the body and working with the right hand near the distal forearm/wrist. Separate finish variants bring the hands toward the mouth. This is a full-body demo, not a verified Gekko first-person preview.
- Strongest currently verified source-format candidate. Acquisition still requires buying the pack; no purchase was made and no FBX from it was downloaded or inspected. Retargeting, wrist clearance, the medkit entry/exit, and first-person framing remain unverified.

**Vanilla Loop — Survival Set**

- Publisher documentation inspected: https://vanillaloop.com/docs/survival-set
- Explicit source statement: “Source files: Included and downloadable from the Additional Files section on Fab.com. You get .fbx files for direct import plus MotionBuilder files for editing in any 3D software.” It also says every pack ships with raw FBX on Fab and after installing the Unity Asset Store pack.
- Public animation list read as CSV: https://docs.google.com/spreadsheets/d/1Ym0NYdJ-G-n5NRnYE7Em1pypiqcSwo1ukCekNcZv8zg/edit
- Relevant full clip names: `A_Med_Bandage_Use`, `A_Med_Bandage_Crouch_Use`. The other two medical clips are syringe use standing/crouching.
- 215 animations total; Epic skeleton with IK bones. Unity product page inspected: https://assetstore.unity.com/packages/3d/animations/survival-set-321472 . Listed price **$24.99 before taxes**, version 1.0, Standard Unity Asset Store EULA.
- Public “Demo Build” link opens compiled demonstration archives, not a source download. No paid source acquired. Its bandage action has not been visually verified for the requested wrist wrap.

### Public files actually downloaded and checked

Local files and inspection reports are in `SourceCandidates/`; provenance and hashes are in `SourceCandidates/README.md`.

1. **Afroman726 / bandageanimation (Vintage Story)** — actual keyframe JSON, downloaded from the author's GitHub repository. `bandageintro`: 21-frame span, 5 authored keyframes; `bandageloop` and `bandageloop-alternative`: 26-frame spans, 7 keyframes each. Channels include upper/lower arms and torso, but no wrist/finger joints. Block-character source; unsuitable as a direct human-hand replacement. Repository API reports no declared license. Not integrated.
2. **Alexandr Prasolov / BandageSpline.blend** — public author attachment from https://blenderartists.org/t/mesh-twitsts-when-armature-follows-curve/1548651 . Opened in a separate Blender background process with auto-execution disabled. One 34-bone chain deforms bandage meshes; no human hand rig. `ArmatureAction` has 340 channels and 680 key points at frames 1–56, but none of these channels vary; winding relies on the rig/constraints rather than usable human-arm motion keys. This is a bandage-strip rig, not a complete two-hand healing animation. Not integrated.
3. **HYDRAV / StandardAnimationFBX sample** — https://hydrav.itch.io/standardanimationfbx . Downloaded the official free `fbxanitest.zip` (3.4 MB) through the author's download flow. Archive contains death/hit, locomotion/jump FBX and mannequin. No heal/medkit/bandage clip in this sample. The “medkit coming soon” comment is not evidence of a supplied medkit animation.

### Other leads with no acquired source

- Gunslinger Bandage animation for STALKER Anomaly: https://www.moddb.com/mods/stalker-anomaly/addons/gunslinger-bandage-animation . Search results identify a downloadable first-person mod and OMF motion-related files in associated packs. ModDB presented a security verification page; no package was obtained. Source contents, conversion and reuse rights are unverified.
- SpadesAnimation, First Person Healing Animations Pack: https://www.fab.com/listings/f74fdc8d-e958-436d-9f24-6cd564e24c35 . Indexed description advertises seven healing animations. Fab security check prevented inspecting formats/downloads; do not describe this as acquired FBX.
- Ultimate Healing Animation Pack: https://www.cgtrader.com/3d-models/scripts-plugins/animation/ultimate-healing-animation-pack . Search snippets mention FBX/uasset, but the product page did not provide readable evidence this turn. No source acquired; not selected over verified publisher documentation.

### Result and Editor state

Complete authored medical animation sources exist for purchase, so capture is not the only available route. No suitable freely obtainable human wrist-wrap source was found in the inspected results; this is limited search coverage, not a claim that none exists. No capture-based reconstruction, animation edit, asset import, runtime or prefab modification was performed.

Unity-Skills read-only verification at the end of the source investigation: `GameScene`, `Assets/FPS/Scenes/GameScene.unity`, active and `isDirty=false`; Editor not playing or compiling. No operation requiring a Save Gate was performed. Approved injection remains untouched.

## Subsequent request: download available source

User authorized downloading available animation files. Obtained a focused subset from the author's repository https://github.com/FeelFried/FDDA-for-STALKER-Anomaly , pinned to commit `c012623f7affac2a0a9973f4e44c743b85794fcf`.

- Local acquisition notes: `SourceCandidates/FDDA/ACQUISITION.md`.
- Hand OMF: 117 motion chunks, 42-bone hand rig, actual changing upper-arm, elbow/forearm, wrist/hand, twist and finger samples. Bandage variants have 171 frames; medkit variants have 261 frames. Some motion names are duplicated; the inspection report preserves chunk IDs.
- Also downloaded matching bandage/medkit prop-motion OMF, basic prop OGF and item configuration files. Every downloaded file's original path, size and SHA-256 is recorded in `download-manifest.json`.
- `inspect_omf.py` passed its valid/truncated chunk self-check and checked all motion track spans to the end of every chunk. This confirms actual packed motion samples, not animation references or video.
- No matching hand rest skeleton/mesh is present in this focused subset. A subsequent faithful Blender/FBX conversion needs that skeleton, followed by Gekko retargeting and visual validation. Do not call the downloaded source ready for Unity, approved or known to wrap the requested wrist correctly.
- Vanilla Loop's newly found free sample pack was checked through its official public animation list. Its sole Survival Set sample is `A_Flashlight_Idle_01_R`; no bandage/medkit sample. Avoid downloading unrelated samples again.
- nikoff links only to buying the paid product; Fab and ModDB still returned security checks. No purchase or attempted security-check bypass was made.
- Only research files changed. Unity-Skills confirmed the active `GameScene` remains `isDirty=false`, not compiling; no animation/prefab/runtime edits and no capture-based reconstruction.

Next step is source conversion/visual inspection, not treating V7 as approved. FDDA repository reuse rights remain unverified.
