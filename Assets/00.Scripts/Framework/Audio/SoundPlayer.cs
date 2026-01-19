using UnityEngine;

public class SoundPlayer : MonoBehaviour
{
    public AudioSource Source { get; private set; }
    public ESound CurrentSound { get; private set; }

    private float _endTime;
    private bool _isPlaying;

    private void Awake()
    {
        Source = GetComponent<AudioSource>();

        if (Source == null)
        {
            Source = gameObject.AddComponent<AudioSource>();
        }
    }

    public void Play(ESound sound, AudioClip clip, float volume, float pitch, bool loop)
    {
        CurrentSound = sound;

        if (clip == null)
        {
            _isPlaying = false;
            return;
        }

        Source.Stop();
        Source.clip = null;

        Source.clip = clip;
        Source.volume = volume;
        Source.pitch = pitch;
        Source.loop = loop;

        Source.spatialBlend = 0f;
        Source.playOnAwake = false;

        _isPlaying = true;
        Source.Play();

        if (loop)
        {
            _endTime = float.MaxValue;
        }
        else
        {
            _endTime = Time.unscaledTime + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
        }
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
        CurrentSound = ESound.None;

        if (Source != null)
        {
            Source.Stop();
            Source.clip = null;
            Source.loop = false;
        }
    }
}
