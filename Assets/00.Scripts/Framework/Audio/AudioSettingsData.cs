using UnityEngine;
using System;

[Serializable]
public class AudioSettingsData
{
    public float master = 1f;
    public float bgm = 1f;
    public float sfx = 1f;
    public float ui = 1f;
    public float voice = 1f;

    public float Get(EAudioChannel channel)
    {
        if(channel == EAudioChannel.BGM)
        {
            return bgm;
        }
        else if(channel == EAudioChannel.SFX)
        {
            return sfx;
        }
        else if(channel == EAudioChannel.UI)
        {
            return ui;
        }
        else if(channel == EAudioChannel.Voice)
        {
            return voice;
        }

        return 1f;
    }

    public void Set(EAudioChannel channel, float value)
    {
        float v = Mathf.Clamp01(value);

        if(channel == EAudioChannel.BGM)
        {
            bgm = v;
        }
        else if(channel == EAudioChannel.SFX)
        {
            sfx = v;
        }
        else if(channel == EAudioChannel.UI)
        {
            ui = v;
        }
        else if(channel == EAudioChannel.Voice)
        {
            voice = v;
        }
    }
}
