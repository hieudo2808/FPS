"""Produce a measured derivative without replacing the source; defaults retain the K1 workflow."""
import argparse
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector

project = Path('E:/Unity/Project/FPS')
stage = project / 'Temp/ApprovedPropImport-20260930'
parser = argparse.ArgumentParser()
parser.add_argument('--source', type=Path, default=stage / 'K1/source/fuse.blend')
parser.add_argument('--output', type=Path, default=stage / 'K1_LOD.fbx')
parser.add_argument('--report', type=Path, default=project / 'Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-09-30/K1-Optimization.json')
parser.add_argument('--ratio', type=float, default=.045)
parser.add_argument('--max-triangles', type=int, default=6500)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
assert 0 < args.ratio <= 1 and args.source.is_file()
assert args.source.resolve() != args.output.resolve(), 'Never overwrite original source'
if args.source.suffix.lower() == '.blend':
    bpy.ops.wm.open_mainfile(filepath=str(args.source), use_scripts=False)
elif args.source.suffix.lower() == '.fbx':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.source))
else:
    raise ValueError('Expected verified BLEND or FBX source')
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert objects

def measure():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points, triangles = [], 0
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        points.extend(evaluated.matrix_world @ v.co for v in mesh.vertices)
        evaluated.to_mesh_clear()
    lo = Vector([min(v[i] for v in points) for i in range(3)])
    hi = Vector([max(v[i] for v in points) for i in range(3)])
    return triangles, lo, hi

before, lo, hi = measure()
for obj in objects:
    decimate = obj.modifiers.new('Campaign_LOD', 'DECIMATE')
    decimate.ratio = args.ratio
    decimate.use_collapse_triangulate = True
after, new_lo, new_hi = measure()
error = max((lo - new_lo).length, (hi - new_hi).length) / (hi - lo).length
assert 0 < after < args.max_triangles, after
assert error < .005, error
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = objects[0]
destination = args.output
bpy.ops.export_scene.fbx(filepath=str(destination), use_selection=True, object_types={'MESH'},
                         bake_anim=False, add_leaf_bones=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
report = {'source_triangles': before, 'derivative_triangles': after, 'relative_bounds_error': error,
          'source': str(args.source), 'output': str(destination),
          'method': f'Blender evaluated decimation, ratio {args.ratio}; original retained',
          'visual_approval': 'Requires render comparison; bounds test alone is not visual approval'}
args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
