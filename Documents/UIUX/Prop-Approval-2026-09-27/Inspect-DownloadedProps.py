"""Read imported source geometry/materials in Blender without executing source scripts."""
import bpy
import json
from pathlib import Path

stage = Path('E:/Unity/Project/FPS/Temp/ApprovedPropImport-20260930')
results = []
for folder in sorted(stage.iterdir()):
    if not folder.is_dir() or folder.name == 'Prepared':
        continue
    models = [p for p in folder.rglob('*') if p.suffix.lower() in {'.fbx', '.blend', '.glb'}]
    if not models:
        continue
    if folder.name == 'LAB_C1':
        models = [p for p in models if p.name == 'SF_LabShelf_and_utilityCart_NakedSingularity.fbx']
    assert len(models) == 1, (folder.name, models)
    model = models[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if model.suffix.lower() == '.blend':
        bpy.ops.wm.open_mainfile(filepath=str(model), use_scripts=False)
    elif model.suffix.lower() == '.glb':
        bpy.ops.import_scene.gltf(filepath=str(model))
    else:
        bpy.ops.import_scene.fbx(filepath=str(model))
    objects = []
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            objects.append(dict(name=obj.name, triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),
                dimensions=list(obj.dimensions), materials=[s.material.name if s.material else None for s in obj.material_slots]))
    materials = []
    for mat in bpy.data.materials:
        nodes = mat.node_tree.nodes if mat.node_tree else []
        bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
        materials.append(dict(name=mat.name,
            color=list(bsdf.inputs['Base Color'].default_value) if bsdf else list(mat.diffuse_color),
            metallic=bsdf.inputs['Metallic'].default_value if bsdf else mat.metallic,
            roughness=bsdf.inputs['Roughness'].default_value if bsdf else mat.roughness,
            images=[dict(name=n.image.name, path=n.image.filepath, packed=bool(n.image.packed_file),
                links=[l.to_socket.name for s in n.outputs for l in s.links]) for n in nodes if n.type == 'TEX_IMAGE' and n.image]))
    results.append(dict(code=folder.name, source=str(model), objects=objects, materials=materials))
    print('INSPECTED', folder.name, len(objects), sum(o['triangles'] for o in objects), flush=True)
(stage / 'Source-Inspection.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
