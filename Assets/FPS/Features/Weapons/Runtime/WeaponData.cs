using UnityEngine;
using UnityEngine.Serialization;

namespace FPS
{
    public enum FireMode { Single, Burst, Auto }
    public enum ReloadMode { Magazine, PerShell }

    [CreateAssetMenu(menuName = "FPS/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName;
        public Sprite weaponIcon;

        [Header("Combat")]
        public float damage = 25f;
        public DamageType damageType = DamageType.Bullet;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
        public float bulletSpeed = 200f;
        [FormerlySerializedAs("fireRate")]
        [SerializeField, HideInInspector, Min(0.001f)] private float bakedFireInterval = 0.1f;
        public float bulletLiveTime = 2f;
        public int burstCount = 3;
        public FireMode fireMode = FireMode.Single;
        [Min(1)] public int projectileCount = 1;
        [Min(0f), Tooltip("Hip-fire cone half-angle in degrees.")]
        public float hipSpreadAngle;
        [Min(0f), Tooltip("Aimed cone half-angle in degrees. Used only by weapons that support aim.")]
        public float aimedSpreadAngle;
        [Min(0.01f)] public float maximumRange = 100f;
        [Min(0f)] public float falloffStartDistance = 100f;
        [Min(0f)] public float falloffEndDistance = 100f;
        [Range(0f, 1f)] public float minimumDamageMultiplier = 1f;
        [Tooltip("Per-shot restarts suit recoil clips. Disable for continuous automatic weapon cycles such as Odin feed/ejection.")]
        public bool restartFireAnimationPerShot = true;
        [Min(0)]
        [Tooltip("First source-clip frame of the continuous Fire loop, as shown in Unity's Animation window.")]
        public int fireLoopStartFrame;
        [Min(0)]
        [Tooltip("Frame that rewinds continuous Fire back to Fire Loop Start Frame.")]
        public int fireLoopEndFrame;

        public float FireInterval => Mathf.Max(0.001f, bakedFireInterval);
        public float RoundsPerSecond => 1f / FireInterval;

        public float GetSpreadAngle(bool aimed)
        {
            return Mathf.Max(0f, supportsAim && aimed ? aimedSpreadAngle : hipSpreadAngle);
        }

        public float EvaluateDamageMultiplier(float distance)
        {
            float clampedDistance = Mathf.Max(0f, distance);
            float start = Mathf.Clamp(falloffStartDistance, 0f, maximumRange);
            float end = Mathf.Clamp(falloffEndDistance, start, maximumRange);
            if (clampedDistance <= start || end <= start)
                return 1f;
            if (clampedDistance >= end)
                return Mathf.Clamp01(minimumDamageMultiplier);

            return Mathf.Lerp(1f, Mathf.Clamp01(minimumDamageMultiplier),
                Mathf.InverseLerp(start, end, clampedDistance));
        }

        public float EvaluateBaseDamage(float distance)
        {
            return Mathf.Max(0f, damage) * EvaluateDamageMultiplier(distance);
        }

        [Header("Ammo")]
        public int magazineSize = 30;
        public int totalAmmo = 120;
        [Tooltip("Magazine fills at once; PerShell inserts one round and repeats Reload until full.")]
        public ReloadMode reloadMode = ReloadMode.Magazine;
        [Min(0)]
        [Tooltip("Magazine reload only: source-clip frame at which the new magazine is seated and ammo is committed. This is a frame number, not seconds.")]
        public int reloadAmmoCommitFrame;
        [Min(0)]
        [Tooltip("First source-clip frame of the per-shell insert loop, as shown in Unity's Animation window.")]
        public int reloadLoopStartFrame;
        [Min(0)]
        [Tooltip("Frame that rewinds per-shell Reload back to Reload Loop Start Frame.")]
        public int reloadLoopEndFrame;

        [FormerlySerializedAs("reloadTime")]
        [SerializeField, HideInInspector, Min(0f)] private float bakedReloadDuration = 1.5f;
        [SerializeField, HideInInspector, Min(0f)] private float bakedReloadAmmoCommitDuration = 1.5f;
        [SerializeField, HideInInspector, Min(0f)] private float bakedPerShellOpeningDuration;
        [SerializeField, HideInInspector, Min(0f)] private float bakedPerShellInterval = 0.25f;
        [SerializeField, HideInInspector, Min(0f)] private float bakedPerShellClosingDuration;

        public float ReloadDuration => Mathf.Max(0f, bakedReloadDuration);
        public float ReloadAmmoCommitDuration => Mathf.Clamp(
            bakedReloadAmmoCommitDuration, 0f, ReloadDuration);
        public float PerShellOpeningDuration => reloadMode == ReloadMode.PerShell
            ? Mathf.Max(0f, bakedPerShellOpeningDuration)
            : 0f;
        public float PerShellInterval => reloadMode == ReloadMode.PerShell
            ? Mathf.Max(0f, bakedPerShellInterval)
            : 0f;
        public float PerShellClosingDuration => reloadMode == ReloadMode.PerShell
            ? Mathf.Max(0f, bakedPerShellClosingDuration)
            : 0f;

        public int GetPerShellRoundsToLoad(int currentMagazineAmmo, int currentReserveAmmo)
        {
            if (reloadMode != ReloadMode.PerShell)
                return 0;

            int missingRounds = Mathf.Max(0, magazineSize - currentMagazineAmmo);
            return Mathf.Min(missingRounds, Mathf.Max(0, currentReserveAmmo));
        }

        [Header("First-Person Presentation")]
        [Tooltip("Animator layer name used by the shared first-person arms controller.")]
        public string firstPersonAnimatorLayer;
        [FormerlySerializedAs("equipTime")]
        [SerializeField, HideInInspector, Min(0f)] private float bakedEquipDuration = 0.75f;

        public float EquipDuration => Mathf.Max(0f, bakedEquipDuration);

        [Header("Aim / Scope")]
        public bool supportsAim;
        [Range(1f, 179f)] public float aimedWorldFov = 25f;
        [Min(0.01f)] public float aimTransitionDuration = 0.12f;
        [Range(0.01f, 1f)] public float aimedSensitivityMultiplier = 0.65f;
        [Tooltip("Show the full-screen sniper scope HUD when physical ADS finishes.")]
        public bool showScopeOverlay;
        [Tooltip("Transparent scope housing and reticle shown after physical ADS aligns with the camera.")]
        public Sprite scopeOverlaySprite;
        [Tooltip("Lower the weapon immediately after a shot so the authored Fire/bolt animation remains visible.")]
        public bool exitAimAfterShot;
        [Tooltip("Legacy serialized setting. Physical ADS keeps the WeaponCamera and viewmodel visible.")]
        public bool hideViewmodelWhenAimed;

        public void ApplyBakedFireInterval(float fireInterval)
        {
            bakedFireInterval = Mathf.Max(0.001f, fireInterval);
        }

        public void ApplyBakedAnimationTimings(
            float equipDuration,
            float reloadDuration,
            float reloadAmmoCommitDuration,
            float perShellOpeningDuration,
            float perShellInterval,
            float perShellClosingDuration)
        {
            bakedEquipDuration = Mathf.Max(0f, equipDuration);
            bakedReloadDuration = Mathf.Max(0f, reloadDuration);
            bakedReloadAmmoCommitDuration = Mathf.Clamp(
                reloadAmmoCommitDuration, 0f, bakedReloadDuration);
            bakedPerShellOpeningDuration = Mathf.Max(0f, perShellOpeningDuration);
            bakedPerShellInterval = Mathf.Max(0f, perShellInterval);
            bakedPerShellClosingDuration = Mathf.Max(0f, perShellClosingDuration);
        }

        private void OnValidate()
        {
            projectileCount = Mathf.Max(1, projectileCount);
            maximumRange = Mathf.Max(0.01f, maximumRange);
            falloffStartDistance = Mathf.Clamp(falloffStartDistance, 0f, maximumRange);
            falloffEndDistance = Mathf.Clamp(falloffEndDistance, falloffStartDistance, maximumRange);
            minimumDamageMultiplier = Mathf.Clamp01(minimumDamageMultiplier);
            hipSpreadAngle = Mathf.Max(0f, hipSpreadAngle);
            aimedSpreadAngle = Mathf.Max(0f, aimedSpreadAngle);
            aimTransitionDuration = Mathf.Max(0.01f, aimTransitionDuration);
            aimedSensitivityMultiplier = Mathf.Clamp(aimedSensitivityMultiplier, 0.01f, 1f);
            muzzleFlashScale = Mathf.Max(0.01f, muzzleFlashScale);
            maxConcurrentSurfaceImpacts = Mathf.Max(1, maxConcurrentSurfaceImpacts);
        }

        [Header("Assets")]
        public GameObject bulletPrefab;
        public AudioClip shootSound;
        public AudioClip reloadSound;
        public RecoilPattern recoilPattern;

        [Header("Shot Feedback")]
        [Tooltip("Reusable muzzle flash instantiated once under the weapon's real MuzzlePoint.")]
        public GameObject muzzleFlashPrefab;
        [Tooltip("Local rotation applied under MuzzlePoint. The shared flash mesh points along local -X.")]
        public Vector3 muzzleFlashLocalEulerAngles = new Vector3(0f, 270f, 0f);
        [Min(0.01f)] public float muzzleFlashScale = 2.5f;
        [Tooltip("Cosmetic world-surface impact containing particles and a temporary bullet-hole decal.")]
        public GameObject surfaceImpactPrefab;
        [Min(1), Tooltip("Per weapon/client cap. Oldest cosmetic impacts are removed first.")]
        public int maxConcurrentSurfaceImpacts = 64;
    }
}
