using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS
{
    [RequireComponent(typeof(Selectable))]
    public class ButtonAudioFeedback : MonoBehaviour, IPointerEnterHandler, ISubmitHandler
    {
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.55f;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(PlayClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClick);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Play(hoverClip, hoverVolume);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayClick();
        }

        private void PlayClick()
        {
            Play(clickClip, clickVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || AudioManager.Instance == null) return;

            AudioManager.Instance.PlaySFXSound(clip, volume);
        }
    }
}
