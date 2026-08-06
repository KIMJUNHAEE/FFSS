using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Presentation.Vfx
{
    [CreateAssetMenu(menuName = "FFSS/Presentation/VFX Cue Catalog", fileName = "VfxCueCatalog")]
    public sealed class VfxCueCatalog : ScriptableObject
    {
        [SerializeField] private List<VfxCueDefinition> cues = new List<VfxCueDefinition>();

        public VfxCueDefinition Get(string cueId)
        {
            VfxCueDefinition cue = cues.Find(item => item != null && item.CueId == cueId);
            if (cue == null)
            {
                throw new InvalidOperationException($"VFX cue is not configured: {cueId}");
            }

            return cue;
        }
    }
}
