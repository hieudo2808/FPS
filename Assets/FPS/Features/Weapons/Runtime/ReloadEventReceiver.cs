using UnityEngine;

namespace FPS
{
    public class ReloadEventReceiver : MonoBehaviour
    {
        [SerializeField] private Weapon weapon;
        
        public void GrabMagazine()
        {
            if (weapon != null)
                weapon.GrabMagazine();
        }
        
        public void InsertMagazine()
        {
            if (weapon != null)
                weapon.InsertMagazine();
        }
        
        private void Start()
        {
            if (weapon == null)
            {
                weapon = GetComponentInChildren<Weapon>();
                if (weapon == null)
                    weapon = FindAnyObjectByType<Weapon>();
            }
        }
    }
}
