using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework.SoundSystem
{
    public class SoundPlayer : MonoBehaviour
    {
        public AudioSource Source { get; private set; }

        /// <summary>현재 재생 중인 사운드의 식별자입니다(Data Parsing Sound 시트의 FileName).
        /// 아무것도 재생 중이 아니면 null입니다.</summary>
        public string CurrentSound { get; private set; }
        public ESoundChannel CurrentChannel { get; private set; }

        private float _endTime;
        private bool _isPlaying;

        private void Awake()
        {
            Source = GetComponent<AudioSource>();
            if (Source == null)
            {
                Source = gameObject.AddComponent<AudioSource>();
            }

            Source.playOnAwake = false;
            Source.spatialBlend = 0f;
        }

        public void SetOutputGroup(AudioMixerGroup group)
        {
            if (Source == null)
            {
                return;
            }

            Source.outputAudioMixerGroup = group;
        }

        public void Play(string sound, ESoundChannel channel, AudioClip clip, float volume, float pitch, bool loop)
        {
            CurrentSound = sound;
            CurrentChannel = channel;

            if (clip == null)
            {
                _isPlaying = false;
                return;
            }

            Source.Stop();
            Source.clip = clip;
            Source.volume = volume;
            Source.pitch = pitch;
            Source.loop = loop;

            Source.spatialBlend = 0f;
            Source.playOnAwake = false;

            _isPlaying = true;
            Source.Play();

            _endTime = loop
                ? float.MaxValue
                : Time.unscaledTime + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
        }

        public bool IsFinished()
        {
            if (_isPlaying == false)
            {
                return true;
            }

            if (Time.unscaledTime >= _endTime)
            {
                _isPlaying = false;
                return true;
            }

            return false;
        }

        public void Stop()
        {
            _isPlaying = false;
            CurrentSound = null;

            if (Source != null)
            {
                Source.Stop();
                Source.clip = null;
                Source.loop = false;
            }
        }
    }
}
