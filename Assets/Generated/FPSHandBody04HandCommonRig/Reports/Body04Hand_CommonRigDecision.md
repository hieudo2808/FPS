# Body04Hand CommonRig Decision

- Recommended candidate: `Assets/Generated/FPSHandBody04HandCommonRig/Exports/Body04Hand_On_FPSHand_CommonRig.fbx`
- Source mesh: `Assets/Prefabs/BodyGuards/Meshes/Body04Hand.fbx`
- Skeleton/animation contract: `Assets/Art/Animations/AKAnimation/FPSHand.fbx`
- Verification clips: `Assets/Art/Animations/AKAnimation/FP_Vandal.fbx`, `Assets/Art/Animations/AKAnimation/FP_Classic.fbx`

## Why This Candidate

- It preserves the Body04Hand rest shape and does not warp fingers into the FPSHand mesh envelope.
- It binds the preserved Body04Hand mesh to the FPSHand armature/bone-name contract.
- Source weighted bone names match the FPSHand target armature.
- Shared rest bones are effectively identical in the measured weighted set.
- Weight cleanup limits each vertex to four influences and normalizes sums.

## Rejected Candidate

`Assets/Generated/FPSHandBody04HandCommonRigRGS/Exports/Body04Hand_On_FPSHand_CommonRig_RGS.fbx` is not recommended as the final hand. It reduces some cross-finger spike metrics, but it introduces visible seam-like breakup because it splits faces and binds cross-finger surfaces rigidly to palm/hand regions.

## Important Finding

The remaining deformation risk is not caused by reusing the FPSHand skeleton. The original `Body04Hand.fbx` already has many cross-finger weighted faces under the same animation clips. This means any fully automated retarget must either accept some source skinning artifacts, do a careful manual weight paint/topology cleanup pass, or use a more advanced tool that preserves topology while solving finger-region weights.

## Preview Evidence

- `Reports/Previews/FP_Vandal/Body04Hand_On_FPSHand_CommonRig.fbx_frame_019.png`
- `Reports/Previews/FP_Vandal/Body04Hand_On_FPSHand_CommonRig.fbx_frame_028.png`
- `Reports/Previews/FP_Classic/Body04Hand_On_FPSHand_CommonRig.fbx_frame_024.png`
- `Reports/Previews/FP_Classic/Body04Hand_On_FPSHand_CommonRig.fbx_frame_030.png`
