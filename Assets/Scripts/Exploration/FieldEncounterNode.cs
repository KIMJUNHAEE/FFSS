using FFSS.Framework.Flow;
using UnityEngine;

namespace CardBattle.Exploration
{
    [DisallowMultipleComponent]
    public sealed class FieldEncounterNode : MonoBehaviour
    {
        [SerializeField] private string enemyId;
        [SerializeField] private Transform player;
        [SerializeField] private FieldEncounterMarkerView markerView;
        [SerializeField, Min(0.25f)] private float focusRadius = 1.6f;
        [SerializeField, Min(0.2f)] private float activationRadius = 0.85f;
        [SerializeField, Min(0f)] private float activationDelay = 0.12f;

        private float enteredAt = -1f;
        private bool loading;

        public string EnemyId => enemyId;

        public void Configure(string id, Transform playerTarget, float radius)
        {
            enemyId = id;
            player = playerTarget;
            activationRadius = Mathf.Max(0.2f, radius);
            focusRadius = Mathf.Max(activationRadius + 0.35f, activationRadius * 1.75f);
            markerView = markerView != null ? markerView : GetComponent<FieldEncounterMarkerView>();
        }

        private void Update()
        {
            if (!Application.isPlaying || loading || player == null || string.IsNullOrWhiteSpace(enemyId))
                return;

            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            markerView?.SetFocused(distance <= focusRadius);

            if (distance > activationRadius)
            {
                enteredAt = -1f;
                return;
            }

            if (enteredAt < 0f)
            {
                enteredAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - enteredAt < activationDelay || !GameKernelReady())
                return;

            loading = FFSS.Framework.Core.GameKernel.Services
                .Get<EncounterFlowManager>()
                .TryEnterEncounter(enemyId);
        }

        private static bool GameKernelReady()
        {
            return FFSS.Framework.Core.GameKernel.IsReady;
        }
    }
}
