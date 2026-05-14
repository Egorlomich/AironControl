using AironControl.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace AironControl.ViewModels
{
    public class SettingsVM : INotifyPropertyChanged
    {
        private readonly ConnectionVM _connectionVm;
        private readonly IConnectionSettingsService _settingsService;

        public Func<string, string, string, string, Task<bool>> ConfirmAsync { get; set; } = (_, _, _, _) => Task.FromResult(false);
        public Func<string, string, string, Task> AlertAsync { get; set; } = (_, _, _) => Task.CompletedTask;

        private bool _isEditing;
        private string _editingPresetName = "";

        #region Вкладки
        private Color _tabConnectionsColor = Colors.White;
        private Color _tabEditColor = Color.FromRgb(245, 245, 245);
        public Color TabConnectionsColor { get => _tabConnectionsColor; set => SetField(ref _tabConnectionsColor, value); }
        public Color TabEditColor { get => _tabEditColor; set => SetField(ref _tabEditColor, value); }

        private bool _isConnectionsZoneVisible = true;
        private bool _isEditZoneVisible;
        public bool IsConnectionsZoneVisible { get => _isConnectionsZoneVisible; set => SetField(ref _isConnectionsZoneVisible, value); }
        public bool IsEditZoneVisible { get => _isEditZoneVisible; set => SetField(ref _isEditZoneVisible, value); }
        #endregion

        #region Текущее подключение
        private string _activePresetName = "Не выбран";
        private string _activeHostDisplay = "-";
        private string _activeUserDisplay = "-";
        private string _activePortDisplay = "-";
        private string _activeModelDisplay = "-";
        private string _connectButtonText = "Подключиться";
        private bool _isTestButtonEnabled = true;
        private bool _isConnectButtonEnabled = true;

        public string ActivePresetName { get => _activePresetName; set => SetField(ref _activePresetName, value); }
        public string ActiveHostDisplay { get => _activeHostDisplay; set => SetField(ref _activeHostDisplay, value); }
        public string ActiveUserDisplay { get => _activeUserDisplay; set => SetField(ref _activeUserDisplay, value); }
        public string ActivePortDisplay { get => _activePortDisplay; set => SetField(ref _activePortDisplay, value); }
        public string ActiveModelDisplay { get => _activeModelDisplay; set => SetField(ref _activeModelDisplay, value); }
        public string ConnectButtonText { get => _connectButtonText; set => SetField(ref _connectButtonText, value); }
        public bool IsTestButtonEnabled { get => _isTestButtonEnabled; set => SetField(ref _isTestButtonEnabled, value); }
        public bool IsConnectButtonEnabled { get => _isConnectButtonEnabled; set => SetField(ref _isConnectButtonEnabled, value); }
        #endregion

        #region Список пресетов
        public ObservableCollection<Host> Presets { get; } = new();
        #endregion

        #region Форма редактирования
        private string _editTitle = "Создание нового подключения";
        private string _formName = "";
        private string _formHost = "";
        private string _formUser = "";
        private string _formPort = "22";
        private string _formPassword = "";
        private int _formModelIndex = 0;

        public string EditTitle { get => _editTitle; set => SetField(ref _editTitle, value); }
        public string FormName { get => _formName; set => SetField(ref _formName, value); }
        public string FormHost { get => _formHost; set => SetField(ref _formHost, value); }
        public string FormUser { get => _formUser; set => SetField(ref _formUser, value); }
        public string FormPort { get => _formPort; set => SetField(ref _formPort, value); }
        public string FormPassword { get => _formPassword; set => SetField(ref _formPassword, value); }
        public int FormModelIndex { get => _formModelIndex; set => SetField(ref _formModelIndex, value); }
        #endregion

        #region Команды
        public ICommand ShowConnectionsTabCommand { get; }
        public ICommand ShowEditTabCommand { get; }
        public ICommand SelectPresetCommand { get; }
        public ICommand EditPresetCommand { get; }
        public ICommand DeletePresetCommand { get; }
        public ICommand SavePresetCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand NewPresetCommand { get; }
        #endregion

        private static readonly string[] ModelNames = { "Raspberry Pi 4", "Jetson Nano", "Custom" };

        public SettingsVM(ConnectionVM connectionVm, IConnectionSettingsService settingsService)
        {
            _connectionVm = connectionVm;
            _settingsService = settingsService;

            ShowConnectionsTabCommand = new Command(() => ShowTab("connections"));
            ShowEditTabCommand = new Command(() => ShowTab("edit"));
            NewPresetCommand = new Command(() => { ClearForm(); ShowTab("edit"); });

            SelectPresetCommand = new Command<Host>(async preset => await OnSelectPresetAsync(preset));
            EditPresetCommand = new Command<Host>(OnEditPreset);
            DeletePresetCommand = new Command<Host>(async preset => await OnDeletePresetAsync(preset));

            SavePresetCommand = new Command(async () => await OnSavePresetAsync());
            CancelCommand = new Command(() => { ClearForm(); ShowTab("connections"); });
            TestConnectionCommand = new Command(async () => await OnTestConnectionAsync());
            ConnectCommand = new Command(async () => await OnConnectAsync());
        }

        public void Initialize()
        {
            LoadPresets();
            RefreshActiveConnectionDisplay();
        }

        private void LoadPresets()
        {
            Presets.Clear();
            foreach (var h in _settingsService.GetAll())
                Presets.Add(h);
        }

        private void RefreshActiveConnectionDisplay()
        {
            var active = _settingsService.GetActive();
            if (string.IsNullOrWhiteSpace(active.settingsName))
            {
                ActivePresetName = "Не выбран";
                ActiveHostDisplay = "-";
                ActiveUserDisplay = "-";
                ActivePortDisplay = "-";
                ActiveModelDisplay = "-";
            }
            else
            {
                ActivePresetName = active.settingsName;
                ActiveHostDisplay = active.host;
                ActiveUserDisplay = active.user;
                ActivePortDisplay = active.port.ToString();
                ActiveModelDisplay = active.deviceName;
            }

            ConnectButtonText = _connectionVm.IsConnected ? "Отключиться" : "Подключиться";
        }

        private void ShowTab(string tab)
        {
            IsConnectionsZoneVisible = tab == "connections";
            IsEditZoneVisible = tab == "edit";

            var gray = Color.FromRgb(245, 245, 245);
            TabConnectionsColor = tab == "connections" ? Colors.White : gray;
            TabEditColor = tab == "edit" ? Colors.White : gray;
        }

        private async Task OnSelectPresetAsync(Host preset)
        {
            try
            {
                _settingsService.SetActive(preset);
                RefreshActiveConnectionDisplay();
                await AlertAsync("Успех", $"Подключение '{preset.settingsName}' выбрано как активное", "OK");
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка", $"Не удалось выбрать подключение: {ex.Message}", "OK");
            }
        }

        private void OnEditPreset(Host preset)
        {
            _isEditing = true;
            _editingPresetName = preset.settingsName;
            EditTitle = "Редактирование подключения";
            FormName = preset.settingsName;
            FormHost = preset.host;
            FormUser = preset.user;
            FormPort = preset.port.ToString();
            FormPassword = preset.password;
            FormModelIndex = Array.IndexOf(ModelNames, preset.deviceName);
            if (FormModelIndex < 0) FormModelIndex = 0;
            ShowTab("edit");
        }

        private async Task OnDeletePresetAsync(Host preset)
        {
            bool confirm = await ConfirmAsync("Подтверждение",
                $"Удалить подключение '{preset.settingsName}'?", "Да", "Нет");

            if (!confirm) return;

            try
            {
                _settingsService.Delete(preset.settingsName);
                var item = Presets.FirstOrDefault(h => h.settingsName == preset.settingsName);
                if (!string.IsNullOrEmpty(item.settingsName))
                    Presets.Remove(item);

                await AlertAsync("Успех", "Подключение удалено", "OK");
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка", $"Не удалось удалить: {ex.Message}", "OK");
            }
        }

        private async Task OnSavePresetAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName) || string.IsNullOrWhiteSpace(FormHost) ||
                string.IsNullOrWhiteSpace(FormUser) || string.IsNullOrWhiteSpace(FormPort) ||
                string.IsNullOrWhiteSpace(FormPassword))
            {
                await AlertAsync("Ошибка", "Заполните все поля", "OK");
                return;
            }

            if (!int.TryParse(FormPort, out int port) || port <= 0)
            {
                await AlertAsync("Ошибка", "Неверный порт", "OK");
                return;
            }

            if (!_isEditing && Presets.Any(p => p.settingsName == FormName))
            {
                await AlertAsync("Ошибка", "Подключение с таким именем уже существует", "OK");
                return;
            }

            var preset = new Host
            {
                settingsName = FormName,
                host = FormHost,
                user = FormUser,
                port = port,
                password = FormPassword,
                deviceName = FormModelIndex >= 0 && FormModelIndex < ModelNames.Length
                    ? ModelNames[FormModelIndex]
                    : "Raspberry Pi 4"
            };

            if (_isEditing && !string.IsNullOrEmpty(_editingPresetName))
            {
                _settingsService.Delete(_editingPresetName);
                var old = Presets.FirstOrDefault(h => h.settingsName == _editingPresetName);
                if (!string.IsNullOrEmpty(old.settingsName))
                    Presets.Remove(old);
            }

            _settingsService.Save(preset);
            Presets.Add(preset);

            RefreshActiveConnectionDisplay();
            ClearForm();

            await AlertAsync("Успех", _isEditing ? "Изменения сохранены" : "Подключение создано", "OK");
            ShowTab("connections");
        }

        private async Task OnTestConnectionAsync()
        {
            var active = _settingsService.GetActive();
            if (string.IsNullOrWhiteSpace(active.settingsName))
            {
                await AlertAsync("Ошибка", "Сначала выберите активное подключение", "OK");
                return;
            }

            try
            {
                IsTestButtonEnabled = false;
                bool result = await _connectionVm.ConnectAsync();
                _connectionVm.RefreshStatus();
                ConnectButtonText = _connectionVm.IsConnected ? "Отключиться" : "Подключиться";

                if (result)
                    await AlertAsync("Успех", "Подключение успешно установлено!", "OK");
                else
                    await AlertAsync("Ошибка", "Не удалось установить подключение", "OK");
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка", $"Ошибка при подключении: {ex.Message}", "OK");
            }
            finally
            {
                IsTestButtonEnabled = true;
            }
        }

        private async Task OnConnectAsync()
        {
            var active = _settingsService.GetActive();
            if (string.IsNullOrWhiteSpace(active.settingsName))
            {
                await AlertAsync("Ошибка", "Сначала выберите активное подключение", "OK");
                return;
            }

            try
            {
                IsConnectButtonEnabled = false;
                ConnectButtonText = "Подключение...";

                bool success = await _connectionVm.ConnectAsync();
                _connectionVm.RefreshStatus();

                ConnectButtonText = _connectionVm.IsConnected ? "Отключиться" : "Подключиться";

                if (success)
                    await AlertAsync("Успех", "Подключение установлено", "OK");
                else
                    await AlertAsync("Ошибка", "Не удалось подключиться", "OK");
            }
            catch (Exception ex)
            {
                await AlertAsync("Ошибка", $"Ошибка подключения: {ex.Message}", "OK");
                ConnectButtonText = "Подключиться";
            }
            finally
            {
                IsConnectButtonEnabled = true;
            }
        }

        private void ClearForm()
        {
            _isEditing = false;
            _editingPresetName = "";
            EditTitle = "Создание нового подключения";
            FormName = "";
            FormHost = "";
            FormUser = "";
            FormPort = "22";
            FormPassword = "";
            FormModelIndex = 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
