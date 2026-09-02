using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Editor
{
    [CustomEditor(typeof(WeaponData))]
    public sealed class WeaponDataEditor : UnityEditor.Editor
    {
        private static bool bakeQueued;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var data = (WeaponData)target;
            DrawMasterAnimatorSpeeds(data);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animator-derived timing (read only)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Fire Interval (seconds)", data.FireInterval);
                EditorGUILayout.FloatField("Rounds / Second", data.RoundsPerSecond);
                EditorGUILayout.FloatField("Equip Duration (seconds)", data.EquipDuration);
                EditorGUILayout.FloatField("Reload Duration (seconds)", data.ReloadDuration);
                if (data.reloadMode == ReloadMode.PerShell)
                {
                    EditorGUILayout.FloatField("Opening Duration (seconds)", data.PerShellOpeningDuration);
                    EditorGUILayout.FloatField("Per-shell Interval (seconds)", data.PerShellInterval);
                    EditorGUILayout.FloatField("Closing Duration (seconds)", data.PerShellClosingDuration);
                }
                else
                {
                    EditorGUILayout.FloatField("Ammo Commit Time (seconds)", data.ReloadAmmoCommitDuration);
                }
            }

            EditorGUILayout.HelpBox(
                "These values are baked from the master speeds above. Shared FPAnim states are synchronized automatically. Actions without a matching gun clip remain hand-authored in FPAnim.",
                MessageType.Info);
            if (GUILayout.Button("Bake Animation Timings"))
                QueueBake(data);
        }

        private static void DrawMasterAnimatorSpeeds(WeaponData data)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gun Animator master speeds", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Edit animation speed here while NOT in Play Mode. The value is written to the gun controller, FPAnim is synchronized, and gameplay timing is baked automatically. Do not edit the shared FPAnim speed directly.",
                MessageType.Info);

            bool playMode = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playMode)
                EditorGUILayout.HelpBox(
                    "Speed authoring is disabled in Play Mode because those edits are not a reliable persistent source.",
                    MessageType.Warning);

            bool changed = false;
            using (new EditorGUI.DisabledScope(playMode))
            {
                foreach (string stateName in new[] { "Equip", "Reload", "Inspect", "Fire" })
                {
                    bool gunMaster = WeaponAnimationTimingBaker.TryGetGunState(
                        data, stateName, out AnimatorController controller, out AnimatorState state);
                    if (!gunMaster && !WeaponAnimationTimingBaker.TryGetFirstPersonState(
                            data, stateName, out controller, out state))
                        continue;

                    float newSpeed = EditorGUILayout.DelayedFloatField(
                        gunMaster ? $"{stateName} Speed" : $"{stateName} Speed (FP only)",
                        state.speed);
                    newSpeed = Mathf.Max(0.01f, newSpeed);
                    if (Mathf.Abs(newSpeed - state.speed) <= 0.0001f)
                        continue;

                    Undo.RecordObject(controller, $"Change {data.name} {stateName} Speed");
                    Undo.RecordObject(state, $"Change {data.name} {stateName} Speed");
                    state.speed = newSpeed;
                    EditorUtility.SetDirty(state);
                    EditorUtility.SetDirty(controller);
                    changed = true;
                }
            }

            if (!changed)
                return;

            QueueBake(data);
        }

        private static void QueueBake(WeaponData context)
        {
            if (bakeQueued)
                return;

            bakeQueued = true;
            EditorApplication.delayCall += () =>
            {
                bakeQueued = false;
                if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                try
                {
                    WeaponAnimationTimingBaker.BakeAll(saveAssets: true);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[WeaponAnimationTimingBaker] {exception.Message}", context);
                }
            };
        }
    }
}
