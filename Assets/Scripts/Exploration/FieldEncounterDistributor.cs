using System;
using System.Collections.Generic;
using FFSS.Framework.Combat;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using UnityEngine;
using FrameworkEnemyRank = FFSS.Framework.Combat.EnemyEncounterRank;

namespace CardBattle.Exploration
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HexTileMapGenerator))]
    public sealed class FieldEncounterDistributor : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private EncounterSceneCatalog catalog;
        [SerializeField, Range(1, 3)] private int previewAct = 1;
        [SerializeField] private bool startRunWhenOpenedDirectly = true;
        [SerializeField] private int directOpenSeed = 1701;

        [Header("Scene references")]
        [SerializeField] private HexTileMapGenerator mapGenerator;
        [SerializeField] private Transform player;

        [Header("Inspectable marker prefabs")]
        [SerializeField] private GameObject normalMarkerPrefab;
        [SerializeField] private GameObject midBossMarkerPrefab;
        [SerializeField] private GameObject bossMarkerPrefab;
        [SerializeField, Min(0.2f)] private float activationRadius = 0.85f;

        private readonly List<GameObject> markerInstances = new();

        private void Awake()
        {
            ResolveReferences();
            ConfigureRuntimeSeed();
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

        private void ConfigureRuntimeSeed()
        {
            if (!Application.isPlaying || mapGenerator == null || !GameKernel.IsReady)
                return;

            RunManager runs = GameKernel.Services.Get<RunManager>();
            if (!runs.HasActiveRun && startRunWhenOpenedDirectly)
                runs.StartNewRun(directOpenSeed);

            if (!runs.HasActiveRun)
                return;

            int act = Mathf.Clamp(runs.Current.act, 1, 3);
            mapGenerator.SetRuntimeSeed(unchecked(runs.Current.seed ^ (act * 486187739)));
        }

        private void BuildMarkers(IReadOnlyList<GeneratedHexTile> tiles)
        {
            ClearMarkers();
            if (catalog == null || tiles == null)
                return;

            int act = GetCurrentAct();
            List<EncounterSceneEntry> entries = GetRemainingEntries(act);
            if (entries.Count == 0)
                return;

            var regularTiles = new List<GeneratedHexTile>();
            GeneratedHexTile? bossTile = null;
            for (int i = 0; i < tiles.Count; i++)
            {
                GeneratedHexTile tile = tiles[i];
                if (!tile.IsInteraction || tile.Tile == null)
                    continue;

                if (tile.IsBoss)
                    bossTile = tile;
                else
                    regularTiles.Add(tile);
            }

            regularTiles.Sort((left, right) => left.Order.CompareTo(right.Order));
            EncounterSceneEntry bossEntry = entries.Find(entry => entry.encounter.rank == FrameworkEnemyRank.Boss);
            entries.Remove(bossEntry);

            int regularCount = Mathf.Min(entries.Count, regularTiles.Count);
            for (int i = 0; i < regularCount; i++)
                CreateMarker(entries[i], regularTiles[i]);

            if (bossEntry != null && bossTile.HasValue)
                CreateMarker(bossEntry, bossTile.Value);
        }

        private List<EncounterSceneEntry> GetRemainingEntries(int act)
        {
            var entries = new List<EncounterSceneEntry>();
            RunState run = Application.isPlaying && GameKernel.IsReady
                ? GameKernel.Services.Get<RunManager>().Current
                : null;

            IReadOnlyList<EncounterSceneEntry> configured = catalog.Entries;
            for (int i = 0; i < configured.Count; i++)
            {
                EncounterSceneEntry entry = configured[i];
                if (entry == null || entry.act != act)
                    continue;

                if (run != null && run.completedEventIds.Contains(EncounterFlowManager.GetCompletionId(entry.enemyId)))
                    continue;

                entries.Add(entry);
            }

            entries.Sort((left, right) =>
            {
                int rank = left.encounter.rank.CompareTo(right.encounter.rank);
                return rank != 0 ? rank : string.CompareOrdinal(left.enemyId, right.enemyId);
            });
            return entries;
        }

        private void CreateMarker(EncounterSceneEntry entry, GeneratedHexTile tile)
        {
            GameObject prefab = GetMarkerPrefab(entry.encounter.rank);
            if (prefab == null || tile.Tile == null)
                return;

            GameObject marker = Instantiate(prefab, tile.Tile.transform);
            marker.name = $"Encounter Marker - {entry.enemyId}";
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            if (!Application.isPlaying)
                marker.hideFlags = HideFlags.DontSave;

            FieldEncounterMarkerView view = marker.GetComponent<FieldEncounterMarkerView>();
            view?.Configure(entry.encounter);

            FieldEncounterNode node = marker.GetComponent<FieldEncounterNode>();
            if (node != null)
            {
                node.Configure(entry.enemyId, player, activationRadius);
                node.enabled = Application.isPlaying;
            }

            markerInstances.Add(marker);
        }

        private GameObject GetMarkerPrefab(FrameworkEnemyRank rank)
        {
            switch (rank)
            {
                case FrameworkEnemyRank.MidBoss:
                    return midBossMarkerPrefab;
                case FrameworkEnemyRank.Boss:
                    return bossMarkerPrefab;
                default:
                    return normalMarkerPrefab;
            }
        }

        private int GetCurrentAct()
        {
            if (Application.isPlaying && GameKernel.IsReady)
            {
                RunManager runs = GameKernel.Services.Get<RunManager>();
                if (runs.HasActiveRun)
                    return Mathf.Clamp(runs.Current.act, 1, 3);
            }

            return previewAct;
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
