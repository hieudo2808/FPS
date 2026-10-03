"""Convert approved RPaciorek assets, retaining authored geometry/material values."""
import bpy
import json
from pathlib import Path
from mathutils import Matrix, Vector

destination = Path('E:/Unity/Project/FPS/Assets/ThirdParty/ApprovedProps/RPaciorekHardCases')
destination.mkdir(parents=True, exist_ok=True)
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        for bone in obj.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()

for filename, names, width in [
    ('EvidenceCase', ['hard_case_2'], .62),
    ('EquipmentCase', ['aluminium_case_1', 'aluminium_case_1_handle', 'aluminium_case_1_locks'], .65),
]:
    converted = []
    for name in names:
        source = bpy.data.objects[name]
        evaluated = source.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
        mesh.transform(source.matrix_world)
        obj = bpy.data.objects.new(filename + '_' + name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        converted.append(obj)
    vertices = [v.co.copy() for obj in converted for v in obj.data.vertices]
    minimum = Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
    maximum = Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
    factor = width / max(maximum - minimum)
    center = Vector(((minimum.x + maximum.x) / 2, (minimum.y + maximum.y) / 2, minimum.z))
    for obj in converted:
        for vertex in obj.data.vertices:
            vertex.co = (vertex.co - center) * factor
        obj.select_set(True)
    for obj in bpy.context.selected_objects:
        obj.select_set(obj in converted)
    bpy.context.view_layer.objects.active = converted[0]
    bpy.ops.export_scene.fbx(filepath=str(destination / (filename + '.fbx')), use_selection=True,
        object_types={'MESH'}, bake_anim=False, axis_forward='-Z', axis_up='Y', add_leaf_bones=False)
    print('EXPORTED_CASE', filename, 'size_m', tuple((maximum - minimum) * factor),
          'triangles', sum(len(p.vertices) - 2 for obj in converted for p in obj.data.polygons))
    for obj in converted:
        bpy.data.objects.remove(obj, do_unlink=True)

materials = []
for material in bpy.data.materials:
    bsdf = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if material.node_tree else None
    if bsdf:
        materials.append(dict(name=material.name, color=list(bsdf.inputs['Base Color'].default_value),
            metallic=bsdf.inputs['Metallic'].default_value, roughness=bsdf.inputs['Roughness'].default_value))
(destination / 'AuthoredMaterials.json').write_text(json.dumps(dict(materials=materials), indent=2))
