using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(menuName = "FPS/Survival Catalog")]
    public sealed class SurvivalCatalog : ScriptableObject
    {
        public Sprite[] icons = new Sprite[4];
        public GameObject[] itemVisuals = new GameObject[4];
        public GameObject[] pickups = new GameObject[4];
        public GameObject fragEffect;
        public GameObject fireEffect;
        public GameObject medicalEffect;
        public GameObject scorchEffect;
        public Sprite progressRing;
        public AudioClip explosionClip;
        public AudioClip throwClip;
        public AudioClip treatmentClip;
        public AudioClip completionClip;
        public AudioClip fireClip;
        public AudioClip[] pickupClips = new AudioClip[4];
        private static SurvivalCatalog cached;
        public static SurvivalCatalog Load() => cached != null ? cached : cached = Resources.Load<SurvivalCatalog>("SurvivalCatalog");
    }
}
