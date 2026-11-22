using System;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// The third-person sidearm hold exactly as the game shows it while standing: local rotation of every arm,
    /// finger and arm-helper bone (from each clavicle down) captured from the character's own Classic idle,
    /// one entry per character controller. The downed pose copies these so both hands hold the gun the same
    /// way, then turns the shoulders onto the aim.
    /// </summary>
    [CreateAssetMenu(menuName = "FPS/Player Sidearm Hold Pose")]
    public sealed class PlayerSidearmHoldPose : ScriptableObject
    {
        [Serializable]
        public struct Bone
        {
            public string name;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        [Serializable]
        public struct Entry
        {
            [Tooltip("The character's Classic third-person controller the pose was captured from.")]
            public string controller;
            public Bone[] bones;
            public Vector3 barrelInRightHand;
            public Vector3 weaponPositionInRightHand;
            public Quaternion weaponRotationInRightHand;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Bone[] Find(string controller)
            => FindEntry(controller).bones;

        public Entry FindEntry(string controller)
        {
            foreach (var entry in entries)
                if (entry.controller == controller) return entry;
            return default;
        }
    }
}
