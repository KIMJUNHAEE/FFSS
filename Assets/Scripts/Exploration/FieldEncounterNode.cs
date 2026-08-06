using FFSS.Framework.Flow;
using FFSS.Framework.Core;
using FFSS.Framework.UI;
using UnityEngine;

namespace CardBattle.Exploration
{
    [DisallowMultipleComponent]
    public sealed class FieldEncounterNode : MonoBehaviour
    {
        [SerializeField] private string enemyId;
        [SerializeField] private string nodeId;
        [SerializeField] private Transform player;
        [SerializeField] private FieldEncounterMarkerView markerView;
        [SerializeField, Min(0.25f)] private float focusRadius = 1.6f;
        [SerializeField, Min(0.2f)] private float activationRadius = 0.85f;
        [SerializeField, Min(0f)] private float activationDelay = 0.12f;

        [Header("Field patrol")]
        [SerializeField] private bool patrol = false;
        [SerializeField, Min(0.1f)] private float patrolRadius = 0.62f;
        [SerializeField, Min(0.05f)] private float patrolSpeed = 0.34f;
        [SerializeField, Min(0f)] private float patrolPause = 0.7f;

        private float enteredAt = -1f;
        private bool loading;
        private Vector3 patrolOrigin;
        private Vector3 patrolTarget;
        private float nextPatrolAt;
        private int patrolStep;

        public string EnemyId => enemyId;
        public string NodeId => nodeId;

        private void Start()
        {
            patrolOrigin = transform.localPosition;
            patrolTarget = patrolOrigin;
            nextPatrolAt = Time.time + patrolPause;
        }

        public void Configure(string id, Transform playerTarget, float radius, string encounterNodeId = "")
        {
            enemyId = id;
            nodeId = encounterNodeId ?? string.Empty;
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
            UpdatePatrol(distance);

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
                .TryEnterEncounter(enemyId, nodeId);
        }

        private void UpdatePatrol(float playerDistance)
        {
            if (!patrol || loading || playerDistance <= focusRadius || IsWorldPaused())
                return;

            Vector3 current = transform.localPosition;
            if ((current - patrolTarget).sqrMagnitude <= 0.01f)
            {
                if (Time.time < nextPatrolAt)
                    return;

                patrolStep++;
                float angle = (Animator.StringToHash(enemyId ?? name) * 0.0001f) +
                              patrolStep * 2.399963f;
                patrolTarget = patrolOrigin + new Vector3(
                    Mathf.Cos(angle) * patrolRadius,
                    0f,
                    Mathf.Sin(angle) * patrolRadius);
                nextPatrolAt = Time.time + patrolPause;
            }

            transform.localPosition = Vector3.MoveTowards(
                current,
                patrolTarget,
                patrolSpeed * Time.deltaTime);
        }

        private static bool IsWorldPaused()
        {
            return GameKernel.IsReady &&
                   GameKernel.Services.TryGet(out UIManager ui) &&
                   ui.HasVisibleModal;
        }

        private static bool GameKernelReady()
        {
            return FFSS.Framework.Core.GameKernel.IsReady;
        }
    }
}
