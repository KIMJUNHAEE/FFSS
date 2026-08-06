using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Exploration
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HexTileMapGenerator))]
    public sealed class FieldEncounterDistributor : MonoBehaviour
    {
        [Header("Run data")]
        [SerializeField] private EncounterSceneCatalog catalog;
        [SerializeField] private RunCampaignDefinition campaign;
        [SerializeField, Range(1, 3)] private int previewAct = 1;
        [SerializeField] private bool startRunWhenOpenedDirectly = true;
        [SerializeField] private int directOpenSeed = 1701;

        [Header("Scene references")]
        [SerializeField] private HexTileMapGenerator mapGenerator;
        [SerializeField] private Transform player;

        [Header("Inspectable marker prefabs")]
        [SerializeField] private GameObject normalMarkerPrefab;
        [SerializeField] private GameObject midBossMarkerPrefab;
        [SerializeField] private GameObject eventMarkerPrefab;
        [SerializeField] private GameObject shopMarkerPrefab;
        [SerializeField] private GameObject restMarkerPrefab;
        [SerializeField] private GameObject bossDoorMarkerPrefab;
        [SerializeField, Min(0.2f)] private float activationRadius = 0.85f;

        private readonly List<GameObject> markerInstances = new();

        private sealed class PlannedNode
        {
            public string nodeId;
            public RunFieldContentType type;
            public string contentId;
            public EncounterSceneEntry encounter;
        }

        private void Awake()
        {
            ResolveReferences();
            ConfigureRuntimeLayout();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (mapGenerator == null)
                return;

            mapGenerator.GenerationStarted += ClearMarkers;
            mapGenerator.GenerationCompleted += BuildMarkers;
            if (mapGenerator.GeneratedTiles.Count > 0)
                BuildMarkers(mapGenerator.GeneratedTiles);
        }

        private void OnDisable()
        {
            if (mapGenerator != null)
            {
                mapGenerator.GenerationStarted -= ClearMarkers;
                mapGenerator.GenerationCompleted -= BuildMarkers;
            }

            ClearMarkers();
        }

        private void ResolveReferences()
        {
            if (mapGenerator == null)
                mapGenerator = GetComponent<HexTileMapGenerator>();

            if (player == null)
            {
                QuarterViewPlayerController controller = FindAnyObjectByType<QuarterViewPlayerController>();
                if (controller != null)
                    player = controller.transform;
            }
        }

        private void ConfigureRuntimeLayout()
        {
            if (!Application.isPlaying || mapGenerator == null || !GameKernel.IsReady)
                return;

            RunManager runs = GameKernel.Services.Get<RunManager>();
            if (!runs.HasActiveRun && startRunWhenOpenedDirectly)
                runs.StartNewRun(directOpenSeed);

            if (!runs.HasActiveRun)
                return;

            int act = Mathf.Clamp(runs.Current.act, 1, 3);
            RunActDefinition definition = campaign.GetAct(act);
            mapGenerator.ConfigureRunLayout(
                runs.Current.CurrentActProgress.generatedTileCount,
                CountPlannedNodes(definition));
            mapGenerator.SetRuntimeSeed(unchecked(runs.Current.seed ^ (act * 486187739)));
        }

        private void BuildMarkers(IReadOnlyList<GeneratedHexTile> tiles)
        {
            ClearMarkers();
            if (catalog == null || campaign == null || tiles == null)
                return;

            int act = GetCurrentAct();
            RunActDefinition definition = campaign.GetAct(act);
            List<PlannedNode> nodes = BuildPlan(definition, act);
            if (nodes.Count == 0)
                return;

            var available = new List<GeneratedHexTile>();
            GeneratedHexTile? bossTile = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                GeneratedHexTile tile = tiles[i];
                if (tile.Tile == null || tile.Order < 2)
                    continue;

                if (tile.IsBoss)
                    bossTile = tile;
                else
                    available.Add(tile);
            }

            available.Sort((left, right) => left.Order.CompareTo(right.Order));
            PlannedNode boss = nodes.Find(value => value.type == RunFieldContentType.BossDoor);
            nodes.Remove(boss);
            if (available.Count < nodes.Count)
            {
                Debug.LogWarning($"[FieldEncounterDistributor] {nodes.Count} nodes need {nodes.Count} tiles, but only {available.Count} are available.", this);
            }

            int count = Mathf.Min(nodes.Count, available.Count);
            for (int i = 0; i < count; i++)
            {
                int tileIndex = Mathf.Clamp(
                    Mathf.FloorToInt((i + 1f) * available.Count / (count + 1f)),
                    0,
                    available.Count - 1);
                CreateMarker(nodes[i], available[tileIndex]);
            }

            if (boss != null)
            {
                GeneratedHexTile target = bossTile ?? (available.Count > 0 ? available[^1] : default);
                if (target.Tile != null)
                    CreateMarker(boss, target);
            }
        }

        private List<PlannedNode> BuildPlan(RunActDefinition definition, int act)
        {
            var nodes = new List<PlannedNode>();
            int normalCount = Mathf.Max(0, definition.requiredNormalVictories);
            for (int i = 0; i < normalCount && definition.normalEnemyIds.Count > 0; i++)
            {
                string enemyId = definition.normalEnemyIds[i % definition.normalEnemyIds.Count];
                nodes.Add(EncounterNode(act, RunFieldContentType.Combat, i + 1, enemyId));
            }

            int eventCount = Mathf.Min(definition.requiredEvents, definition.eventIds.Count);
            for (int i = 0; i < eventCount; i++)
            {
                nodes.Add(ContentNode(act, RunFieldContentType.Event, i + 1, definition.eventIds[i]));
            }

            for (int i = 0; i < definition.shopCount; i++)
                nodes.Add(ContentNode(act, RunFieldContentType.Shop, i + 1, $"shop.act{act}.{i + 1}"));

            for (int i = 0; i < definition.restCount; i++)
                nodes.Add(ContentNode(act, RunFieldContentType.Rest, i + 1, $"rest.act{act}.{i + 1}"));

            if (definition.midBossIds.Count > 0)
            {
                int index = Mathf.Abs((GetRunSeed() + act * 31) % definition.midBossIds.Count);
                nodes.Add(EncounterNode(act, RunFieldContentType.MidBoss, 1, definition.midBossIds[index]));
            }

            if (!string.IsNullOrWhiteSpace(definition.bossId))
                nodes.Add(EncounterNode(act, RunFieldContentType.BossDoor, 1, definition.bossId));

            return nodes;
        }

        private PlannedNode EncounterNode(
            int act,
            RunFieldContentType type,
            int index,
            string enemyId)
        {
            return new PlannedNode
            {
                nodeId = $"act{act}.{type.ToString().ToLowerInvariant()}.{index:D2}",
                type = type,
                contentId = enemyId,
                encounter = catalog.Get(enemyId)
            };
        }

        private static PlannedNode ContentNode(
            int act,
            RunFieldContentType type,
            int index,
            string contentId)
        {
            return new PlannedNode
            {
                nodeId = $"act{act}.{type.ToString().ToLowerInvariant()}.{index:D2}",
                type = type,
                contentId = contentId
            };
        }

        private void CreateMarker(PlannedNode planned, GeneratedHexTile tile)
        {
            GameObject prefab = GetMarkerPrefab(planned.type);
            if (prefab == null || tile.Tile == null)
                return;

            RunState run = CurrentRun();
            if (run != null)
            {
                RunProgressionManager progression = GameKernel.Services.Get<RunProgressionManager>();
                progression.RegisterNode(planned.nodeId, planned.type, planned.contentId, tile.Cell.x, tile.Cell.y);
                RunFieldNodeState state = run.CurrentActProgress.fieldNodes.Find(
                    value => value != null && value.nodeId == planned.nodeId);
                if (state != null && state.resolved)
                    return;
            }

            GameObject marker = Instantiate(prefab, tile.Tile.transform);
            marker.name = $"Run Node - {planned.nodeId} - {planned.contentId}";
            marker.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            marker.transform.localScale = Vector3.one;
            if (!Application.isPlaying)
                marker.hideFlags = HideFlags.DontSave;

            FieldEncounterMarkerView view = marker.GetComponent<FieldEncounterMarkerView>();
            if (planned.encounter != null)
                view?.Configure(planned.encounter.encounter);

            if (planned.type == RunFieldContentType.Combat || planned.type == RunFieldContentType.MidBoss)
            {
                FieldEncounterNode encounter = marker.GetComponent<FieldEncounterNode>();
                if (encounter != null)
                {
                    encounter.Configure(planned.contentId, player, activationRadius, planned.nodeId);
                    encounter.enabled = Application.isPlaying;
                }
            }
            else
            {
                FieldRunContentNode content = marker.GetComponent<FieldRunContentNode>();
                if (content != null)
                {
                    content.Configure(planned.nodeId, planned.type, planned.contentId, player, activationRadius);
                    content.enabled = Application.isPlaying;
                }
            }

            markerInstances.Add(marker);
        }

        private GameObject GetMarkerPrefab(RunFieldContentType type)
        {
            return type switch
            {
                RunFieldContentType.MidBoss => midBossMarkerPrefab,
                RunFieldContentType.Event => eventMarkerPrefab,
                RunFieldContentType.Shop => shopMarkerPrefab,
                RunFieldContentType.Rest => restMarkerPrefab,
                RunFieldContentType.BossDoor => bossDoorMarkerPrefab,
                _ => normalMarkerPrefab
            };
        }

        private int GetCurrentAct()
        {
            RunState run = CurrentRun();
            return run != null ? Mathf.Clamp(run.act, 1, 3) : previewAct;
        }

        private int GetRunSeed()
        {
            RunState run = CurrentRun();
            return run != null ? run.seed : directOpenSeed;
        }

        private static int CountPlannedNodes(RunActDefinition definition)
        {
            return definition.requiredNormalVictories + definition.requiredEvents + definition.shopCount +
                   definition.restCount + (definition.midBossIds.Count > 0 ? 1 : 0) +
                   (!string.IsNullOrWhiteSpace(definition.bossId) ? 1 : 0);
        }

        private static RunState CurrentRun()
        {
            if (!Application.isPlaying || !GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
                return null;

            return runs.Current;
        }

        private void ClearMarkers()
        {
            for (int i = markerInstances.Count - 1; i >= 0; i--)
            {
                GameObject marker = markerInstances[i];
                if (marker == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(marker);
                else
                    DestroyImmediate(marker);
            }

            markerInstances.Clear();
        }
    }
}
