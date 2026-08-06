using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class Weapon : MonoBehaviour
    {
        [Header("Instance References")]
        [SerializeField] private Transform bulletSpawnPoint;
        [SerializeField] private GameObject muzzleEffect;
        [Tooltip("Animator for FPS arms (FirstPersonArms).")]
        [SerializeField] private Animator fpsArmsAnimator;
        [Tooltip("Animator on the weapon model itself.")]
        [SerializeField] private Animator weaponAnimator;

        [Header("Weapon Data")]
        [SerializeField] private WeaponData weaponData;

        private RecoilController recoilController;
        private PlayerCombatTelemetry combatTelemetry;
        private WeaponFireHandler cachedFireHandler;
        private WeaponManager cachedWeaponManager;
        private Camera cachedCamera;
        private PlayerMovement cachedPlayerMovement;
        private ushort fireSequence;

        [Header("Bullet Pooling")]
        [Tooltip("Optional pool for bulletPrefab. Empty uses Instantiate/Destroy fallback.")]
        [SerializeField] private ObjectPooling bulletPool;
        [SerializeField] private bool allowInstantiateFallbackInDevelopment = true;

        [Header("Magazine Visuals")]
        [Tooltip("Magazine currently attached to the gun.")]
        [SerializeField] private GameObject magazineOnGun;
        [Tooltip("Magazine shown in hand during reload.")]
        [SerializeField] private GameObject magazineInHand;

        private int currentAmmo;
        private int reservedAmmo;

        private bool canShoot = true;
        private bool isReloading = false;
        private bool isOwner = false;
        private Coroutine burstCoroutine;
        // Fire requests are server-authoritative. Keep a small local reservation
        // so holding the trigger cannot visually overrun the magazine while the
        // acknowledgement is in flight, but do not mutate authoritative ammo
        // until WeaponOwnerState confirms the shot.
        private int pendingServerShots;

        public int CurrentAmmo => currentAmmo;
        public int ReservedAmmo => reservedAmmo;
        public Sprite WeaponIcon => weaponData != null ? weaponData.weaponIcon : null;
        public WeaponData Data => weaponData;

        private void Start()
        {
            if (weaponData == null)
            {
                currentAmmo = 0;
                reservedAmmo = 0;
                canShoot = false;
                ReportCombatTelemetry();
                return;
            }

            currentAmmo  = weaponData.magazineSize;
            reservedAmmo = weaponData.totalAmmo - currentAmmo;
            ReportCombatTelemetry();
        }

        /// <summary>
        /// Called by WeaponManager.OnNetworkSpawn() to avoid Start()/OnNetworkSpawn race conditions.
        /// </summary>
        public void SetOwner(bool owner)
        {
            isOwner = owner;
            if (isOwner)
            {
                CacheOwnerDependencies();
                ReportCombatTelemetry();
            }
        }

        private void OnEnable()
        {
            canShoot    = weaponData != null;
            isReloading = false;
            burstCoroutine = null;
            pendingServerShots = 0;

            if (isOwner)
                CacheOwnerDependencies();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isReloading = false;
            canShoot    = true;
            pendingServerShots = 0;
            ReportCombatTelemetry();
        }

        private void Update()
        {
            if (!isOwner) return;
            if (weaponData == null) return;

            if (canShoot && !isReloading)
            {
                if (pendingServerShots == 0 && currentAmmo <= 0 && reservedAmmo > 0)
                    ReloadWeapon();

                HandleFire();
            }
        }

        private void HandleFire()
        {
            if (weaponData == null) return;
            if (isReloading) return;
            if (!NetworkMatchStateManager.IsGameplayActive) return;

            if (currentAmmo - pendingServerShots <= 0 && reservedAmmo == 0)
            {
                canShoot = false;
                return;
            }

            if (InputManager.Instance != null && InputManager.Instance.GetReloadInput() && currentAmmo < weaponData.magazineSize && reservedAmmo > 0 && !isReloading)
            {
                ReloadWeapon();
                return;
            }

            switch (weaponData.fireMode)
            {
                case FireMode.Single:
                    if (InputManager.Instance != null && InputManager.Instance.GetFireInputDown() && canShoot)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Auto:
                    if (InputManager.Instance != null && InputManager.Instance.GetFireInput() && canShoot)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Burst:
                    if (InputManager.Instance != null && InputManager.Instance.GetFireInputDown() && burstCoroutine == null)
                        burstCoroutine = StartCoroutine(FireBurst());
                    break;
            }
        }

        private IEnumerator ShootCooldown()
        {
            if (weaponData == null || currentAmmo <= 0)
            {
                canShoot = true;
                yield break;
            }

            canShoot = false;
            try
            {
                FireBullet();
            }
            catch (System.Exception ex)
            {
                GameLog.Error($"[Weapon] Exception during FireBullet: {ex.Message}\n{ex.StackTrace}");
            }

            yield return new WaitForSeconds(weaponData.fireRate);
            canShoot = true;
        }

        private IEnumerator FireBurst()
        {
            if (weaponData == null)
                yield break;

            canShoot = false;
            int bulletsLeft = weaponData.burstCount;

            while (bulletsLeft > 0 && currentAmmo > 0)
            {
                try
                {
                    FireBullet();
                }
                catch (System.Exception ex)
                {
                    GameLog.Error($"[Weapon] Exception during FireBurst bullet: {ex.Message}\n{ex.StackTrace}");
                }
                bulletsLeft--;

                if (bulletsLeft > 0)
                    yield return new WaitForSeconds(weaponData.fireRate);
            }

            yield return new WaitForSeconds(weaponData.fireRate);
            burstCoroutine = null;
            canShoot = true;
        }

        private void FireBullet()
        {
            if (weaponData == null) return;
            if (currentAmmo - pendingServerShots <= 0) return;

            PlayerMovement movement = cachedFireHandler != null
                ? cachedFireHandler.GetComponent<PlayerMovement>()
                : null;
            if (movement == null || !movement.TryGetConfirmedFireReference(
                    out uint inputSequence, out int serverTick))
            {
                return;
            }

            if (recoilController != null && weaponData.recoilPattern != null)
                recoilController.Fire(weaponData.recoilPattern);

            Camera cam = ResolveAimCamera();
            if (cam == null) return;

            PlayMuzzleEffect();
            TriggerAnimation("Fire");
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 targetPoint = Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    500f,
                    GetVisualHitMask(),
                    QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(500f);

            Vector3 spawnPos        = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
            Vector3 shootDirection  = (targetPoint - spawnPos).normalized;
            ushort sequence = unchecked(++fireSequence);

            SpawnVisualBullet(spawnPos, shootDirection);

            bool networkRequest = cachedFireHandler != null
                && cachedFireHandler.IsSpawned
                && cachedFireHandler.NetworkManager != null
                && cachedFireHandler.NetworkManager.IsListening;
            if (networkRequest)
            {
                pendingServerShots++;
                cachedFireHandler.RequestFireServerRpc(new FireCommand
                {
                    sequence = sequence,
                    estimatedServerTick = serverTick,
                    inputSequence = inputSequence,
                    weaponSlot = (byte)Mathf.Clamp(cachedWeaponManager != null ? cachedWeaponManager.CurrentWeaponIndex : 0, 0, byte.MaxValue),
                });
            }
            else
            {
                // Offline/editor fallback has no server to acknowledge the shot.
                currentAmmo = Mathf.Max(0, currentAmmo - 1);
                ReportCombatTelemetry();
                if (HUDManager.HasInstance)
                    HUDManager.Instance.UpdateAmmoInfo();
            }
        }

        public void SpawnVisualBullet(Vector3 position, Vector3 direction)
        {
            if (weaponData == null || weaponData.bulletPrefab == null) return;

            if (bulletPool != null)
            {
                // Use pool when available; otherwise fall back to Instantiate/Destroy.
                GameObject bulletInstance = bulletPool.GetObject();
                if (bulletInstance == null) return;

                bulletInstance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
                Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.linearVelocity = direction * weaponData.bulletSpeed;
                }

                StartCoroutine(ReturnBulletToPool(bulletInstance, weaponData.bulletLiveTime));
            }
            else if (CanUseInstantiateFallback())
            {
                GameLog.Warning(() => $"[Weapon] Bullet pool is missing for {name}; using editor/development Instantiate fallback.");
                GameObject bulletInstance = Instantiate(weaponData.bulletPrefab, position, Quaternion.LookRotation(direction));
                Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.linearVelocity = direction * weaponData.bulletSpeed;

                Destroy(bulletInstance, weaponData.bulletLiveTime);
            }
            else
            {
                GameLog.Warning(() => $"[Weapon] Bullet pool is missing for {name}; visual bullet skipped in release path.");
            }
        }

        private IEnumerator ReturnBulletToPool(GameObject bullet, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (bulletPool != null)
                bulletPool.ReturnObject(bullet);
            else if (bullet != null)
                Destroy(bullet);
        }

        public void PlayMuzzleEffect()
        {
            if (muzzleEffect == null) return;
            var ps = muzzleEffect.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        private void TriggerAnimation(string triggerName)
        {
            weaponAnimator?.SetTrigger(triggerName);
            fpsArmsAnimator?.SetTrigger(triggerName);
        }

        public void PlayShootSound()
        {
            if (weaponData == null) return;
            TriggerAnimation("Fire");
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);
        }

        private void ReloadWeapon()
        {
            if (weaponData == null) return;
            if (isReloading) return;

            if (cachedFireHandler != null && cachedFireHandler.IsSpawned && cachedFireHandler.NetworkManager != null && cachedFireHandler.NetworkManager.IsListening)
            {
                cachedFireHandler.RequestReloadServerRpc();
            }

            canShoot    = false;
            isReloading = true;
            ReportCombatTelemetry();

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            if (weaponData == null)
                yield break;

            if (weaponData.reloadSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.reloadSound);

            cachedWeaponManager?.TriggerAnimation("Reload");
            TriggerAnimation("Reload");

            if (fpsArmsAnimator != null)
            {
                float timeout = 0.5f;
                float elapsed = 0f;
                while (elapsed < timeout)
                {
                    AnimatorStateInfo stateInfo = fpsArmsAnimator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.IsName("Reload"))
                    {
                        yield return new WaitForSeconds(stateInfo.length);
                        ReloadCompleted();
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            yield return new WaitForSeconds(weaponData.reloadTime);
            ReloadCompleted();
        }

        private void ReloadCompleted()
        {
            if (weaponData == null) return;

            int bulletsNeeded   = weaponData.magazineSize - currentAmmo;
            int bulletsToReload = Mathf.Min(bulletsNeeded, reservedAmmo);

            reservedAmmo -= bulletsToReload;
            currentAmmo  += bulletsToReload;

            isReloading = false;
            canShoot    = true;
            ReportCombatTelemetry();

            InsertMagazine();
        }

        public void GrabMagazine()
        {
            if (magazineOnGun != null)  magazineOnGun.SetActive(false);
            if (magazineInHand != null) magazineInHand.SetActive(true);
        }

        public void InsertMagazine()
        {
            if (magazineOnGun != null)  magazineOnGun.SetActive(true);
            if (magazineInHand != null) magazineInHand.SetActive(false);
            TriggerAnimation("Equip");
        }

        public void AddReserveAmmo(int amount)
        {
            reservedAmmo += amount;
            ReportCombatTelemetry();
        }

        public void SetLocalAmmoState(int magazineAmmo, int reserveAmmo, bool reloading)
        {
            pendingServerShots = 0;
            currentAmmo = Mathf.Max(0, magazineAmmo);
            reservedAmmo = Mathf.Max(0, reserveAmmo);
            isReloading = reloading;
            // Network reconciliation owns ammo/reload state only.  The local
            // fire gate is owned by ShootCooldown/FireBurst; resetting it here
            // lets every server response bypass weaponData.fireRate.
            ReportCombatTelemetry();
        }

        public void ReportCombatTelemetry()
        {
            if (!isOwner) return;
            if (weaponData == null) return;

            if (combatTelemetry == null)
                combatTelemetry = GetComponentInParent<PlayerCombatTelemetry>();

            combatTelemetry?.ReportWeaponState(isReloading, currentAmmo, weaponData.magazineSize);
        }

        private void CacheOwnerDependencies()
        {
            recoilController = GetComponentInParent<RecoilController>();
            if (recoilController == null)
                recoilController = FindAnyObjectByType<RecoilController>();

            cachedFireHandler = GetComponentInParent<WeaponFireHandler>();
            cachedWeaponManager = GetComponentInParent<WeaponManager>();
            cachedPlayerMovement = GetComponentInParent<PlayerMovement>();
            cachedCamera = Camera.main;
            combatTelemetry = GetComponentInParent<PlayerCombatTelemetry>();
        }

        private Camera ResolveAimCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
                return cachedCamera;

            cachedCamera = Camera.main;
            return cachedCamera;
        }

        private int GetVisualHitMask()
        {
            if (weaponData != null && weaponData.hitMask.value != 0)
                return weaponData.hitMask.value;

            return Physics.DefaultRaycastLayers;
        }

        private bool CanUseInstantiateFallback()
        {
            return allowInstantiateFallbackInDevelopment && (Application.isEditor || Debug.isDebugBuild);
        }

    }
}
