using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private GameObject bulletHolePrefab;
        private float damage = 25f;

        public void SetDamage(float weaponDamage)
        {
            damage = weaponDamage;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log("Hit " + collision.gameObject.name);

            // Kiểm tra nếu trúng enemy
            EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
            // Tạo bullet hole nếu trúng tường/vật thể (layer 3)
            else if (collision.gameObject.layer == 3)
            {
                CreateBulletHole(collision);
            }

            Destroy(gameObject);
        }

        private void CreateBulletHole(Collision objectHit)
        {
            ContactPoint contact = objectHit.contacts[0];
            GameObject hole = Instantiate(bulletHolePrefab, contact.point, Quaternion.LookRotation(contact.normal));
            hole.transform.SetParent(objectHit.transform);
        }
    }
}
