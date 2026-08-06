using System;
using System.Collections.Generic;
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
        [SerializeField, Range(0f, 0.2f)] private float plainRoadUvPadding = 0.04f;
        [SerializeField, Range(0f, 0.2f)] private float interactionUvPadding = 0.04f;

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
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool generatePreviewInEditMode = true;

        [Header("Player")]
        [SerializeField] private Transform playerTarget = null;
        [SerializeField] private bool placePlayerAtStart = true;

        private readonly List<GameObject> generatedTiles = new();
        private readonly List<GeneratedHexTile> generatedTileDescriptors = new();
        private readonly List<Material> generatedMaterials = new();
        private readonly Dictionary<string, Mesh> generatedMeshes = new();
        private int runtimeSeed;
        private bool hasRuntimeSeed;

        public event Action GenerationStarted;
        public event Action<IReadOnlyList<GeneratedHexTile>> GenerationCompleted;

        public IReadOnlyList<GeneratedHexTile> GeneratedTiles => generatedTileDescriptors;

        public Vector3 ConstrainMovement(Vector3 currentWorldPosition, Vector3 desiredWorldPosition)
        {
            if (generatedTileDescriptors.Count == 0 || IsWalkable(desiredWorldPosition))
                return desiredWorldPosition;

            Vector3 slideX = new(desiredWorldPosition.x, desiredWorldPosition.y, currentWorldPosition.z);
            Vector3 slideZ = new(currentWorldPosition.x, desiredWorldPosition.y, desiredWorldPosition.z);
            bool canSlideX = IsWalkable(slideX);
            bool canSlideZ = IsWalkable(slideZ);
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

        public bool IsWalkable(Vector3 worldPosition, float edgePadding = 0.18f)
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

            Texture2D[] interactionTextures = LoadTileTextures(interactionTextureNames);
            ClearGeneratedMeshes();
            List<Vector2Int> cells = BuildCellPath(out HashSet<Vector2Int> interactionCells, out Vector2Int bossCell);
            for (int i = 0; i < cells.Count; i++)
            {
                bool isInteractionTile = ShouldUseInteractionTile(cells[i], interactionCells, interactionTextures.Length);
                bool isBossTile = isInteractionTile && cells[i] == bossCell;
                Texture2D texture = isInteractionTile
                    ? interactionTextures[ChooseTextureIndex(cells[i], i, interactionTextures.Length)]
                    : plainRoadTexture;
                CreateTile(cells[i], texture, isInteractionTile, isBossTile);
            }

            if (placePlayerAtStart && playerTarget != null)
            {
                Vector3 position = playerTarget.position;
                playerTarget.position = new Vector3(0f, position.y, 0f);
            }

            GenerationCompleted?.Invoke(generatedTileDescriptors);
        }

        public void SetRuntimeSeed(int seed)
        {
            runtimeSeed = seed;
            hasRuntimeSeed = true;
        }

        public void ConfigureRunLayout(int targetTileCount, int contentNodeCount)
        {
            int target = Mathf.Max(12, targetTileCount);
            int areas = Mathf.Clamp(Mathf.RoundToInt(target / 16f) + 1, 3, 6);

            this.targetTileCount = target;
            plannedContentNodeCount = Mathf.Max(1, contentNodeCount);
            mainPathLength = areas;
            branchCount = Mathf.Max(2, areas + Mathf.RoundToInt(target / 24f));
            minBranchLength = 2;
            maxBranchLength = 4;
            softRadiusLimit = Mathf.Max(7, Mathf.CeilToInt(Mathf.Sqrt(target) * 1.45f));
            minInteractionHexDistance = 2;
            interactionTileChance = 0f;
        }

        public void ClearRuntimeSeed()
        {
            runtimeSeed = 0;
            hasRuntimeSeed = false;
        }

        private Texture2D[] LoadTileTextures(string[] textureNames)
        {
            var textures = new List<Texture2D>();
            foreach (string tileName in textureNames)
            {
                if (string.IsNullOrWhiteSpace(tileName))
                    continue;

                Texture2D texture = LoadTileTexture(tileName);
                if (texture == null)
                {
                    Debug.LogWarning($"[HexTileMapGenerator] Missing tile texture: {tileResourceFolder}/{tileName}");
                    continue;
                }

                textures.Add(texture);
            }

            return textures.ToArray();
        }

        private Texture2D LoadTileTexture(string tileName)
        {
            Texture2D texture = Resources.Load<Texture2D>($"{tileResourceFolder}/{tileName}");
            if (texture == null)
                return null;

            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = Mathf.Max(texture.anisoLevel, 16);
            texture.mipMapBias = -0.35f;
            return texture;
        }

        private List<Vector2Int> BuildCellPath(out HashSet<Vector2Int> interactionCells, out Vector2Int bossCell)
        {
            var random = new System.Random(GetEffectiveSeed());
            int targetCount = ResolveTargetTileCount();
            var cells = new HashSet<Vector2Int>();
            List<Vector2Int> areaCenters = BuildAreaCenters(random, targetCount);

            for (int i = 0; i < areaCenters.Count; i++)
            {
                Vector2Int center = areaCenters[i];
                CarveHexBrush(cells, center, PlazaRadiusFor(i, areaCenters.Count, targetCount));
            }

            for (int i = 1; i < areaCenters.Count; i++)
                CarveWideRoad(random, cells, areaCenters[i - 1], areaCenters[i]);

            AddIntentionalLoops(random, cells, areaCenters);
            WidenFieldUntilTarget(random, cells, targetCount);
            TrimFieldToTarget(cells, targetCount);

            List<Vector2Int> orderedCells = SortCellsForProgression(cells);
            bossCell = FindFarthestCell(cells);
            interactionCells = PickInteractionCells(random, orderedCells, areaCenters, bossCell);
            AddOptionalInteractionCells(random, orderedCells, interactionCells);
            interactionCells.Add(bossCell);
            interactionCells.Remove(Vector2Int.zero);

            return orderedCells;
        }

        private int ResolveTargetTileCount()
        {
            if (targetTileCount > 0)
                return Mathf.Max(12, targetTileCount);

            int branchLength = Mathf.RoundToInt((Mathf.Max(1, minBranchLength) + Mathf.Max(1, maxBranchLength)) * 0.5f);
            return Mathf.Max(12, mainPathLength + Mathf.Max(0, branchCount) * branchLength);
        }

        private List<Vector2Int> BuildAreaCenters(System.Random random, int targetCount)
        {
            int areaCount = Mathf.Clamp(Mathf.RoundToInt(targetCount / 16f) + 1, 3, 6);
            var centers = new List<Vector2Int> { Vector2Int.zero };
            int forwardDirection = random.Next(Directions.Length);
            int sideDirection = (forwardDirection + 1 + random.Next(0, 2)) % Directions.Length;
            Vector2Int current = Vector2Int.zero;

            for (int i = 1; i < areaCount; i++)
            {
                int forward = random.Next(3, 6);
                int lateral = random.Next(-2, 3);
                Vector2Int next = current +
                                  Scale(Directions[forwardDirection], forward) +
                                  Scale(Directions[sideDirection], lateral);
                int guard = 0;
                while ((centers.Contains(next) || HexDistance(current, next) < 3) && guard++ < 12)
                {
                    next += Directions[(forwardDirection + guard) % Directions.Length];
                }

                current = next;
                centers.Add(current);

                if (random.NextDouble() < 0.35d)
                    sideDirection = (sideDirection + (random.Next(0, 2) == 0 ? 1 : 5)) % Directions.Length;
            }

            return centers;
        }

        private static int PlazaRadiusFor(int index, int areaCount, int targetCount)
        {
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
            IReadOnlyList<Vector2Int> areaCenters)
        {
            int loops = Mathf.Clamp(areaCenters.Count - 2, 1, 3);
            for (int i = 0; i < loops; i++)
            {
                int fromIndex = Mathf.Clamp(i, 0, areaCenters.Count - 1);
                int toIndex = Mathf.Clamp(i + 2, 0, areaCenters.Count - 1);
                if (fromIndex == toIndex)
                    continue;

                Vector2Int from = areaCenters[fromIndex] + Directions[(i + random.Next(0, 2)) % Directions.Length];
                Vector2Int to = areaCenters[toIndex] + Directions[(i + 3 + random.Next(0, 2)) % Directions.Length];
                CarveWideRoad(random, cells, from, to);
            }
        }

        private static void WidenFieldUntilTarget(
            System.Random random,
            HashSet<Vector2Int> cells,
            int targetCount)
        {
            int maxCount = Mathf.CeilToInt(targetCount * 1.12f);
            int guard = 0;
            while (cells.Count < targetCount && guard++ < targetCount * 24)
            {
                var candidates = new List<Vector2Int>();
                foreach (Vector2Int cell in cells)
                {
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int candidate = cell + direction;
                        if (cells.Contains(candidate))
                            continue;

                        int neighbors = CountExistingNeighbors(candidate, cells);
                        if (neighbors >= 2 && HexDistance(candidate) <= targetCount / 4 + 6)
                            candidates.Add(candidate);
                    }
                }

                if (candidates.Count == 0)
                    break;

                cells.Add(candidates[random.Next(candidates.Count)]);
                if (cells.Count >= maxCount)
                    break;
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
            mesh = CreateHexMesh(textureName, meshScale, uvPadding);
            generatedMeshes[meshKey] = mesh;
            return mesh;
        }

        private Vector3 AxialToLocalPosition(Vector2Int cell)
        {
            float x = tileRadius * Mathf.Sqrt(3f) * (cell.x + cell.y * 0.5f);
            float z = tileRadius * 1.5f * cell.y;
            return new Vector3(x, tileY, z);
        }

        private Mesh CreateHexMesh(string textureName, float meshScale, float uvPadding)
        {
            meshScale = Mathf.Max(0.1f, meshScale);
            uvPadding = Mathf.Clamp(uvPadding, 0f, 0.2f);

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
                    0.5f + Mathf.Cos(angle) * (0.5f - uvPadding),
                    0.5f + Mathf.Sin(angle) * (0.5f - uvPadding));
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

        private static Material CreateMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Texture") ??
                            Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = $"HexTile_{texture.name}",
                mainTexture = texture,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry - 5,
                hideFlags = HideFlags.DontSave,
            };

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
