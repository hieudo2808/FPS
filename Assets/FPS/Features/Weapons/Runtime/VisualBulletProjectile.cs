using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Cosmetic projectile used to make an authoritative hitscan shot visible.
    /// Damage and collision remain owned by WeaponFireHandler; this component
    /// only moves the rendered bullet from the muzzle along the resolved shot.
    /// </summary>
    public sealed class VisualBulletProjectile : MonoBehaviour
    {
        [Tooltip("Mesh-local axis that points toward the bullet tip. The current weapon bullet meshes use -X.")]
        [SerializeField] private Vector3 localForwardAxis = Vector3.left;

        private ObjectPooling returnPool;
        private Vector3 travelDirection;
        private float speed;
        private float remainingLifetime;
        private bool isFlying;

        public Vector3 TravelDirection => travelDirection;
        public Vector3 LocalForwardAxis => localForwardAxis;
        public bool IsFlying => isFlying;

        public void Launch(
            Vector3 position,
            Vector3 direction,
            float travelSpeed,
            float lifetime,
            ObjectPooling pool)
        {
            travelDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            speed = Mathf.Max(0f, travelSpeed);
            remainingLifetime = Mathf.Max(0f, lifetime);
            returnPool = pool;

            Vector3 authoredForward = localForwardAxis.sqrMagnitude > 0.0001f
                ? localForwardAxis.normalized
                : Vector3.left;
            Vector3 stableUp = Mathf.Abs(Vector3.Dot(travelDirection, Vector3.up)) > 0.999f
                ? Vector3.forward
                : Vector3.up;
            Quaternion shotRotation = Quaternion.LookRotation(travelDirection, stableUp);
            Quaternion axisCorrection = Quaternion.FromToRotation(authoredForward, Vector3.forward);
            transform.SetPositionAndRotation(position, shotRotation * axisCorrection);

            isFlying = remainingLifetime > 0f;
            if (!isFlying)
                Release();
        }

        private void Update()
        {
            if (!isFlying)
                return;

            float deltaTime = Time.deltaTime;
            transform.position += travelDirection * (speed * deltaTime);
            remainingLifetime -= deltaTime;
            if (remainingLifetime <= 0f)
                Release();
        }

        private void OnDisable()
        {
            isFlying = false;
            remainingLifetime = 0f;
            returnPool = null;
        }

        private void Release()
        {
            if (!isFlying && remainingLifetime > 0f)
                return;

            isFlying = false;
            ObjectPooling pool = returnPool;
            returnPool = null;
            if (pool != null)
            {
                pool.ReturnObject(gameObject);
                return;
            }

            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (localForwardAxis.sqrMagnitude <= 0.0001f)
                localForwardAxis = Vector3.left;
        }
#endif
    }
}
