using UnityEngine;
using UnityEngine.Audio;

namespace FPS
{
    public class AudioManager : Singleton<AudioManager>
    {
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource sfxSource;

        private const string SFX_VOLUME_PARAM = "sfx";
        private const string MUSIC_VOLUME_PARAM = "music";
        private const string MASTER_VOLUME_PARAM = "master";
        private const float MIN_VOLUME_DB = -80f; // Giá trị âm lượng tối thiểu (im lặng)
        private const float MAX_VOLUME_DB = 0f;   // Giá trị âm lượng tối đa
        private const float VOLUME_STEP = 0.1f;   // Bước tăng/giảm âm lượng
        private const float DEFAULT_VOLUME = 0.5f; // Giá trị âm lượng mặc định
        private const float VOLUME_ADJUST_DELAY = 0.1f; // Thời gian chờ giữa các lần điều chỉnh âm lượng
        private float lastVolumeAdjustTime = 0f;

        private void Start()
        {
            // Khởi tạo âm lượng từ PlayerPrefs hoặc đặt về mặc định
            float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_PARAM, DEFAULT_VOLUME);
            //float musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_PARAM, DEFAULT_VOLUME);
            //float masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_PARAM, DEFAULT_VOLUME);

            SetSFXVolume(sfxVolume);
            //SetMusicVolume(musicVolume);
            //SetMasterVolume(masterVolume);
        }

        public void PlaySFXSound(AudioClip clip, float volume = 1f)
        {
            if (sfxSource == null)
            {
                Debug.LogError("SFX AudioSource is not assigned in AudioManager!");
                return;
            }

            sfxSource.PlayOneShot(clip, volume);
        }

        private bool ValidateAudioMixer()
        {
            if (audioMixer == null)
            {
                Debug.LogError("AudioMixer is not assigned in AudioManager!");
                return false;
            }
            return true;
        }

        public void SetSFXVolume(float volume)
        {
            if (!ValidateAudioMixer()) return;

            volume = Mathf.Clamp01(volume);
            float dB = ConvertVolumeToDecibels(volume);
            audioMixer.SetFloat(SFX_VOLUME_PARAM, dB);
            PlayerPrefs.SetFloat(SFX_VOLUME_PARAM, volume);
        }

        public void SetMusicVolume(float volume)
        {
            if (!ValidateAudioMixer()) return;

            volume = Mathf.Clamp01(volume);
            float dB = ConvertVolumeToDecibels(volume);
            audioMixer.SetFloat(MUSIC_VOLUME_PARAM, dB);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_PARAM, volume);
        }

        public void SetMasterVolume(float volume)
        {
            if (!ValidateAudioMixer()) return;

            volume = Mathf.Clamp01(volume);
            float dB = ConvertVolumeToDecibels(volume);
            audioMixer.SetFloat(MASTER_VOLUME_PARAM, dB);
            PlayerPrefs.SetFloat(MASTER_VOLUME_PARAM, volume);
        }

        // ✅ IMPROVED: Extract common conversion logic
        private float ConvertVolumeToDecibels(float volume)
        {
            return (volume <= 0.01f) ? MIN_VOLUME_DB : Mathf.Log10(volume) * 20f;
        }

        private float ConvertDecibelsToVolume(float dB)
        {
            return (dB <= MIN_VOLUME_DB) ? 0f : Mathf.Pow(10f, dB / 20f);
        }

        public float GetSFXVolume()
        {
            if (!ValidateAudioMixer()) return DEFAULT_VOLUME;

            audioMixer.GetFloat(SFX_VOLUME_PARAM, out float dB);
            return ConvertDecibelsToVolume(dB);
        }

        public float GetMusicVolume()
        {
            if (!ValidateAudioMixer()) return DEFAULT_VOLUME;

            audioMixer.GetFloat(MUSIC_VOLUME_PARAM, out float dB);
            return ConvertDecibelsToVolume(dB);
        }

        public float GetMasterVolume()
        {
            if (!ValidateAudioMixer()) return DEFAULT_VOLUME;

            audioMixer.GetFloat(MASTER_VOLUME_PARAM, out float dB);
            return ConvertDecibelsToVolume(dB);
        }

        // ✅ IMPROVED: Generic volume adjustment method
        private void AdjustVolume(System.Action<float> setVolumeAction, System.Func<float> getVolumeFunc, float adjustment)
        {
            if (Time.time - lastVolumeAdjustTime < VOLUME_ADJUST_DELAY) return;

            float currentVolume = getVolumeFunc();
            float newVolume = Mathf.Clamp01(currentVolume + adjustment);
            setVolumeAction(newVolume);
            lastVolumeAdjustTime = Time.time;
        }

        public void IncreaseSFXVolume()
        {
            AdjustVolume(SetSFXVolume, GetSFXVolume, VOLUME_STEP);
        }

        public void DecreaseSFXVolume()
        {
            AdjustVolume(SetSFXVolume, GetSFXVolume, -VOLUME_STEP);
        }

        public void IncreaseMusicVolume()
        {
            AdjustVolume(SetMusicVolume, GetMusicVolume, VOLUME_STEP);
        }

        public void DecreaseMusicVolume()
        {
            AdjustVolume(SetMusicVolume, GetMusicVolume, -VOLUME_STEP);
        }

        public void IncreaseMasterVolume()
        {
            AdjustVolume(SetMasterVolume, GetMasterVolume, VOLUME_STEP);
        }

        public void DecreaseMasterVolume()
        {
            AdjustVolume(SetMasterVolume, GetMasterVolume, -VOLUME_STEP);
        }

        // ✅ ADDED: Utility methods
        public void MuteAll()
        {
            SetMasterVolume(0f);
        }

        public void UnmuteAll()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(MASTER_VOLUME_PARAM, DEFAULT_VOLUME));
        }

        public void ResetToDefault()
        {
            SetSFXVolume(DEFAULT_VOLUME);
            SetMusicVolume(DEFAULT_VOLUME);
            SetMasterVolume(DEFAULT_VOLUME);
        }

        // ✅ ADDED: Save settings explicitly (good practice)
        public void SaveSettings()
        {
            PlayerPrefs.Save();
        }
    }
}