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

        [Header("Weapon Data")]
        [SerializeField] private WeaponData weaponData;

        private RecoilController recoilController;
        private PlayerCombatTelemetry combatTelemetry;

        [Header("Bullet Pooling")]
        [Tooltip("Optional pool for bulletPrefab. Empty uses Instantiate/Destroy fallback.")]
        [SerializeField] private ObjectPooling bulletPool;

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
                recoilController = GetComponentInParent<RecoilController>();
                if (recoilController == null)
                    recoilController = FindFirstObjectByType<RecoilController>();

                combatTelemetry = GetComponentInParent<PlayerCombatTelemetry>();
                ReportCombatTelemetry();
            }
        }

        private void OnEnable()
        {
            canShoot    = weaponData != null;
            isReloading = false;
            burstCoroutine = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isReloading = false;
            canShoot    = true;
            ReportCombatTelemetry();
        }

        private void Update()
        {
            if (!isOwner) return;
            if (weaponData == null) return;

            if (canShoot && !isReloading)
            {
                if (currentAmmo <= 0 && reservedAmmo > 0)
                    ReloadWeapon();

                HandleFire();
            }
        }

        private void HandleFire()
        {
            if (weaponData == null) return;
            if (isReloading) return;

            if (currentAmmo == 0 && reservedAmmo == 0)
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
            if (weaponData == null)
                yield break;

            canShoot = false;
            FireBullet();
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
                FireBullet();
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
            if (currentAmmo <= 0) return;

            currentAmmo--;
            ReportCombatTelemetry();

            if (recoilController != null && weaponData.recoilPattern != null)
                recoilController.Fire(weaponData.recoilPattern);

            Camera cam = Camera.main;
            if (cam == null) return;

            PlayMuzzleEffect();
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 500f)
                ? hit.point
                : ray.GetPoint(500f);

            Vector3 spawnPos        = bulletSpawnPoint.position;
            Vector3 shootDirection  = (targetPoint - spawnPos).normalized;

            SpawnVisualBullet(spawnPos, shootDirection);
            var fireHandler = GetComponentInParent<WeaponFireHandler>();
            fireHandler?.RequestFireServerRpc(spawnPos, shootDirection);
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
            else
            {
                GameObject bulletInstance = Instantiate(weaponData.bulletPrefab, position, Quaternion.LookRotation(direction));
                Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.linearVelocity = direction * weaponData.bulletSpeed;

                Destroy(bulletInstance, weaponData.bulletLiveTime);
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

        public void PlayShootSound()
        {
            if (weaponData == null) return;
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);
        }

        private void ReloadWeapon()
        {
            if (weaponData == null) return;
            if (isReloading) return;

            GetComponentInParent<WeaponFireHandler>()?.RequestReloadServerRpc();

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

            var weaponManager = GetComponentInParent<WeaponManager>();
            weaponManager?.TriggerAnimation("Reload");

            if (fpsArmsAnimator != null)
            {
                fpsArmsAnimator.SetTrigger("Reload");

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
        }

        public void AddReserveAmmo(int amount)
        {
            reservedAmmo += amount;
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
    }
}
