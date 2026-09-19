using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class SpecialSpatialAudio : MonoBehaviour, IPoolResettable
    {
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Range(0.5f, 2f)] private float presentationPitch = 1f;
        [SerializeField, Range(0, 256)] private int priority = 128;

        private AudioSource source;

        private void Awake() => EnsureSource();

        public void PlayOneShot(AudioClip clip, float volume, float minimumDistance, float maximumDistance)
        {
            if (clip == null)
                return;

            EnsureSource();
            source.minDistance = Mathf.Max(0.01f, minimumDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maximumDistance);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void ResetForPool()
        {
            if (source != null)
                source.Stop();
        }

        private void EnsureSource()
        {
            if (source == null)
                source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
            source.pitch = presentationPitch;
            source.priority = priority;
        }
    }
}
