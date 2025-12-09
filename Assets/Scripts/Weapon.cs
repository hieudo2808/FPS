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

        [Header("Weapon Settings")]
        [SerializeField] private FireMode fireMode = FireMode.Single;
        [SerializeField] private float bulletSpeed = 200f;
        [SerializeField] private float fireRate = 0.1f;
        [SerializeField] private float bulletLiveTime = 2f;
        [SerializeField] private int burstCount = 3;

        [Header("Weapon Reloading")]
        [SerializeField] private float reloadTime = 1.5f;
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int totalAmmo = 120;
        private int currentAmmo;
        private int reservedAmmo;

        private bool canShoot = true;
        private Coroutine burstCoroutine;

        private enum FireMode { Single, Burst, Auto }

        public int CurrentAmmo => currentAmmo;
        public int ReservedAmmo => reservedAmmo;
        public Sprite WeaponIcon => weaponIcon;

        private void Start()
        {
            currentAmmo = magazineSize;
            reservedAmmo = totalAmmo - currentAmmo;
        }

        private void Update()
        {
            if (canShoot) {
                if (currentAmmo <= 0 && reservedAmmo > 0)
                {
                    ReloadWeapon();
                }
                HandleFire();
            }
        }

        private void HandleFire()
        {
            if (currentAmmo == 0 && reservedAmmo == 0)
            {
                canShoot = false;
            }

            if (Input.GetKey(KeyCode.R) && currentAmmo < magazineSize && reservedAmmo > 0)
            {
                ReloadWeapon();
            }

            switch (fireMode)
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
            yield return new WaitForSeconds(fireRate);
            canShoot = true;
        }

        private IEnumerator FireBurst()
        {
            int bulletsLeft = burstCount;

            while (bulletsLeft > 0 && Input.GetKey(KeyCode.Mouse0))
            {
                FireBullet();
                bulletsLeft--;
                yield return new WaitForSeconds(fireRate);
            }

            burstCoroutine = null;
        }

        private void FireBullet()
        {
            GameObject bulletInstance = Instantiate(bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
            muzzleEffect.GetComponent<ParticleSystem>().Play();
            currentAmmo--;
            //camera.transform.localRotation *= Quaternion.Euler(-1f, Random.Range(-0.5f, 0.5f), 0);
            Debug.Log(currentAmmo + "/ " + reservedAmmo);

            Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
            rb.velocity = bulletSpawnPoint.forward * bulletSpeed;

            AudioManager.Instance.PlaySFXSound(shootSound);

            Destroy(bulletInstance, bulletLiveTime);
        }

        private void ReloadWeapon()
        {
            canShoot = false;

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            if (reloadSound != null)
            {
                AudioManager.Instance.PlaySFXSound(reloadSound);
            }

            WeaponManager.Instance.CharacterAnimation.SetTrigger("Reload");
            yield return new WaitForSeconds(reloadTime);
            ReloadCompleted();
        }

        private void ReloadCompleted()
        {
            int bulletsNeeded = magazineSize - currentAmmo;
            int bulletsToReload = Mathf.Min(bulletsNeeded, reservedAmmo);

            reservedAmmo -= bulletsToReload;
            currentAmmo += bulletsToReload;

            canShoot = true;
        }


        private void OnEnable()
        {
            canShoot = true;
        }
    }
}
