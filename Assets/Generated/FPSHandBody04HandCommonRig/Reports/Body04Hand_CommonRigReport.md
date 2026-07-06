# Body04Hand FPSHand CommonRig Report

- Body04Hand source: `E:\Unity\Project\FPS\Assets\Prefabs\BodyGuards\Meshes\Body04Hand.fbx`
- FPSHand skeleton reference: `E:\Unity\Project\FPS\Assets\Art\Animations\AKAnimation\FPSHand.fbx`
- Export: `E:\Unity\Project\FPS\Assets\Generated\FPSHandBody04HandCommonRig\Exports\Body04Hand_On_FPSHand_CommonRig.fbx`

## Inputs
- Source armature: `FP_Core_NewFemale_Skelmesh.ao.001` bones=316
- Target armature: `FP_Core_NewFemale_Skelmesh.ao` bones=250
- Source mesh: `BodyGuard04Hand` verts=880 groups=93
- Source bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.22076, 0.4863, 1.78003], 'center': [0.02117, -0.00189, 1.35985], 'size': [0.39918, 0.97637, 0.84036]}`
- Weighted groups: 93
- Weighted groups missing on source armature: 0
- Weighted groups missing on FPSHand armature: 0

## Rest Skeleton Comparison
- Compared weighted shared bones: 93
- Max head delta: 0.000001
- Max tail delta: 0.000001
- Max length ratio error: 0.000015

| Bone | Head Delta | Tail Delta | Length Ratio |
|---|---:|---:|---:|
| `R_Wrist_Inner` | 0.000001 | 0.000001 | 0.999985 |
| `L_Thumb_Knuckle` | 0.000000 | 0.000000 | 0.999990 |
| `R_Thumb_Knuckle` | 0.000001 | 0.000000 | 0.999991 |
| `L_Pinky2` | 0.000000 | 0.000000 | 1.000008 |
| `L_Thenar` | 0.000000 | 0.000000 | 1.000007 |
| `R_Pinky1` | 0.000001 | 0.000000 | 1.000007 |
| `R_Ring3` | 0.000000 | 0.000001 | 0.999994 |
| `R_Twist2` | 0.000000 | 0.000001 | 0.999994 |
| `R_Thumb_Outerpad` | 0.000001 | 0.000000 | 1.000006 |
| `L_Wrist_Inner` | 0.000000 | 0.000000 | 1.000005 |
| `R_Middle3` | 0.000000 | 0.000000 | 1.000005 |
| `L_Middle3` | 0.000000 | 0.000000 | 0.999995 |

## Source Weight Stats
- Max influences per vertex: 8
- Vertices with >4 influences: 137
- Zero-weight vertices: 0
- Bad weight-sum vertices: 0
- Mean influences: 2.868
- Mean weight sum: 1.000

## Result
- Created mesh: `Body04Hand_On_FPSHand_CommonRig`
- Created bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.22076, 0.4863, 1.78003], 'center': [0.02117, -0.00189, 1.35985], 'size': [0.39918, 0.97637, 0.84037]}`
- Removed non-target vertex groups: 0
- Vertices changed by normalize/top-4 cleanup: 137
- Removed low-weight influences: 168
- Largest removed influence: 0.099354
- Max influences per vertex after cleanup: 4
- Vertices with >4 influences after cleanup: 0
- Bad weight-sum vertices after cleanup: 0
- Mean influences after cleanup: 2.677
- Mean weight sum after cleanup: 1.000
- Pose sanity finite: True
- Pose sanity bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.22707, 0.4863, 1.78003]}`

## Method
- `FPSHand.fbx` is used as the skeleton/rest/bone-name contract.
- `FP_Vandal.fbx` and `FP_Classic.fbx` are not source meshes; they are animation clips used only for deformation verification.
- Body04Hand geometry is preserved in visible world space before binding, then represented in FPSHand armature local space.
- Body04Hand vertex groups/weights are preserved where bone names match the FPSHand armature.
- Weights are pruned to the strongest four influences and normalized; no nearest-neighbor weight transfer and no face deletion are used.
- Original FBX files are copied into `SourceCopies`; no source asset is overwritten.