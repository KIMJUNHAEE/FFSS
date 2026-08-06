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
        [SerializeField] private int mainPathLength = 18;
        [SerializeField] private int branchCount = 6;
        [SerializeField] private int minBranchLength = 3;
        [SerializeField] private int maxBranchLength = 4;
        [SerializeField] private int softRadiusLimit = 5;
        [SerializeField] private int minInteractionHexDistance = 4;
        [SerializeField, Range(0f, 1f)] private float interactionTileChance = 0f;
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
            int branches = Mathf.Max(1, contentNodeCount - 1);
            int trunk = Mathf.Clamp(Mathf.RoundToInt(target * 0.36f), 10, Mathf.Max(10, target - branches));
            int branchBudget = Mathf.Max(branches, target - trunk);
            int averageBranchLength = Mathf.Max(1, Mathf.RoundToInt(branchBudget / (float)branches));

            mainPathLength = trunk;
            branchCount = branches;
            minBranchLength = Mathf.Max(1, averageBranchLength - 1);
            maxBranchLength = Mathf.Max(minBranchLength, averageBranchLength + 1);
            softRadiusLimit = Mathf.Max(6, Mathf.CeilToInt(Mathf.Sqrt(target) * 1.35f));
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
            var cells = new HashSet<Vector2Int> { Vector2Int.zero };
            var orderedCells = new List<Vector2Int> { Vector2Int.zero };
            var reservedInteractionCells = new List<Vector2Int>();

            Vector2Int current = Vector2Int.zero;
            int previousDirection = -1;
            int targetLength = Mathf.Max(1, mainPathLength);

            for (int step = 1; step < targetLength; step++)
            {
                if (!TryPickNextCell(random, current, previousDirection, cells, out Vector2Int next, out int direction))
                    break;

                current = next;
                previousDirection = direction;
                AddCell(current, cells, orderedCells);
            }

            if (current != Vector2Int.zero)
                reservedInteractionCells.Add(current);

            int targetBranchCount = Mathf.Max(0, branchCount);
            int branchAttempts = Mathf.Max(12, targetBranchCount * 24);
            int createdBranches = 0;
            for (int attempt = 0; attempt < branchAttempts && createdBranches < targetBranchCount; attempt++)
            {
                if (!TryBuildBranch(random, cells, orderedCells, reservedInteractionCells, out List<Vector2Int> branchPath))
                    continue;

                foreach (Vector2Int branchCell in branchPath)
                {
                    AddCell(branchCell, cells, orderedCells);
                }

                reservedInteractionCells.Add(branchPath[^1]);
                createdBranches++;
            }

            interactionCells = FindDeadEndCells(cells);
            interactionCells.Remove(Vector2Int.zero);

            foreach (Vector2Int cell in reservedInteractionCells)
                interactionCells.Add(cell);

            AddOptionalInteractionCells(random, orderedCells, interactionCells);
            bossCell = FindFarthestCell(interactionCells);

            return orderedCells;
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
