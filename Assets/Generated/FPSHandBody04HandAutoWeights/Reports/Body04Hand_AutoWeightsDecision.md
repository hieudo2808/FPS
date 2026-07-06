# Body04Hand AutoWeights Decision

- Recommended next Unity test candidate: `Assets/Generated/FPSHandBody04HandAutoWeights/Exports/Body04Hand_On_FPSHand_AutoWeights.fbx`
- Source mesh: `Assets/Prefabs/BodyGuards/Meshes/Body04Hand.fbx`
- Skeleton/animation contract: `Assets/Art/Animations/AKAnimation/FPSHand.fbx`
- Verification clip used for the right-hand failure: `Assets/Art/Animations/AKAnimation/FP_Vandal.fbx`

## Why This Candidate Exists

`Body04Hand_On_FPSHand_CommonRig.fbx` preserved the source shape and source weights, but the right hand still deformed into a pointed shape under FPSHand animation. The root cause is source skinning: several right-hand faces bridge separate finger regions, such as index/middle or thumb/index, so the mesh stretches when the FPSHand animation curls those fingers independently.

## Candidate Comparison

- `CommonRig`: preserves shape and materials best, but keeps the bad source weights that break the right hand.
- `CommonRigNoFingerBridges`: removes bridge faces and reduces spike metrics, but creates visible gaps/holes between fingers.
- `AutoWeights`: preserves topology and rebakes weights against the FPSHand armature. It currently gives the cleanest automated right-hand preview, with no long right-hand spike in the rendered Vandal frame.

## Verification Snapshot

- Blender Automatic Weights export succeeded.
- Max influences per vertex after cleanup: 4.
- Bad weight-sum vertices after cleanup: 0.
- Zero-weight vertices after cleanup: 0.
- Right-hand Vandal preview:
  - `Reports/SidePreviews/FP_VandalHandOnly/Body04Hand_On_FPSHand_AutoWeights.fbx_R_frame_028.png`

## Remaining Risk

Automatic weights may assign broader arm/sleeve influences than a manual artist pass would. If Unity still shows local artifacts, the next correct step is not mesh warping; it is manual or semi-automatic weight paint cleanup around the right thumb/index/middle webbing.
