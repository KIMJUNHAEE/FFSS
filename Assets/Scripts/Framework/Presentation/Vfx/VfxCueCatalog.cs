using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Presentation.Vfx
{
    [CreateAssetMenu(menuName = "FFSS/Presentation/VFX Cue Catalog", fileName = "VfxCueCatalog")]
    public sealed class VfxCueCatalog : ScriptableObject
    {
        [SerializeField] private List<VfxCueDefinition> cues = new List<VfxCueDefinition>();

        public bool TryGet(string cueId, out VfxCueDefinition cue)
        {
            cue = cues.Find(item => item != null && item.CueId == cueId);
            return cue != null;
        }

        public VfxCueDefinition Get(string cueId)
        {
            if (!TryGet(cueId, out VfxCueDefinition cue))
            {
                throw new InvalidOperationException($"VFX cue is not configured: {cueId}");
            }

            return cue;
        }
    }
}
