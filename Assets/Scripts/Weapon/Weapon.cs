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
        [Tooltip("Animator của FPS Arms (FirstPersonArms)")]
        [SerializeField] private Animator fpsArmsAnimator;

        [Header("Weapon Data")]
        [SerializeField] private WeaponData weaponData;

        private RecoilController recoilController;

        [Header("Magazine Visuals")]
        [Tooltip("Băng đạn đang gắn trên súng")]
        [SerializeField] private GameObject magazineOnGun;
        [Tooltip("Băng đạn trong tay (khi rút ra)")]
        [SerializeField] private GameObject magazineInHand;

        private int currentAmmo;
        private int reservedAmmo;

        private bool canShoot = true;
        private bool isReloading = false;
        private bool isOwner = false;
        private Coroutine burstCoroutine;

        public int CurrentAmmo => currentAmmo;
        public int ReservedAmmo => reservedAmmo;
        public Sprite WeaponIcon => weaponData.weaponIcon;
        public WeaponData Data => weaponData;

        private void Start()
        {
            currentAmmo  = weaponData.magazineSize;
            reservedAmmo = weaponData.totalAmmo - currentAmmo;
        }

        /// <summary>
        /// Gọi bởi WeaponManager.OnNetworkSpawn() — tránh race condition Start() chạy trước OnNetworkSpawn().
        /// </summary>
        public void SetOwner(bool owner)
        {
            isOwner = owner;
            if (isOwner)
            {
                recoilController = GetComponentInParent<RecoilController>();
                if (recoilController == null)
                    recoilController = FindFirstObjectByType<RecoilController>();
            }
        }

        private void OnEnable()
        {
            canShoot    = true;
            isReloading = false;
            burstCoroutine = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isReloading = false;
            canShoot    = true;
        }

        private void Update()
        {
            if (!isOwner) return;

            if (canShoot && !isReloading)
            {
                if (currentAmmo <= 0 && reservedAmmo > 0)
                    ReloadWeapon();

                HandleFire();
            }
        }

        private void HandleFire()
        {
            if (isReloading) return;

            if (currentAmmo == 0 && reservedAmmo == 0)
            {
                canShoot = false;
                return;
            }

            if (Input.GetKey(KeyCode.R) && currentAmmo < weaponData.magazineSize && reservedAmmo > 0 && !isReloading)
            {
                ReloadWeapon();
                return;
            }

            switch (weaponData.fireMode)
            {
                case FireMode.Single:
                    if (Input.GetKeyDown(KeyCode.Mouse0) && canShoot)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Auto:
                    if (Input.GetKey(KeyCode.Mouse0) && canShoot)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Burst:
                    if (Input.GetKeyDown(KeyCode.Mouse0) && burstCoroutine == null)
                        burstCoroutine = StartCoroutine(FireBurst());
                    break;
            }
        }

        private IEnumerator ShootCooldown()
        {
            canShoot = false;
            FireBullet();
            yield return new WaitForSeconds(weaponData.fireRate);
            canShoot = true;
        }

        private IEnumerator FireBurst()
        {
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
            if (currentAmmo <= 0) return;

            currentAmmo--;

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
            WeaponManager.LocalInstance?.RequestFireServerRpc(spawnPos, shootDirection);
        }

        public void SpawnVisualBullet(Vector3 position, Vector3 direction)
        {
            if (weaponData.bulletPrefab == null) return;

            GameObject bulletInstance = Instantiate(weaponData.bulletPrefab, position, Quaternion.LookRotation(direction));
            Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = direction * weaponData.bulletSpeed;

            Destroy(bulletInstance, weaponData.bulletLiveTime);
        }

        public void PlayMuzzleEffect()
        {
            if (muzzleEffect == null) return;
            var ps = muzzleEffect.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        public void PlayShootSound()
        {
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);
        }

        private void ReloadWeapon()
        {
            if (isReloading) return;

            canShoot    = false;
            isReloading = true;

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
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
            int bulletsNeeded   = weaponData.magazineSize - currentAmmo;
            int bulletsToReload = Mathf.Min(bulletsNeeded, reservedAmmo);

            reservedAmmo -= bulletsToReload;
            currentAmmo  += bulletsToReload;

            isReloading = false;
            canShoot    = true;

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
        }
    }
}