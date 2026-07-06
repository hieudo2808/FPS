# Body04Hand FPSHand AutoWeights Report

- Body04Hand source: `E:\Unity\Project\FPS\Assets\Prefabs\BodyGuards\Meshes\Body04Hand.fbx`
- FPSHand skeleton reference: `E:\Unity\Project\FPS\Assets\Art\Animations\AKAnimation\FPSHand.fbx`
- Export: `E:\Unity\Project\FPS\Assets\Generated\FPSHandBody04HandAutoWeights\Exports\Body04Hand_On_FPSHand_AutoWeights.fbx`

## Result
- Created mesh: `Body04Hand_On_FPSHand_CommonRig` verts=880 faces=918
- Created bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.22076, 0.4863, 1.78003], 'center': [0.02117, -0.00189, 1.35985], 'size': [0.39918, 0.97637, 0.84037]}`
- Vertex groups after auto bind: 250
- Vertices changed by normalize/top-4 cleanup: 879
- Removed low-weight influences: 576
- Max influences per vertex after cleanup: 4
- Vertices with >4 influences after cleanup: 0
- Zero-weight vertices after cleanup: 0
- Bad weight-sum vertices after cleanup: 0
- Mean influences after cleanup: 3.315
- Mean weight sum after cleanup: 1.000
- Pose sanity finite: True
- Pose sanity bounds: `{'min': [-0.17842, -0.49008, 0.93967], 'max': [0.226, 0.4863, 1.78003]}`

## Method
- Body04Hand rest geometry is preserved.
- Existing Body04Hand skin weights are discarded.
- Blender Automatic Weights binds the mesh to the FPSHand armature.
- Weights are pruned to top four influences and normalized.
- This mirrors the first automated pass a character modder would try before manual weight paint cleanup.