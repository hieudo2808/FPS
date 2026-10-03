using System;
using UnityEngine;

namespace FPS
{
    /// <summary>Small two-bone IK overlay. Restores the animator pose before the next frame.</summary>
    [Serializable]
    public sealed class SurvivalArmPose
    {
        public Transform upper;
        public Transform lower;
        public Transform hand;
        private Quaternion upperRest, lowerRest, handRest;
        private bool applied;

        public void Restore()
        {
            if (!applied) return;
            if (upper != null) upper.localRotation = upperRest;
            if (lower != null) lower.localRotation = lowerRest;
            if (hand != null) hand.localRotation = handRest;
            applied = false;
        }

        public void Apply(Vector3 target, Vector3 bendDirection, float weight)
        {
            if (upper == null || lower == null || hand == null || weight <= 0f) return;
            upperRest = upper.localRotation; lowerRest = lower.localRotation; handRest = hand.localRotation;
            applied = true;
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            if (a < .001f || b < .001f) return;
            Vector3 aim = target - upper.position;
            float length = Mathf.Clamp(aim.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            Vector3 direction = aim.sqrMagnitude > .00001f ? aim.normalized : upper.forward;
            Vector3 bend = Vector3.ProjectOnPlane(bendDirection, direction).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(upper.up, direction).normalized;
            float along = (a * a - b * b + length * length) / (2f * length);
            Vector3 elbow = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Quaternion upperGoal = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, upperGoal, weight);
            Quaternion lowerGoal = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerGoal, weight);
        }
    }
}
