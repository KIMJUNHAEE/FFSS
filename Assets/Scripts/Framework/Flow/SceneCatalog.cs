using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.Flow
{
    public enum GameSceneId
    {
        Bootstrap,
        Title,
        Field,
        Combat,
        Result
    }

    [Serializable]
    public sealed class SceneCatalogEntry
    {
        public GameSceneId id;
        public string sceneName;
    }

    [CreateAssetMenu(menuName = "FFSS/Flow/Scene Catalog", fileName = "SceneCatalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [SerializeField] private List<SceneCatalogEntry> scenes = new List<SceneCatalogEntry>();

        public string GetSceneName(GameSceneId id)
        {
            SceneCatalogEntry entry = scenes.Find(item => item != null && item.id == id);
            if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName))
            {
                throw new InvalidOperationException($"Scene is not configured in SceneCatalog: {id}");
            }

            return entry.sceneName;
        }
    }
}
