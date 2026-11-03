using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>Cross-fades the music tracks and plays the sound effects, with master and music volumes.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const float MusicBase = 0.5f;

        AudioSource music;
        AudioSource effects;
        AudioClip wanted;
        float fade = 1f;
        bool ducked;

        /// <summary>Master volume 0..1.</summary>
        public float Volume { get; private set; } = 0.8f;

        /// <summary>Music volume 0..1, under the master volume.</summary>
        public float MusicVolume { get; private set; } = 0.6f;

        public AudioClip CurrentMusic => wanted;

        public void Init(float volume, float musicVolume)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            SetVolume(volume, musicVolume);
        }

        public void SetVolume(float volume, float musicVolume)
        {
            Volume = Mathf.Clamp01(volume);
            MusicVolume = Mathf.Clamp01(musicVolume);
            AudioListener.volume = Volume;
        }

        public void PlayMusic(AudioClip clip) => wanted = clip;

        /// <summary>Quieter music while a menu is open over the game.</summary>
        public void SetDucked(bool value) => ducked = value;

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float jitter = 0.04f)
        {
            if (clip == null || effects == null)
            {
                return;
            }
            effects.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            effects.PlayOneShot(clip, volume);
        }

        void Update()
        {
            if (music == null)
            {
                return;
            }
            if (music.clip != wanted)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 2.5f);
                if (fade <= 0f || !music.isPlaying)
                {
                    music.clip = wanted;
                    if (wanted != null)
                    {
                        music.Play();
                    }
                }
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 1.5f);
            }
            music.volume = MusicBase * MusicVolume * fade * (ducked ? 0.4f : 1f);
        }
    }
}
