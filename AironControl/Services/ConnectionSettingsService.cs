using System.Text.Json;

namespace AironControl.Services
{
    public class ConnectionSettingsService : IConnectionSettingsService
    {
        private const string PresetsKey = "connection_presets";
        private const string ActiveKey = "active_preset_name";

        public List<Host> GetAll()
        {
            var json = Preferences.Default.Get(PresetsKey, "[]");
            try { return JsonSerializer.Deserialize<List<Host>>(json) ?? new(); }
            catch { return new(); }
        }

        public void Save(Host host)
        {
            var list = GetAll();
            var idx = list.FindIndex(h => h.settingsName == host.settingsName);
            if (idx >= 0) list[idx] = host;
            else list.Add(host);
            Preferences.Default.Set(PresetsKey, JsonSerializer.Serialize(list));
        }

        public void Delete(string settingsName)
        {
            var list = GetAll();
            list.RemoveAll(h => h.settingsName == settingsName);
            Preferences.Default.Set(PresetsKey, JsonSerializer.Serialize(list));
        }

        public Host GetActive()
        {
            var name = Preferences.Default.Get(ActiveKey, string.Empty);
            return GetAll().FirstOrDefault(h => h.settingsName == name);
        }

        public void SetActive(Host host)
        {
            Preferences.Default.Set(ActiveKey, host.settingsName);
        }
    }
}
