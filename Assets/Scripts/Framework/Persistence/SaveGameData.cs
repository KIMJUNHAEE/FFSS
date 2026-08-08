using System;
using FFSS.Framework.Core;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Run;
using UnityEngine;

namespace FFSS.Framework.Persistence
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 8;

        public int schemaVersion = CurrentSchemaVersion;
        public string savedAtUtc;
        public RunState run;
        public PlayerSettingsData settings = new PlayerSettingsData();
    }

    [Serializable]
    public sealed class PlayerSettingsData
    {
        public const string MasterVolumeKey = "settings.masterVolume";
        public const string MusicVolumeKey = "settings.musicVolume";
        public const string EffectsVolumeKey = "settings.effectsVolume";
        public const string InterfaceVolumeKey = "settings.interfaceVolume";
        public const string FullscreenKey = "settings.fullscreen";
        public const string ReduceMotionKey = "settings.reduceMotion";
        public const string ScreenShakeKey = "settings.screenShake";
        public const string HighContrastKey = "settings.highContrastIntents";
        public const string TextScaleKey = "settings.textScale";

        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float effectsVolume = 1f;
        public float interfaceVolume = 1f;
        public bool reduceMotion;
        public bool screenShake = true;
        public bool highContrastIntents;
        public float textScale = 1f;

        public static PlayerSettingsData FromPreferences()
        {
            return new PlayerSettingsData
            {
                masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 0.85f),
                musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f),
                effectsVolume = PlayerPrefs.GetFloat(EffectsVolumeKey, 1f),
                interfaceVolume = PlayerPrefs.GetFloat(InterfaceVolumeKey,
                    PlayerPrefs.GetFloat(EffectsVolumeKey, 1f)),
                fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0,
                reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) != 0,
                screenShake = PlayerPrefs.GetInt(ScreenShakeKey, 1) != 0,
                highContrastIntents = PlayerPrefs.GetInt(HighContrastKey, 0) != 0,
                textScale = PlayerPrefs.GetFloat(TextScaleKey, 1f)
            };
        }

        public bool fullscreen = true;

        public void Apply(bool persist)
        {
            AudioListener.volume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            effectsVolume = Mathf.Clamp01(effectsVolume);
            interfaceVolume = Mathf.Clamp01(interfaceVolume);
            Screen.fullScreen = fullscreen;
            textScale = Mathf.Clamp(textScale, 0.85f, 1.5f);
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out AudioManager audio))
            {
                audio.SetBusVolumes(musicVolume, effectsVolume, interfaceVolume);
            }
            if (!persist)
                return;

            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.SetFloat(EffectsVolumeKey, effectsVolume);
            PlayerPrefs.SetFloat(InterfaceVolumeKey, interfaceVolume);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(ReduceMotionKey, reduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(ScreenShakeKey, screenShake ? 1 : 0);
            PlayerPrefs.SetInt(HighContrastKey, highContrastIntents ? 1 : 0);
            PlayerPrefs.SetFloat(TextScaleKey, textScale);
            PlayerPrefs.Save();
        }
    }
}
