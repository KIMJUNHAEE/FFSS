using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Exploration
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class FieldExplorationTracker : MonoBehaviour
    {
        [SerializeField] private HexTileMapGenerator map;

        public Vector2Int CurrentCell { get; private set; }
        public int VisitedTileCount { get; private set; }
        public bool HasCurrentCell { get; private set; }

        private HexTileMapGenerator subscribedMap;
        private bool initialPositionReady;

        private void Start()
        {
            ResolveMap();
            TryRestoreCurrentTile();
            RecordCurrentTile();
        }

        private void OnDestroy()
        {
            SubscribeToMap(null);
        }

        private void LateUpdate()
        {
            if (map == null)
                ResolveMap();

            TryRestoreCurrentTile();
            RecordCurrentTile();
        }

        private void ResolveMap()
        {
            map = FindAnyObjectByType<HexTileMapGenerator>();
            SubscribeToMap(map);
        }

        private void SubscribeToMap(HexTileMapGenerator target)
        {
            if (subscribedMap == target)
                return;

            if (subscribedMap != null)
                subscribedMap.GenerationCompleted -= HandleMapGenerated;

            subscribedMap = target;
            if (subscribedMap != null)
                subscribedMap.GenerationCompleted += HandleMapGenerated;
        }

        private void HandleMapGenerated(System.Collections.Generic.IReadOnlyList<GeneratedHexTile> _)
        {
            initialPositionReady = false;
            TryRestoreCurrentTile();
        }

        private void TryRestoreCurrentTile()
        {
            if (initialPositionReady || map == null || map.GeneratedTiles.Count == 0 ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) ||
                !runs.HasActiveRun)
            {
                return;
            }

            RunActProgressState progress = runs.Current.CurrentActProgress;
            if (!progress.hasCurrentCell)
            {
                initialPositionReady = true;
                return;
            }

            Vector2Int savedCell = new(progress.currentAxialX, progress.currentAxialY);
            if (!map.TryGetWorldPosition(savedCell, out Vector3 tilePosition) &&
                !TryGetNearestGeneratedPosition(savedCell, out tilePosition))
            {
                return;
            }

            Vector3 current = transform.position;
            SetPlayerPosition(new Vector3(tilePosition.x, current.y, tilePosition.z));
            initialPositionReady = true;
        }

        private void SetPlayerPosition(Vector3 worldPosition)
        {
            CharacterController characterController = GetComponent<CharacterController>();
            bool restoreController = characterController != null && characterController.enabled;
            if (restoreController)
                characterController.enabled = false;

            transform.position = worldPosition;

            if (restoreController)
                characterController.enabled = true;
        }

        private bool TryGetNearestGeneratedPosition(Vector2Int savedCell, out Vector3 worldPosition)
        {
            worldPosition = default;
            int nearestDistance = int.MaxValue;
            bool found = false;
            for (int i = 0; i < map.GeneratedTiles.Count; i++)
            {
                GeneratedHexTile tile = map.GeneratedTiles[i];
                if (tile.Tile == null)
                    continue;

                Vector2Int delta = tile.Cell - savedCell;
                int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.x + delta.y);
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                worldPosition = tile.Tile.transform.position;
                found = true;
            }

            return found;
        }

        private void RecordCurrentTile()
        {
            if (!initialPositionReady || map == null ||
                !map.TryGetCell(transform.position, out Vector2Int cell) ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) ||
                !runs.HasActiveRun)
            {
                return;
            }

            RunActProgressState progress = runs.Current.CurrentActProgress;
            progress.visitedTileIds ??= new System.Collections.Generic.List<string>();
            progress.hasCurrentCell = true;
            progress.currentAxialX = cell.x;
            progress.currentAxialY = cell.y;

            string tileId = $"{cell.x},{cell.y}";
            if (!progress.visitedTileIds.Contains(tileId))
                progress.visitedTileIds.Add(tileId);

            CurrentCell = cell;
            HasCurrentCell = true;
            VisitedTileCount = progress.visitedTileIds.Count;
        }
    }
}
