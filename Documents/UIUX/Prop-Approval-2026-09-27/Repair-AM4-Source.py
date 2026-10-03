"""Repair invalid source image names in Operator ammo FBX, preserving geometry."""
import bpy
import json
from pathlib import Path

stage=Path('E:/Unity/Project/FPS/Temp/ApprovedPropImport-20260930')
source=stage/'AM4/source/Sniper Ammo Box.fbx'
destination=stage/'Prepared/AM4'
data=json.loads((destination/'ImportData.json').read_text(encoding='utf-8'))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
for image in bpy.data.images:
    image.filepath=image.filepath.replace('\\','/').rsplit('/',1)[-1].lstrip('.')
for obj in bpy.context.scene.objects:
    obj.select_set(obj.type=='MESH')
assert sum(len(p.vertices)-2 for o in bpy.context.selected_objects for p in o.data.polygons)==6000
bpy.ops.export_scene.fbx(filepath=str(destination/'Models/AM4.fbx'),use_selection=True,
    object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
data['conversion']='Blender 5.1.1 FBX re-export; repaired invalid leading-dot texture references; original geometry retained'
(destination/'ImportData.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
