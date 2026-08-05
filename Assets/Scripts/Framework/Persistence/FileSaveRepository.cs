using System;
using System.IO;

namespace FFSS.Framework.Persistence
{
    public sealed class FileSaveRepository : ISaveRepository
    {
        private readonly string directory;

        public FileSaveRepository(string directory)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public bool Exists(int slot)
        {
            return File.Exists(GetPath(slot));
        }

        public string Read(int slot)
        {
            return File.ReadAllText(GetPath(slot));
        }

        public void Write(int slot, string payload)
        {
            Directory.CreateDirectory(directory);
            string path = GetPath(slot);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, payload);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporaryPath, path);
        }

        public void Delete(int slot)
        {
            string path = GetPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string GetPath(int slot)
        {
            return Path.Combine(directory, $"run-slot-{slot}.json");
        }
    }
}
