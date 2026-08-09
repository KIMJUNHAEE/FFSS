using UnityEngine;

namespace CardBattle.Exploration
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FieldDangerAtmospherePulse : MonoBehaviour
    {
        [SerializeField] private Light beaconLight;
        [SerializeField, Min(0f)] private float baseIntensity = 0.7f;
        [SerializeField, Min(0f)] private float pulseAmplitude = 0.18f;
        [SerializeField, Min(0.05f)] private float pulseSpeed = 1.1f;
        [SerializeField] private float phaseOffset;

        private void OnEnable()
        {
            ApplyIntensity(0f);
        }

        private void Update()
        {
            float phase = Application.isPlaying
                ? Time.unscaledTime * pulseSpeed + phaseOffset
                : phaseOffset;
            ApplyIntensity(Mathf.Sin(phase) * pulseAmplitude);
        }

        private void ApplyIntensity(float offset)
        {
            if (beaconLight != null)
                beaconLight.intensity = Mathf.Max(0f, baseIntensity + offset);
        }
    }
}
