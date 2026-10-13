"""Bake one five-second injection prototype on the existing Gekko first-person rig."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree
from io_scene_fbx import import_fbx, parse_fbx

OUT = Path(__file__).resolve().parent / "InjectionPrototype-v5"
OUT.mkdir(exist_ok=True)
SOURCE = Path(__file__).resolve().parents[2] / "Assets/FPS/Features/Characters/Content/Players/Gekko/Models/Hand.fbx"
FPS, END = 24, 121

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(SOURCE))
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 1, END
root, _ = parse_fbx.parse(str(SOURCE))
objects = next(e for e in root.elems if e.id == b"Objects")
source_actions = {}
for element in objects.elems:
    if element.id == b"AnimationStack":
        raw = element.props[1].split(b"\x00\x01")[0]
        source_actions[raw.decode().split("|")[-1]] = bpy.data.actions[arm.name + "|" + import_fbx.validate_blend_names(raw)]
equip = source_actions["FP_AggroBot_S0_4_Bubbletov_Equip"]
cast = source_actions["FP_AggroBot_S0_X_BabyShark_Cast"]
idle = source_actions["FP_AggroBot_S0_Q_BabySpikebot_Idle"]
bones = list(arm.pose.bones)


def smooth(a, b, t):
    x = max(0.0, min(1.0, (t - a) / (b - a)))
    return x * x * (3.0 - 2.0 * x)


def sample(action, frame):
    arm.animation_data.action = action
    arm.animation_data.action_slot = action.slots[0]
    scene.frame_set(int(frame), subframe=frame - int(frame))
    bpy.context.view_layer.update()
    return {b.name: b.matrix_basis.copy() for b in bones}


def blend(a, b, weight):
    result = {}
    for name in a:
        pa, qa, sa = a[name].decompose()
        pb, qb, sb = b[name].decompose()
        result[name] = Matrix.LocRotScale(pa.lerp(pb, weight), qa.slerp(qb, weight), sa.lerp(sb, weight))
    return result


# Keep the source entrance and forearm pose; the grip must be authored for a syringe.
entry_pose = sample(equip, 11)
raised_pose = sample(cast, 15)
poses = []
for frame in range(END):
    t = frame / FPS
    weight = smooth(.15, 1.1, t) * (1 - smooth(4.5, 5.0, t))
    poses.append(blend(entry_pose, raised_pose, weight))

arm.animation_data_clear()
action = bpy.data.actions.new("FP_Antidote_SelfInject_v5_5s")
action.use_fake_user = True


def attach_action(obj):
    obj.animation_data_create()
    obj.animation_data.action = action
    obj.animation_data.action_slot = action.slots.new("OBJECT", obj.name)


attach_action(arm)
for bone in bones:
    bone.rotation_mode = "QUATERNION"


def material(name, color, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*color, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = metallic
    return m


white = material("Syringe - ivory", (.8, .85, .87))
metal = material("Syringe - steel", (.55, .65, .7), .8)
fluid = material("Antidote - amber", (.95, .55, .08))
blue = material("Syringe - blue cap", (.08, .35, .5))
hands = material("Preview hands", (.35, .54, .48))
for obj in list(scene.objects):
    if obj.type == "MESH":
        obj.data.materials.clear()
        obj.data.materials.append(hands)
meshes = [obj for obj in scene.objects if obj.type == "MESH"]
left_mesh = max(meshes, key=lambda obj: sum(g.weight for v in obj.data.vertices for g in v.groups
                                          if obj.vertex_groups[g.group].name.startswith("L_")))

syringe = bpy.data.objects.new("Antidote syringe", None)
scene.collection.objects.link(syringe)
syringe.parent = arm
attach_action(syringe)


def cylinder(name, radius, depth, z, mat, parent=syringe):
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=radius, depth=depth)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = (0, 0, z)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Soft edge", "BEVEL")
    bevel.width, bevel.segments = .0008, 2
    return obj


cylinder("Needle", .0011, .025, .0125, metal)
cylinder("Needle hub", .007, .012, .031, blue)
cylinder("Barrel", .012, .066, .067, white)
cylinder("Medicine window", .0122, .027, .061, fluid)
cylinder("Finger flange", .021, .004, .102, white)
piston = bpy.data.objects.new("Piston", None)
scene.collection.objects.link(piston)
piston.parent = syringe
piston.location.z = .103
attach_action(piston)
cylinder("Piston shaft", .004, .042, .021, white, piston)
cylinder("Thumb pad", .014, .006, .044, blue, piston)


def rotate_bone(bone, quat):
    current = bone.matrix.copy()
    rotation = (quat.to_matrix().to_4x4() @ current)
    rotation.translation = current.translation
    bone.matrix = rotation
    bpy.context.view_layer.update()


def solve_arm(prefix, target, bend):
    upper, lower, hand = [arm.pose.bones[prefix + n] for n in ("_Shoulder", "_Elbow", "_Hand")]
    origin, elbow, wrist = upper.head.copy(), lower.head.copy(), hand.head.copy()
    a, b = (elbow - origin).length, (wrist - elbow).length
    aim = target - origin
    length = min(a + b - .001, max(abs(a - b) + .001, aim.length))
    direction = aim.normalized()
    side = (bend - direction * bend.dot(direction)).normalized()
    along = (a * a - b * b + length * length) / (2 * length)
    goal = origin + direction * along + side * math.sqrt(max(0, a * a - along * along))
    rotate_bone(upper, (elbow - origin).rotation_difference(goal - origin))
    rotate_bone(lower, (hand.head - lower.head).rotation_difference(target - lower.head))


# Bone tails in this imported rig are display axes, not the next anatomical joint.
# Use joint heads and mesh-derived fingertip lengths to solve the finger chains.
rest_tips, fingertip_vertices = {}, {}
for prefix in ("R", "L"):
    for finger in ("Index", "Middle", "Ring", "Pinky", "Thumb"):
        second = arm.data.bones[prefix + "_" + finger + "2"]
        third = arm.data.bones[prefix + "_" + finger + "3"]
        direction = (third.head_local - second.head_local).normalized()
        candidates = []
        for obj in meshes:
            group = obj.vertex_groups.get(third.name)
            if group is None:
                continue
            for vertex in obj.data.vertices:
                if any(g.group == group.index and g.weight > .65 for g in vertex.groups):
                    point = arm.matrix_world.inverted() @ obj.matrix_world @ vertex.co
                    candidates.append((point, obj, vertex.index))
        candidates.sort(key=lambda entry: (entry[0] - third.head_local).dot(direction), reverse=True)
        distal = candidates[:max(3, len(candidates)//5)]
        pad = sum((entry[0] for entry in distal), Vector()) / len(distal)
        rest_tips[prefix + "_" + finger] = third.matrix_local.inverted() @ pad
        fingertip_vertices[prefix + "_" + finger] = [(obj, index) for _, obj, index in distal]


def point_tip(prefix, finger):
    return arm.pose.bones[prefix + "_" + finger + "3"].matrix @ rest_tips[prefix + "_" + finger]


def finger_pose(prefix, finger, target_local, first_direction, bend_local):
    hand = arm.pose.bones[prefix + "_Hand"]
    first, second, third = [arm.pose.bones[prefix + "_" + finger + str(i)] for i in (1, 2, 3)]
    root = first.head.copy()
    target = hand.matrix @ Vector(target_local)
    length1 = (second.head - first.head).length
    direction = (hand.matrix.to_3x3() @ Vector(first_direction)).normalized()
    pip = root + direction * length1
    rotate_bone(first, (second.head - first.head).rotation_difference(pip - root))
    pip = second.head.copy()
    a, b = (third.head - pip).length, (point_tip(prefix, finger) - third.head).length
    aim = target - pip
    d = max(abs(a - b) + .0001, min(a + b - .0001, aim.length))
    forward = aim.normalized()
    bend = hand.matrix.to_3x3() @ Vector(bend_local)
    side = (bend - forward * bend.dot(forward)).normalized()
    along = (a*a - b*b + d*d) / (2*d)
    dip = pip + forward * along + side * math.sqrt(max(0, a*a - along*along))
    rotate_bone(second, (third.head - second.head).rotation_difference(dip - second.head))
    rotate_bone(third, (point_tip(prefix, finger) - third.head).rotation_difference(target - third.head))
    return (point_tip(prefix, finger) - target).length


def set_hand(hand, rotation):
    hand.matrix = Matrix.LocRotScale(hand.head, rotation, hand.matrix.to_scale())
    bpy.context.view_layer.update()




def align_elbow_hinge():
    upper, lower, hand = [arm.pose.bones["R_" + n] for n in ("Shoulder", "Elbow", "Hand")]
    origin, elbow, wrist = upper.head.copy(), lower.head.copy(), hand.head.copy()
    rest_upper = lower.bone.head_local - upper.bone.head_local
    rest_lower = hand.bone.head_local - lower.bone.head_local
    rest_normal = rest_upper.cross(rest_lower).normalized()
    pose_normal = (elbow - origin).cross(wrist - elbow).normalized()

    def frame(direction, normal):
        direction = direction.normalized()
        normal = (normal - direction * normal.dot(direction)).normalized()
        return Matrix((direction, normal, direction.cross(normal))).transposed()

    for bone, position, rest_direction, direction in (
        (upper, origin, rest_upper, elbow - origin),
        (lower, elbow, rest_lower, wrist - elbow),
    ):
        mapping = frame(direction, pose_normal) @ frame(rest_direction, rest_normal).transposed()
        rotation = mapping.to_quaternion() @ bone.bone.matrix_local.to_quaternion()
        bone.matrix = Matrix.LocRotScale(position, rotation, Vector((1, 1, 1)))
        bpy.context.view_layer.update()
    assert (hand.head - wrist).length < .0001


def distribute_forearm_roll(rotation):
    lower = arm.pose.bones["R_Elbow"]
    hand = arm.pose.bones["R_Hand"]
    forearm = (hand.head - lower.head).normalized()
    rest_relative = lower.bone.matrix_local.inverted() @ hand.bone.matrix_local
    neutral = lower.matrix @ rest_relative
    delta = rotation @ neutral.to_quaternion().inverted()
    if delta.w < 0:
        delta.negate()
    axial = forearm * Vector((delta.x, delta.y, delta.z)).dot(forearm)
    twist = Quaternion((delta.w, axial.x, axial.y, axial.z))
    twist.normalize()
    rest_direction = hand.bone.head_local - lower.bone.head_local
    for name in ("R_Twist1", "R_Twist2", "R_HandTwist"):
        bone = arm.pose.bones[name]
        weight = max(0, min(1, (bone.bone.head_local - lower.bone.head_local).dot(rest_direction) / rest_direction.length_squared))
        baseline = lower.matrix @ (lower.bone.matrix_local.inverted() @ bone.bone.matrix_local)
        turn = Quaternion().slerp(twist, weight)
        spun = turn.to_matrix().to_4x4() @ baseline
        spun.translation = lower.head + turn @ (baseline.translation - lower.head)
        bone.matrix = spun
        bpy.context.view_layer.update()
    return math.degrees(abs(twist.angle))


rhand, lhand = arm.pose.bones["R_Hand"], arm.pose.bones["L_Hand"]
for bone in bones:
    bone.matrix_basis = raised_pose[bone.name]
bpy.context.view_layer.update()
# Left palm remains relaxed, with the fingers pointing forward and across the screen.
left_rest = arm.data.bones["L_Hand"].matrix_local.to_quaternion()
left_x = Vector((.85, -.5, -.12)).normalized()
left_y = Vector((0, 0, -1))
left_z = left_x.cross(left_y).normalized()
left_y = left_z.cross(left_x).normalized()
left_rotation = Matrix((left_x, left_y, left_z)).transposed().to_quaternion()
# Right palm faces the camera; the visible syringe sits in front of the palm.
axis = Vector((.10, -.78, .62)).normalized()
palm = Vector((-1, 0, .1))
side = axis.cross(palm).normalized()
palm = side.cross(axis).normalized()
right_rotation = Quaternion(axis, math.radians(50)) @ Matrix((axis, palm, side)).transposed().to_quaternion()
carry_rotation = Quaternion(Vector((1, 0, 0)), math.radians(15)) @ Quaternion(axis, math.radians(20)) @ right_rotation
local_tip = Vector((-.205, .044, -.018))
local_syringe_rotation = Vector((0, 0, 1)).rotation_difference(Vector((1, 0, 0)))
previous, contact_errors, thumb_errors = {}, [], []
wrist_angles, roll_angles = [], []
skin_hits = 0
for frame, pose in enumerate(poses, 1):
    scene.frame_set(frame)
    for bone in bones:
        bone.matrix_basis = pose[bone.name]
    bpy.context.view_layer.update()
    t = (frame - 1) / FPS
    raised = smooth(.2, 1.1, t) * (1 - smooth(4.5, 5.0, t))
    rweight = smooth(.05, 1.35, t) * (1 - smooth(4.05, 4.5, t))
    original_left = lhand.head.copy()
    solve_arm("L", original_left.lerp(Vector((.48, -.06, 1.45)), raised), Vector((0, 1, -.8)))
    set_hand(lhand, lhand.matrix.to_quaternion().slerp(left_rotation, raised))
    forearm = arm.pose.bones["L_Elbow"].head.lerp(lhand.head, .05)
    # The needle approaches the skin from above, away from the wrist cuff.
    tree = BVHTree.FromObject(left_mesh, bpy.context.evaluated_depsgraph_get())
    inverse = left_mesh.matrix_world.inverted()
    hit, normal, _, _ = tree.ray_cast(inverse @ (forearm + axis * .3),
                                     (inverse.to_3x3() @ -axis).normalized(), .6)
    assert hit is not None, frame
    contact = left_mesh.matrix_world @ hit
    gap = .065 * (1 - smooth(1.2, 1.85, t)) + .065 * smooth(3.65, 4.05, t)
    penetration = .003 * smooth(1.75, 1.95, t) * (1 - smooth(3.55, 3.8, t))
    target_tip = contact + axis * (gap - penetration)
    rotation = carry_rotation.slerp(right_rotation, rweight)
    wrist_target = target_tip - rotation @ local_tip
    carry_wrist = Vector((.32, -.39, 1.42))
    # Retract the shoulder during entrance/exit so the upper arm stays beyond the wide-screen edge.
    shoulder_offset = .12 * (1 - smooth(.85, 1.35, t) + smooth(4.05, 4.5, t))
    clavicle = arm.pose.bones["R_Clavicle"]
    clavicle_pose = clavicle.matrix.copy()
    clavicle_pose.translation.x -= shoulder_offset
    clavicle.matrix = clavicle_pose
    bpy.context.view_layer.update()
    # Pick the elbow on the reach circle closest to a straight wrist.
    solve_arm("R", carry_wrist.lerp(wrist_target, rweight), rotation @ Vector((1, 0, 0)))
    # Keep the entrance wrist within the same bend limit as the injection pose.
    forearm_direction = (rhand.head - arm.pose.bones["R_Elbow"].head).normalized()
    hand_direction = (rotation @ Vector((-1, 0, 0))).normalized()
    bend_angle = hand_direction.angle(forearm_direction)
    if bend_angle > math.radians(25):
        correction = hand_direction.rotation_difference(forearm_direction)
        rotation = Quaternion().slerp(correction, 1 - math.radians(25) / bend_angle) @ rotation
    align_elbow_hinge()
    roll_angles.append(distribute_forearm_roll(rotation))
    set_hand(rhand, rotation)
    forearm_forward = (rhand.head - arm.pose.bones["R_Elbow"].head).normalized()
    hand_forward = (rotation @ Vector((-1, 0, 0))).normalized()
    wrist_angles.append(math.degrees(hand_forward.angle(forearm_forward)))
    # Finger poses are defined in hand space and remain attached during entrance/exit.
    for finger, target, first_direction in (
        ("Index", (-.105, .044, -.047), (-.64, .77, -.02)),
        ("Middle", (-.105, .044, .011), (-.60, .80, -.02)),
        ("Ring", (-.095, .049, .036), (-.65, .74, .17)),
        ("Pinky", (-.077, .041, .057), (-.58, .77, .26)),
    ):
        finger_pose("R", finger, target, first_direction, (0, 1, 0))
    press = smooth(2.05, 3.25, t)
    piston.location.z = .103 - .025 * press
    thumb_x = local_tip.x + piston.location.z + .047 + .004
    thumb_error = finger_pose("R", "Thumb", (thumb_x, .044, -.018), (-.46, .72, -.52), (0, 1, -.2))
    thumb_errors.append(thumb_error)
    syringe.rotation_mode = "QUATERNION"
    syringe.rotation_quaternion = rhand.matrix.to_quaternion() @ local_syringe_rotation
    syringe.location = rhand.matrix @ local_tip
    if 1.95 <= t <= 3.5:
        contact_errors.append((syringe.location - (contact - axis * penetration)).length)
        skin_hits += 1
    for bone in bones:
        q = bone.rotation_quaternion
        if bone.name in previous:
            q.make_compatible(previous[bone.name])
        previous[bone.name] = q.copy()
        for property_name in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(property_name, frame=frame, group=bone.name)
    syringe.keyframe_insert("location", frame=frame)
    syringe.keyframe_insert("rotation_quaternion", frame=frame)
    piston.keyframe_insert("location", frame=frame)

assert len(poses) == END and len(action.slots) == 3
assert contact_errors and max(contact_errors) < .006, max(contact_errors)
assert max(thumb_errors) < .002, max(thumb_errors)
assert skin_hits == len(contact_errors)
print("WRIST_METRICS", max(wrist_angles), max(roll_angles))
assert max(wrist_angles) < 35, max(wrist_angles)
assert max(roll_angles) < 90, max(roll_angles)
for source_action in list(bpy.data.actions):
    if source_action != action:
        bpy.data.actions.remove(source_action)

camera_data = bpy.data.cameras.new("First person preview")
camera = bpy.data.objects.new("First person preview", camera_data)
scene.collection.objects.link(camera)
camera.location = (0, 0, 1.674)
camera.rotation_euler = Vector((1, 0, -.32)).to_track_quat("-Z", "Y").to_euler()
camera_data.lens, camera_data.clip_start = 22, .01
scene.camera = camera
# Point markers let Unity reconstruct the same eye/up/forward after FBX axis conversion.
for name, point in (("FP_PreviewEye", camera.location),
                    ("FP_PreviewForward", camera.location + Vector((1, 0, -.32))),
                    ("FP_PreviewUp", camera.location + Vector((0, 0, 1)))):
    marker = bpy.data.objects.new(name, None)
    scene.collection.objects.link(marker)
    marker.parent = arm
    marker.location = point
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x, scene.render.resolution_y = 800, 500
scene.render.resolution_percentage = 100
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.background_type = "WORLD"
scene.world.color = (.025, .035, .045)
scene.frame_set(61)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "SelfInjection_Prototype.blend"))
report = {"duration": 5, "fps": FPS, "frames": END, "action": action.name,
          "max_needle_target_error_m": max(contact_errors), "skin_raycast_contact_frames": skin_hits,
          "piston_travel_m": .025, "source": str(SOURCE)}
# Source actions were removed after baking; keep their original clip names as provenance.
report["source_segments"] = ["4_Bubbletov_Equip", "X_BabyShark_Cast"]
report["thumb_tip_max_error_m"] = max(thumb_errors)
report["max_wrist_bend_degrees"] = max(wrist_angles)
report["max_forearm_roll_degrees"] = max(roll_angles)
report["wrist_bend_per_frame_degrees"] = wrist_angles
(OUT / "verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
for frame in (13, 37, 61, 85, 109):
    scene.frame_set(frame)
    scene.render.filepath = str(OUT / ("pose-%03d.png" % frame))
    bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action="DESELECT")
for obj in scene.objects:
    if obj != camera:
        obj.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=str(OUT / "SelfInjection_Prototype.fbx"), use_selection=True,
                         object_types={"ARMATURE", "MESH", "EMPTY"}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                         bake_anim_simplify_factor=0, axis_forward="-Z", axis_up="Y")
print("INJECTION_PREVIEW_READY", OUT)



