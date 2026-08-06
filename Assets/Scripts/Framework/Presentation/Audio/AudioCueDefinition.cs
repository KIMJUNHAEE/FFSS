using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace FFSS.Framework.Presentation.Audio
{
    public enum AudioBus
    {
        Music,
        Ambience,
        Effects,
        Interface,
        Voice
    }

    [CreateAssetMenu(menuName = "FFSS/Presentation/Audio Cue", fileName = "AudioCue")]
    public sealed class AudioCueDefinition : ScriptableObject
    {
        [SerializeField] private string cueId;
        [SerializeField] private AudioBus bus = AudioBus.Effects;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
        [SerializeField] private bool loop;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private Vector2 pitchRange = Vector2.one;
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(1)] private int maximumInstances = 4;
        [Header("Sequence mix")]
        [Tooltip("Number of plays kept at full volume after AudioManager.BeginSequence. Zero disables repeat attenuation.")]
        [SerializeField, Min(0)] private int fullVolumePlayCount;
        [Tooltip("Volume change applied after the full-volume play count is exhausted.")]
        [SerializeField, Range(-24f, 0f)] private float repeatedVolumeDb;

        public string CueId => cueId;
        public AudioBus Bus => bus;
        public AudioMixerGroup Output => output;
        public bool Loop => loop;
        public float Volume => volume;
        public float SpatialBlend => spatialBlend;
        public float CooldownSeconds => cooldownSeconds;
        public int MaximumInstances => maximumInstances;
        public int FullVolumePlayCount => fullVolumePlayCount;
        public float RepeatedVolumeDb => repeatedVolumeDb;

        public AudioClip PickClip(AudioClip avoid = null)
        {
            if (clips.Count == 0)
            {
                return null;
            }

            if (clips.Count == 1 || avoid == null)
            {
                return clips[Random.Range(0, clips.Count)];
            }

            int start = Random.Range(0, clips.Count);
            for (int offset = 0; offset < clips.Count; offset++)
            {
                AudioClip candidate = clips[(start + offset) % clips.Count];
                if (candidate != avoid)
                {
                    return candidate;
                }
            }

            return clips[start];
        }

        public float PickPitch()
        {
            float minimum = Mathf.Min(pitchRange.x, pitchRange.y);
            float maximum = Mathf.Max(pitchRange.x, pitchRange.y);
            return Random.Range(minimum, maximum);
        }

        public float VolumeForSequencePlay(int zeroBasedPlayIndex)
        {
            if (fullVolumePlayCount <= 0 || zeroBasedPlayIndex < fullVolumePlayCount)
            {
                return volume;
            }

            return volume * Mathf.Pow(10f, repeatedVolumeDb / 20f);
        }
    }
}
