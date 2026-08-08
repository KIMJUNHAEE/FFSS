using System.Collections;
using System.Collections.Generic;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.Presentation.Audio
{
    public sealed class AudioManager : GameServiceBehaviour
    {
        [SerializeField] private AudioCueCatalog catalog;
        [Header("Music crossfade")]
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField, Min(0f)] private float defaultMusicFadeSeconds = 0.8f;
        [Header("One shot pool")]
        [SerializeField] private List<AudioSource> oneShotSources = new List<AudioSource>();

        private readonly Dictionary<int, AudioSource> activeSources = new Dictionary<int, AudioSource>();
        private readonly Dictionary<int, string> activeCueIds = new Dictionary<int, string>();
        private readonly Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>();
        private readonly Dictionary<string, AudioClip> lastPlayedClips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, int> sequencePlayCounts = new Dictionary<string, int>();
        private int nextPlaybackId = 1;
        private bool useFirstMusicSource = true;
        private Coroutine musicFade;
        private Coroutine musicDuck;
        private float duckRestoreVolumeA;
        private float duckRestoreVolumeB;

        public int TotalPlayCount { get; private set; }
        public string CurrentMusicCueId { get; private set; } = string.Empty;

        public int Play(string cueId, Vector3? worldPosition = null)
        {
            AudioCueDefinition cue = catalog.Get(cueId);
            if (cue.Bus == AudioBus.Music)
            {
                PlayMusic(cueId, defaultMusicFadeSeconds);
                return 0;
            }

            if (!CanPlay(cue))
            {
                return 0;
            }

            AudioSource source = FindAvailableSource();
            lastPlayedClips.TryGetValue(cue.CueId, out AudioClip previousClip);
            AudioClip clip = cue.PickClip(previousClip);
            if (source == null || clip == null)
            {
                return 0;
            }

            int playbackId = nextPlaybackId++;
            sequencePlayCounts.TryGetValue(cue.CueId, out int sequencePlayIndex);
            ConfigureSource(source, cue, clip, cue.VolumeForSequencePlay(sequencePlayIndex), worldPosition);
            activeSources[playbackId] = source;
            activeCueIds[playbackId] = cue.CueId;
            lastPlayedAt[cue.CueId] = Time.unscaledTime;
            lastPlayedClips[cue.CueId] = clip;
            sequencePlayCounts[cue.CueId] = sequencePlayIndex + 1;
            source.Play();
            TotalPlayCount++;
            return playbackId;
        }

        public void BeginSequence()
        {
            sequencePlayCounts.Clear();
        }

        public void Stop(int playbackId)
        {
            if (!activeSources.TryGetValue(playbackId, out AudioSource source))
            {
                return;
            }

            source.Stop();
            Release(playbackId);
        }

        public void PlayMusic(string cueId, float fadeSeconds)
        {
            if (CurrentMusicCueId == cueId &&
                ((musicSourceA != null && musicSourceA.isPlaying) ||
                 (musicSourceB != null && musicSourceB.isPlaying)))
            {
                return;
            }

            AudioCueDefinition cue = catalog.Get(cueId);
            AudioClip clip = cue.PickClip();
            if (clip == null)
            {
                return;
            }

            CurrentMusicCueId = cueId;

            AudioSource incoming = useFirstMusicSource ? musicSourceA : musicSourceB;
            AudioSource outgoing = useFirstMusicSource ? musicSourceB : musicSourceA;
            useFirstMusicSource = !useFirstMusicSource;

            if (musicFade != null)
            {
                StopCoroutine(musicFade);
            }

            musicFade = StartCoroutine(CrossfadeMusic(outgoing, incoming, cue, clip, fadeSeconds));
        }

        public void DuckMusic(float durationSeconds, float volumeMultiplier = 0.55f)
        {
            if (musicDuck != null)
            {
                StopCoroutine(musicDuck);
                SetMusicVolumes(duckRestoreVolumeA, duckRestoreVolumeB);
            }

            duckRestoreVolumeA = musicSourceA != null ? musicSourceA.volume : 0f;
            duckRestoreVolumeB = musicSourceB != null ? musicSourceB.volume : 0f;
            musicDuck = StartCoroutine(DuckMusicRoutine(
                Mathf.Max(0.02f, durationSeconds),
                Mathf.Clamp01(volumeMultiplier)));
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            activeSources.Clear();
            activeCueIds.Clear();
            lastPlayedAt.Clear();
            lastPlayedClips.Clear();
            sequencePlayCounts.Clear();
            TotalPlayCount = 0;
            CurrentMusicCueId = string.Empty;
            musicDuck = null;
        }

        protected override void OnShutdown()
        {
            StopAllCoroutines();
            for (int i = 0; i < oneShotSources.Count; i++)
            {
                if (oneShotSources[i] != null)
                {
                    oneShotSources[i].Stop();
                }
            }

            musicSourceA?.Stop();
            musicSourceB?.Stop();
            activeSources.Clear();
            activeCueIds.Clear();
            lastPlayedAt.Clear();
            lastPlayedClips.Clear();
            sequencePlayCounts.Clear();
            CurrentMusicCueId = string.Empty;
        }

        private void Update()
        {
            if (activeSources.Count == 0)
            {
                return;
            }

            var completed = new List<int>();
            foreach (KeyValuePair<int, AudioSource> pair in activeSources)
            {
                if (pair.Value == null || !pair.Value.isPlaying)
                {
                    completed.Add(pair.Key);
                }
            }

            for (int i = 0; i < completed.Count; i++)
            {
                Release(completed[i]);
            }
        }

        private bool CanPlay(AudioCueDefinition cue)
        {
            if (lastPlayedAt.TryGetValue(cue.CueId, out float lastPlayed) &&
                Time.unscaledTime - lastPlayed < cue.CooldownSeconds)
            {
                return false;
            }

            int instances = 0;
            foreach (string activeCueId in activeCueIds.Values)
            {
                if (activeCueId == cue.CueId)
                {
                    instances++;
                }
            }

            return instances < cue.MaximumInstances;
        }

        private AudioSource FindAvailableSource()
        {
            for (int i = 0; i < oneShotSources.Count; i++)
            {
                AudioSource source = oneShotSources[i];
                if (source != null && !source.isPlaying)
                {
                    return source;
                }
            }

            return null;
        }

        private static void ConfigureSource(
            AudioSource source,
            AudioCueDefinition cue,
            AudioClip clip,
            float volume,
            Vector3? worldPosition)
        {
            source.clip = clip;
            source.outputAudioMixerGroup = cue.Output;
            source.loop = cue.Loop;
            source.volume = volume;
            source.pitch = cue.PickPitch();
            source.spatialBlend = worldPosition.HasValue ? cue.SpatialBlend : 0f;
            if (worldPosition.HasValue)
            {
                source.transform.position = worldPosition.Value;
            }
        }

        private IEnumerator CrossfadeMusic(
            AudioSource outgoing,
            AudioSource incoming,
            AudioCueDefinition cue,
            AudioClip clip,
            float fadeSeconds)
        {
            incoming.clip = clip;
            incoming.outputAudioMixerGroup = cue.Output;
            incoming.loop = true;
            incoming.pitch = cue.PickPitch();
            incoming.volume = 0f;
            incoming.Play();
            TotalPlayCount++;

            float outgoingVolume = outgoing != null ? outgoing.volume : 0f;
            float duration = Mathf.Max(0.01f, fadeSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                incoming.volume = Mathf.Lerp(0f, cue.Volume, t);
                if (outgoing != null)
                {
                    outgoing.volume = Mathf.Lerp(outgoingVolume, 0f, t);
                }

                yield return null;
            }

            incoming.volume = cue.Volume;
            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
            }

            musicFade = null;
        }

        private IEnumerator DuckMusicRoutine(float durationSeconds, float volumeMultiplier)
        {
            float startA = duckRestoreVolumeA;
            float startB = duckRestoreVolumeB;
            float attackSeconds = Mathf.Min(0.04f, durationSeconds * 0.25f);
            float releaseSeconds = Mathf.Max(0.01f, durationSeconds - attackSeconds);

            float elapsed = 0f;
            while (elapsed < attackSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float multiplier = Mathf.Lerp(1f, volumeMultiplier, Mathf.Clamp01(elapsed / attackSeconds));
                SetMusicVolumes(startA * multiplier, startB * multiplier);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < releaseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float multiplier = Mathf.Lerp(volumeMultiplier, 1f, Mathf.Clamp01(elapsed / releaseSeconds));
                SetMusicVolumes(startA * multiplier, startB * multiplier);
                yield return null;
            }

            SetMusicVolumes(startA, startB);
            musicDuck = null;
        }

        private void SetMusicVolumes(float volumeA, float volumeB)
        {
            if (musicSourceA != null)
            {
                musicSourceA.volume = volumeA;
            }

            if (musicSourceB != null)
            {
                musicSourceB.volume = volumeB;
            }
        }

        private void Release(int playbackId)
        {
            activeSources.Remove(playbackId);
            activeCueIds.Remove(playbackId);
        }
    }
}
