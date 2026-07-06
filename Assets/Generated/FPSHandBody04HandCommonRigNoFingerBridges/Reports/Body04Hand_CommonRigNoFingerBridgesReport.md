# Body04Hand FPSHand CommonRig NoFingerBridges Report

- Body04Hand source: `E:\Unity\Project\FPS\Assets\Prefabs\BodyGuards\Meshes\Body04Hand.fbx`
- FPSHand skeleton reference: `E:\Unity\Project\FPS\Assets\Art\Animations\AKAnimation\FPSHand.fbx`
- Export: `E:\Unity\Project\FPS\Assets\Generated\FPSHandBody04HandCommonRigNoFingerBridges\Exports\Body04Hand_On_FPSHand_CommonRig_NoFingerBridges.fbx`

## Result
- Source mesh: `BodyGuard04Hand` verts=880 faces=918
- Created mesh: `Body04Hand_On_FPSHand_CommonRig` verts=862 faces=774
- Created bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.22076, 0.4863, 1.78003], 'center': [0.02117, -0.00189, 1.35985], 'size': [0.39918, 0.97637, 0.84037]}`
- Removed bridge faces: 144
- Removed bridge faces by side: `{'L': 66, 'R': 78}`
- Vertices changed by normalize/top-4 cleanup: 137
- Removed low-weight influences: 168
- Max influences per vertex after cleanup: 4
- Vertices with >4 influences after cleanup: 0
- Bad weight-sum vertices after cleanup: 0
- Pose sanity finite: True
- Pose sanity bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.22707, 0.4863, 1.78003]}`

## Removed Face Examples
| Face | Finger totals |
|---:|---|
| 367 | `[('L_Index', 2.201), ('L_Middle', 0.638), ('L_Ring', 0.03), ('L_Thumb', 0.009)]` |
| 374 | `[('L_Middle', 1.215), ('L_Ring', 0.91), ('L_Index', 0.281), ('L_Pinky', 0.218)]` |
| 377 | `[('L_Middle', 2.443), ('L_Ring', 0.512), ('L_Index', 0.016)]` |
| 403 | `[('L_Middle', 2.287), ('L_Index', 0.577), ('L_Ring', 0.066)]` |
| 404 | `[('L_Middle', 1.835), ('L_Index', 0.957), ('L_Ring', 0.034), ('L_Thumb', 0.005)]` |
| 408 | `[('L_Index', 1.797), ('L_Thumb', 0.73)]` |
| 409 | `[('L_Index', 1.406), ('L_Thumb', 1.215)]` |
| 419 | `[('L_Middle', 2.145), ('L_Index', 1.443), ('L_Ring', 0.212), ('L_Thumb', 0.009)]` |
| 420 | `[('L_Middle', 2.31), ('L_Index', 1.623), ('L_Ring', 0.032)]` |
| 421 | `[('L_Middle', 1.557), ('L_Index', 1.432), ('L_Ring', 0.437), ('L_Thumb', 0.145)]` |
| 422 | `[('L_Ring', 1.816), ('L_Middle', 1.537), ('L_Pinky', 0.25), ('L_Index', 0.141)]` |
| 423 | `[('L_Index', 3.164), ('L_Thumb', 0.541), ('L_Middle', 0.005)]` |
| 424 | `[('L_Ring', 2.328), ('L_Middle', 1.587), ('L_Pinky', 0.032), ('L_Index', 0.009)]` |
| 425 | `[('L_Pinky', 1.982), ('L_Ring', 1.567), ('L_Middle', 0.146), ('L_Index', 0.003)]` |
| 426 | `[('L_Pinky', 2.138), ('L_Ring', 1.805), ('L_Middle', 0.006)]` |
| 428 | `[('L_Middle', 3.212), ('L_Index', 0.788)]` |

## Method
- Body04Hand geometry and source weights are preserved except for faces that bridge two or more finger regions.
- This targets the spike root cause seen on the right hand: triangles weighted to separate fingers stretch when FPSHand clips curl those fingers independently.
- No finger envelope warp, no full-face rigid split, and no original FBX overwrite.