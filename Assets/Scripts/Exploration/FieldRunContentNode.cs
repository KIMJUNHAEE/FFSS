using CardBattle.UI;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using UnityEngine;

namespace CardBattle.Exploration
{
    [DisallowMultipleComponent]
    public sealed class FieldRunContentNode : MonoBehaviour
    {
        [SerializeField] private string nodeId;
        [SerializeField] private RunFieldContentType contentType;
        [SerializeField] private string contentId;
        [SerializeField] private Transform player;
        [SerializeField] private FieldEncounterMarkerView markerView;
        [SerializeField, Min(0.25f)] private float focusRadius = 1.6f;
        [SerializeField, Min(0.2f)] private float activationRadius = 0.85f;
        [SerializeField, Min(0f)] private float activationDelay = 0.12f;

        private float enteredAt = -1f;
        private bool opened;

        public string NodeId => nodeId;
        public RunFieldContentType ContentType => contentType;
        public string ContentId => contentId;

        public void Configure(
            string runNodeId,
            RunFieldContentType type,
            string runContentId,
            Transform playerTarget,
            float radius,
            string locationName = null)
        {
            nodeId = runNodeId ?? string.Empty;
            contentType = type;
            contentId = runContentId ?? string.Empty;
            player = playerTarget;
            activationRadius = Mathf.Max(0.2f, radius);
            focusRadius = Mathf.Max(activationRadius + 0.35f, activationRadius * 1.75f);
            markerView = markerView != null ? markerView : GetComponent<FieldEncounterMarkerView>();
            markerView?.Configure(FieldEncounterMarkerView.DisplayLabelFor(type), Accent(type));
        }

        private void Update()
        {
            if (!Application.isPlaying || player == null || string.IsNullOrWhiteSpace(nodeId))
                return;

            if (!GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out RunManager runs) ||
                !runs.HasActiveRun)
            {
                return;
            }

            float distance = ExplorationGeometryUtility.PlanarDistance(player.position, transform.position);
            markerView?.SetFocused(distance <= focusRadius);

            if (opened)
            {
                if (distance > focusRadius)
                    opened = false;
                return;
            }

            if (distance <= focusRadius)
                GameKernel.Services.Get<RunProgressionManager>().DiscoverNode(nodeId);

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

            if (Time.unscaledTime - enteredAt < activationDelay)
                return;

            OpenContent();
        }

        private void OpenContent()
        {
            RunState run = GameKernel.Services.Get<RunManager>().Current;
            RunFieldNodeState state = run?.CurrentActProgress.fieldNodes.Find(value => value != null && value.nodeId == nodeId);
            if (state == null || state.resolved)
                return;

            UIScreenId screenId;
            GameFlowState? flowState = null;
            switch (contentType)
            {
                case RunFieldContentType.Event:
                    screenId = UIScreenId.Event;
                    flowState = GameFlowState.Event;
                    break;
                case RunFieldContentType.Shop:
                    screenId = UIScreenId.Shop;
                    flowState = GameFlowState.Event;
                    GameKernel.Services.Get<RunProgressionManager>().ResolveNode(nodeId);
                    break;
                case RunFieldContentType.BossDoor:
                    screenId = UIScreenId.BossDoor;
                    break;
                default:
                    return;
            }

            if (flowState.HasValue &&
                !GameKernel.Services.Get<GameFlowManager>().TryChangeState(flowState.Value))
                return;

            opened = true;
            RunUIScreenController.ShowScreen(screenId, nodeId, contentId);
        }

        private static Color Accent(RunFieldContentType type)
        {
            return type switch
            {
                RunFieldContentType.Event => new Color(0.67f, 0.28f, 0.78f, 1f),
                RunFieldContentType.Shop => new Color(0.2f, 0.72f, 0.48f, 1f),
                RunFieldContentType.BossDoor => new Color(0.92f, 0.2f, 0.16f, 1f),
                _ => Color.white
            };
        }
    }
}
