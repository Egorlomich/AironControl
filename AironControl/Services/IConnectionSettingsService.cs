namespace AironControl.Services
{
    public interface IConnectionSettingsService
    {
        List<Host> GetAll();
        void Save(Host host);
        void Delete(string settingsName);
        Host GetActive();
        void SetActive(Host host);
    }
}
