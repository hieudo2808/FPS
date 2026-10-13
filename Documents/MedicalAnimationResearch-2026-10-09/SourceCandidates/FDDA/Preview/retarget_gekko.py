"""Retarget both original FDDA actions to Gekko; mirror treatment to the left arm."""
import json
import math
from pathlib import Path
import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parent
OUT = ROOT.parents[2] / 'Retarget-FDDA-v1'
OUT.mkdir(exist_ok=True)
MODEL = ROOT.parents[4] / 'Assets/FPS/Features/Characters/Content/Players/Gekko/Models/Hand.fbx'
report = json.loads((ROOT / 'preview-verification.json').read_text(encoding='utf-8'))
source = bpy.data.objects['hand_armature']
scene = bpy.context.scene
source_names = json.loads((ROOT.parent / 'motion-inspection.json').read_text(encoding='utf-8'))[0]['bones']
source_names = list(source_names.values()) if isinstance(source_names, dict) else source_names
source_rest = {n: source.data.bones[n].matrix_local.copy() for n in source_names}
samples = {}
for info in report['motions']:
    label = info['label']
    action = bpy.data.actions[f"FDDA_{label}_Original_Chunk{info['chunk_id']}"]
    source.animation_data.action = action
    source.animation_data.action_slot = action.slots[0]
    samples[label] = []
    for frame in range(info['frames']):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[label].append({n: source.pose.bones[n].matrix.copy() for n in source_names})

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(MODEL))
imported = set(bpy.data.objects) - before
arm = next(o for o in imported if o.type == 'ARMATURE')
meshes = [o for o in imported if o.type == 'MESH']
assert len(meshes) == 2
for obj in list(bpy.data.objects):
    if obj not in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
arm.animation_data_clear()
for obj in meshes:
    obj.hide_set(True)
    for modifier in obj.modifiers:
        modifier.show_viewport = False
for bone in arm.pose.bones:
    bone.rotation_mode = 'QUATERNION'
    for constraint in list(bone.constraints):
        bone.constraints.remove(constraint)
    bone.matrix_basis = Matrix.Identity(4)
rest = {b.name: b.matrix_local.copy() for b in arm.data.bones}
bpy.context.view_layer.update()


# Source +Y is forward; Gekko +X is forward. Reflection swaps left/right.
convert = Matrix(((0, 1, 0), (1, 0, 0), (0, 0, 1)))
placement = Matrix.Translation(Vector((.11, -.09, -1.69))) @ Matrix.Rotation(math.radians(2), 4, 'Y')
OFFSET = placement.inverted().translation
source_camera_alignment = Matrix.Rotation(math.radians(-12), 3, 'Y')
finger_names = ('Index', 'Middle', 'Ring', 'Pinky', 'Thumb')
source_fingers = {'Index': 'finger1', 'Middle': 'finger2', 'Ring': 'finger3', 'Pinky': 'finger4', 'Thumb': 'finger0'}

# X-Ray's second phalanx is finger11/21/31/41, and the third is finger12/22/32/42.
def finger_name(side, finger, joint):
    number = source_fingers[finger][-1]
    if finger == 'Thumb':
        return side + '_finger0' + ('', '1', '2')[joint - 1]
    return 'bip01_' + side + '_finger' + number + ('', '1', '2')[joint - 1]

def frame_matrix(direction, normal):
    direction = direction.normalized()
    normal = normal - direction * normal.dot(direction)
    assert normal.length > 0.00001
    normal.normalize()
    return Matrix((direction, normal, direction.cross(normal))).transposed()

def source_palm(matrices, side, mirror=True):
    wrist = matrices[side + '_hand'].translation
    roots = [matrices[finger_name(side, f, 1)].translation for f in finger_names[:4]]
    forward = sum((p - wrist for p in roots), Vector()) / 4
    thumbward = roots[0] - roots[3]
    return frame_matrix(convert @ forward, convert @ thumbward) if mirror else frame_matrix(forward, thumbward)

def target_palm(side):
    wrist = rest[side + '_Hand'].translation
    roots = [rest[side + '_' + f + '1'].translation for f in finger_names[:4]]
    return frame_matrix(sum((p - wrist for p in roots), Vector()) / 4, roots[0] - roots[3])

source_palm_rest = {s: source_palm(source_rest, s, False) for s in ('l', 'r')}
target_palm_rest = {s: target_palm(s) for s in ('L', 'R')}
finger_frames = {}
for target_side in ('L', 'R'):
  for source_side in ('l', 'r'):
    for finger in finger_names:
        for joint in (1, 2, 3):
            sn = finger_name(source_side, finger, joint)
            tn = target_side + '_' + finger + str(joint)
            assert sn in source_rest and tn in rest, (sn, tn)
            if joint < 3:
                sd = source_rest[finger_name(source_side, finger, joint + 1)].translation - source_rest[sn].translation
                td = rest[target_side + '_' + finger + str(joint + 1)].translation - rest[tn].translation
            else:
                sd = source_rest[sn].translation - source_rest[finger_name(source_side, finger, 2)].translation
                td = rest[tn].translation - rest[target_side + '_' + finger + '2'].translation
            normal = source_palm_rest[source_side].col[2].copy()
            tf = frame_matrix(td, target_palm_rest[target_side].col[2])
            finger_frames[(target_side, source_side, tn)] = (sn, sd, normal, tf.transposed() @ rest[tn].to_3x3())

def set_pose(name, position, rotation):
    arm.pose.bones[name].matrix = Matrix.LocRotScale(position, rotation, Vector((1, 1, 1)))
    bpy.context.view_layer.update()

def forearm_twist(side, rotation):
    lower, hand = arm.pose.bones[side + '_Elbow'], arm.pose.bones[side + '_Hand']
    axis = (hand.head - lower.head).normalized()
    neutral = lower.matrix @ rest[lower.name].inverted() @ rest[hand.name]
    delta = rotation @ neutral.to_quaternion().inverted()
    if delta.w < 0:
        delta.negate()
    v = axis * Vector((delta.x, delta.y, delta.z)).dot(axis)
    twist = Quaternion((delta.w, v.x, v.y, v.z)).normalized()
    direction = rest[hand.name].translation - rest[lower.name].translation
    for suffix in ('Twist1', 'Twist2', 'HandTwist'):
        bone = arm.pose.bones[side + '_' + suffix]
        baseline = lower.matrix @ rest[lower.name].inverted() @ rest[bone.name]
        weight = (rest[bone.name].translation - rest[lower.name].translation).dot(direction) / direction.length_squared
        turn = Quaternion().slerp(twist, max(0, min(1, weight)))
        set_pose(bone.name, lower.head + turn @ (baseline.translation - lower.head), turn @ baseline.to_quaternion())

actions = {}
verification = {'source': str(MODEL), 'treated_arm': 'left', 'path_scale': [1.0, .45, 1.0],
                'lateral_path_center_from_camera_m': .20, 'lateral_source_center': 'median per clip',
                'prefab_reference': 'Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab',
                'prefab_hand_position_unity': [.09, -1.69, .11],
                'prefab_hand_rotation_xyzw': [-.012340715, .7069991, -.012340715, .7069991],
                'camera_alignment_pitch_deg': -12,
                'path_offset': list(OFFSET), 'source_bone_mapping': {}, 'motions': []}

for info in report['motions']:
    label = info['label']
    mirrored = label == 'Bandage'
    convert = source_camera_alignment @ Matrix(((0, 1, 0), (1 if mirrored else -1, 0, 0), (0, 0, 1)))
    sides = (('L', 'r'), ('R', 'l')) if mirrored else (('L', 'l'), ('R', 'r'))
    treated_source_side = 'r' if mirrored else 'l'
    lateral_samples = sorted((convert @ (p[treated_source_side+'_hand'].translation+Vector(info['hud_offset_blender']))).y for p in samples[label])
    lateral_center = lateral_samples[len(lateral_samples)//2]
    mapping = []
    for side, source_side in sides:
        mapping.extend([(source_side + '_' + a, side + '_' + b) for a, b in (('upperarm', 'Shoulder'), ('forearm', 'Elbow'), ('hand', 'Hand'))])
        mapping.extend([(finger_name(source_side, f, j), side + '_' + f + str(j)) for f in finger_names for j in (1, 2, 3)])
    verification['source_bone_mapping'][label] = mapping
    arm.animation_data_clear()
    previous = {}
    motion = {'label': label, 'mirrored': mirrored, 'source_chunk': info['chunk_id'], 'source_frames': info['frames'],
              'fps': 60, 'duration': info['frames'] / 60, 'max_joint_length_error_m': 0,
              'max_wrist_target_error_m': 0, 'max_wrist_step_m': 0, 'max_wrist_rotation_step_deg': 0,
              'max_wrist_bend_deg': 0, 'max_scale_error': 0, 'max_reach_adjustment_m': 0,
              'max_hand_orientation_adjustment_deg': 0}
    prior_hands = {}
    for frame in range(info['frames'] + 1):
        poses = samples[label][min(frame, info['frames'] - 1)]
        scene.frame_set(frame)
        for bone in arm.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        treated_side = 'r' if mirrored else 'l'
        working_side = 'l' if mirrored else 'r'
        treated_wrist = poses[treated_side + '_hand'].translation
        treated_axis = convert @ (treated_wrist - poses[treated_side + '_forearm'].translation).normalized()
        relative = convert @ (poses[working_side + '_hand'].translation - treated_wrist)
        axial = treated_axis * relative.dot(treated_axis)
        source_length = (source_rest[treated_side + '_hand'].translation - source_rest[treated_side + '_forearm'].translation).length
        target_length = (rest['L_Hand'].translation - rest['L_Elbow'].translation).length
        radial = (relative-axial)*1.60
        if radial.length > .0001:
            radial = radial.normalized()*math.sqrt(radial.length_squared+.18**2)
        along_contact = relative.dot(treated_axis)*(target_length/source_length)
        if label == 'Bandage':
            # Keep the wrapping pass near the wrist instead of Gekko's bulky upper-arm sleeve.
            wrap_weight = max(0, min(1, (frame - 48) / 12, (150 - frame) / 12))
            along_contact = along_contact * (1 - wrap_weight) + .08 * math.tanh(along_contact / .08) * wrap_weight
        working_relative = treated_axis*along_contact + radial
        treated_point = convert @ (treated_wrist + Vector(info['hud_offset_blender']))
        height_offset = .04 if label == 'Bandage' else .08
        treated_target = Vector((treated_point.x+.15, .20+(treated_point.y-lateral_center)*.45, treated_point.z+height_offset)) + OFFSET
        for side, source_side in sides:
            src_upper, src_lower, src_hand = (poses[source_side + '_' + n] for n in ('upperarm', 'forearm', 'hand'))
            u = convert @ (src_lower.translation - src_upper.translation).normalized()
            v = convert @ (src_hand.translation - src_lower.translation).normalized()
            su = source_rest[source_side + '_forearm'].translation - source_rest[source_side + '_upperarm'].translation
            sv = source_rest[source_side + '_hand'].translation - source_rest[source_side + '_forearm'].translation
            delta_upper = src_upper.to_3x3() @ source_rest[source_side + '_upperarm'].to_3x3().inverted()
            normal = (convert @ (delta_upper @ su.cross(sv).normalized())) * (-1 if mirrored else 1)
            upper, lower, hand = (arm.pose.bones[side + '_' + n] for n in ('Shoulder', 'Elbow', 'Hand'))
            ru = rest[lower.name].translation - rest[upper.name].translation
            rv = rest[hand.name].translation - rest[lower.name].translation
            rn = ru.cross(rv).normalized()
            wrist = treated_target.copy()
            contact_rotation = Quaternion()
            if side == 'R':
                left_wrist = arm.pose.bones['L_Hand'].head.copy()
                left_axis = (left_wrist-arm.pose.bones['L_Elbow'].head).normalized()
                contact_rotation = treated_axis.rotation_difference(left_axis)
                wrist = left_wrist + contact_rotation @ working_relative
            # Fit the cropped upper arm below the camera without shifting either wrist.
            body_hint = Vector((.05, OFFSET.y + (.75 if side == 'L' else -.30), .80)) if label == 'Bandage' else Vector((-.10, OFFSET.y + (.45 if side == 'L' else -.20), 1.27))
            if label == 'Bandage' and side == 'R':
                v = (v + .35 * (wrist - body_hint).normalized()).normalized()
            elbow = wrist - v * rv.length
            u = (elbow - body_hint).normalized()
            shoulder = elbow - u * ru.length
            if side == 'L':
                contact_rotation = treated_axis.rotation_difference(v)
            bend_normal = u.cross(v)
            if bend_normal.dot(normal) < 0:
                bend_normal.negate()
            normal = bend_normal
            clavicle = arm.pose.bones[side + '_Clavicle']
            baseline = clavicle.matrix.copy()
            baseline.translation += shoulder - upper.head
            clavicle.matrix = baseline
            bpy.context.view_layer.update()
            ur = (frame_matrix(u, normal) @ frame_matrix(ru, rn).transposed() @ rest[upper.name].to_3x3()).to_quaternion()
            lr = (frame_matrix(v, normal) @ frame_matrix(rv, rn).transposed() @ rest[lower.name].to_3x3()).to_quaternion()
            set_pose(upper.name, shoulder, ur)
            set_pose(lower.name, elbow, lr)
            palm = contact_rotation.to_matrix() @ source_palm(poses, source_side)
            hr = (palm @ target_palm_rest[side].transposed() @ rest[hand.name].to_3x3()).to_quaternion()
            hand_adjustment = Quaternion()
            bend = palm.col[0].angle(v)
            if bend > math.radians(65):
                hand_adjustment = Quaternion().slerp(palm.col[0].rotation_difference(v), 1 - math.radians(65) / bend)
                hr = hand_adjustment @ hr
            motion['max_hand_orientation_adjustment_deg'] = max(motion['max_hand_orientation_adjustment_deg'], math.degrees(hand_adjustment.angle))
            set_pose(hand.name, wrist, hr)
            forearm_twist(side, hr)
            needle = arm.pose.bones[side + '_Elbow_Ndl']
            ud = ur @ rest[upper.name].to_quaternion().inverted()
            ld = lr @ rest[lower.name].to_quaternion().inverted()
            set_pose(needle.name, elbow, ud.slerp(ld, 0.5) @ rest[needle.name].to_quaternion())
            for finger in finger_names:
                for joint in (1, 2, 3):
                    name = side + '_' + finger + str(joint)
                    sn, sd, snormal, target_frame = finger_frames[(side, source_side, name)]
                    delta = poses[sn].to_3x3() @ source_rest[sn].to_3x3().inverted()
                    rotation = (frame_matrix(convert @ (delta @ sd), (convert @ (delta @ snormal)) * (-1 if mirrored else 1)) @ target_frame).to_quaternion()
                    set_pose(name, arm.pose.bones[name].head.copy(), hand_adjustment @ contact_rotation @ rotation)
            if side == 'R' and label == 'Medkit' and abs(frame - 84) < 8:
                # Clear Gekko's longer middle finger without moving either wrist.
                middle = arm.pose.bones['R_Middle1']
                direction = arm.pose.bones['R_Middle2'].head - middle.head
                away = (arm.pose.bones['R_Middle2'].head - arm.pose.bones['L_Pinky3'].head).normalized()
                weight = .5 + .5 * math.cos(math.pi * (frame - 84) / 8)
                turn = direction.rotation_difference(direction + away * .012 * weight)
                set_pose(middle.name, middle.head.copy(), turn @ middle.matrix.to_quaternion())
            motion['max_wrist_target_error_m'] = max(motion['max_wrist_target_error_m'], (hand.head - wrist).length)
            motion['max_joint_length_error_m'] = max(motion['max_joint_length_error_m'],
                abs((lower.head - upper.head).length - ru.length), abs((hand.head - lower.head).length - rv.length))
            motion['max_wrist_bend_deg'] = max(motion['max_wrist_bend_deg'], math.degrees((hand_adjustment @ palm.col[0]).angle(v)))
            if side in prior_hands:
                p, q = prior_hands[side]
                motion['max_wrist_step_m'] = max(motion['max_wrist_step_m'], (hand.head - p).length)
                angle = math.degrees(hr.rotation_difference(q).angle)
                motion['max_wrist_rotation_step_deg'] = max(motion['max_wrist_rotation_step_deg'], min(angle, 360 - angle))
            prior_hands[side] = (hand.head.copy(), hr.copy())
        for bone in arm.pose.bones:
            q = bone.rotation_quaternion
            if bone.name in previous and q.dot(previous[bone.name]) < 0:
                q.negate()
            previous[bone.name] = q.copy()
            motion['max_scale_error'] = max(motion['max_scale_error'], max(abs(x - 1) for x in bone.scale))
            for prop in ('location', 'rotation_quaternion', 'scale'):
                bone.keyframe_insert(prop, frame=frame, group=bone.name)
    action = arm.animation_data.action
    action.name = 'FP_Heal_FDDA_' + label + '_Left'
    action.use_fake_user = True
    actions[label] = action
    for layer in action.layers:
        for strip in layer.strips:
            bag = strip.channelbag(action.slots[0])
            if bag:
                for curve in bag.fcurves:
                    for point in curve.keyframe_points:
                        point.interpolation = 'LINEAR'
    assert motion['max_joint_length_error_m'] < 0.0001, motion
    assert motion['max_wrist_target_error_m'] < 0.0001, motion
    assert motion['max_scale_error'] < 0.0001, motion
    verification['motions'].append(motion)
    print('RETARGET_BAKED=' + json.dumps(motion), flush=True)

for action in list(bpy.data.actions):
    if action not in actions.values():
        bpy.data.actions.remove(action)
verification['mesh_geometry_modified'] = False
verification['contact_path'] = {'axial_scale': 'target/source forearm length',
                              'radial_scale': 1.60, 'radial_clearance_quadrature_m': .18,
                              'wrist_pose_offset_model_m': {'Bandage': [.15, 0, .04], 'Medkit': [.15, 0, .08]},
                              'medkit_middle_finger_clearance': '12 mm directional target; cosine envelope frames 77-91; wrists unchanged',
                              'bandage_working_forearm_body_direction_weight': .35,
                              'forearm_routing': 'source treated forearm preserved; working Bandage forearm fitted toward body; original Gekko segment lengths',
                              'bandage_wrap_axial_limit_m': .08,
                              'bandage_wrap_axial_envelope_frames': [48, 60, 138, 150],
                              'upper_arm_fit': 'toward body hint below camera; original segment length preserved',
                              'common_contact_rotation': True, 'mesh_rest_rig_unchanged': True}
for obj in meshes:
    obj.hide_set(False)
    for modifier in obj.modifiers:
        modifier.show_viewport = True
    obj.hide_render = False
    obj.hide_viewport = False
    mat = bpy.data.materials.new('Gekko source preview hands')
    mat.diffuse_color = (0.35, 0.54, 0.48, 1)
    obj.data.materials.clear()
    obj.data.materials.append(mat)

camera_data = bpy.data.cameras.new('Gekko first person review')
camera = bpy.data.objects.new('Gekko first person review', camera_data)
scene.collection.objects.link(camera)
camera.location = OFFSET
camera_forward = placement.inverted().to_3x3() @ Vector((1, 0, 0))
camera_up = placement.inverted().to_3x3() @ Vector((0, 0, 1))
camera.rotation_euler = Matrix((camera_forward.cross(camera_up), camera_up, -camera_forward)).transposed().to_euler()
camera_data.lens = 36 / (2 * math.tan(math.radians(43.38307) / 2) * (960 / 540))
camera_data.sensor_width = 36
camera_data.sensor_fit = 'HORIZONTAL'
camera_data.clip_start = 0.1
scene.camera = camera
for name, point in (('FP_PreviewEye', camera.location), ('FP_PreviewForward', camera.location + camera_forward), ('FP_PreviewUp', camera.location + camera_up)):
    marker = bpy.data.objects.new(name, None)
    scene.collection.objects.link(marker)
    marker.parent = arm
    marker.location = point
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.fps = 60
scene.render.resolution_x, scene.render.resolution_y = 960, 540
scene.render.resolution_percentage = 100
scene.render.image_settings.media_type = 'IMAGE'
scene.render.image_settings.file_format = 'PNG'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.background_type = 'WORLD'
scene.world.color = (0.025, 0.035, 0.045)
scene.view_settings.view_transform = 'Standard'
bpy.context.preferences.filepaths.save_version = 0
for motion in verification['motions']:
    label = motion['label']
    folder = OUT / label
    folder.mkdir(exist_ok=True)
    action = actions[label]
    arm.animation_data.action = action
    arm.animation_data.action_slot = action.slots[0]
    scene.frame_start, scene.frame_end = 0, motion['source_frames']
    scene.frame_set(motion['source_frames'] // 2)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / f'SelfHeal_FDDA_{label}.blend'))
    for frame in (30, motion['source_frames'] // 2, motion['source_frames'] - 30):
        scene.frame_set(frame)
        scene.render.filepath = str(folder / f'pose-{frame:03d}.png')
        bpy.ops.render.render(write_still=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in scene.objects:
        if obj.type in ('ARMATURE', 'MESH', 'EMPTY'):
            obj.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=str(folder / f'SelfHeal_FDDA_{label}.fbx'), use_selection=True,
        object_types={'ARMATURE', 'MESH', 'EMPTY'}, add_leaf_bones=False, bake_anim=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False, bake_anim_simplify_factor=0,
        axis_forward='-Z', axis_up='Y')
    (folder / 'verification.json').write_text(json.dumps(motion, indent=2), encoding='utf-8')
(OUT / 'retarget-verification.json').write_text(json.dumps(verification, indent=2), encoding='utf-8')
print('GEKKO_RETARGET_EXPORTED', flush=True)
