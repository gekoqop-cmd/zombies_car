using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace ZombiCar.Core
{
    /// <summary>
    /// The game's sound: one-shot effects from a fixed pool of AudioSources on the SFX group of the game mixer, named
    /// effects from the <see cref="SoundBank"/> (<see cref="PlaySfx"/>), and music on the Music group with a cross-fade
    /// between two sources (<see cref="PlayMusic"/>). One manager lives for the whole session: every scene that needs
    /// sound has an "[Audio]" object, the first one to wake up is kept (DontDestroyOnLoad) and later copies remove
    /// themselves. Volumes (master / music / effects) go to the mixer's exposed parameters; the Web build has no audio
    /// mixer, so there the listener carries the master volume and every source is scaled by its group's volume.
    /// Browsers only allow audio after a user gesture: in the Web build nothing plays (and the music waits) until the
    /// first click, key press or touch. All sound stops while the tab loses focus, during ads and platform pauses
    /// (<see cref="GamePause"/>).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const string c_MasterVolume = "MasterVolume";
        private const string c_SfxVolume = "SfxVolume";
        private const string c_MusicVolume = "MusicVolume";
        private const float c_MinDecibels = -80f;

        [SerializeField] private AudioMixer m_Mixer;
        [SerializeField] private AudioMixerGroup m_SfxGroup;
        [SerializeField] private AudioMixerGroup m_MusicGroup;
        [SerializeField] private SoundBank m_Bank;
        [Tooltip("Simultaneous sounds; the oldest one is replaced when all are busy.")]
        [SerializeField, Min(1)] private int m_Voices = 16;
        [Tooltip("Level of the music under the effects (before the player's music volume).")]
        [SerializeField, Range(0f, 1f)] private float m_MusicLevel = 0.55f;
        [SerializeField, Min(0.01f)] private float m_MusicFade = 1.5f;

        private readonly Dictionary<Sound, float> m_LastPlayed = new Dictionary<Sound, float>();
        private AudioSource[] m_Sources;
        private AudioSource[] m_Music;
        private float[] m_MusicWeight;
        private int m_ActiveMusic;
        private AudioClip m_PendingMusic;
        private int m_Next;
        private bool m_Unlocked;
        private bool m_MixerWorks;
        private bool m_MusicSilent;

        public static AudioManager Instance { get; private set; }

        public bool IsUnlocked => m_Unlocked;

        /// <summary>The track playing (or waiting for the first gesture).</summary>
        public AudioClip CurrentMusic => m_PendingMusic != null ? m_PendingMusic : m_Music != null ? m_Music[m_ActiveMusic].clip : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);                                    // the session's manager already runs
                return;
            }
            Instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);
            m_Sources = new AudioSource[m_Voices];
            for (int i = 0; i < m_Voices; i++)
                m_Sources[i] = NewSource(m_SfxGroup, false);
            m_Music = new[] { NewSource(m_MusicGroup, true), NewSource(m_MusicGroup, true) };
            m_MusicWeight = new float[2];
            // Only the Web build must wait for a gesture before playing.
            m_Unlocked = Application.platform != RuntimePlatform.WebGLPlayer;
        }

        private AudioSource NewSource(AudioMixerGroup group, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;                 // top-down camera: plain 2D sounds
            source.outputAudioMixerGroup = group;
            return source;
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
            if (!m_Unlocked)
            {
                bool gesture = (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) ||
                               (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                               (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
                if (!gesture)
                    return;
                m_Unlocked = true;
            }
            if (m_PendingMusic != null)
            {
                AudioClip clip = m_PendingMusic;
                m_PendingMusic = null;
                StartMusic(clip);
            }
            FadeMusic();
        }

        // ---------------------------------------------------------------- effects
        /// <summary>Plays <paramref name="clip"/> once. Pitch varies by ±<paramref name="pitchJitter"/> so repeats do not sound identical.</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            if (!m_Unlocked || clip == null || m_Sources == null)
                return;
            AudioSource source = FreeSource();
            source.clip = clip;
            source.volume = volume * SfxScale;
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
            source.volume = volume * SfxScale;
            // On the Web a clip is decoded asynchronously: its length is unknown until it has loaded.
            source.pitch = clip.loadState == AudioDataLoadState.Loaded ? Mathf.Clamp(clip.length / duration, 0.5f, 2f) : 1f;
            source.Play();
        }

        /// <summary>Plays a named effect of the <see cref="SoundBank"/> (no-op without a manager or before the first gesture).</summary>
        public static void PlaySfx(Sound id, float volumeScale = 1f, float pitch = 1f)
        {
            if (Instance != null)
                Instance.PlayNamed(id, volumeScale, pitch);
        }

        private void PlayNamed(Sound id, float volumeScale, float pitch)
        {
            if (!m_Unlocked || m_Bank == null || id == Sound.None)
                return;
            SoundBank.Entry entry = m_Bank.Get(id);
            if (entry == null)
                return;
            float now = Time.unscaledTime;
            if (entry.MinInterval > 0f && m_LastPlayed.TryGetValue(id, out float last) && now - last < entry.MinInterval)
                return;
            m_LastPlayed[id] = now;
            AudioSource source = FreeSource();
            source.clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            source.volume = entry.Volume * volumeScale * SfxScale;
            source.pitch = pitch * (1f + (entry.PitchJitter > 0f ? Random.Range(-entry.PitchJitter, entry.PitchJitter) : 0f));
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

        // ---------------------------------------------------------------- music
        /// <summary>Cross-fades to <paramref name="clip"/> (looped); the same track keeps playing, null fades the music out.</summary>
        public static void PlayMusic(AudioClip clip)
        {
            if (Instance != null)
                Instance.RequestMusic(clip);
        }

        private void RequestMusic(AudioClip clip)
        {
            if (m_Music == null)
                return;
            if (!m_Unlocked)
            {
                m_PendingMusic = clip;                                  // starts with the first gesture
                return;
            }
            StartMusic(clip);
        }

        private void StartMusic(AudioClip clip)
        {
            AudioSource active = m_Music[m_ActiveMusic];
            if (clip != null && active.clip == clip && active.isPlaying && !m_MusicSilent)
                return;
            if (clip == null)
            {
                m_MusicSilent = true;                                   // both sources fade out
                return;
            }
            m_MusicSilent = false;
            if (active.clip == clip && active.isPlaying)
                return;                                                 // fading back in after a silence
            m_ActiveMusic = 1 - m_ActiveMusic;
            AudioSource next = m_Music[m_ActiveMusic];
            next.clip = clip;
            next.time = 0f;
            m_MusicWeight[m_ActiveMusic] = 0f;
            next.volume = 0f;
            next.Play();
        }

        private void FadeMusic()
        {
            float step = Time.unscaledDeltaTime / m_MusicFade;
            float scale = m_MusicLevel * MusicScale;
            for (int i = 0; i < m_Music.Length; i++)
            {
                float target = !m_MusicSilent && i == m_ActiveMusic ? 1f : 0f;
                m_MusicWeight[i] = Mathf.MoveTowards(m_MusicWeight[i], target, step);
                AudioSource source = m_Music[i];
                source.volume = m_MusicWeight[i] * scale;
                if (m_MusicWeight[i] <= 0f && source.isPlaying)
                    source.Stop();
            }
        }

        // ---------------------------------------------------------------- volume
        private float SfxScale => m_MixerWorks ? 1f : SettingsService.SfxVolume;
        private float MusicScale => m_MixerWorks ? 1f : SettingsService.MusicVolume;

        private void ApplyVolumes()
        {
            // The Web build has no audio mixer (and a mixer without the exposed parameters fails too): the listener
            // carries the master volume and the sources are scaled by their group's volume.
            m_MixerWorks = Application.platform != RuntimePlatform.WebGLPlayer && m_Mixer != null &&
                           m_Mixer.SetFloat(c_MasterVolume, ToDecibels(SettingsService.MasterVolume));
            if (!m_MixerWorks)
            {
                AudioListener.volume = SettingsService.MasterVolume;
                return;
            }
            AudioListener.volume = 1f;
            m_Mixer.SetFloat(c_MusicVolume, ToDecibels(SettingsService.MusicVolume));
            m_Mixer.SetFloat(c_SfxVolume, ToDecibels(SettingsService.SfxVolume));
        }

        private static float ToDecibels(float linear) => linear > 1e-4f ? 20f * Mathf.Log10(linear) : c_MinDecibels;
    }
}
