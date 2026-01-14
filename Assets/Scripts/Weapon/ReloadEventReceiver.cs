using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Bridge script để Animation Events có thể gọi functions trên Weapon
    /// Đặt script này trên cùng GameObject với Animator (FPS Arms)
    /// </summary>
    public class ReloadEventReceiver : MonoBehaviour
    {
        [SerializeField] private Weapon weapon;
        
        /// <summary>
        /// Gọi từ Animation Event khi rút băng đạn
        /// </summary>
        public void GrabMagazine()
        {
            if (weapon != null)
                weapon.GrabMagazine();
        }
        
        /// <summary>
        /// Gọi từ Animation Event khi lắp băng đạn
        /// </summary>
        public void InsertMagazine()
        {
            if (weapon != null)
                weapon.InsertMagazine();
        }
        
        /// <summary>
        /// Tự động tìm Weapon nếu chưa assign
        /// </summary>
        private void Start()
        {
            if (weapon == null)
            {
                weapon = GetComponentInChildren<Weapon>();
                if (weapon == null)
                    weapon = FindObjectOfType<Weapon>();
            }
        }
    }
}
