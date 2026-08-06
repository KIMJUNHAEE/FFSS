using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFSS.Framework.UI
{
    public enum UILayer
    {
        Screen,
        Overlay,
        Modal
    }

    [Serializable]
    public sealed class UIScreenCatalogEntry
    {
        public UIScreenId id;
        public UIScreen prefab;
        public UILayer layer;
        public bool keepAlive = true;
    }

    [CreateAssetMenu(menuName = "FFSS/UI/Screen Catalog", fileName = "UIScreenCatalog")]
    public sealed class UIScreenCatalog : ScriptableObject
    {
        [SerializeField] private List<UIScreenCatalogEntry> screens = new List<UIScreenCatalogEntry>();

        public UIScreenCatalogEntry Get(UIScreenId id)
        {
            UIScreenCatalogEntry entry = screens.Find(item => item != null && item.id == id);
            if (entry == null || entry.prefab == null)
            {
                throw new InvalidOperationException($"UI screen is not configured: {id}");
            }

            return entry;
        }
    }
}
