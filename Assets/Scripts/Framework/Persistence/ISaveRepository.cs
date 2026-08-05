namespace FFSS.Framework.Persistence
{
    public interface ISaveRepository
    {
        bool Exists(int slot);
        string Read(int slot);
        void Write(int slot, string payload);
        void Delete(int slot);
    }
}
