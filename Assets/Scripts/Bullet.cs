using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private GameObject bulletHolePrefab;
        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log("Hit " + collision.gameObject.name);

            if (collision.gameObject.layer == 3)
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