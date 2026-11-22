using System;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// The Downed body (Left 4 Dead style), applied after the Animator each frame on top of a "lying on the back"
    /// Death frame. The pose is set in Humanoid muscle space so joints stay inside human limits: legs straight
    /// with the right knee up, spine curled so the shoulders leave the ground, neck nodding to look over the
    /// feet. The hips keep the clip's placement, so the body does not slide when the muscles change. Then the
    /// shoulders turn the sidearm (held in the standing pose by the arms layer) onto the aim. Weights blend it
    /// in and out; at 0 the body lies exactly as the clip left it.
    /// </summary>
    public sealed class PlayerDownedPose : IDisposable
    {
        /// <summary>
        /// Sign of the "Front-Back"/"Nod" muscles that lifts the shoulders of a body lying on its back. Measured on
        /// the player avatars by PlayerDownedPoseTests (+1 sinks the head to -0.30 m, -1 lifts it to 0.61 m).
        /// </summary>
        public static float SpineLiftSign = -1f;

        // Leg shape in joint angles; converted to this avatar's muscle values once (muscle 0 is not "straight").
        const float LeftThighForward = 0f, LeftThighOut = 7f, LeftKneeBend = 6f;
        const float RightThighForward = 30f, RightThighOut = 6f, RightKneeBend = 55f;

        readonly (string muscle, float value)[] Legs =
        {
            ("Left Upper Leg Front-Back", 0f), ("Left Upper Leg In-Out", 0f), ("Left Upper Leg Twist In-Out", 0f),
            ("Left Lower Leg Stretch", 0f), ("Left Lower Leg Twist In-Out", 0f), ("Left Foot Up-Down", 0f),
            ("Left Foot Twist In-Out", 0f), ("Left Toes Up-Down", 0f),
            ("Right Upper Leg Front-Back", 0f), ("Right Upper Leg In-Out", 0f), ("Right Upper Leg Twist In-Out", 0f),
            ("Right Lower Leg Stretch", 0f), ("Right Lower Leg Twist In-Out", 0f), ("Right Foot Up-Down", 0f),
            ("Right Foot Twist In-Out", 0f), ("Right Toes Up-Down", 0f)
        };

        static readonly (string muscle, float value)[] Torso =
        {
            ("Spine Front-Back", 0.45f), ("Chest Front-Back", 0.38f), ("UpperChest Front-Back", 0.22f),
            ("Spine Left-Right", 0f), ("Spine Twist Left-Right", 0f), ("Chest Left-Right", 0f), ("Chest Twist Left-Right", 0f),
            ("UpperChest Left-Right", 0f), ("UpperChest Twist Left-Right", 0f),
            ("Neck Nod Down-Up", 0.35f), ("Neck Tilt Left-Right", 0f), ("Head Nod Down-Up", 0.2f), ("Head Tilt Left-Right", 0f)
        };

        readonly Animator animator;
        readonly HumanPoseHandler handler;
        HumanPose pose;
        readonly int[] legIndex, torsoIndex;
        readonly Quaternion neutralHipsLocal;

        public PlayerDownedPose(Animator animator)
        {
            this.animator = animator;
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            pose = new HumanPose();
            legIndex = Indices(Legs);
            torsoIndex = Indices(Torso);
            // Hips of the avatar standing straight, facing the animator's forward. The lying hips are this,
            // tipped onto the back, so the result does not depend on how a clip happened to end.
            var current = new HumanPose();
            handler.GetHumanPose(ref current);
            var neutral = new HumanPose { bodyPosition = current.bodyPosition, bodyRotation = Quaternion.identity, muscles = new float[current.muscles.Length] };
            handler.SetHumanPose(ref neutral);
            neutralHipsLocal = Quaternion.Inverse(animator.transform.rotation) * animator.GetBoneTransform(HumanBodyBones.Hips).rotation;
            CalibrateLeg(neutral, "Left", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, -1f,
                LeftThighForward, LeftThighOut, LeftKneeBend);
            CalibrateLeg(neutral, "Right", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 1f,
                RightThighForward, RightThighOut, RightKneeBend);
            handler.SetHumanPose(ref current);
        }

        /// <summary>Find the muscle values that give the wanted thigh and knee angles, measured on the standing avatar.</summary>
        void CalibrateLeg(HumanPose neutral, string side, HumanBodyBones hip, HumanBodyBones knee, HumanBodyBones foot, float outwardSign,
            float forwardDegrees, float outDegrees, float kneeDegrees)
        {
            Transform root = animator.transform;
            Vector3 F = root.forward, D = -root.up, O = root.right * outwardSign;
            Transform H = animator.GetBoneTransform(hip), K = animator.GetBoneTransform(knee), A = animator.GetBoneTransform(foot);
            int frontBack = Array.IndexOf(HumanTrait.MuscleName, side + " Upper Leg Front-Back");
            int inOut = Array.IndexOf(HumanTrait.MuscleName, side + " Upper Leg In-Out");
            int stretch = Array.IndexOf(HumanTrait.MuscleName, side + " Lower Leg Stretch");
            var test = new HumanPose { bodyPosition = neutral.bodyPosition, bodyRotation = neutral.bodyRotation, muscles = (float[])neutral.muscles.Clone() };
            float Solve(int muscle, Func<float> measure, float target)
            {
                float best = 0f, error = float.MaxValue;
                for (float v = -1f; v <= 1.0001f; v += 0.02f)
                {
                    test.muscles[muscle] = v;
                    handler.SetHumanPose(ref test);
                    float e = Mathf.Abs(Mathf.DeltaAngle(measure(), target));
                    if (e < error) { error = e; best = v; }
                }
                test.muscles[muscle] = best;
                return best;
            }
            float Forward() { Vector3 v = K.position - H.position; return Mathf.Atan2(Vector3.Dot(v, F), Vector3.Dot(v, D)) * Mathf.Rad2Deg; }
            float Outward() { Vector3 v = K.position - H.position; return Mathf.Atan2(Vector3.Dot(v, O), Vector3.Dot(v, D)) * Mathf.Rad2Deg; }
            float Bend() => Vector3.Angle(K.position - H.position, A.position - K.position);
            Set(side + " Upper Leg Front-Back", Solve(frontBack, Forward, forwardDegrees));
            Set(side + " Upper Leg In-Out", Solve(inOut, Outward, outDegrees));
            Set(side + " Lower Leg Stretch", Solve(stretch, Bend, kneeDegrees));
        }

        void Set(string muscle, float value)
        {
            for (int i = 0; i < Legs.Length; i++)
                if (Legs[i].muscle == muscle) Legs[i].value = value;
        }

        /// <summary>Calibrated leg muscle values, for diagnostics.</summary>
        public string LegCalibration => string.Join(", ", Array.ConvertAll(Legs, l => $"{l.muscle}={l.value:0.00}"));

        static int[] Indices((string muscle, float value)[] table)
        {
            var result = new int[table.Length];
            for (int i = 0; i < table.Length; i++) result[i] = Array.IndexOf(HumanTrait.MuscleName, table[i].muscle);
            return result;
        }

        public void Dispose() => handler?.Dispose();

        /// <param name="body">0 = as the clip left it, 1 = lying flat on the back, legs tidy, shoulders raised.</param>
        /// <param name="feetToward">World direction the feet point along the ground (where the player looks).</param>
        /// <param name="aim">World direction the sidearm points at.</param>
        /// <param name="arms">0 = arms as animated, 1 = both hands on the sidearm along <paramref name="aim"/>.</param>
        public void Apply(float body, Vector3 feetToward, Vector3 aim, float arms)
        {
            ApplyBody(body, feetToward);
            AimShoulders(aim, arms);
        }

        /// <summary>Lying flat on the back, legs tidy, shoulders raised (weight <paramref name="body"/>).</summary>
        public void ApplyBody(float body, Vector3 feetToward)
        {
            if (animator == null || !animator.isHuman) return;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null) return;

            if (body > 0.001f)
            {
                Vector3 hipsPosition = hips.position;
                Quaternion hipsRotation = Quaternion.Slerp(hips.rotation, LyingHips(feetToward), body);
                handler.GetHumanPose(ref pose);
                Blend(Legs, legIndex, body, 1f);
                Blend(Torso, torsoIndex, body, SpineLiftSign);
                handler.SetHumanPose(ref pose);
                // The muscles define the limbs relative to the hips; keep the hips where the clip put them.
                hips.SetPositionAndRotation(hipsPosition, hipsRotation);
            }
        }

        /// <summary>
        /// Aim the standing sidearm hold without moving either shoulder socket. The old implementation rotated
        /// each upper arm around its own shoulder, which made the two wrists drift apart. Solve both two-bone
        /// chains to a rigidly rotated hand pair, then restore the authored wrist rotations.
        /// </summary>
        public void AimShoulders(Vector3 aim, float arms)
            => AimShoulders(aim, arms, Vector3.zero);

        /// <summary>Variant that uses the actual authored barrel direction when it is available.</summary>
        public void AimShoulders(Vector3 aim, float arms, Vector3 currentBarrel)
        {
            if (animator == null || !animator.isHuman) return;
            if (arms <= 0.001f || aim.sqrMagnitude < 1e-6f) return;
            Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand), rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftArm == null || rightArm == null || leftForearm == null || rightForearm == null || leftHand == null || rightHand == null) return;
            Vector3 shoulders = (leftArm.position + rightArm.position) * 0.5f;
            Vector3 gun = (leftHand.position + rightHand.position) * 0.5f - shoulders;
            if (gun.sqrMagnitude < 1e-6f) return;
            Vector3 source = currentBarrel.sqrMagnitude > 1e-6f ? currentBarrel.normalized : gun.normalized;
            Quaternion turn = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(source, aim.normalized), arms);
            // Clear the curled chest without changing the authored grip or the barrel direction.
            Vector3 clearance = Vector3.up * (0.12f * arms);
            Vector3 leftTarget = shoulders + turn * (leftHand.position - shoulders) + clearance;
            Vector3 rightTarget = shoulders + turn * (rightHand.position - shoulders) + clearance;
            Vector3 center = (leftTarget + rightTarget) * 0.5f;
            Vector3 direction = (center - shoulders).normalized;
            float near = 0f, far = Vector3.Distance(center, shoulders);
            if (!FitReach(leftArm, leftForearm, leftHand, shoulders + leftTarget - center, direction, ref near, ref far) ||
                !FitReach(rightArm, rightForearm, rightHand, shoulders + rightTarget - center, direction, ref near, ref far) || near > far) return;
            // Move the pair together; clamping the two arms separately breaks the authored grip again.
            Vector3 shift = shoulders + direction * far - center;
            leftTarget += shift;
            rightTarget += shift;
            Vector3 leftElbow = shoulders + turn * (leftForearm.position - shoulders) + clearance + shift;
            Vector3 rightElbow = shoulders + turn * (rightForearm.position - shoulders) + clearance + shift;
            Quaternion leftRotation = turn * leftHand.rotation;
            Quaternion rightRotation = turn * rightHand.rotation;
            SolveArm(leftArm, leftForearm, leftHand, leftTarget, leftRotation, leftElbow);
            SolveArm(rightArm, rightForearm, rightHand, rightTarget, rightRotation, rightElbow);
        }

        static bool FitReach(Transform upper, Transform forearm, Transform hand, Vector3 origin, Vector3 direction, ref float near, ref float far)
        {
            float reach = Vector3.Distance(upper.position, forearm.position) + Vector3.Distance(forearm.position, hand.position) - 0.001f;
            Vector3 offset = origin - upper.position;
            float along = Vector3.Dot(offset, direction);
            float discriminant = along * along + reach * reach - offset.sqrMagnitude;
            if (discriminant < 0f) return false;
            float radius = Mathf.Sqrt(discriminant);
            near = Mathf.Max(near, -along - radius);
            far = Mathf.Min(far, -along + radius);
            return far >= 0f;
        }

        static void SolveArm(Transform upper, Transform forearm, Transform hand, Vector3 target, Quaternion handRotation, Vector3 elbowHint)
        {
            Vector3 shoulder = upper.position;
            Transform corrective = forearm.Find(forearm.name + "_Ndl");
            Quaternion neutralCorrective = corrective != null ? forearm.localRotation * corrective.localRotation : Quaternion.identity;
            float upperLength = Vector3.Distance(shoulder, forearm.position);
            float forearmLength = Vector3.Distance(forearm.position, hand.position);
            if (upperLength < 1e-4f || forearmLength < 1e-4f) return;

            // Preserve the authored arm roll as well as its elbow plane before adjusting for fixed sockets.
            Quaternion turn = handRotation * Quaternion.Inverse(hand.rotation);
            Quaternion forearmRotation = turn * forearm.rotation;
            upper.rotation = turn * upper.rotation;
            forearm.rotation = forearmRotation;

            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - forearmLength) + 1e-4f,
                upperLength + forearmLength - 1e-4f);
            Vector3 direction = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (hand.position - shoulder).normalized;
            target = shoulder + direction * distance;
            Vector3 pole = Vector3.ProjectOnPlane(elbowHint - shoulder, direction);
            if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(direction, Vector3.up);
            if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(direction, Vector3.right);
            pole.Normalize();

            float along = (upperLength * upperLength + distance * distance - forearmLength * forearmLength) / (2f * distance);
            float across = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbow = shoulder + direction * along + pole * across;

            Quaternion upperTurn = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder);
            upper.rotation = upperTurn * upper.rotation;
            Vector3 newElbow = forearm.position;
            Vector3 currentHand = hand.position - newElbow;
            Vector3 desiredHand = target - newElbow;
            if (currentHand.sqrMagnitude > 1e-8f && desiredHand.sqrMagnitude > 1e-8f)
                forearm.rotation = Quaternion.FromToRotation(currentHand, desiredHand) * forearm.rotation;
            hand.rotation = handRotation;
            if (corrective != null) corrective.localRotation = Quaternion.Inverse(forearm.localRotation) * neutralCorrective;
        }

        /// <summary>Neutral hips turned so the chest faces the sky and the legs run along <paramref name="feetToward"/>.</summary>
        Quaternion LyingHips(Vector3 feetToward)
        {
            Transform root = animator.transform;
            Vector3 feet = Vector3.ProjectOnPlane(feetToward, Vector3.up);
            if (feet.sqrMagnitude < 1e-4f) feet = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            feet.Normalize();
            // Standing: forward = root.forward, up = root.up. Lying: forward = sky, up = towards the head.
            Quaternion tip = Quaternion.LookRotation(Vector3.up, -feet) * Quaternion.Inverse(Quaternion.LookRotation(root.forward, root.up));
            return tip * (root.rotation * neutralHipsLocal);
        }

        void Blend((string muscle, float value)[] table, int[] index, float weight, float frontBackSign)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (index[i] < 0) continue;
                float target = table[i].muscle.Contains("Front-Back") || table[i].muscle.Contains("Nod") ? table[i].value * frontBackSign : table[i].value;
                pose.muscles[index[i]] = Mathf.Lerp(pose.muscles[index[i]], target, weight);
            }
        }

        /// <summary>
        /// Yaw that turns a lying body so its feet point along <paramref name="facing"/>. The caller rotates the
        /// visual model by it so the downed player aims over their feet, the way they are looking.
        /// </summary>
        public static float FeetYaw(Animator animator, Vector3 facing)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || left == null || right == null) return 0f;
            Vector3 feet = Vector3.ProjectOnPlane((left.position + right.position) * 0.5f - hips.position, Vector3.up);
            facing = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (feet.sqrMagnitude < 1e-4f || facing.sqrMagnitude < 1e-4f) return 0f;
            return Vector3.SignedAngle(feet, facing, Vector3.up);
        }
    }
}
