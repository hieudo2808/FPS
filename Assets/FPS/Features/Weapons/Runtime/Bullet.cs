using UnityEngine;

namespace FPS
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private GameObject bulletHolePrefab;
        [SerializeField] private LayerMask bulletHoleLayer;

        private void OnCollisionEnter(Collision collision)
        {
            LayerMask effectiveMask = bulletHoleLayer.value != 0 
                ? bulletHoleLayer 
                : (LayerMask)(LayerMask.GetMask("Default", "Ground") != 0 ? LayerMask.GetMask("Default", "Ground") : ~0);

            if (((1 << collision.gameObject.layer) & effectiveMask.value) != 0)
                CreateBulletHole(collision);

            gameObject.SetActive(false);
        }

        private void CreateBulletHole(Collision objectHit)
        {
            ContactPoint contact = objectHit.contacts[0];
            GameObject hole = Instantiate(bulletHolePrefab, contact.point, Quaternion.LookRotation(contact.normal));
            hole.transform.SetParent(objectHit.transform);
        }
    }
}