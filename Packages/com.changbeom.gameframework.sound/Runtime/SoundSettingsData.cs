using System;
using UnityEngine;

namespace GameFramework.Sound
{
    [Serializable]
    public class SoundSettingsData
    {
        public float master = 1f;
        public float bgm = 1f;
        public float sfx = 1f;
        public float ui = 1f;
        public float voice = 1f;

        public float Get(ESoundChannel channel)
        {
            switch (channel)
            {
                case ESoundChannel.BGM: return bgm;
                case ESoundChannel.SFX: return sfx;
                case ESoundChannel.UI: return ui;
                case ESoundChannel.Voice: return voice;
                default: return 1f;
            }
        }

        public void Set(ESoundChannel channel, float value)
        {
            float v = Mathf.Clamp01(value);

            switch (channel)
            {
                case ESoundChannel.BGM: bgm = v; break;
                case ESoundChannel.SFX: sfx = v; break;
                case ESoundChannel.UI: ui = v; break;
                case ESoundChannel.Voice: voice = v; break;
            }
        }
    }
}
