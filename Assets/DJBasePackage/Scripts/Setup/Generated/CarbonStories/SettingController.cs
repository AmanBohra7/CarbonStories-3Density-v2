using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CarbonStories
{
    [RequireComponent(typeof(CanvasGroup))]
    public class SettingController : MonoBehaviour
    {
        private CanvasGroup settingsCanvasGroup;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float initialVolume = 1f;
        [SerializeField, Min(0.01f)] private float volumeStep = 0.1f;
        [SerializeField] private Image volumeFillImage;

        [Header("Music Buttons")]
        public GameObject music_on_btn_enabled;
        public GameObject music_on_btn_disabled;
        public GameObject music_off_btn_enabled;
        public GameObject music_off_btn_disabled;

        [Header("SFX Buttons")]
        public GameObject sfx_on_btn_enabled;
        public GameObject sfx_on_btn_disabled;
        public GameObject sfx_off_btn_enabled;
        public GameObject sfx_off_btn_disabled;

        [Header("Events")]
        public UnityEvent<float> onVolumeChanged = new UnityEvent<float>();
        public UnityEvent<bool> onMusicStateChanged = new UnityEvent<bool>();
        public UnityEvent<bool> onSfxStateChanged = new UnityEvent<bool>();

        public static float globalVolume = 1f;
        public static bool MusicEnabled { get; private set; } = true;
        public static bool SfxEnabled { get; private set; } = true;

        private void Awake()
        {
            settingsCanvasGroup = GetComponent<CanvasGroup>();
            globalVolume = Mathf.Clamp01(initialVolume);
            ApplyAudioSettings();
            UpdateVolumeFill();
            UpdateMusicButtons();
            UpdateSfxButtons();
        }

        private void Start()
        {
            onVolumeChanged.Invoke(globalVolume);
            onMusicStateChanged.Invoke(MusicEnabled);
            onSfxStateChanged.Invoke(SfxEnabled);
        }

        public void IncreaseVolume()
        {
            SetVolume(globalVolume + volumeStep);
        }

        public void DecreaseVolume()
        {
            SetVolume(globalVolume - volumeStep);
        }

        public void OpenSettings()
        {
            SetSettingsVisible(true);
        }

        public void CloseSettings()
        {
            SetSettingsVisible(false);
        }

        public void QuitApplication()
        {
            Application.Quit();
        }

        public void SetVolume(float volume)
        {
            float updatedVolume = Mathf.Clamp01(volume);

            if (Mathf.Approximately(globalVolume, updatedVolume))
            {
                UpdateVolumeFill();
                return;
            }

            globalVolume = updatedVolume;
            ApplyAudioSettings();
            UpdateVolumeFill();
            onVolumeChanged.Invoke(globalVolume);
        }

        public void EnableMusic()
        {
            SetMusicEnabled(true);
        }

        public void DisableMusic()
        {
            SetMusicEnabled(false);
        }

        public void EnableSfx()
        {
            SetSfxEnabled(true);
        }

        public void DisableSfx()
        {
            SetSfxEnabled(false);
        }

        public void SetMusicEnabled(bool isEnabled)
        {
            MusicEnabled = isEnabled;
            ApplyAudioSettings();
            UpdateMusicButtons();
            onMusicStateChanged.Invoke(MusicEnabled);
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            SfxEnabled = isEnabled;
            ApplyAudioSettings();
            UpdateSfxButtons();
            onSfxStateChanged.Invoke(SfxEnabled);
        }

        private void UpdateVolumeFill()
        {
            if (volumeFillImage != null)
            {
                volumeFillImage.fillAmount = globalVolume;
            }
        }

        private static void ApplyAudioSettings()
        {
            AudioListener.volume = globalVolume;
            if (App.Instance != null)
            {
                App.Instance.ApplyAudioSettings();
            }
        }

        private void SetSettingsVisible(bool isVisible)
        {
            settingsCanvasGroup.alpha = isVisible ? 1f : 0f;
            settingsCanvasGroup.interactable = isVisible;
            settingsCanvasGroup.blocksRaycasts = isVisible;
        }

        private void UpdateMusicButtons()
        {
            SetActiveIfAssigned(music_on_btn_enabled, MusicEnabled);
            SetActiveIfAssigned(music_on_btn_disabled, !MusicEnabled);
            SetActiveIfAssigned(music_off_btn_enabled, !MusicEnabled);
            SetActiveIfAssigned(music_off_btn_disabled, MusicEnabled);
        }

        private void UpdateSfxButtons()
        {
            SetActiveIfAssigned(sfx_on_btn_enabled, SfxEnabled);
            SetActiveIfAssigned(sfx_on_btn_disabled, !SfxEnabled);
            SetActiveIfAssigned(sfx_off_btn_enabled, !SfxEnabled);
            SetActiveIfAssigned(sfx_off_btn_disabled, SfxEnabled);
        }

        private static void SetActiveIfAssigned(GameObject target, bool isActive)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }

        private void OnValidate()
        {
            initialVolume = Mathf.Clamp01(initialVolume);
            volumeStep = Mathf.Max(0.01f, volumeStep);

            if (!Application.isPlaying && volumeFillImage != null)
            {
                volumeFillImage.fillAmount = initialVolume;
            }
        }
    }
}
