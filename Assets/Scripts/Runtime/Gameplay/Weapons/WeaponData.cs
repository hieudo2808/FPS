using UnityEngine;

namespace FPS
{
    public enum FireMode { Single, Burst, Auto }

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
        public float fireRate = 0.1f;
        public float bulletLiveTime = 2f;
        public int burstCount = 3;
        public FireMode fireMode = FireMode.Single;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int totalAmmo = 120;
        public float reloadTime = 1.5f;

        [Header("Assets")]
        public GameObject bulletPrefab;
        public AudioClip shootSound;
        public AudioClip reloadSound;
        public RecoilPattern recoilPattern;
    }
}
