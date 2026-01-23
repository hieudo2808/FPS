using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class Weapon : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject bullet;
        [SerializeField] private Transform bulletSpawnPoint;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private GameObject muzzleEffect;
        [SerializeField] private AudioClip reloadSound;
        [SerializeField] private Sprite weaponIcon;
        [Tooltip("Animator của FPS Arms (FirstPersonArms)")]
        [SerializeField] private Animator fpsArmsAnimator;

        [Header("Weapon Settings")]
        [SerializeField] private FireMode fireMode = FireMode.Single;
        [SerializeField] private float bulletSpeed = 200f;
        [SerializeField] private float bulletDamage = 25f;
        [SerializeField] private float fireRate = 0.1f;
        [SerializeField] private float bulletLiveTime = 2f;
        [SerializeField] private int burstCount = 3;

        [Header("Weapon Reloading")]
        [SerializeField] private float reloadTime = 1.5f;
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int totalAmmo = 120;
        
        [Header("Magazine Visuals")]
        [Tooltip("Băng đạn đang gắn trên súng")]
        [SerializeField] private GameObject magazineOnGun;
        [Tooltip("Băng đạn trong tay (khi rút ra)")]
        [SerializeField] private GameObject magazineInHand;
        
        private int currentAmmo;
        private int reservedAmmo;

        private bool canShoot = true;
        private bool isReloading = false;
        private Coroutine burstCoroutine;

        private enum FireMode { Single, Burst, Auto }

        public int CurrentAmmo => currentAmmo;
        public int ReservedAmmo => reservedAmmo;
        public Sprite WeaponIcon => weaponIcon;

        private bool isQ = false;

        private void Start()
        {
            currentAmmo = magazineSize;
            reservedAmmo = totalAmmo - currentAmmo;
        }

        private void OnEnable()
        {
            canShoot = true;
            isReloading = false;
            burstCoroutine = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isReloading = false;
            canShoot = true;
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Q))
            {
                Time.timeScale = isQ ? 1 : 0;
                isQ = !isQ;
            }

            if (canShoot && !isReloading) {
                if (currentAmmo <= 0 && reservedAmmo > 0)
                {
                    ReloadWeapon();
                }
                HandleFire();
            }
        }

        private void HandleFire()
        {
            if (isReloading) return;
            
            if (currentAmmo == 0 && reservedAmmo == 0)
            {
                canShoot = false;
            }

            if (Input.GetKey(KeyCode.R) && currentAmmo < magazineSize && reservedAmmo > 0 && !isReloading)
            {
                ReloadWeapon();
                return;
            }

            switch (fireMode)
            {
                case FireMode.Single:
                    if (Input.GetKeyDown(KeyCode.Mouse0) && canShoot && !isReloading)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Auto:
                    if (Input.GetKey(KeyCode.Mouse0) && canShoot && !isReloading)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Burst:
                    if (Input.GetKeyDown(KeyCode.Mouse0) && burstCoroutine == null && !isReloading)
                        burstCoroutine = StartCoroutine(FireBurst());
                    break;
            }
        }

        private IEnumerator ShootCooldown()
        {
            canShoot = false;
            FireBullet();
            yield return new WaitForSeconds(fireRate);
            canShoot = true;
        }

        private IEnumerator FireBurst()
        {
            canShoot = false;
            int bulletsLeft = burstCount;

            while (bulletsLeft > 0 && currentAmmo > 0)
            {
                FireBullet();
                bulletsLeft--;
                
                if (bulletsLeft > 0)
                    yield return new WaitForSeconds(fireRate);
            }

            yield return new WaitForSeconds(fireRate); 
            
            burstCoroutine = null;
            canShoot = true;
        }

        private void FireBullet()
        {
            muzzleEffect.GetComponent<ParticleSystem>().Play();

            if (gameObject.layer == LayerMask.NameToLayer("FirstPerson") || 
                gameObject.layer == LayerMask.NameToLayer("Weapon"))
            {
                Camera cam = Camera.main;
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                
                Vector3 targetPoint;
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    targetPoint = hit.point;
                }
                else
                {
                    targetPoint = ray.GetPoint(500f);
                }
                
                Vector3 shootDirection = (targetPoint - bulletSpawnPoint.position).normalized;
                
                GameObject bulletInstance = Instantiate(bullet, bulletSpawnPoint.position, 
                    Quaternion.LookRotation(shootDirection));
                currentAmmo--;
                
                Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
                if (bulletScript != null)
                {
                    bulletScript.SetDamage(bulletDamage);
                }
                
                Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
                rb.linearVelocity = shootDirection * bulletSpeed;

                AudioManager.Instance.PlaySFXSound(shootSound);
                Destroy(bulletInstance, bulletLiveTime);
            }
        }

        private void ReloadWeapon()
        {
            if (isReloading) return;
            
            canShoot = false;
            isReloading = true;

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            if (reloadSound != null)
            {
                AudioManager.Instance.PlaySFXSound(reloadSound);
            }

            WeaponManager.Instance.CharacterAnimation.SetTrigger("Reload");
            
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
            
            yield return new WaitForSeconds(reloadTime);
            ReloadCompleted();
        }

        private void ReloadCompleted()
        {
            int bulletsNeeded = magazineSize - currentAmmo;
            int bulletsToReload = Mathf.Min(bulletsNeeded, reservedAmmo);

            reservedAmmo -= bulletsToReload;
            currentAmmo += bulletsToReload;

            isReloading = false;
            canShoot = true;
            
            InsertMagazine();
        }
        
        public void GrabMagazine()
        {
            if (magazineOnGun != null) magazineOnGun.SetActive(false);
            if (magazineInHand != null) magazineInHand.SetActive(true);
        }
        
        public void InsertMagazine()
        {
            if (magazineOnGun != null) magazineOnGun.SetActive(true);
            if (magazineInHand != null) magazineInHand.SetActive(false);
        }
    }
}
