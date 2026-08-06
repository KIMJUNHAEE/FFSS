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

        public string CueId => cueId;
        public AudioBus Bus => bus;
        public AudioMixerGroup Output => output;
        public bool Loop => loop;
        public float Volume => volume;
        public float SpatialBlend => spatialBlend;
        public float CooldownSeconds => cooldownSeconds;
        public int MaximumInstances => maximumInstances;

        public AudioClip PickClip()
        {
            if (clips.Count == 0)
            {
                return null;
            }

            return clips[Random.Range(0, clips.Count)];
        }

        public float PickPitch()
        {
            float minimum = Mathf.Min(pitchRange.x, pitchRange.y);
            float maximum = Mathf.Max(pitchRange.x, pitchRange.y);
            return Random.Range(minimum, maximum);
        }
    }
}
