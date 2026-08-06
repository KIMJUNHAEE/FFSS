using System;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;

namespace FFSS.Framework.Persistence
{
    public readonly struct SaveCompletedEvent
    {
        public SaveCompletedEvent(int slot)
        {
            Slot = slot;
        }

        public int Slot { get; }
    }

    public sealed class SaveManager : GameServiceBehaviour
    {
        [SerializeField, Range(1, 8)] private int slotCount = 3;
        [SerializeField] private bool forcePlayerPrefsRepository;

        private GameServiceRegistry services;
        private GameEventBus events;
        private ISaveRepository repository;

        public int SlotCount => slotCount;

        public bool HasSave(int slot)
        {
            ValidateSlot(slot);
            return repository.Exists(slot);
        }

        public void Save(int slot, PlayerSettingsData settings = null)
        {
            ValidateSlot(slot);
            RunManager runs = services.Get<RunManager>();
            if (runs.Current == null)
            {
                throw new InvalidOperationException("There is no run to save.");
            }

            var data = new SaveGameData
            {
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                run = runs.Current,
                settings = settings ?? new PlayerSettingsData()
            };

            repository.Write(slot, JsonUtility.ToJson(data, true));
            events.Publish(new SaveCompletedEvent(slot));
        }

        public SaveGameData Load(int slot)
        {
            ValidateSlot(slot);
            if (!repository.Exists(slot))
            {
                return null;
            }

            SaveGameData data = SaveDataMigrations.Upgrade(
                JsonUtility.FromJson<SaveGameData>(repository.Read(slot)));
            ValidateData(data);
            services.Get<RunManager>().Restore(data.run);
            return data;
        }

        public void Delete(int slot)
        {
            ValidateSlot(slot);
            repository.Delete(slot);
        }

        protected override void OnInitialize(GameServiceContext context)
        {
            services = context.Services;
            events = context.Events;
#if UNITY_WEBGL && !UNITY_EDITOR
            repository = new PlayerPrefsSaveRepository();
#else
            repository = forcePlayerPrefsRepository
                ? new PlayerPrefsSaveRepository()
                : new FileSaveRepository(Application.persistentDataPath);
#endif
        }

        protected override void OnShutdown()
        {
            repository = null;
            services = null;
            events = null;
        }

        private void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Save slot must be between 0 and {slotCount - 1}.");
            }
        }

        private static void ValidateData(SaveGameData data)
        {
            if (data == null || data.run == null)
            {
                throw new InvalidOperationException("The save data is empty or invalid.");
            }

            if (data.schemaVersion > SaveGameData.CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Save schema {data.schemaVersion} is newer than supported schema {SaveGameData.CurrentSchemaVersion}.");
            }
        }
    }
}
