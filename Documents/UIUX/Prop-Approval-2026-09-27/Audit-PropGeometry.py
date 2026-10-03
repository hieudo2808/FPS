"""Compare source and prepared geometry; no asset modifications or downloaded scripts."""
import json
from pathlib import Path
import bpy

project = Path('E:/Unity/Project/FPS')
stage = project / 'Temp/ApprovedPropImport-20260930'
results = []
for code in ('AM2', 'AM5', 'LAB_C2', 'LAB_C3', 'LAB_C4'):
    package = project / 'Assets/ThirdParty/ApprovedProps' / code
    data = json.loads((package / 'ImportData.json').read_text(encoding='utf-8'))
    record = {'code': code}
    for label, path in [('original', stage / data['source']), ('prepared', package / data['model'])]:
        assert path.is_file(), path
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if path.suffix == '.blend':
            bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
        elif path.suffix == '.glb':
            bpy.ops.import_scene.gltf(filepath=str(path))
        else:
            bpy.ops.import_scene.fbx(filepath=str(path))
        objects = []
        for obj in bpy.context.scene.objects:
            if obj.type != 'MESH':
                continue
            mesh = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
            mesh.calc_loop_triangles()
            degenerate = sum(t.area < 1e-12 for t in mesh.loop_triangles)
            objects.append({'name': obj.name, 'vertices': len(mesh.vertices), 'triangles': len(mesh.loop_triangles),
                            'degenerate': degenerate, 'hidden': obj.hide_get(), 'selectable': not obj.hide_select})
            obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
        record[label] = objects
    results.append(record)
destination = project / 'Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-09-30/Geometry-Fidelity.json'
destination.write_text(json.dumps(results, indent=2), encoding='utf-8')
for row in results:
    print(row['code'], {k: {'meshes': len(row[k]), 'tris': sum(x['triangles'] for x in row[k]),
                           'zero_area': sum(x['degenerate'] for x in row[k]),
                           'empty': sum(not x['triangles'] for x in row[k])} for k in ('original', 'prepared')})
