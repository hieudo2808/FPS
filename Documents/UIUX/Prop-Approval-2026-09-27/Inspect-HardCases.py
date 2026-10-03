import bpy

for obj in bpy.data.objects:
    print('CASE_OBJECT', obj.name, obj.type, 'location', tuple(obj.location), 'dimensions', tuple(obj.dimensions),
          'materials', [m.name for m in obj.data.materials] if obj.type == 'MESH' else [],
          'asset', obj.asset_data is not None)
for material in bpy.data.materials:
    print('CASE_MATERIAL', material.name, 'nodes', [(n.name, n.type) for n in material.node_tree.nodes] if material.use_nodes else [])
print('CASE_LIBRARIES', [lib.filepath for lib in bpy.data.libraries])
