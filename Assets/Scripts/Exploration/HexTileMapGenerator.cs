using System;
using System.Collections.Generic;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Exploration
{
    public readonly struct GeneratedHexTile
    {
        public GeneratedHexTile(GameObject tile, Vector2Int cell, bool isInteraction, bool isBoss, int order)
        {
            Tile = tile;
            Cell = cell;
            IsInteraction = isInteraction;
            IsBoss = isBoss;
            Order = order;
        }

        public GameObject Tile { get; }
        public Vector2Int Cell { get; }
        public bool IsInteraction { get; }
        public bool IsBoss { get; }
        public int Order { get; }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HexTileMapGenerator : MonoBehaviour
    {
        private static readonly Vector2Int[] Directions =
        {
            new(1, 0),
            new(1, -1),
            new(0, -1),
            new(-1, 0),
            new(-1, 1),
            new(0, 1),
        };

        [Header("Tiles")]
        [SerializeField] private string tileResourceFolder = "ClockworkTimekeeper/HexTiles";
        [SerializeField] private string plainRoadTextureName = "hex_plain_road";
        [SerializeField] private string cityTileResourceFolder = "ClockworkTimekeeper/HexTiles/City";
        [SerializeField] private string actOneTileResourceFolder = "ClockworkTimekeeper/HexTiles/FieldV6/Act1";
        [SerializeField] private string actTwoTileResourceFolder = "ClockworkTimekeeper/HexTiles/FieldV6/Act2";
        [SerializeField] private string actThreeTileResourceFolder = "ClockworkTimekeeper/HexTiles/FieldV6/Act3";
        [SerializeField]
        private string[] actOneRoadTextureNames =
        {
            "hex_act1_v6_01",
            "hex_act1_v6_02",
            "hex_act1_v6_03",
            "hex_act1_v6_04",
            "hex_act1_v6_05",
            "hex_act1_v6_06",
            "hex_act1_v6_07",
            "hex_act1_v6_08",
            "hex_act1_v6_09",
            "hex_act1_v6_10",
            "hex_act1_v6_11",
            "hex_act1_v6_13",
            "hex_act1_v6_14",
            "hex_act1_v6_16",
            "hex_act1_v6_17",
        };
        [SerializeField]
        private string[] actTwoRoadTextureNames =
        {
            "hex_act2_v6_01",
            "hex_act2_v6_02",
            "hex_act2_v6_03",
            "hex_act2_v6_04",
            "hex_act2_v6_05",
            "hex_act2_v6_06",
            "hex_act2_v6_07",
            "hex_act2_v6_08",
            "hex_act2_v6_09",
            "hex_act2_v6_10",
            "hex_act2_v6_11",
            "hex_act2_v6_12",
            "hex_act2_v6_14",
            "hex_act2_v6_16",
        };
        [SerializeField]
        private string[] actThreeRoadTextureNames =
        {
            "hex_act3_v6_01",
            "hex_act3_v6_02",
            "hex_act3_v6_03",
            "hex_act3_v6_04",
            "hex_act3_v6_05",
            "hex_act3_v6_06",
            "hex_act3_v6_07",
            "hex_act3_v6_08",
            "hex_act3_v6_09",
            "hex_act3_v6_10",
            "hex_act3_v6_11",
            "hex_act3_v6_12",
            "hex_act3_v6_13",
            "hex_act3_v6_15",
            "hex_act3_v6_16",
        };
        [SerializeField]
        private string[] actOneInteractionTextureNames =
        {
            "hex_act1_v6_12",
            "hex_act1_v6_14",
            "hex_act1_v6_15",
            "hex_act1_v6_18",
        };
        [SerializeField]
        private string[] actTwoInteractionTextureNames =
        {
            "hex_act2_v6_10",
            "hex_act2_v6_13",
            "hex_act2_v6_15",
            "hex_act2_v6_17",
            "hex_act2_v6_18",
        };
        [SerializeField]
        private string[] actThreeInteractionTextureNames =
        {
            "hex_act3_v6_05",
            "hex_act3_v6_14",
            "hex_act3_v6_17",
            "hex_act3_v6_18",
        };
        [SerializeField]
        private string[] interactionTextureNames =
        {
            "hex_heart_plaza",
            "hex_spade_plaza",
            "hex_club_plaza",
            "hex_diamond_plaza",
        };
        [SerializeField] private float tileRadius = 1.8f;
        [SerializeField] private float tileY = -0.03f;
        [SerializeField] private float plainRoadMeshScale = 1f;
        [SerializeField] private float interactionMeshScale = 1f;
        [SerializeField, Range(0f, 0.2f)] private float plainRoadUvPadding = 0f;
        [SerializeField, Range(0f, 0.2f)] private float interactionUvPadding = 0f;
        [SerializeField] private Vector2 fieldV6UvRadius = new(0.405f, 0.468f);
        [SerializeField] private Material tileMaterialTemplate = null;

        [Header("Layout")]
        [SerializeField] private int randomSeed = 0;
        [SerializeField, Min(12)] private int targetTileCount = 40;
        [SerializeField] private int mainPathLength = 18;
        [SerializeField] private int branchCount = 6;
        [SerializeField] private int minBranchLength = 3;
        [SerializeField] private int maxBranchLength = 4;
        [SerializeField] private int softRadiusLimit = 5;
        [SerializeField] private int minInteractionHexDistance = 4;
        [SerializeField, Range(0f, 1f)] private float interactionTileChance = 0f;
        [SerializeField, Min(1)] private int plannedContentNodeCount = 8;
        [SerializeField, Min(1)] private int layoutValidationAttempts = 24;
        [SerializeField, Range(0.1f, 0.5f)] private float maximumNarrowTileRatio = 0.33f;
        [SerializeField, Min(0)] private int maximumDeadEndTiles = 2;
        [SerializeField, Min(1)] private int maximumSingleWidthCorridorLength = 3;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool generatePreviewInEditMode = true;

        [Header("City Layout")]
        [SerializeField, Min(4)] private int districtSpacing = 5;
        [SerializeField, Min(3)] private int minimumDistrictCount = 5;
        [SerializeField, Min(4)] private int maximumDistrictCount = 10;
        [SerializeField, Min(1)] private int cityLoopConnections = 3;
        [SerializeField, Range(0f, 1f)] private float alleyExpansionChance = 0.34f;

        [Header("Player")]
        [SerializeField] private Transform playerTarget = null;
        [SerializeField] private bool placePlayerAtStart = true;

        private readonly List<GameObject> generatedTiles = new();
        private readonly List<GeneratedHexTile> generatedTileDescriptors = new();
        private readonly List<Material> generatedMaterials = new();
        private readonly Dictionary<string, Mesh> generatedMeshes = new();
        private int runtimeSeed;
        private bool hasRuntimeSeed;
        private int districtAct = 1;
        private RunFieldLayoutPattern districtLayoutPattern = RunFieldLayoutPattern.BroadRoadY;

        public event Action GenerationStarted;
        public event Action<IReadOnlyList<GeneratedHexTile>> GenerationCompleted;

        public IReadOnlyList<GeneratedHexTile> GeneratedTiles => generatedTileDescriptors;

        public bool TryGetCell(Vector3 worldPosition, out Vector2Int cell)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float usableRadius = Mathf.Max(0.1f, tileRadius);
            float maximumX = usableRadius * Mathf.Sqrt(3f) * 0.5f;
            float nearestDistance = float.MaxValue;
            cell = default;
            bool found = false;

            for (int i = 0; i < generatedTileDescriptors.Count; i++)
            {
                GeneratedHexTile descriptor = generatedTileDescriptors[i];
                if (descriptor.Tile == null)
                    continue;

                Vector3 offset = local - descriptor.Tile.transform.localPosition;
                float x = Mathf.Abs(offset.x);
                float z = Mathf.Abs(offset.z);
                if (x > maximumX || z + x / Mathf.Sqrt(3f) > usableRadius)
                    continue;

                float distance = offset.x * offset.x + offset.z * offset.z;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                cell = descriptor.Cell;
                found = true;
            }

            return found;
        }

        public bool TryGetWorldPosition(Vector2Int cell, out Vector3 worldPosition)
        {
            for (int i = 0; i < generatedTileDescriptors.Count; i++)
            {
                GeneratedHexTile descriptor = generatedTileDescriptors[i];
                if (descriptor.Cell != cell || descriptor.Tile == null)
                    continue;

                worldPosition = descriptor.Tile.transform.position;
                return true;
            }

            worldPosition = default;
            return false;
        }

        public Vector3 ConstrainMovement(Vector3 currentWorldPosition, Vector3 desiredWorldPosition)
        {
            if (generatedTileDescriptors.Count == 0 || IsWalkable(desiredWorldPosition, 0f))
                return desiredWorldPosition;

            Vector3 slideX = new(desiredWorldPosition.x, desiredWorldPosition.y, currentWorldPosition.z);
            Vector3 slideZ = new(currentWorldPosition.x, desiredWorldPosition.y, desiredWorldPosition.z);
            bool canSlideX = IsWalkable(slideX, 0f);
            bool canSlideZ = IsWalkable(slideZ, 0f);
            if (canSlideX && canSlideZ)
            {
                float xDistance = (slideX - currentWorldPosition).sqrMagnitude;
                float zDistance = (slideZ - currentWorldPosition).sqrMagnitude;
                return xDistance >= zDistance ? slideX : slideZ;
            }

            if (canSlideX)
                return slideX;
            if (canSlideZ)
                return slideZ;
            return currentWorldPosition;
        }

        public bool IsWalkable(Vector3 worldPosition, float edgePadding = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float usableRadius = Mathf.Max(0.1f, tileRadius - Mathf.Max(0f, edgePadding));
            float maximumX = usableRadius * Mathf.Sqrt(3f) * 0.5f;
            for (int i = 0; i < generatedTileDescriptors.Count; i++)
            {
                GameObject tile = generatedTileDescriptors[i].Tile;
                if (tile == null)
                    continue;

                Vector3 offset = local - tile.transform.localPosition;
                float x = Mathf.Abs(offset.x);
                float z = Mathf.Abs(offset.z);
                if (x <= maximumX && z + x / Mathf.Sqrt(3f) <= usableRadius)
                    return true;
            }

            return false;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && generatePreviewInEditMode)
                Generate();
        }

        private void Start()
        {
            if (Application.isPlaying && generateOnStart)
                Generate();
        }

        private void OnDestroy()
        {
            ClearGenerated();
            ClearGeneratedMeshes();
        }

        [ContextMenu("Regenerate Hex Map")]
        public void Generate()
        {
            GenerationStarted?.Invoke();
            ClearGenerated();

            Texture2D plainRoadTexture = LoadTileTexture(plainRoadTextureName);
            if (plainRoadTexture == null)
            {
                Debug.LogWarning($"[HexTileMapGenerator] Missing plain road texture: {tileResourceFolder}/{plainRoadTextureName}");
                GenerationCompleted?.Invoke(generatedTileDescriptors);
                return;
            }

            string districtTileResourceFolder = ResolveDistrictTileResourceFolder();
            Texture2D[] districtRoadTextures = LoadTileTextures(
                districtTileResourceFolder,
                ResolveDistrictRoadTextureNames());
            Texture2D[] districtInteractionTextures = LoadTileTextures(
                districtTileResourceFolder,
                ResolveDistrictInteractionTextureNames());
            Texture2D[] legacyInteractionTextures = LoadTileTextures(interactionTextureNames);
            ClearGeneratedMeshes();
            List<Vector2Int> cells = BuildCellPath(out HashSet<Vector2Int> interactionCells, out Vector2Int bossCell);
            for (int i = 0; i < cells.Count; i++)
            {
                int interactionTextureCount = districtInteractionTextures.Length > 0
                    ? districtInteractionTextures.Length
                    : legacyInteractionTextures.Length;
                bool isInteractionTile = ShouldUseInteractionTile(cells[i], interactionCells, interactionTextureCount);
                bool isBossTile = isInteractionTile && cells[i] == bossCell;
                Texture2D texture;
                if (isInteractionTile)
                {
                    Texture2D[] interactions = districtInteractionTextures.Length > 0
                        ? districtInteractionTextures
                        : legacyInteractionTextures;
                    texture = interactions[ChooseTextureIndex(cells[i], i, interactions.Length)];
                }
                else if (districtRoadTextures.Length > 0)
                {
                    texture = districtRoadTextures[ChooseDistrictTextureIndex(
                        cells[i],
                        i,
                        districtRoadTextures.Length)];
                }
                else
                {
                    texture = plainRoadTexture;
                }
                CreateTile(cells[i], texture, isInteractionTile, isBossTile);
            }

            if (placePlayerAtStart && playerTarget != null)
            {
                Vector3 position = playerTarget.position;
                Vector3 target = TryGetSavedPlayerPosition(out Vector3 savedPosition)
                    ? savedPosition
                    : transform.TransformPoint(Vector3.zero);
                playerTarget.position = new Vector3(target.x, position.y, target.z);
            }

            GenerationCompleted?.Invoke(generatedTileDescriptors);
        }

        private bool TryGetSavedPlayerPosition(out Vector3 worldPosition)
        {
            worldPosition = default;
            if (!Application.isPlaying || !GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                return false;
            }

            RunActProgressState progress = runs.Current.CurrentActProgress;
            return progress.hasCurrentCell && TryGetWorldPosition(
                new Vector2Int(progress.currentAxialX, progress.currentAxialY),
                out worldPosition);
        }

        public void SetRuntimeSeed(int seed)
        {
            runtimeSeed = seed;
            hasRuntimeSeed = true;
        }

        public void ConfigureRunLayout(int targetTileCount, int contentNodeCount)
        {
            ConfigureRunLayoutForAct(targetTileCount, contentNodeCount, 1);
        }

        public void ConfigureRunLayoutForAct(int targetTileCount, int contentNodeCount, int act)
        {
            RunFieldLayoutPattern pattern = Mathf.Clamp(act, 1, 3) switch
            {
                2 => RunFieldLayoutPattern.CanalDoubleLoop,
                3 => RunFieldLayoutPattern.PalaceDoubleRing,
                _ => RunFieldLayoutPattern.BroadRoadY
            };
            ConfigureRunLayoutForAct(targetTileCount, contentNodeCount, act, pattern);
        }

        public void ConfigureRunLayoutForAct(
            int targetTileCount,
            int contentNodeCount,
            int act,
            RunFieldLayoutPattern layoutPattern)
        {
            int target = Mathf.Max(12, targetTileCount);
            int areas = Mathf.Clamp(
                Mathf.RoundToInt(target / 14f) + 1,
                Mathf.Max(3, minimumDistrictCount),
                Mathf.Max(minimumDistrictCount, maximumDistrictCount));

            this.targetTileCount = target;
            plannedContentNodeCount = Mathf.Max(1, contentNodeCount);
            mainPathLength = areas;
            branchCount = Mathf.Max(2, areas + Mathf.RoundToInt(target / 24f));
            minBranchLength = 2;
            maxBranchLength = 4;
            districtSpacing = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(target) * 0.55f), 4, 6);
            cityLoopConnections = Mathf.Clamp(Mathf.RoundToInt(target / 28f), 2, 6);
            softRadiusLimit = Mathf.Max(9, Mathf.CeilToInt(Mathf.Sqrt(target) * 1.7f));
            minInteractionHexDistance = 2;
            interactionTileChance = 0f;
            districtAct = Mathf.Clamp(act, 1, 3);
            districtLayoutPattern = layoutPattern;
        }

        public void ClearRuntimeSeed()
        {
            runtimeSeed = 0;
            hasRuntimeSeed = false;
        }

        private Texture2D[] LoadTileTextures(string[] textureNames)
        {
            return LoadTileTextures(tileResourceFolder, textureNames);
        }

        private Texture2D[] LoadTileTextures(string resourceFolder, string[] textureNames)
        {
            var textures = new List<Texture2D>();
            foreach (string tileName in textureNames)
            {
                if (string.IsNullOrWhiteSpace(tileName))
                    continue;

                Texture2D texture = LoadTileTexture(resourceFolder, tileName);
                if (texture == null)
                {
                    Debug.LogWarning($"[HexTileMapGenerator] Missing tile texture: {resourceFolder}/{tileName}");
                    continue;
                }

                textures.Add(texture);
            }

            return textures.ToArray();
        }

        private Texture2D LoadTileTexture(string tileName)
        {
            return LoadTileTexture(tileResourceFolder, tileName);
        }

        private static Texture2D LoadTileTexture(string resourceFolder, string tileName)
        {
            Texture2D texture = Resources.Load<Texture2D>($"{resourceFolder}/{tileName}");
            if (texture == null)
                return null;

            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = Mathf.Max(texture.anisoLevel, 16);
            texture.mipMapBias = -0.35f;
            return texture;
        }

        private string[] ResolveDistrictRoadTextureNames()
        {
            return districtAct switch
            {
                2 => actTwoRoadTextureNames,
                3 => actThreeRoadTextureNames,
                _ => actOneRoadTextureNames,
            };
        }

        private string[] ResolveDistrictInteractionTextureNames()
        {
            return districtAct switch
            {
                2 => actTwoInteractionTextureNames,
                3 => actThreeInteractionTextureNames,
                _ => actOneInteractionTextureNames,
            };
        }

        private List<Vector2Int> BuildCellPath(out HashSet<Vector2Int> interactionCells, out Vector2Int bossCell)
        {
            int baseSeed = GetEffectiveSeed();
            int targetCount = ResolveTargetTileCount();
            HashSet<Vector2Int> bestCells = null;
            List<Vector2Int> bestAreaCenters = null;
            System.Random bestRandom = null;
            float bestQuality = float.NegativeInfinity;
            int attempts = Mathf.Max(1, layoutValidationAttempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var random = new System.Random(unchecked(baseSeed + attempt * 48611));
                HashSet<Vector2Int> cells = BuildCellCandidate(random, targetCount, out List<Vector2Int> areaCenters);
                float quality = CalculateLayoutQuality(cells, targetCount);
                if (quality > bestQuality)
                {
                    bestCells = cells;
                    bestAreaCenters = areaCenters;
                    bestRandom = random;
                    bestQuality = quality;
                }

                if (!IsLayoutValid(cells, targetCount))
                    continue;

                return FinalizeCellPath(random, cells, areaCenters, out interactionCells, out bossCell);
            }

            Debug.LogWarning(
                $"{nameof(HexTileMapGenerator)} could not satisfy every layout rule after {attempts} attempts. " +
                "Using the strongest connected candidate.",
                this);
            return FinalizeCellPath(bestRandom, bestCells, bestAreaCenters, out interactionCells, out bossCell);
        }

        private HashSet<Vector2Int> BuildCellCandidate(
            System.Random random,
            int targetCount,
            out List<Vector2Int> areaCenters)
        {
            var cells = new HashSet<Vector2Int>();
            areaCenters = BuildAreaCenters(
                random,
                targetCount,
                districtLayoutPattern,
                districtSpacing,
                minimumDistrictCount,
                maximumDistrictCount);

            for (int i = 0; i < areaCenters.Count; i++)
            {
                Vector2Int center = areaCenters[i];
                CarveHexBrush(cells, center, PlazaRadiusFor(i, areaCenters.Count, targetCount));
            }

            CarveDistrictRoadNetwork(random, cells, areaCenters);

            AddIntentionalLoops(random, cells, areaCenters, districtLayoutPattern);
            AddCityCrossStreets(random, cells, areaCenters, cityLoopConnections);
            WidenFieldUntilTarget(random, cells, targetCount);
            TrimFieldToTarget(cells, targetCount);

            return cells;
        }

        private List<Vector2Int> FinalizeCellPath(
            System.Random random,
            HashSet<Vector2Int> cells,
            IReadOnlyList<Vector2Int> areaCenters,
            out HashSet<Vector2Int> interactionCells,
            out Vector2Int bossCell)
        {
            List<Vector2Int> orderedCells = SortCellsForProgression(cells);
            bossCell = FindFarthestCell(cells);
            interactionCells = PickInteractionCells(random, orderedCells, areaCenters, bossCell);
            AddOptionalInteractionCells(random, orderedCells, interactionCells);
            interactionCells.Add(bossCell);
            interactionCells.Remove(Vector2Int.zero);

            return orderedCells;
        }

        private bool IsLayoutValid(HashSet<Vector2Int> cells, int targetCount)
        {
            if (cells == null || cells.Count != targetCount || !cells.Contains(Vector2Int.zero))
                return false;

            int deadEnds = 0;
            int narrowTiles = 0;
            foreach (Vector2Int cell in cells)
            {
                int neighbors = CountExistingNeighbors(cell, cells);
                if (neighbors <= 1)
                    deadEnds++;
                if (neighbors <= 2)
                    narrowTiles++;
            }

            return CountReachableCells(cells) == cells.Count &&
                   deadEnds <= maximumDeadEndTiles &&
                   narrowTiles / (float)cells.Count < maximumNarrowTileRatio &&
                   FindLongestSingleWidthCorridor(cells) <= maximumSingleWidthCorridorLength;
        }

        private float CalculateLayoutQuality(HashSet<Vector2Int> cells, int targetCount)
        {
            if (cells == null || cells.Count == 0)
                return float.NegativeInfinity;

            int deadEnds = 0;
            int narrowTiles = 0;
            foreach (Vector2Int cell in cells)
            {
                int neighbors = CountExistingNeighbors(cell, cells);
                if (neighbors <= 1)
                    deadEnds++;
                if (neighbors <= 2)
                    narrowTiles++;
            }

            int disconnected = cells.Count - CountReachableCells(cells);
            int corridorLength = FindLongestSingleWidthCorridor(cells);
            return -Mathf.Abs(cells.Count - targetCount) * 1000f -
                   disconnected * 1000f -
                   deadEnds * 100f -
                   narrowTiles * 10f -
                   corridorLength;
        }

        private static int CountReachableCells(HashSet<Vector2Int> cells)
        {
            if (cells == null || !cells.Contains(Vector2Int.zero))
                return 0;

            var visited = new HashSet<Vector2Int> { Vector2Int.zero };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(Vector2Int.zero);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = current + direction;
                    if (cells.Contains(next) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return visited.Count;
        }

        private static int FindLongestSingleWidthCorridor(HashSet<Vector2Int> cells)
        {
            var narrowCells = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in cells)
            {
                if (CountExistingNeighbors(cell, cells) <= 2)
                    narrowCells.Add(cell);
            }

            int longest = 0;
            var visited = new HashSet<Vector2Int>();
            foreach (Vector2Int start in narrowCells)
            {
                if (!visited.Add(start))
                    continue;

                int length = 0;
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    length++;
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int next = current + direction;
                        if (narrowCells.Contains(next) && visited.Add(next))
                            queue.Enqueue(next);
                    }
                }

                longest = Mathf.Max(longest, length);
            }

            return longest;
        }

        private int ResolveTargetTileCount()
        {
            if (targetTileCount > 0)
                return Mathf.Max(12, targetTileCount);

            int branchLength = Mathf.RoundToInt((Mathf.Max(1, minBranchLength) + Mathf.Max(1, maxBranchLength)) * 0.5f);
            return Mathf.Max(12, mainPathLength + Mathf.Max(0, branchCount) * branchLength);
        }

        private static List<Vector2Int> BuildAreaCenters(
            System.Random random,
            int targetCount,
            RunFieldLayoutPattern pattern,
            int spacing,
            int minimumCount,
            int maximumCount)
        {
            int areaCount = Mathf.Clamp(
                Mathf.RoundToInt(targetCount / 14f) + 1,
                Mathf.Max(3, minimumCount),
                Mathf.Max(minimumCount, maximumCount));
            spacing = Mathf.Max(4, spacing);
            int forwardDirection = random.Next(Directions.Length);
            Vector2Int forward = Directions[forwardDirection];
            Vector2Int left = Directions[(forwardDirection + 1) % Directions.Length];
            Vector2Int[] template = pattern switch
            {
                RunFieldLayoutPattern.CanalDoubleLoop => new[]
                {
                    new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 0),
                    new Vector2Int(1, -1), new Vector2Int(3, 1), new Vector2Int(3, -1),
                    new Vector2Int(4, 0), new Vector2Int(2, 2), new Vector2Int(2, -2),
                    new Vector2Int(4, 1)
                },
                RunFieldLayoutPattern.PalaceDoubleRing => new[]
                {
                    new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 1),
                    new Vector2Int(3, 0), new Vector2Int(2, -1), new Vector2Int(1, -1),
                    new Vector2Int(2, 0), new Vector2Int(3, 1), new Vector2Int(3, -1),
                    new Vector2Int(4, 0)
                },
                _ => new[]
                {
                    new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 1),
                    new Vector2Int(2, -1), new Vector2Int(3, 0), new Vector2Int(3, 1),
                    new Vector2Int(3, -1), new Vector2Int(4, 0), new Vector2Int(4, 1),
                    new Vector2Int(4, -1)
                }
            };

            var centers = new List<Vector2Int>(areaCount);
            var unique = new HashSet<Vector2Int>();
            int lateralSpacing = Mathf.Max(2, spacing - 2);
            for (int i = 0; i < template.Length && centers.Count < areaCount; i++)
            {
                Vector2Int logical = template[i];
                Vector2Int center = Scale(forward, logical.x * spacing) +
                                    Scale(left, logical.y * lateralSpacing);
                if (i > 0 && random.NextDouble() < 0.45d)
                {
                    int side = random.NextDouble() < 0.5d ? 1 : -1;
                    center += Scale(left, side);
                }

                if (unique.Add(center))
                    centers.Add(center);
            }

            return centers;
        }

        private string ResolveDistrictTileResourceFolder()
        {
            string folder = districtAct switch
            {
                2 => actTwoTileResourceFolder,
                3 => actThreeTileResourceFolder,
                _ => actOneTileResourceFolder,
            };

            return string.IsNullOrWhiteSpace(folder) ? cityTileResourceFolder : folder;
        }

        private static void CarveDistrictRoadNetwork(
            System.Random random,
            HashSet<Vector2Int> cells,
            IReadOnlyList<Vector2Int> areaCenters)
        {
            for (int i = 1; i < areaCenters.Count; i++)
            {
                int nearestIndex = 0;
                int nearestDistance = int.MaxValue;
                for (int candidate = 0; candidate < i; candidate++)
                {
                    int distance = HexDistance(areaCenters[i], areaCenters[candidate]);
                    if (distance >= nearestDistance)
                        continue;

                    nearestIndex = candidate;
                    nearestDistance = distance;
                }

                CarveWideRoad(random, cells, areaCenters[nearestIndex], areaCenters[i]);
            }
        }

        private static int PlazaRadiusFor(int index, int areaCount, int targetCount)
        {
            if (targetCount >= 36 && (index == 0 || index == areaCount - 1))
                return 2;

            if (targetCount >= 68 && index > 0 && index < areaCount - 1 && index % 2 == 0)
                return 2;

            if (targetCount >= 48 && index == areaCount / 2)
                return 2;

            return 1;
        }

        private static void CarveWideRoad(
            System.Random random,
            HashSet<Vector2Int> cells,
            Vector2Int from,
            Vector2Int to)
        {
            Vector2Int current = from;
            int guard = 0;
            while (current != to && guard++ < 80)
            {
                CarveHexBrush(cells, current, 1);
                Vector2Int next = StepToward(random, current, to);
                if (next == current)
                    break;

                current = next;
            }

            CarveHexBrush(cells, to, 1);
        }

        private static Vector2Int StepToward(System.Random random, Vector2Int current, Vector2Int target)
        {
            int bestScore = int.MaxValue;
            Vector2Int best = current;
            int startDirection = random.Next(Directions.Length);

            for (int offset = 0; offset < Directions.Length; offset++)
            {
                Vector2Int candidate = current + Directions[(startDirection + offset) % Directions.Length];
                int score = HexDistance(candidate, target) * 10 + random.Next(0, 3);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private static void AddIntentionalLoops(
            System.Random random,
            HashSet<Vector2Int> cells,
            IReadOnlyList<Vector2Int> areaCenters,
            RunFieldLayoutPattern pattern)
        {
            if (pattern == RunFieldLayoutPattern.BroadRoadY)
            {
                if (areaCenters.Count >= 4)
                    CarveWideRoad(random, cells, areaCenters[2], areaCenters[3]);
                return;
            }

            if (pattern == RunFieldLayoutPattern.CanalDoubleLoop)
            {
                if (areaCenters.Count >= 4)
                {
                    CarveWideRoad(random, cells, areaCenters[0], areaCenters[3]);
                    CarveWideRoad(random, cells, areaCenters[1], areaCenters[3]);
                }
                if (areaCenters.Count >= 5)
                    CarveWideRoad(random, cells, areaCenters[2], areaCenters[4]);
                return;
            }

            if (areaCenters.Count >= 5)
            {
                CarveWideRoad(random, cells, areaCenters[0], areaCenters[^1]);
                CarveWideRoad(random, cells, areaCenters[1], areaCenters[4]);
                CarveWideRoad(random, cells, areaCenters[0], areaCenters[3]);
            }
        }

        private static void AddCityCrossStreets(
            System.Random random,
            HashSet<Vector2Int> cells,
            IReadOnlyList<Vector2Int> areaCenters,
            int connectionCount)
        {
            if (areaCenters.Count < 4)
                return;

            var pairs = new List<(int left, int right, int distance)>();
            for (int left = 0; left < areaCenters.Count - 1; left++)
            {
                for (int right = left + 1; right < areaCenters.Count; right++)
                {
                    int distance = HexDistance(areaCenters[left], areaCenters[right]);
                    if (distance < 4 || distance > 13)
                        continue;

                    pairs.Add((left, right, distance));
                }
            }

            for (int i = pairs.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (pairs[i], pairs[swap]) = (pairs[swap], pairs[i]);
            }

            int count = Mathf.Min(Mathf.Max(1, connectionCount), pairs.Count);
            for (int i = 0; i < count; i++)
            {
                (int left, int right, int distance) pair = pairs[i];
                CarveWideRoad(random, cells, areaCenters[pair.left], areaCenters[pair.right]);
            }
        }

        private void WidenFieldUntilTarget(
            System.Random random,
            HashSet<Vector2Int> cells,
            int targetCount)
        {
            int guard = 0;
            while (cells.Count < targetCount && guard++ < targetCount * 24)
            {
                var candidates = new HashSet<Vector2Int>();
                foreach (Vector2Int cell in cells)
                {
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int candidate = cell + direction;
                        if (cells.Contains(candidate))
                            continue;

                        int neighbors = CountExistingNeighbors(candidate, cells);
                        if (neighbors >= 2 && HexDistance(candidate) <= softRadiusLimit + 2)
                            candidates.Add(candidate);
                    }
                }

                if (candidates.Count == 0)
                    break;

                Vector2Int selected = Vector2Int.zero;
                int bestScore = int.MinValue;
                foreach (Vector2Int candidate in candidates)
                {
                    int neighbors = CountExistingNeighbors(candidate, cells);
                    int streetScore = 32 - Mathf.Abs(neighbors - 3) * 12;
                    int spreadScore = Mathf.Min(HexDistance(candidate), softRadiusLimit) * 2;
                    int alleyScore = neighbors == 2 && random.NextDouble() < alleyExpansionChance ? 18 : 0;
                    int score = streetScore + spreadScore + alleyScore + random.Next(0, 21);
                    if (score <= bestScore)
                        continue;

                    selected = candidate;
                    bestScore = score;
                }

                cells.Add(selected);
            }
        }

        private static void TrimFieldToTarget(HashSet<Vector2Int> cells, int targetCount)
        {
            int guard = 0;
            while (cells.Count > targetCount && guard++ < targetCount * 48)
            {
                bool found = false;
                Vector2Int bestCell = Vector2Int.zero;
                int bestScore = int.MinValue;
                foreach (Vector2Int cell in cells)
                {
                    if (cell == Vector2Int.zero)
                        continue;

                    int neighbors = CountExistingNeighbors(cell, cells);
                    if (!CanRemoveWithoutDisconnecting(cell, cells))
                        continue;

                    int score = HexDistance(cell) * 12 + (6 - neighbors) * 8;
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestCell = cell;
                    found = true;
                }

                if (!found)
                    break;

                cells.Remove(bestCell);
            }
        }

        private static bool CanRemoveWithoutDisconnecting(Vector2Int removedCell, HashSet<Vector2Int> cells)
        {
            if (removedCell == Vector2Int.zero || !cells.Contains(Vector2Int.zero))
                return false;

            int expectedCount = cells.Count - 1;
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            visited.Add(Vector2Int.zero);
            queue.Enqueue(Vector2Int.zero);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = current + direction;
                    if (next == removedCell || !cells.Contains(next) || !visited.Add(next))
                        continue;

                    queue.Enqueue(next);
                }
            }

            return visited.Count == expectedCount;
        }

        private HashSet<Vector2Int> PickInteractionCells(
            System.Random random,
            IReadOnlyList<Vector2Int> orderedCells,
            IReadOnlyList<Vector2Int> areaCenters,
            Vector2Int bossCell)
        {
            var interactions = new HashSet<Vector2Int>();
            int targetInteractions = Mathf.Min(
                orderedCells.Count,
                Mathf.Max(4, plannedContentNodeCount + 1));

            for (int i = 1; i < areaCenters.Count && interactions.Count < targetInteractions; i++)
            {
                Vector2Int center = areaCenters[i];
                if (center != Vector2Int.zero && center != bossCell)
                    interactions.Add(center);
            }

            var cellSet = new HashSet<Vector2Int>(orderedCells);
            for (int i = 1; i <= targetInteractions && interactions.Count < targetInteractions; i++)
            {
                int index = Mathf.Clamp(
                    Mathf.FloorToInt(i * orderedCells.Count / (float)(targetInteractions + 1)),
                    0,
                    orderedCells.Count - 1);
                Vector2Int candidate = FindNearbyInteractionCandidate(
                    random,
                    orderedCells,
                    cellSet,
                    index,
                    interactions,
                    bossCell);
                if (candidate != Vector2Int.zero && candidate != bossCell)
                    interactions.Add(candidate);
            }

            return interactions;
        }

        private Vector2Int FindNearbyInteractionCandidate(
            System.Random random,
            IReadOnlyList<Vector2Int> orderedCells,
            HashSet<Vector2Int> cellSet,
            int preferredIndex,
            HashSet<Vector2Int> interactions,
            Vector2Int bossCell)
        {
            int minDistance = Mathf.Max(1, minInteractionHexDistance);
            Vector2Int fallback = Vector2Int.zero;
            int fallbackScore = int.MinValue;
            for (int radius = 0; radius < orderedCells.Count; radius++)
            {
                int left = preferredIndex - radius;
                int right = preferredIndex + radius;
                if (TryEvaluateInteractionCandidate(left, orderedCells, cellSet, interactions, bossCell, minDistance, ref fallback, ref fallbackScore) ||
                    TryEvaluateInteractionCandidate(right, orderedCells, cellSet, interactions, bossCell, minDistance, ref fallback, ref fallbackScore))
                {
                    return fallback;
                }
            }

            return fallback == Vector2Int.zero && random.NextDouble() < 0.1d ? bossCell : fallback;
        }

        private static bool TryEvaluateInteractionCandidate(
            int index,
            IReadOnlyList<Vector2Int> orderedCells,
            HashSet<Vector2Int> cellSet,
            HashSet<Vector2Int> interactions,
            Vector2Int bossCell,
            int minDistance,
            ref Vector2Int fallback,
            ref int fallbackScore)
        {
            if (index < 0 || index >= orderedCells.Count)
                return false;

            Vector2Int candidate = orderedCells[index];
            if (candidate == Vector2Int.zero || candidate == bossCell || interactions.Contains(candidate))
                return false;

            int score = CountExistingNeighbors(candidate, cellSet);
            if (score > fallbackScore)
            {
                fallback = candidate;
                fallbackScore = score;
            }

            return IsFarFromInteractionCells(candidate, interactions, minDistance);
        }

        private static List<Vector2Int> SortCellsForProgression(HashSet<Vector2Int> cells)
        {
            var ordered = new List<Vector2Int>(cells);
            ordered.Sort((left, right) =>
            {
                int distance = HexDistance(left).CompareTo(HexDistance(right));
                if (distance != 0)
                    return distance;

                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });
            return ordered;
        }

        private static void CarveHexBrush(HashSet<Vector2Int> cells, Vector2Int center, int radius)
        {
            int clampedRadius = Mathf.Max(0, radius);
            for (int q = -clampedRadius; q <= clampedRadius; q++)
            {
                for (int r = -clampedRadius; r <= clampedRadius; r++)
                {
                    Vector2Int offset = new(q, r);
                    if (HexDistance(offset) <= clampedRadius)
                        cells.Add(center + offset);
                }
            }
        }

        private static Vector2Int Scale(Vector2Int cell, int value)
        {
            return new Vector2Int(cell.x * value, cell.y * value);
        }

        private static bool ShouldUseInteractionTile(
            Vector2Int cell,
            HashSet<Vector2Int> interactionCells,
            int interactionTextureCount)
        {
            return interactionTextureCount > 0 && interactionCells.Contains(cell);
        }

        private bool TryBuildBranch(
            System.Random random,
            HashSet<Vector2Int> cells,
            List<Vector2Int> orderedCells,
            List<Vector2Int> reservedInteractionCells,
            out List<Vector2Int> branchPath)
        {
            branchPath = new List<Vector2Int>();
            if (orderedCells.Count < 4)
                return false;

            int minLength = Mathf.Max(1, minBranchLength);
            int maxLength = Mathf.Max(minLength, maxBranchLength);

            for (int startAttempt = 0; startAttempt < 12; startAttempt++)
            {
                Vector2Int branchStart = orderedCells[random.Next(1, orderedCells.Count - 1)];
                if (CountExistingNeighbors(branchStart, cells) < 2)
                    continue;

                var temporaryCells = new HashSet<Vector2Int>(cells);
                var candidatePath = new List<Vector2Int>();
                Vector2Int current = branchStart;
                int previousDirection = -1;
                int targetLength = random.Next(minLength, maxLength + 1);

                for (int step = 0; step < targetLength; step++)
                {
                    if (!TryPickNextCell(random, current, previousDirection, temporaryCells, out Vector2Int next, out int direction))
                        break;

                    current = next;
                    previousDirection = direction;
                    temporaryCells.Add(current);
                    candidatePath.Add(current);
                }

                if (candidatePath.Count < minLength)
                    continue;

                Vector2Int endCell = candidatePath[^1];
                int minInteractionDistance = Mathf.Max(1, minInteractionHexDistance);
                if (HexDistance(endCell) < minInteractionDistance)
                    continue;

                if (!IsFarFromInteractionCells(endCell, reservedInteractionCells, minInteractionDistance))
                    continue;

                branchPath = candidatePath;
                return true;
            }

            return false;
        }

        private bool TryPickNextCell(
            System.Random random,
            Vector2Int current,
            int previousDirection,
            HashSet<Vector2Int> cells,
            out Vector2Int next,
            out int direction)
        {
            next = current;
            direction = -1;
            int startDirection = random.Next(Directions.Length);
            int bestScore = int.MinValue;

            for (int offset = 0; offset < Directions.Length; offset++)
            {
                int candidateDirection = (startDirection + offset) % Directions.Length;
                if (previousDirection >= 0 && candidateDirection == OppositeDirection(previousDirection))
                    continue;

                Vector2Int candidate = current + Directions[candidateDirection];
                if (!CanAddMazeCell(candidate, current, cells))
                    continue;

                int distance = HexDistance(candidate);
                int score = random.Next(0, 100)
                    + (candidateDirection == previousDirection ? 18 : 0)
                    - Mathf.Max(0, distance - softRadiusLimit) * 25;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                next = candidate;
                direction = candidateDirection;
            }

            return direction >= 0;
        }

        private static bool CanAddMazeCell(Vector2Int candidate, Vector2Int parent, HashSet<Vector2Int> cells)
        {
            if (cells.Contains(candidate))
                return false;

            int existingNeighborCount = 0;
            foreach (Vector2Int offset in Directions)
            {
                Vector2Int neighbor = candidate + offset;
                if (!cells.Contains(neighbor))
                    continue;

                if (neighbor != parent)
                    return false;

                existingNeighborCount++;
            }

            return existingNeighborCount == 1;
        }

        private static int CountExistingNeighbors(Vector2Int cell, HashSet<Vector2Int> cells)
        {
            int count = 0;
            foreach (Vector2Int offset in Directions)
            {
                if (cells.Contains(cell + offset))
                    count++;
            }

            return count;
        }

        private static HashSet<Vector2Int> FindDeadEndCells(HashSet<Vector2Int> cells)
        {
            var deadEnds = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in cells)
            {
                if (CountExistingNeighbors(cell, cells) <= 1)
                    deadEnds.Add(cell);
            }

            return deadEnds;
        }

        private void AddOptionalInteractionCells(
            System.Random random,
            List<Vector2Int> orderedCells,
            HashSet<Vector2Int> interactionCells)
        {
            if (interactionTileChance <= 0f)
                return;

            int minInteractionDistance = Mathf.Max(1, minInteractionHexDistance);
            for (int i = 3; i < orderedCells.Count; i++)
            {
                Vector2Int cell = orderedCells[i];
                if (cell == Vector2Int.zero || interactionCells.Contains(cell))
                    continue;

                if (HexDistance(cell) < minInteractionDistance)
                    continue;

                if (!IsFarFromInteractionCells(cell, interactionCells, minInteractionDistance))
                    continue;

                if (random.NextDouble() <= interactionTileChance)
                    interactionCells.Add(cell);
            }
        }

        private static bool IsFarFromInteractionCells(
            Vector2Int candidate,
            IEnumerable<Vector2Int> interactionCells,
            int minDistance)
        {
            foreach (Vector2Int interactionCell in interactionCells)
            {
                if (HexDistance(candidate, interactionCell) < minDistance)
                    return false;
            }

            return true;
        }

        private static Vector2Int FindFarthestCell(HashSet<Vector2Int> cells)
        {
            Vector2Int farthestCell = Vector2Int.zero;
            int farthestDistance = -1;
            foreach (Vector2Int cell in cells)
            {
                int distance = HexDistance(cell);
                if (distance <= farthestDistance)
                    continue;

                farthestDistance = distance;
                farthestCell = cell;
            }

            return farthestCell;
        }

        private static int OppositeDirection(int direction)
        {
            return (direction + 3) % Directions.Length;
        }

        private static int HexDistance(Vector2Int cell)
        {
            return (Mathf.Abs(cell.x) + Mathf.Abs(cell.y) + Mathf.Abs(cell.x + cell.y)) / 2;
        }

        private static int HexDistance(Vector2Int a, Vector2Int b)
        {
            return HexDistance(a - b);
        }

        private static void AddCell(Vector2Int cell, HashSet<Vector2Int> cells, List<Vector2Int> orderedCells)
        {
            if (!cells.Add(cell))
                return;

            orderedCells.Add(cell);
        }

        private int GetEffectiveSeed()
        {
            if (hasRuntimeSeed)
                return runtimeSeed;

            if (randomSeed != 0)
                return randomSeed;

            return unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        }

        private void CreateTile(Vector2Int cell, Texture2D texture, bool isInteractionTile, bool isBossTile)
        {
            string tileName = isBossTile
                ? "Boss Interaction Hex Tile"
                : isInteractionTile
                    ? "Interaction Hex Tile"
                    : "Road Hex Tile";
            GameObject tile = new($"{tileName} {cell.x},{cell.y}", typeof(MeshFilter), typeof(MeshRenderer));
            tile.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
            tile.transform.SetParent(transform, false);
            tile.transform.localPosition = AxialToLocalPosition(cell);
            tile.transform.localRotation = Quaternion.identity;
            tile.transform.localScale = Vector3.one;

            tile.GetComponent<MeshFilter>().sharedMesh = GetOrCreateMesh(texture.name, isInteractionTile);

            Material material = CreateMaterial(texture);
            if (material == null)
            {
                DestroyRuntimeObject(tile);
                return;
            }
            tile.GetComponent<MeshRenderer>().sharedMaterial = material;

            generatedTiles.Add(tile);
            generatedTileDescriptors.Add(new GeneratedHexTile(
                tile,
                cell,
                isInteractionTile,
                isBossTile,
                generatedTileDescriptors.Count));
            generatedMaterials.Add(material);
        }

        private Mesh GetOrCreateMesh(string textureName, bool isInteractionTile)
        {
            string meshKey = isInteractionTile ? $"interaction:{textureName}" : $"road:{textureName}";
            if (generatedMeshes.TryGetValue(meshKey, out Mesh mesh) && mesh != null)
                return mesh;

            float meshScale = isInteractionTile ? interactionMeshScale : plainRoadMeshScale;
            float uvPadding = isInteractionTile ? interactionUvPadding : plainRoadUvPadding;
            Vector2 uvRadius = Vector2.one * (0.5f - uvPadding);
            if (textureName.Contains("_v6_", StringComparison.OrdinalIgnoreCase))
                uvRadius = fieldV6UvRadius;
            mesh = CreateHexMesh(textureName, meshScale, uvRadius);
            generatedMeshes[meshKey] = mesh;
            return mesh;
        }

        private Vector3 AxialToLocalPosition(Vector2Int cell)
        {
            float x = tileRadius * Mathf.Sqrt(3f) * (cell.x + cell.y * 0.5f);
            float z = tileRadius * 1.5f * cell.y;
            return new Vector3(x, tileY, z);
        }

        private Mesh CreateHexMesh(string textureName, float meshScale, Vector2 uvRadius)
        {
            meshScale = Mathf.Max(0.1f, meshScale);
            uvRadius.x = Mathf.Clamp(uvRadius.x, 0.3f, 0.5f);
            uvRadius.y = Mathf.Clamp(uvRadius.y, 0.3f, 0.5f);

            var mesh = new Mesh
            {
                name = $"RuntimeHexTileMesh_{textureName}",
                hideFlags = HideFlags.DontSave,
            };
            var vertices = new Vector3[7];
            var uvs = new Vector2[7];
            var triangles = new int[18];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (30f + 60f * i);
                float x = Mathf.Cos(angle) * tileRadius * meshScale;
                float z = Mathf.Sin(angle) * tileRadius * meshScale;
                vertices[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(
                    0.5f + Mathf.Cos(angle) * uvRadius.x,
                    0.5f + Mathf.Sin(angle) * uvRadius.y);
            }

            for (int i = 0; i < 6; i++)
            {
                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i == 5 ? 1 : i + 2;
                triangles[triangle + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateMaterial(Texture2D texture)
        {
            Material material;
            if (tileMaterialTemplate != null)
            {
                material = new Material(tileMaterialTemplate);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                                Shader.Find("Unlit/Texture") ??
                                Shader.Find("Standard");
                if (shader == null)
                {
                    Debug.LogError("[HexTileMapGenerator] No tile material template or compatible shader is available.");
                    return null;
                }

                material = new Material(shader);
            }

            material.name = $"HexTile_{texture.name}";
            material.mainTexture = texture;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry - 5;
            material.hideFlags = HideFlags.DontSave;

            SetTextureIfPresent(material, texture, "_BaseMap", "_MainTex");
            SetColorIfPresent(material, Color.white, "_BaseColor", "_Color");
            SetFloatIfPresent(material, 0f, "_Cull");
            return material;
        }

        private void ClearGenerated()
        {
            var trackedTiles = new HashSet<GameObject>(generatedTiles);
            foreach (GameObject tile in generatedTiles)
                DestroyRuntimeObject(tile);
            generatedTiles.Clear();
            generatedTileDescriptors.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (trackedTiles.Contains(child.gameObject))
                    continue;

                if (child.name.StartsWith("Road Hex Tile", StringComparison.Ordinal) ||
                    child.name.StartsWith("Interaction Hex Tile", StringComparison.Ordinal) ||
                    child.name.StartsWith("Boss Interaction Hex Tile", StringComparison.Ordinal))
                {
                    DestroyRuntimeObject(child.gameObject);
                }
            }

            foreach (Material material in generatedMaterials)
                DestroyRuntimeObject(material);
            generatedMaterials.Clear();
        }

        private void ClearGeneratedMeshes()
        {
            foreach (Mesh mesh in generatedMeshes.Values)
                DestroyRuntimeObject(mesh);
            generatedMeshes.Clear();
        }

        private static void DestroyRuntimeObject(UnityEngine.Object instance)
        {
            if (instance == null)
                return;

            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }

        private static int ChooseTextureIndex(Vector2Int cell, int order, int textureCount)
        {
            if (textureCount <= 0)
                return 0;

            unchecked
            {
                int hash = cell.x * 73856093 ^ cell.y * 19349663 ^ order * 83492791;
                return (int)((uint)hash % (uint)textureCount);
            }
        }

        private static int ChooseDistrictTextureIndex(Vector2Int cell, int order, int textureCount)
        {
            if (textureCount <= 0)
                return 0;

            int districtX = Mathf.FloorToInt(cell.x / 3f);
            int districtY = Mathf.FloorToInt(cell.y / 3f);
            unchecked
            {
                int progressionBand = Mathf.Max(0, order / 6);
                int hash = districtX * 73856093 ^ districtY * 19349663 ^ progressionBand * 83492791;
                return (int)((uint)hash % (uint)textureCount);
            }
        }

        private static void SetTextureIfPresent(Material material, Texture texture, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                    material.SetTexture(name, texture);
            }
        }

        private static void SetColorIfPresent(Material material, Color color, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                    material.SetColor(name, color);
            }
        }

        private static void SetFloatIfPresent(Material material, float value, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                    material.SetFloat(name, value);
            }
        }
    }
}
