using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class ColdLedgerHelicopterRig : MonoBehaviour
    {
        [SerializeField] private Transform mainRotor;
        [SerializeField] private Transform tailRotor;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float mainRotorDegreesPerSecond = 1800f;
        [SerializeField] private float tailRotorDegreesPerSecond = 2200f;
        [SerializeField] private float hoverAmplitude = 0.16f;
        [SerializeField] private float hoverFrequency = 0.7f;

        private Vector3 visualStartLocalPosition;

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;
            visualStartLocalPosition = visualRoot.localPosition;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            if (mainRotor != null)
                mainRotor.Rotate(Vector3.up, mainRotorDegreesPerSecond * delta, Space.Self);
            if (tailRotor != null)
                tailRotor.Rotate(Vector3.right, tailRotorDegreesPerSecond * delta, Space.Self);

            if (visualRoot != null && visualRoot != transform)
            {
                float hover = Mathf.Sin(Time.time * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
                visualRoot.localPosition = visualStartLocalPosition + Vector3.up * hover;
            }
        }

        public void Configure(Transform main, Transform tail, Transform visuals)
        {
            mainRotor = main;
            tailRotor = tail;
            visualRoot = visuals != null ? visuals : transform;
            visualStartLocalPosition = visualRoot.localPosition;
        }
    }
}
