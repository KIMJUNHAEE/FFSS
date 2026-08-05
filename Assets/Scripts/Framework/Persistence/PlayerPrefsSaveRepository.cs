using UnityEngine;

namespace FFSS.Framework.Persistence
{
    public sealed class PlayerPrefsSaveRepository : ISaveRepository
    {
        private const string Prefix = "FFSS.Save.";

        public bool Exists(int slot)
        {
            return PlayerPrefs.HasKey(GetKey(slot));
        }

        public string Read(int slot)
        {
            return PlayerPrefs.GetString(GetKey(slot));
        }

        public void Write(int slot, string payload)
        {
            PlayerPrefs.SetString(GetKey(slot), payload);
            PlayerPrefs.Save();
        }

        public void Delete(int slot)
        {
            PlayerPrefs.DeleteKey(GetKey(slot));
            PlayerPrefs.Save();
        }

        private static string GetKey(int slot)
        {
            return Prefix + slot;
        }
    }
}
