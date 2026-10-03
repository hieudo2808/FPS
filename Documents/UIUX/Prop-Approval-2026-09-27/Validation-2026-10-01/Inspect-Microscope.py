"""Read the existing sourced FBX in a disposable background Blender process."""
import bpy
import json

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/ExperimentFacility/Models/LabEquipment/OmaxMicroscope.fbx')
print('MICROSCOPE_SOURCE', json.dumps([
    dict(name=o.name, vertices=len(o.data.vertices), polygons=len(o.data.polygons),
         dimensions=list(o.dimensions))
    for o in bpy.context.scene.objects if o.type == 'MESH'
]))
