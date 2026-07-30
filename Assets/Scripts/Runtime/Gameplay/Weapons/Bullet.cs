using UnityEngine;

namespace FPS
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private GameObject bulletHolePrefab;
        [SerializeField] private LayerMask bulletHoleLayer;

        private void OnCollisionEnter(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & bulletHoleLayer) != 0)
                CreateBulletHole(collision);

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