using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Editor
{
    [CreateAssetMenu(
        fileName = "HandAnimationRetargetProfile",
        menuName = "FPS/Animation/Hand Retarget Profile")]
    public sealed class HandAnimationRetargetProfile : ScriptableObject
    {
        [HideInInspector]
        [Tooltip("Legacy single-source field. Use Source Models for new profiles.")]
        public GameObject sourceModel;

        [Tooltip("FBX files that contain the source animation clips. They must use compatible source skeletons.")]
        public List<GameObject> sourceModels = new List<GameObject>();

        [Tooltip("Animator Controller whose states will be overridden with the generated clips.")]
        public AnimatorController baseController;

        [Tooltip("Scene used by the verification menu command. This is not required for baking.")]
        public SceneAsset testScene;

        [Tooltip("Add one entry per hand rig. The model and prefab can be dragged here from the Project window.")]
        public List<Target> targets = new List<Target>();

        [Serializable]
        public sealed class Target
        {
            public string characterName;
            public GameObject handModel;
            public GameObject prefab;
            public string probeBoneName = "L_Hand";
        }
    }
}
