using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Presentation.Vfx
{
    [CreateAssetMenu(menuName = "FFSS/Presentation/VFX Cue", fileName = "VfxCue")]
    public sealed class VfxCueDefinition : ScriptableObject
    {
        [SerializeField] private string cueId;
        [SerializeField] private List<GameObject> prefabs = new List<GameObject>();
        [SerializeField, Min(0.01f)] private float lifetime = 1f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private Vector3 defaultScale = Vector3.one;

        public string CueId => cueId;
        public float Lifetime => lifetime;
        public bool UseUnscaledTime => useUnscaledTime;
        public Vector3 DefaultScale => defaultScale;

        public GameObject PickPrefab()
        {
            if (prefabs.Count == 0)
            {
                return null;
            }

            return prefabs[Random.Range(0, prefabs.Count)];
        }
    }
}
