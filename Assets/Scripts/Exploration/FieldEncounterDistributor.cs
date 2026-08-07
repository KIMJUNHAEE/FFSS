using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Exploration
{
    [Serializable]
    public sealed class FieldLandmarkVisualDefinition
    {
        [Range(1, 3)] public int act = 1;
        public RunFieldContentType contentType = RunFieldContentType.Event;
        [Min(1)] public int variant = 1;
        public string displayName;
        public Sprite sprite;
        [Min(0.25f)] public float targetHeight = 3.2f;
        public Vector2 localOffset;
    }

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
        [SerializeField] private GameObject bossDoorMarkerPrefab;
        [SerializeField] private GameObject ambientLandmarkPrefab;
        [SerializeField, Min(0.2f)] private float activationRadius = 0.85f;
        [SerializeField] private List<FieldLandmarkVisualDefinition> landmarkVisuals = new();

        private readonly List<GameObject> markerInstances = new();
        private readonly HashSet<Transform> occupiedTileRoots = new();
        private readonly Dictionary<Transform, float> occupiedTileRadii = new();

        private sealed class PlannedNode
        {
            public string nodeId;
            public RunFieldContentType type;
            public string contentId;
            public EncounterSceneEntry encounter;
            public int variant;
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

            PlannedNode boss = nodes.Find(value => value.type == RunFieldContentType.BossDoor);
            nodes.Remove(boss);

            var available = new List<GeneratedHexTile>();
            var interactionAnchors = new List<GeneratedHexTile>();
            GeneratedHexTile? bossTile = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                GeneratedHexTile tile = tiles[i];
                if (tile.Tile == null || tile.Order < 2)
                    continue;

                if (tile.IsBoss)
                    bossTile = tile;
                else if (tile.IsInteraction)
                    interactionAnchors.Add(tile);
                else
                    available.Add(tile);
            }

            if (interactionAnchors.Count >= nodes.Count)
                available = interactionAnchors;
            else
                available.InsertRange(0, interactionAnchors);

            available.Sort((left, right) => left.Order.CompareTo(right.Order));
            if (available.Count < nodes.Count)
            {
                Debug.LogWarning($"[FieldEncounterDistributor] {nodes.Count} nodes need {nodes.Count} tiles, but only {available.Count} are available.", this);
            }

            int count = Mathf.Min(nodes.Count, available.Count);
            for (int i = 0; i < count; i++)
            {
                int preferredIndex = Mathf.Clamp(
                    Mathf.FloorToInt((i + 1f) * available.Count / (count + 1f)),
                    0,
                    available.Count - 1);
                int tileIndex = FindClearTileIndex(available, preferredIndex, GetNodeFootprint(nodes[i].type));
                if (tileIndex < 0)
                    continue;

                GeneratedHexTile selected = available[tileIndex];
                CreateMarker(nodes[i], selected);
                available.RemoveAt(tileIndex);
            }

            if (boss != null)
            {
                GeneratedHexTile target = bossTile ?? (available.Count > 0 ? available[^1] : default);
                if (target.Tile != null)
                    CreateMarker(boss, target);
            }

            CreateAmbientLandmarks(tiles, act);
        }

        private List<PlannedNode> BuildPlan(RunActDefinition definition, int act)
        {
            var normalNodes = new Queue<PlannedNode>();
            int normalCount = Mathf.Max(0, definition.requiredNormalVictories);
            for (int i = 0; i < normalCount && definition.normalEnemyIds.Count > 0; i++)
            {
                string enemyId = definition.normalEnemyIds[i % definition.normalEnemyIds.Count];
                normalNodes.Enqueue(EncounterNode(act, RunFieldContentType.Combat, i + 1, enemyId));
            }

            var eventNodes = new Queue<PlannedNode>();
            int eventCount = Mathf.Min(definition.requiredEvents, definition.eventIds.Count);
            for (int i = 0; i < eventCount; i++)
            {
                eventNodes.Enqueue(ContentNode(act, RunFieldContentType.Event, i + 1, definition.eventIds[i]));
            }

            var shopNodes = new Queue<PlannedNode>();
            for (int i = 0; i < definition.shopCount; i++)
                shopNodes.Enqueue(ContentNode(act, RunFieldContentType.Shop, i + 1, $"shop.act{act}.{i + 1}"));

            // Rest is an act-transition choice after 13/18, never a field landmark.
            var nodes = WeaveFieldRoute(act, normalNodes, eventNodes, shopNodes);

            if (definition.midBossIds.Count > 0)
            {
                int index = Mathf.Abs((GetRunSeed() + act * 31) % definition.midBossIds.Count);
                nodes.Add(EncounterNode(act, RunFieldContentType.MidBoss, 1, definition.midBossIds[index]));
            }

            if (!string.IsNullOrWhiteSpace(definition.bossId))
                nodes.Add(EncounterNode(act, RunFieldContentType.BossDoor, 1, definition.bossId));

            return nodes;
        }

        private static List<PlannedNode> WeaveFieldRoute(
            int act,
            Queue<PlannedNode> normalNodes,
            Queue<PlannedNode> eventNodes,
            Queue<PlannedNode> shopNodes)
        {
            var nodes = new List<PlannedNode>();
            RunFieldContentType[] cadence = act switch
            {
                1 => new[]
                {
                    RunFieldContentType.Event, RunFieldContentType.Combat, RunFieldContentType.Combat,
                    RunFieldContentType.Shop, RunFieldContentType.Combat, RunFieldContentType.Event,
                    RunFieldContentType.Combat, RunFieldContentType.Event, RunFieldContentType.Combat
                },
                2 => new[]
                {
                    RunFieldContentType.Combat, RunFieldContentType.Event, RunFieldContentType.Combat,
                    RunFieldContentType.Shop, RunFieldContentType.Combat, RunFieldContentType.Event,
                    RunFieldContentType.Combat, RunFieldContentType.Event, RunFieldContentType.Combat,
                    RunFieldContentType.Shop, RunFieldContentType.Combat, RunFieldContentType.Event
                },
                _ => new[]
                {
                    RunFieldContentType.Event, RunFieldContentType.Combat, RunFieldContentType.Combat,
                    RunFieldContentType.Shop, RunFieldContentType.Combat, RunFieldContentType.Event,
                    RunFieldContentType.Combat, RunFieldContentType.Event, RunFieldContentType.Combat,
                    RunFieldContentType.Shop, RunFieldContentType.Combat, RunFieldContentType.Event,
                    RunFieldContentType.Combat, RunFieldContentType.Event
                }
            };

            for (int i = 0; i < cadence.Length; i++)
                TryAddNext(nodes, cadence[i], normalNodes, eventNodes, shopNodes);

            while (normalNodes.Count > 0 || eventNodes.Count > 0 || shopNodes.Count > 0)
            {
                TryAddNext(nodes, RunFieldContentType.Combat, normalNodes, eventNodes, shopNodes);
                TryAddNext(nodes, RunFieldContentType.Event, normalNodes, eventNodes, shopNodes);
                TryAddNext(nodes, RunFieldContentType.Shop, normalNodes, eventNodes, shopNodes);
            }

            return nodes;
        }

        private static bool TryAddNext(
            List<PlannedNode> nodes,
            RunFieldContentType type,
            Queue<PlannedNode> normalNodes,
            Queue<PlannedNode> eventNodes,
            Queue<PlannedNode> shopNodes)
        {
            Queue<PlannedNode> source = type switch
            {
                RunFieldContentType.Combat => normalNodes,
                RunFieldContentType.Event => eventNodes,
                RunFieldContentType.Shop => shopNodes,
                _ => null
            };

            if (source == null || source.Count == 0)
                return false;

            nodes.Add(source.Dequeue());
            return true;
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
                encounter = catalog.Get(enemyId),
                variant = index
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
                contentId = contentId,
                variant = index
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
            FieldLandmarkVisualDefinition landmark = FindLandmark(GetCurrentAct(), planned.type, planned.variant);
            if (landmark?.sprite != null)
            {
                view?.ConfigureLandmark(
                    landmark.sprite,
                    landmark.displayName,
                    landmark.targetHeight,
                    landmark.localOffset);
            }
            view?.ConfigureMarkerType(planned.type);
            if (planned.encounter != null && planned.type != RunFieldContentType.BossDoor)
                view?.Configure(planned.encounter.encounter);

            if (planned.type == RunFieldContentType.Combat || planned.type == RunFieldContentType.MidBoss)
            {
                FieldEncounterNode encounter = marker.GetComponent<FieldEncounterNode>();
                if (encounter != null)
                {
                    float radius = view != null
                        ? Mathf.Max(activationRadius, view.SuggestedActivationRadius)
                        : activationRadius;
                    encounter.Configure(planned.contentId, player, radius, planned.nodeId);
                    encounter.enabled = Application.isPlaying;
                }
            }
            else
            {
                FieldRunContentNode content = marker.GetComponent<FieldRunContentNode>();
                if (content != null)
                {
                    float radius = view != null
                        ? Mathf.Max(activationRadius, view.SuggestedActivationRadius)
                        : activationRadius;
                    content.Configure(
                        planned.nodeId,
                        planned.type,
                        planned.contentId,
                        player,
                        radius,
                        landmark?.displayName);
                    content.enabled = Application.isPlaying;
                }
            }

            markerInstances.Add(marker);
            occupiedTileRoots.Add(tile.Tile.transform);
            occupiedTileRadii[tile.Tile.transform] = GetNodeFootprint(planned.type);
        }

        private void CreateAmbientLandmarks(IReadOnlyList<GeneratedHexTile> tiles, int act)
        {
            const float actorClearance = 3.2f;
            const float landmarkClearance = 4.4f;
            const float landmarkFootprint = 2.2f;
            if (ambientLandmarkPrefab == null)
                return;

            var landmarks = new List<FieldLandmarkVisualDefinition>();
            for (int i = 0; i < landmarkVisuals.Count; i++)
            {
                FieldLandmarkVisualDefinition visual = landmarkVisuals[i];
                if (visual != null && visual.act == act &&
                    visual.contentType == RunFieldContentType.Road && visual.sprite != null)
                {
                    landmarks.Add(visual);
                }
            }

            if (landmarks.Count == 0)
                return;

            var candidates = new List<GeneratedHexTile>();
            for (int i = 0; i < tiles.Count; i++)
            {
                GeneratedHexTile tile = tiles[i];
                if (tile.Tile == null || tile.Order < 2 || tile.IsInteraction || tile.IsBoss ||
                    occupiedTileRoots.Contains(tile.Tile.transform))
                    continue;

                Vector3 position = tile.Tile.transform.position;
                if (player != null && ExplorationGeometryUtility.PlanarSqrDistance(position, player.position) < actorClearance * actorClearance)
                    continue;

                if (IsClearOfOccupied(position, landmarkFootprint))
                    candidates.Add(tile);
            }
            candidates.Sort((left, right) => left.Order.CompareTo(right.Order));

            for (int i = 0; i < landmarks.Count && candidates.Count > 0; i++)
            {
                GeneratedHexTile tile = candidates[0];
                GameObject marker = Instantiate(ambientLandmarkPrefab, tile.Tile.transform);
                marker.name = $"Field Landmark - Act {act} - {landmarks[i].displayName}";
                marker.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                marker.transform.localScale = Vector3.one;
                if (!Application.isPlaying)
                    marker.hideFlags = HideFlags.DontSave;

                marker.GetComponent<FieldEncounterMarkerView>()?.ConfigureLandmark(
                    landmarks[i].sprite,
                    landmarks[i].displayName,
                    landmarks[i].targetHeight,
                    landmarks[i].localOffset);
                markerInstances.Add(marker);
                occupiedTileRoots.Add(tile.Tile.transform);
                occupiedTileRadii[tile.Tile.transform] = landmarkFootprint;
                Vector3 landmarkPosition = tile.Tile.transform.position;
                candidates.RemoveAll(candidate =>
                    candidate.Tile == null ||
                    ExplorationGeometryUtility.PlanarSqrDistance(candidate.Tile.transform.position, landmarkPosition) <
                    landmarkClearance * landmarkClearance);
            }
        }

        private int FindClearTileIndex(
            IReadOnlyList<GeneratedHexTile> candidates,
            int preferredIndex,
            float footprint)
        {
            int bestIndex = -1;
            int bestIndexDistance = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                GeneratedHexTile candidate = candidates[i];
                if (candidate.Tile == null || occupiedTileRoots.Contains(candidate.Tile.transform) ||
                    !IsClearOfOccupied(candidate.Tile.transform.position, footprint))
                    continue;

                int indexDistance = Mathf.Abs(i - preferredIndex);
                if (indexDistance >= bestIndexDistance)
                    continue;

                bestIndex = i;
                bestIndexDistance = indexDistance;
            }

            return bestIndex;
        }

        private bool IsClearOfOccupied(Vector3 position, float footprint)
        {
            foreach (KeyValuePair<Transform, float> occupied in occupiedTileRadii)
            {
                if (occupied.Key == null)
                    continue;

                float clearance = footprint + occupied.Value;
                if (ExplorationGeometryUtility.PlanarSqrDistance(position, occupied.Key.position) < clearance * clearance)
                    return false;
            }

            return true;
        }

        private static float GetNodeFootprint(RunFieldContentType type)
        {
            return type switch
            {
                RunFieldContentType.Shop => 2.25f,
                RunFieldContentType.BossDoor => 2.5f,
                RunFieldContentType.MidBoss => 2.1f,
                RunFieldContentType.Event => 1.75f,
                RunFieldContentType.Combat => 1.75f,
                _ => 1.5f
            };
        }

        private GameObject GetMarkerPrefab(RunFieldContentType type)
        {
            return type switch
            {
                RunFieldContentType.MidBoss => midBossMarkerPrefab,
                RunFieldContentType.Event => eventMarkerPrefab,
                RunFieldContentType.Shop => shopMarkerPrefab,
                RunFieldContentType.BossDoor => bossDoorMarkerPrefab,
                _ => normalMarkerPrefab
            };
        }

        private FieldLandmarkVisualDefinition FindLandmark(
            int act,
            RunFieldContentType type,
            int variant)
        {
            FieldLandmarkVisualDefinition fallback = null;
            for (int i = 0; i < landmarkVisuals.Count; i++)
            {
                FieldLandmarkVisualDefinition candidate = landmarkVisuals[i];
                if (candidate == null || candidate.act != act || candidate.contentType != type)
                    continue;

                fallback ??= candidate;
                if (candidate.variant == variant)
                    return candidate;
            }

            return fallback;
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
                   (definition.midBossIds.Count > 0 ? 1 : 0) +
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
            occupiedTileRoots.Clear();
            occupiedTileRadii.Clear();
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
