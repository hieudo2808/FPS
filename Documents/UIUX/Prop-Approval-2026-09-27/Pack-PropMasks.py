"""Convert source PBR channels to Unity Standard metallic(R)/smoothness(A)."""
import bpy
import json
import re
import sys
import numpy as np
from pathlib import Path

stage = Path(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else Path('E:/Unity/Project/FPS/Temp/ApprovedPropImport-20260930/Prepared')

def channel(path, index, size):
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        image.colorspace_settings.name = 'Non-Color'
        if tuple(image.size) != size:
            image.scale(*size)
        data = np.empty(size[0]*size[1]*4, dtype=np.float32)
        image.pixels.foreach_get(data)
        return data.reshape(size[1],size[0],4)[:,:,index].copy()
    finally:
        bpy.data.images.remove(image)

# Runnable check for the channel contract; never invert metallic or gamma-convert masks.
assert float(1.0 - np.array([0.25], dtype=np.float32)[0]) == 0.75
for path in sorted(stage.glob('*/ImportData.json')):
    record = json.loads(path.read_text(encoding='utf-8'))
    for mat in record['materials']:
        tex=mat['textures']
        if 'opacity' in tex and 'albedo' in tex:
            image=bpy.data.images.load(str(path.parent/tex['albedo']),check_existing=False)
            size=tuple(min(2048,int(v)) for v in image.size)
            image.scale(*size)
            rgba=np.empty(size[0]*size[1]*4,dtype=np.float32)
            image.pixels.foreach_get(rgba)
            rgba=rgba.reshape(size[1],size[0],4)
            rgba[:,:,3]=channel(path.parent/tex['opacity'],0,size)
            bpy.data.images.remove(image)
            image=bpy.data.images.new('DiffuseOpacity',width=size[0],height=size[1],alpha=True)
            image.pixels.foreach_set(rgba.ravel())
            name=mat['name']+'_DiffuseOpacity.png'
            image.filepath_raw=str(path.parent/'Textures'/name)
            image.file_format='PNG'
            image.save()
            bpy.data.images.remove(image)
            mat['packedAlbedo']='Textures/'+name
        if not ('roughness' in tex or 'metallic' in tex):
            continue
        chosen=path.parent/tex.get('roughness',tex.get('metallic'))
        image=bpy.data.images.load(str(chosen),check_existing=False)
        size=tuple(min(2048,int(v)) for v in image.size)
        bpy.data.images.remove(image)
        assert min(size)>0, chosen
        rgba=np.zeros((size[1],size[0],4),dtype=np.float32)
        metal=channel(path.parent/tex['metallic'],mat['channels'].get('metallic',0),size) if 'metallic' in tex else mat['metallic']
        rough=channel(path.parent/tex['roughness'],mat['channels'].get('roughness',0),size) if 'roughness' in tex else mat['roughness']
        rgba[:,:,0]=metal
        rgba[:,:,3]=1.0-rough
        image=bpy.data.images.new('UnityMask',width=size[0],height=size[1],alpha=True)
        image.colorspace_settings.name='Non-Color'
        image.pixels.foreach_set(np.clip(rgba,0,1).ravel())
        name=re.sub(r'[^\w.\-]','_',mat['name']).strip('.')+'_MetallicSmoothness.png'
        target=path.parent/'Textures'/name
        image.filepath_raw=str(target)
        image.file_format='PNG'
        image.save()
        bpy.data.images.remove(image)
        mat['packedMask']='Textures/'+name
    path.write_text(json.dumps(record,indent=2),encoding='utf-8')
    print('PACKED',record['code'],flush=True)
