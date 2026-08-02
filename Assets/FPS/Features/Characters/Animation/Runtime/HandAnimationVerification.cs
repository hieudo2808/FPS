using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPS.Animation
{
    public sealed class HandAnimationVerification : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string label;
            public Animator animator;
            public string stateName;
            public string[] stateNames;
            public string expectedClipPrefix;
            public string probeBoneName = "L_Hand";

            [NonSerialized] public bool passed;
            [NonSerialized] public bool statePassed;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private int sampleFrames = 30;
        [SerializeField] private float minimumRotationDelta = 0.0001f;

        public bool IsComplete { get; private set; }
        public bool Passed { get; private set; }

        private IEnumerator Start()
        {
            Passed = true;
            IsComplete = false;
            yield return null;

            foreach (Entry entry in entries)
            {
                yield return VerifyEntry(entry);
                Passed &= entry != null && entry.passed;
            }

            IsComplete = true;
            Debug.Log(Passed
                ? "Hand animation verification passed for every configured hand."
                : "Hand animation verification failed for one or more configured hands.", this);
        }

        private IEnumerator VerifyEntry(Entry entry)
        {
            if (entry != null)
            {
                entry.passed = false;
            }

            if (entry == null || entry.animator == null)
            {
                Debug.LogError("Hand verification entry has no Animator.", this);
                yield break;
            }

            if (string.IsNullOrEmpty(entry.stateName))
            {
                Debug.LogError($"Hand verification entry '{entry.label}' has no state name.", entry.animator);
                yield break;
            }

            Transform probe = FindBone(entry.animator.transform, entry.probeBoneName);
            if (probe == null)
            {
                Debug.LogError($"Hand verification entry '{entry.label}' cannot find probe bone '{entry.probeBoneName}'.", entry.animator);
                yield break;
            }

            List<string> states = new List<string>();
            if (entry.stateNames != null)
            {
                for (int i = 0; i < entry.stateNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(entry.stateNames[i]) && !states.Contains(entry.stateNames[i]))
                    {
                        states.Add(entry.stateNames[i]);
                    }
                }
            }

            if (states.Count == 0 && !string.IsNullOrEmpty(entry.stateName))
            {
                states.Add(entry.stateName);
            }

            if (states.Count == 0)
            {
                Debug.LogError($"Hand verification entry '{entry.label}' has no states to verify.", entry.animator);
                yield break;
            }

            for (int i = 0; i < states.Count; i++)
            {
                string state = states[i];
                bool requireMovement = string.Equals(state, entry.stateName, StringComparison.Ordinal);
                yield return VerifyState(entry, probe, state, requireMovement);
                if (!entry.statePassed)
                {
                    yield break;
                }
            }

            entry.passed = true;
            Debug.Log($"Hand '{entry.label}' verified {states.Count} animation state(s) using controller '{entry.animator.runtimeAnimatorController?.name}'.", entry.animator);
        }

        private IEnumerator VerifyState(Entry entry, Transform probe, string stateName, bool requireMovement)
        {
            entry.statePassed = false;
            Quaternion initialRotation = probe.localRotation;
            Vector3 initialPosition = probe.localPosition;
            entry.animator.Rebind();
            entry.animator.Update(0f);
            entry.animator.Play(stateName, 0, 0f);
            entry.animator.Update(0f);

            bool hasState = entry.animator.HasState(0, Animator.StringToHash(stateName));
            if (!hasState)
            {
                Debug.LogError(
                    $"Hand verification entry '{entry.label}' cannot find state '{stateName}' on " +
                    $"Animator '{entry.animator.transform.name}' using controller '{entry.animator.runtimeAnimatorController?.name}'.",
                    entry.animator);
                yield break;
            }

            for (int frame = 0; frame < Mathf.Max(1, sampleFrames); frame++)
            {
                yield return null;
            }

            AnimatorClipInfo[] clipInfo = entry.animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo == null || clipInfo.Length == 0 || clipInfo[0].clip == null)
            {
                Debug.LogError(
                    $"Hand verification entry '{entry.label}' state '{stateName}' has no active animation clip. " +
                    $"Controller='{entry.animator.runtimeAnimatorController?.name}'.",
                    entry.animator);
                yield break;
            }

            string activeClipName = clipInfo[0].clip.name;
            if (!string.IsNullOrEmpty(entry.expectedClipPrefix) &&
                !activeClipName.StartsWith(entry.expectedClipPrefix, StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"Hand '{entry.label}' state '{stateName}' is playing '{activeClipName}' instead of a baked clip " +
                    $"with prefix '{entry.expectedClipPrefix}'.",
                    entry.animator);
                yield break;
            }

            float rotationDelta = Quaternion.Angle(initialRotation, probe.localRotation);
            float positionDelta = Vector3.Distance(initialPosition, probe.localPosition);
            bool moved = rotationDelta >= minimumRotationDelta || positionDelta >= minimumRotationDelta;
            if (requireMovement && !moved)
            {
                Debug.LogError(
                    $"Hand '{entry.label}' did not move bone '{entry.probeBoneName}' while playing '{stateName}'. " +
                    $"AnimatorRoot='{entry.animator.transform.name}', ProbePath='{RelativePath(entry.animator.transform, probe)}', " +
                    $"Controller='{entry.animator.runtimeAnimatorController?.name}', ActiveClip='{activeClipName}', " +
                    $"CurrentStateHash='{entry.animator.GetCurrentAnimatorStateInfo(0).fullPathHash}'.",
                    entry.animator);
                yield break;
            }

            entry.statePassed = true;
            Debug.Log(
                $"Hand '{entry.label}' verified state '{stateName}' with clip '{activeClipName}' " +
                $"(rotation delta {rotationDelta:F5} degrees, position delta {positionDelta:F5}).",
                entry.animator);
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, boneName, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static string RelativePath(Transform root, Transform transform)
        {
            if (transform == root)
            {
                return string.Empty;
            }

            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments.ToArray());
        }
    }
}
