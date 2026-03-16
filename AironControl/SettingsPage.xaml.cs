using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
namespace AironControl;

public partial class SettingsPage : ContentPage
{
    private bool _isEditing = false;
    private string _editingPresetName = "";

    public ObservableCollection<Host> Presets;
    private Connection _connection;
    private Host _activeHost;
    private Host? _editingPreset;

    public SettingsPage()
    {
        InitializeComponent();
        Presets = new ObservableCollection<Host>();
        if (PresetsCollectionView == null)
        {
            Debug.WriteLine("ERROR: PresetsListView is null!");
        }
        else
        {
            PresetsCollectionView.ItemsSource = Presets;
        }
        _connection = new Connection();
        _activeHost = _connection.CurrentHost;

        // Обработчики вкладок
        TabConnections.Clicked += async (_, __) => await ShowZone("connections");
        TabEdit.Clicked += async (_, __) => await ShowZone("edit");

        // Загружаем пресеты и отображаем активное подключение
        LoadPresets();
        UpdateActiveConnectionDisplay();

        // По умолчанию показываем вкладку подключений
        ShowZone("connections");
    }

    private async Task ShowZone(string zone)
    {
        // Скрываем все зоны
        ConnectionsZone.IsVisible = false;
        EditZone.IsVisible = false;

        // Сбрасываем цвета вкладок
        TabConnections.BackgroundColor = Color.FromRgb(245, 245, 245);
        TabEdit.BackgroundColor = Color.FromRgb(245, 245, 245);

        // Показываем выбранную зону
        switch (zone)
        {
            case "connections":
                ConnectionsZone.IsVisible = true;
                TabConnections.BackgroundColor = Colors.White;
                break;

            case "edit":
                EditZone.IsVisible = true;
                TabEdit.BackgroundColor = Colors.White;
                break;
        }
    }

    private async Task LoadPresetsAsync()
    {
        try
        {
            // Загружаем в фоновом потоке
            var allConnections = await Task.Run(() => _connection.GetAllConnections());

            // Обновляем в UI потоке
            await Dispatcher.DispatchAsync(async () =>
            {
                Presets.Clear();
                foreach (var connection in allConnections)
                {
                    Presets.Add(connection);
                }

                // Обновляем видимость метки
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load error: {ex.Message}");
        }
    }
    private async void LoadPresets()
    {
        await LoadPresetsAsync();
    }
    private void UpdateActiveConnectionDisplay()
    {
        _activeHost = _connection.CurrentHost;

        // Используем default(Host) для проверки
        if (string.IsNullOrWhiteSpace(_activeHost.settingsName))
        {
            ActivePresetNameLabel.Text = "Не выбран";
            ActiveHostLabel.Text = "-";
            ActiveUserLabel.Text = "-";
            ActivePortLabel.Text = "-";
            ActiveModelLabel.Text = "-";
            ConnectButton.Text = "Подключиться";
            return;
        }

        ActivePresetNameLabel.Text = _activeHost.settingsName;
        ActiveHostLabel.Text = _activeHost.host;
        ActiveUserLabel.Text = _activeHost.user;
        ActivePortLabel.Text = _activeHost.port.ToString();
        ActiveModelLabel.Text = _activeHost.deviceName;

        // Проверяем, активно ли подключение
        ConnectButton.Text = (!string.IsNullOrWhiteSpace(_activeHost.settingsName) ? "Отключиться" : "Подключиться");
    }

    private async void OnSelectPresetClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var preset = (button?.BindingContext as Host?) ?? default(Host?);

        if (preset.HasValue)
        {
            try
            {
                // Устанавливаем как активное подключение
                await _connection.SetActiveConnection(preset.Value);

                // Обновляем отображение
                UpdateActiveConnectionDisplay();

                await DisplayAlert("Успех",
                    $"Подключение '{preset.Value.settingsName}' выбрано как активное",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка",
                    $"Не удалось установить подключение: {ex.Message}",
                    "OK");
            }
        }
    }

    private void OnEditPresetClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var preset = (button?.BindingContext as Host?) ?? default(Host?);

        if (preset.HasValue)
        {
            // Переходим в режим редактирования
            _isEditing = true;
            _editingPresetName = preset.Value.settingsName;
            _editingPreset = preset.Value;

            // Заполняем форму данными для редактирования
            EditTitleLabel.Text = "Редактирование подключения";
            PresetNameEntry.Text = preset.Value.settingsName;
            HostEntry.Text = preset.Value.host;
            UserEntry.Text = preset.Value.user;
            PortEntry.Text = preset.Value.port.ToString();
            PasswordEntry.Text = preset.Value.password;
            ModelPicker.SelectedItem = preset.Value.deviceName;

            // Переключаемся на вкладку редактирования
            ShowZone("edit");
        }
    }

    private async void OnDeletePresetClicked(object sender, EventArgs e)
    {
        // Получаем кнопку и ее контекст данных
        var button = sender as Button;

        if (button?.BindingContext is Host selectedPreset)
        {
            // Подтверждение удаления
            bool confirm = await DisplayAlert("Подтверждение",
                $"Удалить подключение '{selectedPreset.settingsName}'?",
                "Да", "Нет");

            if (confirm)
            {
                try
                {
                    // Удаляем из файла
                    _connection.DeleteConnection(selectedPreset.settingsName);

                    // Удаляем из коллекции в UI потоке
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        Presets.Remove(selectedPreset);
                    });

                    await DisplayAlert("Успех", "Подключение удалено", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Ошибка", $"Не удалось удалить: {ex.Message}", "OK");
                }
            }
        }
    }
    private async void OnSavePreset(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("=== OnSavePreset START ===");
            Debug.WriteLine($"Режим редактирования: {_isEditing}");
            Debug.WriteLine($"Старое имя: {_editingPresetName}");



            string presetName = PresetNameEntry.Text?.Trim() ?? "";
            string host = HostEntry.Text?.Trim() ?? "";
            string user = UserEntry.Text?.Trim() ?? "";
            string portText = PortEntry.Text ?? "";
            string password = PasswordEntry.Text ?? "";
            string deviceName = ModelPicker.SelectedItem?.ToString() ?? "Raspberry Pi 4";

            Debug.WriteLine($"Новое имя: {presetName}");

            if (string.IsNullOrWhiteSpace(presetName) ||
                string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(portText) ||
                string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Ошибка", "Заполните все поля", "OK");
                return;
            }

            if (!int.TryParse(portText, out int port) || port <= 0)
            {
                await DisplayAlert("Ошибка", "Некорректный порт", "OK");
                return;
            }

            var newPreset = new Host
            {
                settingsName = presetName,
                host = host,
                user = user,
                port = port,
                password = password,
                deviceName = deviceName
            };

            Debug.WriteLine($"Создан newPreset: {newPreset.GetHashCode()}");

            if (!_isEditing)
            {
                Debug.WriteLine("Проверка дубликатов для нового пресета...");
                bool isDuplicate = Presets.Any(p => p.settingsName == newPreset.settingsName);
                if (isDuplicate)
                {
                    Debug.WriteLine("Найден дубликат!");
                    await DisplayAlert("Ошибка", "Подключение с таким именем уже существует", "OK");
                    return;
                }
            }

            if (_isEditing && !string.IsNullOrEmpty(_editingPresetName))
            {
                Debug.WriteLine("Найден дубликат!");
                _connection.DeleteConnection(_editingPresetName);
            }

            await _connection.SaveConnection(newPreset);

            await Dispatcher.DispatchAsync(async () =>
            {
                try
                {
                    Debug.WriteLine("=== НАЧАЛО ОБНОВЛЕНИЯ UI ===");
                    Debug.WriteLine($"Текущее количество пресетов: {Presets.Count}");

                    for (int i = 0; i < Presets.Count; i++)
                    {
                        Debug.WriteLine($"[{i}] {Presets[i].settingsName} (HashCode: {Presets[i].GetHashCode()})");
                    }

                    if (_isEditing && !string.IsNullOrEmpty(_editingPresetName))
                    {
                        Debug.WriteLine($"Поиск пресетов для удаления: {_editingPresetName}");

                        // Ищем все пресеты с таким именем
                        var itemsToRemove = new List<Host>();
                        for (int i = 0; i < Presets.Count; i++)
                        {
                            var item = Presets[i];
                            Debug.WriteLine($"Проверяю [{i}]: {item.settingsName} == {_editingPresetName} ? {item.settingsName == _editingPresetName}");
                            if (item.settingsName == _editingPresetName)
                            {
                                itemsToRemove.Add(item);
                                Debug.WriteLine($"Добавил в список на удаление: {item.settingsName}");
                            }
                        }

                        Debug.WriteLine($"Найдено для удаления: {itemsToRemove.Count} элементов");

                        // Удаляем
                        foreach (var item in itemsToRemove)
                        {
                            Debug.WriteLine($"Удаляю: {item.settingsName}");
                            bool removed = Presets.Remove(item);
                            Debug.WriteLine($"Результат удаления: {removed}");
                        }

                        Debug.WriteLine($"После удаления осталось: {Presets.Count} пресетов");
                    }

                    // Добавляем новый
                    Debug.WriteLine($"Добавляю новый пресет: {newPreset.settingsName} (HashCode: {newPreset.GetHashCode()})");
                    Presets.Add(newPreset);
                    Debug.WriteLine($"После добавления: {Presets.Count} пресетов");


                    // Обновляем активный пресет
                    if (_activeHost.settingsName == _editingPresetName)
                    {
                        Debug.WriteLine($"Обновляю активный пресет с {_editingPresetName} на {newPreset.settingsName}");
                        _activeHost = newPreset;
                        UpdateActiveConnectionDisplay();
                    }

                    // Выводим итоговый список
                    Debug.WriteLine("=== ИТОГОВЫЙ СПИСОК ===");
                    for (int i = 0; i < Presets.Count; i++)
                    {
                        Debug.WriteLine($"[{i}] {Presets[i].settingsName} (HashCode: {Presets[i].GetHashCode()})");
                    }

                    Debug.WriteLine("=== КОНЕЦ ОБНОВЛЕНИЯ UI ===");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ОШИБКА В UI: {ex.Message}");
                    Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                }
            });

            // 8. Очищаем форму и возвращаемся к списку
            ClearForm();

            await DisplayAlert("Успех",
                _isEditing ? "Изменения сохранены" : "Подключение создано",
                "OK");

            Debug.WriteLine("=== OnSavePreset SUCCESS ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"=== OnSavePreset ERROR ===");
            Debug.WriteLine($"Type: {ex.GetType().Name}");
            Debug.WriteLine($"Message: {ex.Message}");
            Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            Device.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Ошибка", ex.Message, "OK");
            });
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
    private void ClearForm()
    {
        _isEditing = false;
        _editingPresetName = "";
        _editingPreset = null;

        EditTitleLabel.Text = "Создание нового подключения";
        PresetNameEntry.Text = "";
        HostEntry.Text = "";
        UserEntry.Text = "";
        PortEntry.Text = "22";
        PasswordEntry.Text = "";
        ModelPicker.SelectedIndex = 0;

    }

    private void OnCancel(object sender, EventArgs e)
    {
        ClearForm();
        ShowZone("connections");
    }

    private async void OnTestConnection(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeHost.settingsName))
        {
            await DisplayAlert("Ошибка", "Сначала выберите активное подключение", "OK");
            return;
        }

        try
        {
            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Text = "Проверяем...";

            // Создаем временное подключение для теста
            var testConnection = new Connection();

            // Устанавливаем тестируемый хост
            // Нужно добавить метод SetHost в Connection или использовать временный
            bool result  = await testConnection.ConnectToDevice();

            if (result)
            {
                await DisplayAlert("Успех", "Подключение установлено успешно!", "OK");
            }
            else
            {
                await DisplayAlert("Ошибка", "Не удалось установить подключение", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка",
                $"Ошибка при подключении: {ex.Message}",
                "OK");
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
            TestConnectionButton.Text = "Проверить подключение";
        }
    }

    private async void OnConnect(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeHost.settingsName))
        {
            await DisplayAlert("Ошибка", "Сначала выберите активное подключение", "OK");
            return;
        }

        try
        {
            ConnectButton.IsEnabled = false;

            // Проверяем состояние подключения
            bool isConnected = _connection.IsConnected; // Нужно добавить это свойство в класс Connection

            if (isConnected)
            {
                // Отключаемся
                ConnectButton.Text = "Отключение...";
                _connection.Disconnect();

                ConnectButton.Text = "Подключиться";

            }
            else
            {
                ConnectButton.Text = "Подключаемся...";

                // Подключаемся
                bool success = await _connection.ConnectToDevice();

                if (success)
                {
                    ConnectButton.Text = "Отключиться";
                    await DisplayAlert("Успех", "Подключение установлено", "OK");

                }
                else
                {
                    ConnectButton.Text = "Подключиться";
                    await DisplayAlert("Ошибка", "Не удалось установить подключение", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка",
                $"Ошибка подключения: {ex.Message}",
                "OK");
            ConnectButton.Text = "Подключиться";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();    
        try
    {
        LoadPresets();
        UpdateActiveConnectionDisplay();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"OnAppearing error: {ex.Message}");
    }

        // Обновляем список при каждом появлении страницы
    }
}