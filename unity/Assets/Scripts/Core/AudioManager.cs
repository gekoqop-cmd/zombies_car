using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace ZombiCar.Core
{
    /// <summary>
    /// One-shot sound effects from a fixed pool of AudioSources routed to the SFX group of the game mixer
    /// (Master / Music / SFX). Browsers only allow audio after a user gesture, so nothing plays until the first click,
    /// key press or touch. Lives in the scene that needs it (Map01); <see cref="Instance"/> is that scene's manager.
    /// All sound stops while the tab loses focus, during ads and platform pauses (<see cref="GamePause"/>).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const string c_MasterVolume = "MasterVolume";
        private const string c_SfxVolume = "SfxVolume";
        private const string c_MusicVolume = "MusicVolume";
        private const float c_MinDecibels = -80f;

        [SerializeField] private AudioMixer m_Mixer;
        [SerializeField] private AudioMixerGroup m_SfxGroup;
        [Tooltip("Simultaneous sounds; the oldest one is replaced when all are busy.")]
        [SerializeField, Min(1)] private int m_Voices = 12;

        private AudioSource[] m_Sources;
        private int m_Next;
        private bool m_Unlocked;

        public static AudioManager Instance { get; private set; }

        public bool IsUnlocked => m_Unlocked;

        private void Awake()
        {
            Instance = this;
            m_Sources = new AudioSource[m_Voices];
            for (int i = 0; i < m_Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;                 // top-down camera: plain 2D effects
                source.outputAudioMixerGroup = m_SfxGroup;
                m_Sources[i] = source;
            }
        }

        private void OnEnable()
        {
            SettingsService.VolumesChanged += ApplyVolumes;
            ApplyVolumes();
        }

        private void OnDisable() => SettingsService.VolumesChanged -= ApplyVolumes;

        /// <summary>Mutes everything while an outside pause (ads, platform overlay) is active.</summary>
        public static void SetExternalPause(bool paused) => GamePause.Set(GamePause.Reason.Platform, paused);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (m_Unlocked)
                return;
            bool gesture = (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) ||
                           (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                           (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
            if (gesture)
                m_Unlocked = true;
        }

        /// <summary>Plays <paramref name="clip"/> once. Pitch varies by ±<paramref name="pitchJitter"/> so repeats do not sound identical.</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            if (!m_Unlocked || clip == null || m_Sources == null)
                return;
            AudioSource source = FreeSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
            source.Play();
        }

        /// <summary>Plays <paramref name="clip"/> stretched or squeezed to last <paramref name="duration"/> seconds (pitch = length / duration).</summary>
        public void PlayFitted(AudioClip clip, float duration, float volume = 1f)
        {
            if (!m_Unlocked || clip == null || duration <= 0f)
                return;
            AudioSource source = FreeSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Mathf.Clamp(clip.length / duration, 0.5f, 2f);
            source.Play();
        }

        private AudioSource FreeSource()
        {
            for (int i = 0; i < m_Sources.Length; i++)
            {
                AudioSource candidate = m_Sources[(m_Next + i) % m_Sources.Length];
                if (!candidate.isPlaying)
                {
                    m_Next = (m_Next + i + 1) % m_Sources.Length;
                    return candidate;
                }
            }
            AudioSource oldest = m_Sources[m_Next];
            m_Next = (m_Next + 1) % m_Sources.Length;
            return oldest;
        }

        private void ApplyVolumes()
        {
            // No mixer (or one without the exposed parameters): the listener carries the master volume.
            if (m_Mixer == null || !m_Mixer.SetFloat(c_MasterVolume, ToDecibels(SettingsService.MasterVolume)))
            {
                AudioListener.volume = SettingsService.MasterVolume * SettingsService.SfxVolume;
                return;
            }
            m_Mixer.SetFloat(c_MusicVolume, ToDecibels(SettingsService.MusicVolume));
            m_Mixer.SetFloat(c_SfxVolume, ToDecibels(SettingsService.SfxVolume));
        }

        private static float ToDecibels(float linear) => linear > 1e-4f ? 20f * Mathf.Log10(linear) : c_MinDecibels;
    }
}
