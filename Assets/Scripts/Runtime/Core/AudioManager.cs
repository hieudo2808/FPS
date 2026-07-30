using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace FPS
{
    public class AudioManager : Singleton<AudioManager>
    {
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip menuMusic;

        private const string SFX_VOLUME_PARAM = "sfx";
        private const string MUSIC_VOLUME_PARAM = "music";
        private const string MASTER_VOLUME_PARAM = "master";
        private const float MIN_VOLUME_DB = -80f;
        private const float MAX_VOLUME_DB = 0f;
        private const float VOLUME_STEP = 0.1f;
        private const float DEFAULT_VOLUME = 0.5f;
        private const float VOLUME_ADJUST_DELAY = 0.1f;
        private float lastVolumeAdjustTime = 0f;
        private float masterVolume = DEFAULT_VOLUME;
        private float musicVolume = DEFAULT_VOLUME;
        private float sfxVolume = DEFAULT_VOLUME;

        private void Start()
        {
            // Auto-tạo AudioSource nếu quên gán trong Inspector
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                Debug.LogWarning("[AudioManager] SFX AudioSource was not assigned — auto-created one.");
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
            }
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            SetMasterVolume(PlayerPrefs.GetFloat(MASTER_VOLUME_PARAM, DEFAULT_VOLUME));
            SetMusicVolume(PlayerPrefs.GetFloat(MUSIC_VOLUME_PARAM, DEFAULT_VOLUME));
            SetSFXVolume(PlayerPrefs.GetFloat(SFX_VOLUME_PARAM, DEFAULT_VOLUME));

            SceneManager.sceneLoaded += HandleSceneLoaded;
            UpdateMusicForScene(SceneManager.GetActiveScene().name);
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            base.OnDestroy();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                UpdateMusicForScene(scene.name);
        }

        // Nhạc nền chỉ chạy ở menu/lobby; vào gameplay thì dừng để nhường ambience/SFX.
        private void UpdateMusicForScene(string sceneName)
        {
            if (musicSource == null) return;

            bool wantsMusic = sceneName == "MainMenu" || sceneName == "LobbyScene";
            if (wantsMusic && menuMusic != null)
            {
                if (musicSource.clip != menuMusic || !musicSource.isPlaying)
                {
                    musicSource.clip = menuMusic;
                    musicSource.volume = musicVolume;
                    musicSource.Play();
                }
            }
            else if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        public void PlaySFXSound(AudioClip clip, float volume = 1f)
        {
            if (sfxSource == null)
            {
                Debug.LogError("SFX AudioSource is not assigned in AudioManager!");
                return;
            }

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * sfxVolume);
        }

        private bool ValidateAudioMixer()
        {
            return audioMixer != null;
        }

        public void SetSFXVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            sfxVolume = volume;
            if (sfxSource != null)
            {
                sfxSource.volume = 1f;
            }

            float dB = ConvertVolumeToDecibels(volume);
            if (ValidateAudioMixer())
            {
                audioMixer.SetFloat(SFX_VOLUME_PARAM, dB);
            }
            PlayerPrefs.SetFloat(SFX_VOLUME_PARAM, volume);
        }

        public void SetMusicVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            musicVolume = volume;
            if (musicSource != null)
            {
                musicSource.volume = volume;
            }

            float dB = ConvertVolumeToDecibels(volume);
            if (ValidateAudioMixer())
            {
                audioMixer.SetFloat(MUSIC_VOLUME_PARAM, dB);
            }
            PlayerPrefs.SetFloat(MUSIC_VOLUME_PARAM, volume);
        }

        public void SetMasterVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            masterVolume = volume;
            AudioListener.volume = volume;
            float dB = ConvertVolumeToDecibels(volume);
            if (ValidateAudioMixer())
            {
                audioMixer.SetFloat(MASTER_VOLUME_PARAM, dB);
            }
            PlayerPrefs.SetFloat(MASTER_VOLUME_PARAM, volume);
        }

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
            return PlayerPrefs.GetFloat(SFX_VOLUME_PARAM, sfxVolume);
        }

        public float GetMusicVolume()
        {
            return PlayerPrefs.GetFloat(MUSIC_VOLUME_PARAM, musicVolume);
        }

        public float GetMasterVolume()
        {
            return PlayerPrefs.GetFloat(MASTER_VOLUME_PARAM, masterVolume);
        }

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

        public void SaveSettings()
        {
            PlayerPrefs.Save();
        }
    }
}
