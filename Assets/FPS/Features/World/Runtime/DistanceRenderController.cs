using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class DistanceRenderController : MonoBehaviour
    {
        [SerializeField] private DistanceRenderSettings settings;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform viewer;
        [SerializeField] private bool autoFindTargetsOnEnable = true;

        private readonly List<DistanceRenderTarget> targets = new List<DistanceRenderTarget>();
        private float nextUpdateTime;
        private DistanceRenderSettings runtimeDefaultSettings;
        private bool cullDistancesApplied;

        public int TargetCount => targets.Count;
        private DistanceRenderSettings ActiveSettings => settings != null ? settings : runtimeDefaultSettings;

        private void OnEnable()
        {
            if (settings == null && runtimeDefaultSettings == null)
                runtimeDefaultSettings = ScriptableObject.CreateInstance<DistanceRenderSettings>();

            ResolveViewer();

            if (autoFindTargetsOnEnable)
                RefreshTargets();

            ApplyCameraLayerCullDistances();
        }

        private void Update()
        {
            DistanceRenderSettings activeSettings = ActiveSettings;
            if (activeSettings == null)
                return;

            if (viewer == null || targetCamera == null)
                ResolveViewer();
            if (viewer == null)
                return;

            if (!cullDistancesApplied && targetCamera != null)
                ApplyCameraLayerCullDistances();

            if (Time.unscaledTime < nextUpdateTime)
                return;

            nextUpdateTime = Time.unscaledTime + activeSettings.UpdateInterval;
            UpdateTargets();
        }

        public void RefreshTargets()
        {
            targets.Clear();

            DistanceRenderTarget[] foundTargets = FindObjectsByType<DistanceRenderTarget>(
                FindObjectsInactive.Exclude);

            for (int i = 0; i < foundTargets.Length; i++)
            {
                if (foundTargets[i] != null)
                    targets.Add(foundTargets[i]);
            }
        }

        public void RegisterTarget(DistanceRenderTarget target)
        {
            if (target != null && !targets.Contains(target))
                targets.Add(target);
        }

        public void UnregisterTarget(DistanceRenderTarget target)
        {
            targets.Remove(target);
        }

        public void UpdateTargets()
        {
            DistanceRenderSettings activeSettings = ActiveSettings;
            if (activeSettings == null || viewer == null)
                return;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                DistanceRenderTarget target = targets[i];
                if (target == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(viewer.position, target.transform.position);
                DistanceRenderBucket nextBucket = activeSettings.EvaluateBucket(distance, target.CurrentBucket);
                target.ApplyBucket(nextBucket, activeSettings);
            }
        }

        public void ApplyCameraLayerCullDistances()
        {
            DistanceRenderSettings activeSettings = ActiveSettings;
            if (activeSettings == null || targetCamera == null)
                return;

            LayerCullDistance[] configuredDistances = activeSettings.LayerCullDistances;
            if (configuredDistances == null || configuredDistances.Length == 0)
                return;

            float[] distances = targetCamera.layerCullDistances;
            if (distances == null || distances.Length != 32)
                distances = new float[32];

            for (int i = 0; i < configuredDistances.Length; i++)
            {
                string layerName = configuredDistances[i].layerName;
                if (string.IsNullOrWhiteSpace(layerName))
                    continue;

                int layer = LayerMask.NameToLayer(layerName);
                if (layer < 0 || layer >= distances.Length)
                    continue;

                distances[layer] = Mathf.Max(0f, configuredDistances[i].distance);
            }

            targetCamera.layerCullDistances = distances;
            cullDistancesApplied = true;
        }

        public void Configure(
            DistanceRenderSettings renderSettings,
            Camera camera = null,
            Transform viewerTransform = null)
        {
            settings = renderSettings;
            targetCamera = camera;
            viewer = viewerTransform;
            cullDistancesApplied = false;
            ResolveViewer();
            ApplyCameraLayerCullDistances();
        }

        private void ResolveViewer()
        {
            if (targetCamera == null)
            {
                Camera localCamera = GetComponent<Camera>();
                targetCamera = localCamera != null ? localCamera : Camera.main;
                cullDistancesApplied = false;
            }

            if (viewer == null && targetCamera != null)
                viewer = targetCamera.transform;
        }
    }
}
