using AironControl.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace AironControl.ViewModels
{
    public class LaunchPageViewModel : INotifyPropertyChanged
    {
        private readonly ConnectionVM _connectionVm;
        private readonly IRos2Service _ros2;

        // Делегаты диалогов — устанавливаются из View
        public Func<string, string, string, string, Task<bool>> ConfirmAsync { get; set; } = (_, _, _, _) => Task.FromResult(false);
        public Func<string, string, string, Task> AlertAsync { get; set; } = (_, _, _) => Task.CompletedTask;

        private bool IsConnected => _connectionVm.IsConnected;

        private List<string> _availableNodes = new();
        private List<string> _availableTopics = new();

        #region Цвета вкладок
        private Color _tabLaunchColor = Colors.White;
        private Color _tabNodesColor = Color.FromRgb(245, 245, 245);
        private Color _tabSystemColor = Color.FromRgb(245, 245, 245);

        public Color TabLaunchColor { get => _tabLaunchColor; set => SetField(ref _tabLaunchColor, value); }
        public Color TabNodesColor { get => _tabNodesColor; set => SetField(ref _tabNodesColor, value); }
        public Color TabSystemColor { get => _tabSystemColor; set => SetField(ref _tabSystemColor, value); }
        #endregion

        #region Видимость зон
        private bool _isLaunchZoneVisible = true;
        private bool _isNodesZoneVisible;
        private bool _isSystemZoneVisible;

        public bool IsLaunchZoneVisible { get => _isLaunchZoneVisible; set => SetField(ref _isLaunchZoneVisible, value); }
        public bool IsNodesZoneVisible { get => _isNodesZoneVisible; set => SetField(ref _isNodesZoneVisible, value); }
        public bool IsSystemZoneVisible { get => _isSystemZoneVisible; set => SetField(ref _isSystemZoneVisible, value); }
        #endregion

        #region Пакеты и Launch файлы
        public ObservableCollection<string> Packages { get; } = new();

        private string _selectedPackage;
        public string SelectedPackage
        {
            get => _selectedPackage;
            set
            {
                if (SetField(ref _selectedPackage, value) && value != null && !IsPlaceholder(value))
                    _ = UpdateLaunchFilesAsync();
            }
        }

        public ObservableCollection<string> LaunchFiles { get; } = new();

        private string _selectedLaunchFile;
        public string SelectedLaunchFile { get => _selectedLaunchFile; set => SetField(ref _selectedLaunchFile, value); }
        #endregion

        #region Ноды / Топики
        public ObservableCollection<string> NodeTopicItems { get; } = new();

        private string _selectedNodeTopicItem;
        public string SelectedNodeTopicItem
        {
            get => _selectedNodeTopicItem;
            set
            {
                if (SetField(ref _selectedNodeTopicItem, value) && value != null)
                    _ = UpdateViewPickerAsync();
            }
        }

        public ObservableCollection<string> ViewItems { get; } = new();

        private string _selectedViewItem;
        public string SelectedViewItem { get => _selectedViewItem; set => SetField(ref _selectedViewItem, value); }

        private string _nodesLog;
        public string NodesLog { get => _nodesLog; set => SetField(ref _nodesLog, value); }
        #endregion

        #region Системная информация
        private string _systemInfo;
        public string SystemInfo { get => _systemInfo; set => SetField(ref _systemInfo, value); }
        #endregion

        #region Состояние кнопок
        private bool _isBtnRunEnabled = true;
        private bool _isBtnRebootEnabled = true;
        private bool _isBtnViewEnabled = true;
        private bool _isBtnDeleteEnabled = true;

        public bool IsBtnRunEnabled { get => _isBtnRunEnabled; set => SetField(ref _isBtnRunEnabled, value); }
        public bool IsBtnRebootEnabled { get => _isBtnRebootEnabled; set => SetField(ref _isBtnRebootEnabled, value); }
        public bool IsBtnViewEnabled { get => _isBtnViewEnabled; set => SetField(ref _isBtnViewEnabled, value); }
        public bool IsBtnDeleteEnabled { get => _isBtnDeleteEnabled; set => SetField(ref _isBtnDeleteEnabled, value); }
        #endregion

        #region Команды
        public ICommand ShowLaunchTabCommand { get; }
        public ICommand ShowNodesTabCommand { get; }
        public ICommand ShowSystemTabCommand { get; }
        public ICommand RunLaunchCommand { get; }
        public ICommand RebootRobotCommand { get; }
        public ICommand ViewCommand { get; }
        public ICommand DeleteNodesCommand { get; }
        #endregion

        public LaunchPageViewModel(ConnectionVM connectionVm, IRos2Service ros2Service)
        {
            _connectionVm = connectionVm;
            _ros2 = ros2Service;

            ShowLaunchTabCommand = new Command(async () => await ShowZoneAsync("launch"));
            ShowNodesTabCommand = new Command(async () => await ShowZoneAsync("nodes"));
            ShowSystemTabCommand = new Command(async () => await ShowZoneAsync("system"));
            RunLaunchCommand = new Command(async () => await OnRunLaunchAsync());
            RebootRobotCommand = new Command(async () => await OnRebootRobotAsync());
            ViewCommand = new Command(async () => await OnViewClickedAsync());
            DeleteNodesCommand = new Command(async () => await OnDeleteNodesAsync());

            InitNodeTopicItems();
        }

        private void InitNodeTopicItems()
        {
            NodeTopicItems.Add("Список узлов");
            NodeTopicItems.Add("Список Топики");
            NodeTopicItems.Add("Просмотр Топика (информация)");
            NodeTopicItems.Add("Просмотр Топика (данные)");
            NodeTopicItems.Add("Просмотр Узла (информация)");
            NodeTopicItems.Add("Остановить узел");
            NodeTopicItems.Add("Проверка работоспособности");
            NodeTopicItems.Add("Просмотр логов узла");
            _selectedNodeTopicItem = NodeTopicItems[0];
        }

        public async Task InitializeAsync()
        {
            NodesLog = "Выберите параметры для работы с нодами и топиками";
            SystemInfo = "Загрузка информации о системе...";

            await _connectionVm.ConnectAsync();
            _connectionVm.RefreshStatus();

            await ShowZoneAsync("launch");

            if (IsConnected)
            {
                await InitPackagesAsync();
                await UpdateSystemInfoAsync();
            }
            else
            {
                SystemInfo = "ОШИБКА: Не удалось подключиться к устройству. Проверьте соединение и попробуйте снова.";
            }
        }

        private async Task ShowZoneAsync(string zone)
        {
            IsLaunchZoneVisible = false;
            IsNodesZoneVisible = false;
            IsSystemZoneVisible = false;

            var gray = Color.FromRgb(245, 245, 245);
            TabLaunchColor = gray;
            TabNodesColor = gray;
            TabSystemColor = gray;

            switch (zone)
            {
                case "launch":
                    IsLaunchZoneVisible = true;
                    TabLaunchColor = Colors.White;
                    await UpdateLaunchFilesAsync();
                    break;
                case "nodes":
                    IsNodesZoneVisible = true;
                    TabNodesColor = Colors.White;
                    await LoadNodesAndTopicsAsync();
                    break;
                case "system":
                    IsSystemZoneVisible = true;
                    TabSystemColor = Colors.White;
                    await UpdateSystemInfoAsync();
                    break;
            }
        }

        private async Task InitPackagesAsync()
        {
            if (!IsConnected) return;

            Packages.Clear();
            Packages.Add("Загрузка пакетов...");
            SelectedPackage = null;

            var list = await _ros2.GetPackagesAsync();
            Packages.Clear();

            if (list == null || list.Count == 0)
            {
                Packages.Add("Пакеты не загружены");
                return;
            }

            if (list.Count == 1 && list[0].StartsWith("Error:"))
            {
                Packages.Add($"Ошибка: {list[0]}");
                return;
            }

            foreach (var p in list)
                if (!string.IsNullOrWhiteSpace(p) && !p.StartsWith("Error:"))
                    Packages.Add(p);

            if (Packages.Count > 0)
            {
                SelectedPackage = Packages[0];
                await UpdateLaunchFilesAsync();
            }
        }

        private async Task UpdateLaunchFilesAsync()
        {
            if (SelectedPackage == null || !IsConnected || IsPlaceholder(SelectedPackage)) return;

            LaunchFiles.Clear();
            LaunchFiles.Add("Загрузка launch файлов...");
            SelectedLaunchFile = null;

            var list = await _ros2.GetLaunchFilesAsync(SelectedPackage);
            LaunchFiles.Clear();

            if (list == null || list.Count == 0 ||
                (list.Count == 1 && (list[0].Contains("Error") || list[0] == "No launch files found")))
            {
                LaunchFiles.Add("Launch файлы отсутствуют");
            }
            else
            {
                foreach (var l in list)
                    if (!l.StartsWith("Error:"))
                        LaunchFiles.Add(l);

                if (LaunchFiles.Count == 0)
                    LaunchFiles.Add("Launch файлы отсутствуют");
            }
        }

        private async Task UpdateSystemInfoAsync()
        {
            if (!IsConnected)
            {
                SystemInfo = "НЕТ ПОДКЛЮЧЕНИЯ К УСТРОЙСТВУ\n\n" +
                             "Проверьте:\n1. Сетевое подключение\n2. IP адрес устройства\n" +
                             "3. Доступность SSH\n4. Правильность учетных данных";
                return;
            }

            SystemInfo = "ПОЛУЧЕНИЕ ИНФОРМАЦИИ О СИСТЕМЕ...\n\nПожалуйста, подождите";

            try
            {
                var info = await _ros2.GetSystemInfoAsync();
                SystemInfo = string.IsNullOrWhiteSpace(info)
                    ? "ИНФОРМАЦИЯ НЕ ПОЛУЧЕНА\n\nПопробуйте обновить страницу"
                    : info;
            }
            catch (Exception ex)
            {
                SystemInfo = $"ОШИБКА ПОЛУЧЕНИЯ ИНФОРМАЦИИ:\n\n{ex.Message}\n\nПодробности в логах приложения";
            }
        }

        private async Task LoadNodesAndTopicsAsync()
        {
            if (!IsConnected)
            {
                NodesLog = "НЕТ ПОДКЛЮЧЕНИЯ К УСТРОЙСТВУ\n\nДля работы с нодами и топиками необходимо подключение";
                return;
            }

            try
            {
                NodesLog = "ЗАГРУЗКА ДАННЫХ...\n\nПолучаю информацию о нодах и топиках\nПожалуйста, подождите";

                _availableNodes = await _ros2.GetNodesAsync();
                _availableTopics = await _ros2.GetTopicsAsync();

                NodesLog = $"ДАННЫЕ ЗАГРУЖЕНЫ УСПЕШНО\n\n" +
                           $"• НОДЫ: {_availableNodes.Count} штук\n" +
                           $"• ТОПИКИ: {_availableTopics.Count} штук\n\n" +
                           $"Выберите в верхних пикерах:\n1. Что просматривать (Ноды/Топики)\n" +
                           $"2. Тип просмотра (list/info/echo)\n3. Нажмите кнопку 'Посмотреть'";
            }
            catch (Exception ex)
            {
                NodesLog = $"ОШИБКА ЗАГРУЗКИ ДАННЫХ:\n\n{ex.Message}\n\n" +
                           "Попробуйте:\n1. Проверить подключение\n2. Перезагрузить страницу\n3. Убедиться, что ROS запущен";
            }
        }

        private async Task UpdateViewPickerAsync()
        {
            if (SelectedNodeTopicItem == null || !IsConnected) return;

            ViewItems.Clear();

            switch (SelectedNodeTopicItem)
            {
                case "Список узлов":
                case "Список Топики":
                case "Очистить все топики":
                case "Проверка работоспособности":
                    ViewItems.Add("Выполнить команду");
                    break;

                case "Просмотр Топика (информация)":
                case "Просмотр Топика (данные)":
                    ViewItems.Add("ТОПИКИ");
                    foreach (var t in _availableTopics) ViewItems.Add(t);
                    break;

                case "Просмотр Узла (информация)":
                case "Остановить узел":
                case "Перезапустить узел":
                case "Просмотр логов узла":
                    ViewItems.Add("УЗЛЫ");
                    foreach (var n in _availableNodes) ViewItems.Add(n);
                    break;
            }

            if (ViewItems.Count > 0)
                SelectedViewItem = ViewItems[0];
        }

        private async Task OnRunLaunchAsync()
        {
            if (!IsConnected)
            {
                await AlertAsync("Ошибка подключения", "Нет подключения к устройству", "OK");
                return;
            }

            if (SelectedPackage == null || SelectedLaunchFile == null || SelectedLaunchFile == "Launch файлы отсутствуют")
            {
                await AlertAsync("Ошибка выбора", "Выберите пакет и launch файл", "OK");
                return;
            }

            try
            {
                IsBtnRunEnabled = false;
                var result = await _ros2.RunLaunchFileAsync(SelectedPackage, SelectedLaunchFile);

                if (result.StartsWith("Success"))
                {
                    await AlertAsync("Успешный запуск",
                        $"Launch файл '{SelectedLaunchFile}' успешно запущен в пакете '{SelectedPackage}'", "OK");
                    await LoadNodesAndTopicsAsync();
                }
                else
                {
                    await AlertAsync("Ошибка запуска", result, "OK");
                }
            }
            catch (Exception ex)
            {
                await AlertAsync("Критическая ошибка", $"Не удалось запустить launch файл:\n{ex.Message}", "OK");
            }
            finally
            {
                IsBtnRunEnabled = true;
            }
        }

        private async Task OnRebootRobotAsync()
        {
            if (!IsConnected)
            {
                await AlertAsync("Ошибка подключения", "Нет подключения к устройству", "OK");
                return;
            }

            bool answer = await ConfirmAsync(
                "ПРЕДУПРЕЖДЕНИЕ: ПЕРЕЗАГРУЗКА РОБОТА",
                "Вы уверены, что хотите перезагрузить робота?\n\nВНИМАНИЕ:\n" +
                "• Это может занять несколько минут\n• Все текущие процессы будут остановлены\n• Сетевое соединение прервется",
                "Да, перезагрузить", "Отмена");

            if (!answer) return;

            try
            {
                IsBtnRebootEnabled = false;
                var result = await _ros2.RebootRobotAsync();

                if (result.StartsWith("Success"))
                {
                    await AlertAsync("Перезагрузка начата",
                        "Робот перезагружается...\n\nПожалуйста, подождите 2-3 минуты\nСоединение будет восстановлено автоматически", "OK");

                    SystemInfo = "РОБОТ ПЕРЕЗАГРУЖАЕТСЯ...\n\nПожалуйста, подождите\nПримерное время: 2-3 минуты\n\n" +
                                 "После перезагрузки:\n1. Подключение восстановится\n2. Данные обновятся\n3. Система будет готова к работе";

                    await Task.Delay(180000);
                    await _connectionVm.ConnectAsync();

                    if (IsConnected)
                    {
                        await AlertAsync("Перезагрузка завершена", "Робот успешно перезагружен!\n\nСоединение восстановлено", "OK");
                        await InitPackagesAsync();
                        await UpdateSystemInfoAsync();
                    }
                    else
                    {
                        await AlertAsync("Внимание",
                            "Не удалось автоматически переподключиться\n\nПожалуйста:\n" +
                            "1. Убедитесь, что робот включен\n2. Проверьте сетевое подключение\n3. Попробуйте перезапустить приложение", "OK");
                    }
                }
                else
                {
                    await AlertAsync("Ошибка перезагрузки", result, "OK");
                }
            }
            catch (Exception ex)
            {
                await AlertAsync("Критическая ошибка", $"Не удалось перезагрузить робота:\n{ex.Message}", "OK");
            }
            finally
            {
                IsBtnRebootEnabled = true;
            }
        }

        private async Task OnViewClickedAsync()
        {
            if (!IsConnected)
            {
                NodesLog = "НЕТ ПОДКЛЮЧЕНИЯ К УСТРОЙСТВУ\n\nПодключитесь к устройству для выполнения команд ROS 2";
                return;
            }

            if (SelectedNodeTopicItem == null)
            {
                NodesLog = "ВЫБЕРИТЕ КОМАНДУ\n\nВыберите команду ROS 2 из первого пикера";
                return;
            }

            string selectedObject = SelectedViewItem ?? "";

            try
            {
                IsBtnViewEnabled = false;
                NodesLog = $"ВЫПОЛНЕНИЕ КОМАНДЫ ROS 2...\n\nКоманда: {SelectedNodeTopicItem}\nОбъект: {selectedObject}\n\nПожалуйста, подождите";

                string result = SelectedNodeTopicItem switch
                {
                    "Список узлов" => await ProcessNodesListAsync(),
                    "Список Топики" => await ProcessTopicsListAsync(),
                    "Просмотр Топика (информация)" when IsHeaderItem(selectedObject) =>
                        "ВЫБЕРИТЕ КОНКРЕТНЫЙ ТОПИК\n\nВыберите топик из списка для получения информации",
                    "Просмотр Топика (информация)" => await ProcessTopicInfoAsync(selectedObject),
                    "Просмотр Топика (данные)" when IsHeaderItem(selectedObject) =>
                        "ВЫБЕРИТЕ КОНКРЕТНЫЙ ТОПИК\n\nВыберите топик из списка для просмотра данных",
                    "Просмотр Топика (данные)" => await ProcessTopicEchoAsync(selectedObject),
                    "Остановить узел" when IsHeaderItem(selectedObject) =>
                        "ВЫБЕРИТЕ КОНКРЕТНЫЙ УЗЕЛ\n\nВыберите узел из списка для остановки",
                    "Остановить узел" => await ProcessNodeStopAsync(selectedObject),
                    "Просмотр Узла (информация)" when IsHeaderItem(selectedObject) =>
                        "ВЫБЕРИТЕ КОНКРЕТНЫЙ УЗЕЛ\n\nВыберите узел для мониторинга параметров",
                    "Просмотр Узла (информация)" => await ProcessNodeInfoAsync(selectedObject.Replace(" (параметры)", "")),
                    "Проверка работоспособности" => await ProcessDoctorAsync(),
                    "Просмотр логов узла" when IsHeaderItem(selectedObject) =>
                        "ВЫБЕРИТЕ КОНКРЕТНЫЙ УЗЕЛ\n\nВыберите узел для просмотра логов",
                    "Просмотр логов узла" => await ProcessNodeLogsAsync(selectedObject),
                    _ => ""
                };

                NodesLog = result;
            }
            catch (Exception ex)
            {
                NodesLog = $"ОШИБКА ВЫПОЛНЕНИЯ КОМАНДЫ ROS 2:\n\n{ex.Message}\n\n" +
                           "Попробуйте:\n1. Проверить соединение\n2. Убедиться, что ROS 2 демон запущен\n" +
                           "3. Проверить правильность Domain ID\n4. Повторить запрос";
            }
            finally
            {
                IsBtnViewEnabled = true;
            }
        }

        private async Task OnDeleteNodesAsync()
        {
            if (!IsConnected)
            {
                await AlertAsync("Ошибка подключения", "Нет подключения к устройству", "OK");
                return;
            }

            bool answer = await ConfirmAsync(
                "ПОДТВЕРЖДЕНИЕ УДАЛЕНИЯ",
                "Вы уверены, что хотите удалить ВСЕ ноды?\n\nВНИМАНИЕ:\n" +
                "• Все запущенные ноды будут остановлены\n• Топики прекратят публикацию\n" +
                "• Запущенные процессы будут прерваны\n• ВСЕ screen сессии будут закрыты\n• Launch файлы будут остановлены",
                "Да, удалить все", "Отмена");

            if (!answer) return;

            try
            {
                IsBtnDeleteEnabled = false;
                NodesLog = "УДАЛЕНИЕ ВСЕХ НОД И ПРОЦЕССОВ...\n\nПожалуйста, подождите\nЭто может занять несколько секунд";

                string deleteResult = await _ros2.DeleteAllNodesAsync();

                bool success = !deleteResult.StartsWith("Ошибка:") &&
                               !deleteResult.StartsWith("Исключение:") &&
                               !deleteResult.StartsWith("ERROR:") &&
                               !string.IsNullOrWhiteSpace(deleteResult);

                if (success)
                {
                    NodesLog = $" ВСЕ НОДЫ И ПРОЦЕССЫ УДАЛЕНЫ УСПЕШНО\n\nРезультаты:\n{deleteResult}\n\n" +
                               " Все запущенные ноды остановлены\n Топики прекратили публикацию\n" +
                               " Screen сессии закрыты\n Launch файлы остановлены\n Система полностью очищена\n\n" +
                               "Для запуска новых нод используйте вкладку 'Запуск'";

                    _availableNodes.Clear();
                    _availableTopics.Clear();

                    await AlertAsync("Успех", "Все ноды и процессы успешно удалены", "OK");
                }
                else
                {
                    NodesLog = $"⚠ОШИБКА ПРИ УДАЛЕНИИ:\n\n{deleteResult}\n\n" +
                               "Рекомендуется:\n1. Проверить командой: ros2 node list\n" +
                               "2. Проверить командой: screen -list\n3. При необходимости перезагрузить систему";
                    await AlertAsync("Внимание", "Возникла ошибка при удалении.\nПроверьте лог для деталей.", "OK");
                }
            }
            catch (Exception ex)
            {
                NodesLog = $"КРИТИЧЕСКАЯ ОШИБКА ПРИ УДАЛЕНИИ:\n\n{ex.Message}\n\n" +
                           "Рекомендуется:\n1. Перезагрузить приложение\n2. Проверить SSH соединение\n3. Перезагрузить устройство";
                await AlertAsync("Критическая ошибка", $"Ошибка при удалении нод: {ex.Message}", "OK");
            }
            finally
            {
                IsBtnDeleteEnabled = true;
                await Task.Delay(2000);
                if (IsConnected)
                    await LoadNodesAndTopicsAsync();
            }
        }

        private async Task<string> ProcessNodesListAsync()
        {
            _availableNodes = await _ros2.GetNodesAsync();
            if (_availableNodes == null || _availableNodes.Count == 0)
                return "СПИСОК УЗЛОВ ПУСТ\n\nУзлы ROS 2 не обнаружены\nЗапустите launch файлы для создания узлов";

            return $"СПИСОК АКТИВНЫХ УЗЛОВ ROS 2 ({_availableNodes.Count}):\n\n" +
                   string.Join("\n", _availableNodes.Select((n, i) => $"{i + 1}. {n}")) +
                   $"\n\nИТОГО: {_availableNodes.Count} узл(ов)";
        }

        private Task<string> ProcessTopicsListAsync()
        {
            if (_availableTopics == null || _availableTopics.Count == 0)
                return Task.FromResult("СПИСОК ТОПИКОВ ПУСТ\n\nНоды не публикуют топики");

            return Task.FromResult(
                $"СПИСОК АКТИВНЫХ ТОПИКОВ ({_availableTopics.Count}):\n\n" +
                string.Join("\n", _availableTopics.Select((t, i) => $"{i + 1}. {t}")) +
                $"\n\nИТОГО: {_availableTopics.Count} топик(ов)");
        }

        private async Task<string> ProcessTopicInfoAsync(string topicName)
        {
            try
            {
                var info = await _ros2.GetTopicInfoAsync(topicName);
                return $"ИНФОРМАЦИЯ О ТОПИКЕ: {topicName}\n\n{info}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПОЛУЧЕНИЯ ИНФОРМАЦИИ О ТОПИКЕ {topicName}:\n\n{ex.Message}";
            }
        }

        private async Task<string> ProcessTopicEchoAsync(string topicName)
        {
            try
            {
                var data = await _ros2.EchoTopicAsync(topicName, 5);
                return $"ДАННЫЕ ТОПИКА ROS 2: {topicName}\n\nПоследние 5 сообщений:\n\n{data}\n\nДля продолжения просмотра нажмите 'Посмотреть' снова";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПРОСМОТРА ТОПИКА {topicName}:\n\n{ex.Message}";
            }
        }

        private async Task<string> ProcessNodeInfoAsync(string nodeName)
        {
            try
            {
                var info = await _ros2.GetNodeInfoAsync(nodeName);
                return $"ИНФОРМАЦИЯ О УЗЛЕ: {nodeName}\n\n{info}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПОЛУЧЕНИЯ ИНФОРМАЦИИ О УЗЛЕ {nodeName}:\n\n{ex.Message}";
            }
        }

        private async Task<string> ProcessNodeStopAsync(string nodeName)
        {
            try
            {
                bool confirm = await ConfirmAsync(
                    "ПОДТВЕРЖДЕНИЕ ОСТАНОВКИ",
                    $"Вы уверены, что хотите остановить узел:\n{nodeName}?\n\nУзел будет переведен в состояние shutdown",
                    "Да, остановить", "Отмена");

                if (!confirm) return "ОСТАНОВКА УЗЛА ОТМЕНЕНА";

                var result = await _ros2.StopNodeAsync(nodeName);

                if (result.StartsWith("Success"))
                {
                    _availableNodes = await _ros2.GetNodesAsync();
                    await UpdateViewPickerAsync();
                    return $"Проверьте удаление узла: {nodeName}\n\nСписок активных узлов обновлен";
                }

                return "Проверьте список узлов";
            }
            catch (Exception ex)
            {
                return $"Проверьте список узлов {nodeName}:\n\n{ex.Message}";
            }
        }

        private async Task<string> ProcessNodeLogsAsync(string nodeName)
        {
            try
            {
                var logs = await _ros2.GetNodeLogsAsync(nodeName, 5);
                return $"ЛОГИ УЗЛА: {nodeName}\n\nПоследние 5 сообщений:\n\n{logs}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПОЛУЧЕНИЯ ЛОГОВ УЗЛА {nodeName}:\n\n{ex.Message}";
            }
        }

        private async Task<string> ProcessDoctorAsync()
        {
            try
            {
                var result = await _ros2.CheckHealthAsync();
                return $"ПРОВЕРКА РАБОТОСПОСОБНОСТИ ROS 2:\n\n{result}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПРОВЕРКИ РАБОТОСПОСОБНОСТИ:\n\n{ex.Message}";
            }
        }

        private static bool IsPlaceholder(string value) =>
            value.Contains("Ошибка") || value.Contains("не загружены") ||
            value.Contains("подключения") || value.Contains("Загрузка");

        private static bool IsHeaderItem(string value) =>
            value == "ТОПИКИ" || value == "УЗЛЫ" || value.StartsWith("---");

        #region INotifyPropertyChanged
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
        #endregion
    }
}
