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

        private void Start()
        {
            ResolveMap();
            RecordCurrentTile();
        }

        private void LateUpdate()
        {
            if (map == null)
                ResolveMap();

            RecordCurrentTile();
        }

        private void ResolveMap()
        {
            map = FindAnyObjectByType<HexTileMapGenerator>();
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
