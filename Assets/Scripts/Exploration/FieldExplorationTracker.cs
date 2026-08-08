using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace CardBattle.Exploration
{
    [DisallowMultipleComponent]
    public sealed class FieldExplorationTracker : MonoBehaviour
    {
        [SerializeField] private HexTileMapGenerator map;

        public Vector2Int CurrentCell { get; private set; }
        public int VisitedTileCount { get; private set; }
        public bool HasCurrentCell { get; private set; }

        private HexTileMapGenerator subscribedMap;
        private bool restoreAttempted;

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
            restoreAttempted = false;
            TryRestoreCurrentTile();
        }

        private void TryRestoreCurrentTile()
        {
            if (restoreAttempted || map == null || map.GeneratedTiles.Count == 0 ||
                !GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) ||
                !runs.HasActiveRun)
            {
                return;
            }

            RunActProgressState progress = runs.Current.CurrentActProgress;
            restoreAttempted = true;
            if (!progress.hasCurrentCell ||
                !map.TryGetWorldPosition(
                    new Vector2Int(progress.currentAxialX, progress.currentAxialY),
                    out Vector3 tilePosition))
            {
                return;
            }

            Vector3 current = transform.position;
            transform.position = new Vector3(tilePosition.x, current.y, tilePosition.z);
        }

        private void RecordCurrentTile()
        {
            if (map == null || !map.TryGetCell(transform.position, out Vector2Int cell) ||
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
