using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(menuName = "FPS/Campaign/Settings")]
    public sealed class CampaignSettings : ScriptableObject
    {
        [Min(0)] public float insertionSeconds = 18;
        [Min(1)] public float factoryEncounterSeconds = 60;
        [Min(1)] public float asylumEncounterSeconds = 35;
        [Min(1)] public float labEncounterSeconds = 90;
        [Min(0)] public float preparationSeconds = 5;
        [Min(0)] public float boardingSeconds = 5;
        [Min(0)] public float transferSeconds = 6;
        [Min(1)] public float endingSeconds = 10;
        [Min(1)] public float disconnectGraceSeconds = 30;
        [Min(1)] public float interactionRange = 2.5f;
        [Min(1)] public float bleedoutSeconds = 30;
        [Min(0)] public float bleedoutDamageSeconds = .25f;
        [Min(1)] public float reviveSeconds = 4;
        [Min(1)] public float reviveHealth = 30;
        [Min(0)] public float reviveProtectionSeconds = 2;
        [Min(1)] public float medicineSeconds = 3;
        [Min(1)] public float medicineHealth = 50;
        [Range(0, 4)] public byte medicineCapacity = 2;
        [Min(0)] public float beatRestSeconds = 25;
        [Min(0)] public float readingGraceSeconds = 25;
        public float Duration(CampaignChapter chapter) => chapter == CampaignChapter.Factory ? factoryEncounterSeconds
            : chapter == CampaignChapter.Asylum ? asylumEncounterSeconds : labEncounterSeconds;
    }
}
