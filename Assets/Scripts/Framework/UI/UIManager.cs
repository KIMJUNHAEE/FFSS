using System.Collections.Generic;
using FFSS.Framework.Core;
using UnityEngine;

namespace FFSS.Framework.UI
{
    public readonly struct UIScreenShownEvent
    {
        public UIScreenShownEvent(UIScreenId screen)
        {
            Screen = screen;
        }

        public UIScreenId Screen { get; }
    }

    public sealed class UIManager : GameServiceBehaviour
    {
        [SerializeField] private UIScreenCatalog catalog;
        [SerializeField] private Transform screenRoot;
        [SerializeField] private Transform overlayRoot;
        [SerializeField] private Transform modalRoot;

        private readonly Dictionary<UIScreenId, UIScreen> instances = new Dictionary<UIScreenId, UIScreen>();
        private readonly Stack<UIScreenId> modalStack = new Stack<UIScreenId>();
        private GameEventBus events;

        public UIScreen Show(UIScreenId id, bool playAnimation = true)
        {
            UIScreenCatalogEntry entry = catalog.Get(id);
            UIScreen screen = GetOrCreate(entry);

            if (entry.layer == UILayer.Screen)
            {
                HideLayer(UILayer.Screen, id, playAnimation);
            }
            else if (entry.layer == UILayer.Modal)
            {
                modalStack.Push(id);
            }

            screen.SetVisible(true, playAnimation);
            events.Publish(new UIScreenShownEvent(id));
            return screen;
        }

        public void Hide(UIScreenId id, bool playAnimation = true)
        {
            if (!instances.TryGetValue(id, out UIScreen screen))
            {
                return;
            }

            UIScreenCatalogEntry entry = catalog.Get(id);
            screen.SetVisible(false, playAnimation);

            if (!entry.keepAlive)
            {
                instances.Remove(id);
                Destroy(screen.gameObject);
            }
        }

        public void CloseTopModal(bool playAnimation = true)
        {
            while (modalStack.Count > 0)
            {
                UIScreenId id = modalStack.Pop();
                if (instances.TryGetValue(id, out UIScreen screen) && screen.IsVisible)
                {
                    Hide(id, playAnimation);
                    return;
                }
            }
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            events = context.Events;
            RegisterPreloadedScreens(screenRoot);
            RegisterPreloadedScreens(overlayRoot);
            RegisterPreloadedScreens(modalRoot);
        }

        protected override void OnShutdown()
        {
            modalStack.Clear();
            instances.Clear();
            events = null;
        }

        private UIScreen GetOrCreate(UIScreenCatalogEntry entry)
        {
            if (instances.TryGetValue(entry.id, out UIScreen existing))
            {
                return existing;
            }

            Transform parent = GetLayerRoot(entry.layer);
            UIScreen created = Instantiate(entry.prefab, parent);
            created.name = entry.prefab.name;
            instances.Add(entry.id, created);
            return created;
        }

        private void HideLayer(UILayer layer, UIScreenId except, bool playAnimation)
        {
            var screensToHide = new List<UIScreenId>();
            foreach (KeyValuePair<UIScreenId, UIScreen> pair in instances)
            {
                if (pair.Key == except || !pair.Value.IsVisible)
                {
                    continue;
                }

                UIScreenCatalogEntry entry = catalog.Get(pair.Key);
                if (entry.layer == layer)
                {
                    screensToHide.Add(pair.Key);
                }
            }

            for (int i = 0; i < screensToHide.Count; i++)
            {
                Hide(screensToHide[i], playAnimation);
            }
        }

        private Transform GetLayerRoot(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Screen:
                    return screenRoot;
                case UILayer.Overlay:
                    return overlayRoot;
                case UILayer.Modal:
                    return modalRoot;
                default:
                    return transform;
            }
        }

        private void RegisterPreloadedScreens(Transform root)
        {
            if (root == null)
            {
                return;
            }

            UIScreen[] preloaded = root.GetComponentsInChildren<UIScreen>(true);
            for (int i = 0; i < preloaded.Length; i++)
            {
                UIScreen screen = preloaded[i];
                if (instances.TryGetValue(screen.Id, out UIScreen existing) && existing != screen)
                {
                    Debug.LogWarning($"Duplicate preloaded UI screen ignored: {screen.Id}", screen);
                    continue;
                }

                instances[screen.Id] = screen;
            }
        }
    }
}
