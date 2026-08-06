using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Presentation.Audio
{
    [CreateAssetMenu(menuName = "FFSS/Presentation/Audio Cue Catalog", fileName = "AudioCueCatalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        [SerializeField] private List<AudioCueDefinition> cues = new List<AudioCueDefinition>();

        public bool TryGet(string cueId, out AudioCueDefinition cue)
        {
            cue = cues.Find(item => item != null && item.CueId == cueId);
            return cue != null;
        }

        public AudioCueDefinition Get(string cueId)
        {
            if (TryGet(cueId, out AudioCueDefinition cue))
            {
                return cue;
            }

            throw new InvalidOperationException($"Audio cue is not configured: {cueId}");
        }
    }
}
