"""Prepare downloaded props only; source ZIPs and the Unity scene remain untouched."""
import bpy
import csv
import json
import re
import shutil
from pathlib import Path

stage = Path('E:/Unity/Project/FPS/Temp/ApprovedPropImport-20260930')
prepared = stage / 'Prepared'
inspection = json.loads((stage / 'Source-Inspection.json').read_text(encoding='utf-8'))
manifest = {r['assetCode'].replace('-', '_'): r for r in csv.DictReader((Path(__file__).parent / 'Provenance-Manifest.csv').open(encoding='utf-8-sig'))}
image_extensions = {'.png', '.jpg', '.jpeg', '.tga', '.tif', '.tiff'}

def key(name):
    return re.sub('[^a-z0-9]', '', Path(name.replace('\\', '/')).stem.lower())

def safe(name):
    return re.sub(r'[^\w.\-]', '_', name).strip('.') or 'Asset'

def upstream(socket, seen=None):
    seen = set() if seen is None else seen
    for link in socket.links:
        node = link.from_node
        if node.as_pointer() in seen:
            continue
        seen.add(node.as_pointer())
        if node.type == 'TEX_IMAGE' and node.image:
            return node.image, 0
        for child in node.inputs:
            found = upstream(child, seen)
            if found:
                image, channel = found
                if node.type in {'SEPARATE_COLOR', 'SEPRGB'}:
                    channel = {'Red': 0, 'Green': 1, 'Blue': 2, 'R': 0, 'G': 1, 'B': 2}.get(link.from_socket.name, channel)
                return image, channel
    return None

for entry in inspection:
    code = entry['code']
    source = Path(entry['source'])
    root = prepared / code
    (root / 'Models').mkdir(parents=True, exist_ok=True)
    (root / 'Textures').mkdir(exist_ok=True)
    files = [p for p in (stage / code).rglob('*') if p.suffix.lower() in image_extensions]
    issues = []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if source.suffix == '.blend':
        bpy.ops.wm.open_mainfile(filepath=str(source), use_scripts=False)
    elif source.suffix == '.glb':
        bpy.ops.import_scene.gltf(filepath=str(source))
    else:
        bpy.ops.import_scene.fbx(filepath=str(source))

    def copy_texture(path, filename=None):
        target = root / 'Textures' / (filename or path.name).lstrip('.')
        if target.exists() and target.read_bytes() != path.read_bytes():
            raise ValueError(f'Texture collision: {target}')
        if not target.exists():
            shutil.copy2(path, target)
        return 'Textures/' + target.name

    def texture_named(name):
        candidates = [p for p in files if key(p.name) == key(name)]
        if not candidates:
            issues.append('Missing source texture: ' + name)
            return None
        # Prefer source-pack texture over Sketchfab's re-encoded top-level copy.
        candidates.sort(key=lambda p: ('source' not in p.parts, len(str(p))))
        return copy_texture(candidates[0])

    def image_path(image):
        if image.packed_file:
            filename = safe(image.name) + '.png'
            target = root / 'Textures' / filename
            image.filepath_raw = str(target)
            image.file_format = 'PNG'
            image.save()
            return 'Textures/' + filename
        return texture_named(Path(image.filepath.replace('\\', '/')).name or image.name)

    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if code == 'L2_R':
        allowed = {'Computer', 'Keyboard', 'Monitor', 'Mouse', 'Mat', 'Loudspeaker01', 'Loudspeaker02'}
        objects = [o for o in objects if o.name in allowed]
        assert len(objects) == 7
    if code == 'AM5':
        atlas = bpy.data.materials.new('AmmoBoxAtlas')
        for obj in objects:
            obj.data.materials.clear()
            obj.data.materials.append(atlas)
    used_materials = {s.material for o in objects for s in o.material_slots if s.material}
    materials = []
    for mat in sorted(used_materials, key=lambda m: m.name):
        nodes = mat.node_tree.nodes if mat.node_tree else []
        bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
        color = list(bsdf.inputs['Base Color'].default_value) if bsdf else list(mat.diffuse_color)
        record = dict(name=mat.name, color=color, metallic=float(bsdf.inputs['Metallic'].default_value) if bsdf else float(mat.metallic),
            roughness=float(bsdf.inputs['Roughness'].default_value) if bsdf else float(mat.roughness),
            alpha=float(bsdf.inputs['Alpha'].default_value) if bsdf else 1.0, textures={}, channels={})
        if bsdf:
            for semantic, socket in [('albedo','Base Color'),('normal','Normal'),('metallic','Metallic'),('roughness','Roughness'),('emission','Emission Color')]:
                found = upstream(bsdf.inputs[socket])
                if found:
                    image, channel = found
                    path = image_path(image)
                    if path:
                        record['textures'][semantic] = path
                        record['channels'][semantic] = channel
            if bsdf.inputs['Transmission Weight'].default_value > 0:
                issues.append('Transmission material requires visual review: ' + mat.name)

        def set_texture(semantic, name):
            path = texture_named(name)
            if path:
                record['textures'][semantic] = path
                record['channels'][semantic] = 0

        if code == 'L2_R':
            for semantic, suffix in [('albedo','BaseColor'),('normal','Normal'),('roughness','Roughness'),('metallic','Metallic'),('ao','AO')]:
                name = mat.name + '_' + suffix + '.png'
                if any(key(p.name) == key(name) for p in files):
                    set_texture(semantic, name)
        elif code == 'G1':
            prefix = {'Sel_Red':'base','Sel_Green':'segurar','Sel_Yellow':'pipe','lambert1':'gatilho','Sel_Blue':'bomba'}[mat.name]
            for semantic,suffix in [('albedo','albedo'),('normal','normal'),('roughness','rough'),('metallic','metal'),('ao','ao')]:
                set_texture(semantic, prefix+'_'+suffix+'.png')
        elif code == 'K2':
            for semantic,suffix in [('albedo','Diff'),('normal','Normal'),('roughness','Roughness'),('metallic','Metallic')]:
                set_texture(semantic,'Card_'+suffix+'.png')
        elif code == 'LAB_C1':
            prefix = 'T_Utility_cart_metal_' if mat.name == 'metal' else 'T_Lab_shelf_metal_'
            for semantic,suffix in [('albedo','D'),('normal','N'),('roughness','R'),('metallic','M'),('ao','O')]:
                set_texture(semantic,prefix+suffix+'.png')
        elif code == 'R1':
            for semantic,name in [('albedo','radio_low_dark_BaseColor.png'),('normal','dark_Normal_DirectX.png'),('roughness','radio_low_dark_Roughness.png'),('metallic','radio_low_dark_Metallic.png'),('ao','dark_Mixed_AO.png'),('emission','radio_low_dark_Emissive.png')]:
                set_texture(semantic,name)
            record['flipNormalGreen'] = True
        elif code == 'AM5':
            prefix = '006_AmmunitionBox_FinalRenderLow_AmmoBoxTe'
            for semantic,suffix in [('albedo','_23F77C2DF8C1'),('normal',''),('metallic','_AF77D49E35CB'),('roughness','_F2C9EE199B0A')]:
                set_texture(semantic,prefix+suffix+'.png')
        elif code == 'AM1':
            maps = {'Box': ['9mm_Ammo_Col_Remake.jpg','9mm_ammo_box_REMAKE_Normal.jpg','9mm_ammo_box_REMAKE_Roughness.png'],
                    'bullet holder': ['Bullet_holder_Diffuse.png','Bullet_holder_normal.jpg','Bullet_holder_roughness.jpg'],
                    'ammo body': ['bullet_Diffuse.png','bullet_Normal.png','Bullet_Roughness.png'],
                    'bullet bottom': ['bullet_Diffuse.png','bullet_Normal.png','Bullet_Roughness.png']}
            for semantic,name in zip(['albedo','normal','roughness'],maps[mat.name]):
                set_texture(semantic,name)
            if mat.name in {'ammo body','bullet bottom'}:
                set_texture('metallic','Bullet_Metallic.png')
        elif code == 'AM4' and mat.name == 'bullets':
            set_texture('albedo','Bullet_diffuseOriginal.png')
        if record['textures'].get('albedo'):
            record['color'] = [1.0,1.0,1.0,1.0]
        materials.append(record)
    assert objects and materials, code
    output_model = root / 'Models' / (code + '.fbx')
    if source.suffix in {'.blend','.glb'} or code in {'L2_R','AM5'}:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.hide_set(False)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.export_scene.fbx(filepath=str(output_model), use_selection=True, object_types={'MESH'},
            bake_anim=False, add_leaf_bones=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    else:
        shutil.copy2(source, output_model)
    record = dict(code=code, source=source.relative_to(stage).as_posix(), model='Models/'+output_model.name,
        materials=materials, issues=sorted(set(issues)), sourceMeshCount=len(objects),
        sourceTriangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
        conversion='Blender 5.1.1 FBX export' if source.suffix != '.fbx' or code in {'L2_R','AM5'} else 'Original FBX bytes')
    (root/'ImportData.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
    print('PREPARED',code,len(materials),'materials',len(issues),'source issues',flush=True)

# Unity's ModelImporter supports Collada directly; no new converter dependency required.
root = prepared/'G2'
(root/'Models').mkdir(parents=True,exist_ok=True)
(root/'Textures').mkdir(exist_ok=True)
source = stage/'G2/source/model.zip.unpacked/model'
shutil.copy2(source/'model.dae',root/'Models/G2.dae')
textures = {}
for semantic,name in [('albedo','lambert1_albedo.jpg'),('normal','lambert1_normal.png'),('metallic','lambert1_metallic.jpg'),('roughness','lambert1_roughness.jpg'),('ao','lambert1_AO.jpg')]:
    shutil.copy2(source/'textures'/name,root/'Textures'/name)
    textures[semantic]='Textures/'+name
(root/'ImportData.json').write_text(json.dumps(dict(code='G2',model='Models/G2.dae',materials=[dict(name='lambert1',color=[1,1,1,1],metallic=0,roughness=.5,alpha=1,textures=textures,channels={})],issues=[],conversion='Original DAE bytes'),indent=2),encoding='utf-8')

for folder in prepared.iterdir():
    row=manifest[folder.name]
    (folder/'SOURCE.md').write_text(f"# {row['title']}\n\nAuthor: {row['author']}\n\nSource: {row['sourceUrl']}\n\nLicense: {row['license']} — {row['licenseUrl']}\n\n{row['creditRequirement']}\n\nArchive SHA-256: `{row['archiveSha256']}`\n\nApproval: {row['approvalStatus']}. Import does not approve scene placement.\n\nChanges: format conversion where required; texture/material rebinding for Unity Built-in. L2_R contains computer parts only. No source shape modeled or replaced. Original archive is retained in Downloads.\n",encoding='utf-8')
assert len(list(prepared.glob('*/ImportData.json'))) == 17
print('PASS: 17 prepared packages; original ZIPs untouched',flush=True)
