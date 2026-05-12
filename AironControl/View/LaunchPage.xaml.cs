using AironControl;
using Android.Content.PM;
using Microsoft.Maui.Controls;
using System.Reflection.PortableExecutable;

namespace AironControl
{
    public partial class LaunchPage : ContentPage
    {
        private readonly Connection _connection;
        private bool _isConnected = false;
        private List<string> _availableNodes = new List<string>();
        private List<string> _availableTopics = new List<string>();

        public LaunchPage()
        {
            InitializeComponent();
            _connection = new Connection();
            InitializeEventHandlers();
            InitializePickers();
        }

        private void InitializeEventHandlers()
        {
            // Обработчики вкладок
            TabLaunch.Clicked += async (_, __) => await ShowZone("launch");
            TabNodes.Clicked += async (_, __) => await ShowZone("nodes");
            TabSystem.Clicked += async (_, __) => await ShowZone("system");

            // Обработчики пикеров
            PackagePicker.SelectedIndexChanged += OnPackageSelected;
            NodeTopickPicker.SelectedIndexChanged += OnNodeTopicPickerSelected;

            // Обработчики кнопок
            BtnRunLaunch.Clicked += OnRunLaunch;
            BtnRebootRobot.Clicked += OnRebootRobot;
            BtnView.Clicked += OnViewClicked;
            BtnDeleteNodes.Clicked += OnDeleteNodesClicked;
        }

        private void InitializePickers()
        {
            // Инициализация пикера для выбора Нода/Топик
            NodeTopickPicker.Items.Clear();
            NodeTopickPicker.Items.Add("Список узлов");
            NodeTopickPicker.Items.Add("Список Топики");
            NodeTopickPicker.Items.Add("Просмотр Топика (информация)");
            NodeTopickPicker.Items.Add("Просмотр Топика (данные)");



            NodeTopickPicker.Items.Add("Просмотр Узла (информация)");
            NodeTopickPicker.Items.Add("Остановить узел");
            NodeTopickPicker.Items.Add("Проверка работоспособности");

            NodeTopickPicker.Items.Add("Просмотр логов узла");
            NodeTopickPicker.SelectedIndex = 0;

            // Инициализация пикера для выбора действия
            ViewPicker.Items.Clear();
            ViewPicker.Title = "Выберите объект для работы";
            ViewPicker.SelectedIndex = 0;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Инициализируем лейблы
            NodesLog.Text = "Выберите параметры для работы с нодами и топиками";
            InfoRobotLabel.Text = "Загрузка информации о системе...";

            // Подключаемся при открытии страницы
            _isConnected = await _connection.ConnectToDevice();

            await ShowZone("launch");
            if (_isConnected)
            {
                await InitPackages();
                await UpdateSystemInfo();

            }
            else
            {
                InfoRobotLabel.Text = "ОШИБКА: Не удалось подключиться к устройству. Проверьте соединение и попробуйте снова.";
            }
        }

        private async Task InitPackages()
        {
            if (!_isConnected) return;

            PackagePicker.Items.Clear();
            PackagePicker.Items.Add("Загрузка пакетов...");

            List<string> listPackages = await _connection.GetRosLocalWorkespace();
            PackagePicker.Items.Clear();

            if (listPackages == null || listPackages.Count == 0)
            {
                PackagePicker.Items.Add("Пакеты не загружены");
                return;
            }

            if (listPackages.Count == 1 && listPackages[0].StartsWith("Error:"))
            {
                PackagePicker.Items.Add($"Ошибка: {listPackages[0]}");
                return;
            }

            // Добавляем все пакеты
            foreach (var package in listPackages)
            {
                if (!string.IsNullOrWhiteSpace(package) && !package.StartsWith("Error:"))
                {
                    PackagePicker.Items.Add(package);
                }
            }

            // Выбираем первый пакет по умолчанию
            if (PackagePicker.Items.Count > 0 && PackagePicker.SelectedIndex == -1)
            {
                PackagePicker.SelectedIndex = 0;
                await UpdateLaunchFiles();
            }
        }

        private async Task ShowZone(string zone)
        {
            // Скрываем все зоны
            LaunchZone.IsVisible = false;
            NodesZone.IsVisible = false;
            SystemZone.IsVisible = false;

            // Сбрасываем цвета вкладок
            TabLaunch.BackgroundColor = Color.FromRgb(245, 245, 245);
            TabNodes.BackgroundColor = Color.FromRgb(245, 245, 245);
            TabSystem.BackgroundColor = Color.FromRgb(245, 245, 245);

            // Показываем выбранную зону
            switch (zone)
            {
                case "launch":
                    LaunchZone.IsVisible = true;
                    TabLaunch.BackgroundColor = Colors.White;
                    await UpdateLaunchFiles();
                    break;

                case "nodes":
                    NodesZone.IsVisible = true;
                    TabNodes.BackgroundColor = Colors.White;

                    // Загружаем доступные ноды и топики
                    await LoadNodesAndTopics();
                    break;

                case "system":
                    SystemZone.IsVisible = true;
                    TabSystem.BackgroundColor = Colors.White;
                    await UpdateSystemInfo();
                    break;
            }
        }

        private async void OnPackageSelected(object sender, EventArgs e)
        {
            if (PackagePicker.SelectedItem == null)
                return;

            string selectedPackage = PackagePicker.SelectedItem.ToString();

            if (selectedPackage.Contains("Ошибка") ||
                selectedPackage.Contains("не загружены") ||
                selectedPackage.Contains("подключения"))
                return;

            await UpdateLaunchFiles();
        }
        private async void OnNodeTopicPickerSelected(object sender, EventArgs e)
        {
            if (NodeTopickPicker.SelectedItem == null) return;

            string selectedCommand = NodeTopickPicker.SelectedItem.ToString();

            // Обновляем второй пикер в зависимости от выбранной команды
           await UpdateSecondPicker();

            // Показываем описание команды
            //  ShowCommandDescription(selectedCommand);
        }
        private async Task UpdateSecondPicker()
        {
            if (NodeTopickPicker.SelectedItem == null || !_isConnected)
                return;

            string selectedCommand = NodeTopickPicker.SelectedItem.ToString();
            ViewPicker.Items.Clear();

            switch (selectedCommand)
            {
                case "Список узлов":
                case "Список Топики":
                case "Очистить все топики":
                case "Проверка работоспособности":
                    ViewPicker.Items.Add("Выполнить команду");
                    ViewPicker.Title = "Нажмите 'Посмотреть' для выполнения";
                    break;

                case "Просмотр Топика (информация)":
                case "Просмотр Топика (данные)":
                    // Только топики
                    ViewPicker.Items.Add("ТОПИКИ");
                    foreach (var topic in _availableTopics)
                    {
                        ViewPicker.Items.Add(topic);
                    }
                    ViewPicker.Title = "Выберите топик";
                    break;

                case "Просмотр Узла (информация)":
                case "Остановить узел":
                case "Перезапустить узел":
                case "Просмотр логов узла":
                    // Только узлы
                    ViewPicker.Items.Add("УЗЛЫ");
                    foreach (var node in _availableNodes)
                    {
                        ViewPicker.Items.Add(node);
                    }
                    ViewPicker.Title = "Выберите узел";
                    break;
             
            }

            if (ViewPicker.Items.Count > 0 && ViewPicker.SelectedIndex == -1)
            {
                ViewPicker.SelectedIndex = 0;
            }
        }
     
        private async Task UpdateLaunchFiles()
        {
            if (PackagePicker.SelectedItem == null || !_isConnected)
                return;

            string selectedPackage = PackagePicker.SelectedItem.ToString();

            if (selectedPackage.Contains("Ошибка") ||
                selectedPackage.Contains("не загружены") ||
                selectedPackage.Contains("подключения"))
                return;

            LaunchPicker.Items.Clear();
            LaunchPicker.Items.Add("Загрузка launch файлов...");

            List<string> launchList = await _connection.GetRosLaunchName(selectedPackage);
            LaunchPicker.Items.Clear();

            if (launchList == null || launchList.Count == 0 ||
                (launchList.Count == 1 && (launchList[0].Contains("Error") ||
                                           launchList[0] == "No launch files found")))
            {
                LaunchPicker.Items.Add("Launch файлы отсутствуют");
            }
            else
            {
                foreach (var launch in launchList)
                {
                    if (!launch.StartsWith("Error:"))
                        LaunchPicker.Items.Add(launch);
                }

                if (LaunchPicker.Items.Count == 0)
                    LaunchPicker.Items.Add("Launch файлы отсутствуют");
            }
        }

        private async Task UpdateSystemInfo()
        {
            if (!_isConnected)
            {
                InfoRobotLabel.Text = "НЕТ ПОДКЛЮЧЕНИЯ К УСТРОЙСТВУ\n\n" +
                                    "Проверьте:\n" +
                                    "1. Сетевое подключение\n" +
                                    "2. IP адрес устройства\n" +
                                    "3. Доступность SSH\n" +
                                    "4. Правильность учетных данных";
                return;
            }

            InfoRobotLabel.Text = "ПОЛУЧЕНИЕ ИНФОРМАЦИИ О СИСТЕМЕ...\n\n" +
                                "Пожалуйста, подождите";

            try
            {
                string systemInfo = await _connection.GetSystemInfo();
                InfoRobotLabel.Text = systemInfo ?? "ИНФОРМАЦИЯ НЕ ПОЛУЧЕНА\n\nПопробуйте обновить страницу";
            }
            catch (Exception ex)
            {
                InfoRobotLabel.Text = $"ОШИБКА ПОЛУЧЕНИЯ ИНФОРМАЦИИ:\n\n{ex.Message}\n\n" +
                                    "Подробности в логах приложения";
            }
        }

        private async void OnRunLaunch(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                await DisplayAlert("Ошибка подключения",
                    "Нет подключения к устройству", "OK");
                return;
            }

            if (PackagePicker.SelectedItem == null || LaunchPicker.SelectedItem == null ||
                LaunchPicker.SelectedItem.ToString() == "Launch файлы отсутствуют")
            {
                await DisplayAlert("Ошибка выбора",
                    "Выберите пакет и launch файл", "OK");
                return;
            }

            string package = PackagePicker.SelectedItem.ToString();
            string launchFile = LaunchPicker.SelectedItem.ToString();

            try
            {
                BtnRunLaunch.IsEnabled = false;

                string result = await _connection.RunLaunchFile(package, launchFile);

                if (result.StartsWith("Success"))
                {
                    await DisplayAlert("Успешный запуск",
                        $"Launch файл '{launchFile}' успешно запущен в пакете '{package}'",
                        "OK");

                    // Обновляем список нод после запуска
                    await LoadNodesAndTopics();
                }
                else
                {
                    await DisplayAlert("Ошибка запуска", result, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Критическая ошибка",
                    $"Не удалось запустить launch файл:\n{ex.Message}",
                    "OK");
            }
            finally
            {
                BtnRunLaunch.IsEnabled = true;
            }
        }

        private async void OnReloadProgram(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                await DisplayAlert("Ошибка подключения",
                    "Нет подключения к устройству", "OK");
                return;
            }

            bool answer = await DisplayAlert("Подтверждение перезагрузки",
                "Вы уверены, что хотите перезагрузить программу?", "Да", "Нет");

            if (!answer) return;

            try
            {
                //ReloadProgramButton.IsEnabled = false;
                string result =  await _connection.ReloadProgram();

                if (result.StartsWith("Success"))
                {
                    await DisplayAlert("Перезагрузка завершена",
                        "Программа успешно перезагружена", "OK");

                    // Переподключаемся и обновляем данные
                    _isConnected = await _connection.ConnectToDevice();
                    await InitPackages();
                    await UpdateSystemInfo();
                }
                else
                {
                    await DisplayAlert("Ошибка перезагрузки", result, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Критическая ошибка",
                    $"Не удалось перезагрузить программу:\n{ex.Message}",
                    "OK");
            }
            finally
            {
                //ReloadProgramButton.IsEnabled = true;
            }
        }

        private async void OnRebootRobot(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                await DisplayAlert("Ошибка подключения",
                    "Нет подключения к устройству", "OK");
                return;
            }

            bool answer = await DisplayAlert("ПРЕДУПРЕЖДЕНИЕ: ПЕРЕЗАГРУЗКА РОБОТА",
                "Вы уверены, что хотите перезагрузить робота?\n\n" +
                "ВНИМАНИЕ:\n" +
                "• Это может занять несколько минут\n" +
                "• Все текущие процессы будут остановлены\n" +
                "• Сетевое соединение прервется",
                "Да, перезагрузить", "Отмена");

            if (!answer) return;

            try
            {
                BtnRebootRobot.IsEnabled = false;
                string result = await _connection.RebootRobot();

                if (result.StartsWith("Success"))
                {
                    await DisplayAlert("Перезагрузка начата",
                        "Робот перезагружается...\n\n" +
                        "Пожалуйста, подождите 2-3 минуты\n" +
                        "Соединение будет восстановлено автоматически",
                        "OK");

                    _isConnected = false;
                    InfoRobotLabel.Text = "РОБОТ ПЕРЕЗАГРУЖАЕТСЯ...\n\n" +
                                        "Пожалуйста, подождите\n" +
                                        "Примерное время: 2-3 минуты\n\n" +
                                        "После перезагрузки:\n" +
                                        "1. Подключение восстановится\n" +
                                        "2. Данные обновятся\n" +
                                        "3. Система будет готова к работе";

                    // Ждем и пытаемся переподключиться
                    await Task.Delay(180000); // 3 минуты
                    _isConnected = await _connection.ConnectToDevice();

                    if (_isConnected)
                    {
                        await DisplayAlert("Перезагрузка завершена",
                            "Робот успешно перезагружен!\n\n" +
                            "Соединение восстановлено",
                            "OK");
                        await InitPackages();
                        await UpdateSystemInfo();
                    }
                    else
                    {
                        await DisplayAlert("Внимание",
                            "Не удалось автоматически переподключиться\n\n" +
                            "Пожалуйста:\n" +
                            "1. Убедитесь, что робот включен\n" +
                            "2. Проверьте сетевое подключение\n" +
                            "3. Попробуйте перезапустить приложение",
                            "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Ошибка перезагрузки", result, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Критическая ошибка",
                    $"Не удалось перезагрузить робота:\n{ex.Message}",
                    "OK");
            }
            finally
            {
                BtnRebootRobot.IsEnabled = true;
            }
        }

        private async Task LoadNodesAndTopics()
        {
            if (!_isConnected)
            {
                NodesLog.Text = "НЕТ ПОДКЛЮЧЕНИЯ К УСТРОЙСТВУ\n\n" +
                              "Для работы с нодами и топиками необходимо подключение";
                return;
            }

            try
            {
                NodesLog.Text = "ЗАГРУЗКА ДАННЫХ...\n\n" +
                              "Получаю информацию о нодах и топиках\n" +
                              "Пожалуйста, подождите";

                // Загружаем ноды
                _availableNodes = await _connection.GetRosNode();

                // ЗАГРУЖАЕМ ТОПИКИ ← ДОБАВЬТЕ ЭТУ СТРОКУ
                _availableTopics = await _connection.GetAllTopics();

                NodesLog.Text = $"ДАННЫЕ ЗАГРУЖЕНЫ УСПЕШНО\n\n" +
                              $"• НОДЫ: {_availableNodes.Count} штук\n" +
                              $"• ТОПИКИ: {_availableTopics.Count} штук\n\n" +
                              $"Выберите в верхних пикерах:\n" +
                              $"1. Что просматривать (Ноды/Топики)\n" +
                              $"2. Тип просмотра (list/info/echo)\n" +
                              $"3. Нажмите кнопку 'Посмотреть'";
            }
            catch (Exception ex)
            {
                NodesLog.Text = $"ОШИБКА ЗАГРУЗКИ ДАННЫХ:\n\n{ex.Message}\n\n" +
                              "Попробуйте:\n" +
                              "1. Проверить подключение\n" +
                              "2. Перезагрузить страницу\n" +
                              "3. Убедиться, что ROS запущен";
            }
        }
        private async void OnViewClicked(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                NodesLog.Text = "НЕТ ПОДКЛЮЧЕНИЯ К УСТРОЙСТВУ\n\n" +
                              "Подключитесь к устройству для выполнения команд ROS 2";
                return;
            }

            if (NodeTopickPicker.SelectedItem == null)
            {
                NodesLog.Text = "ВЫБЕРИТЕ КОМАНДУ\n\n" +
                              "Выберите команду ROS 2 из первого пикера";
                return;
            }

            string selectedCommand = NodeTopickPicker.SelectedItem.ToString();
            string selectedObject = ViewPicker.SelectedItem?.ToString() ?? "";

            try
            {
                BtnView.IsEnabled = false;
                NodesLog.Text = $"ВЫПОЛНЕНИЕ КОМАНДЫ ROS 2...\n\n" +
                              $"Команда: {selectedCommand}\n" +
                              $"Объект: {selectedObject}\n\n" +
                              $"Пожалуйста, подождите";

                string result = "";

                switch (selectedCommand)
                {
                    case "Список узлов":
                        result = await ProcessNodesList();
                        break;

                    case "Список Топики":
                        result = await ProcessTopicsList();
                        break;

                    case "Просмотр Топика (информация)":
                        if (selectedObject.StartsWith("---"))
                        {
                            result = "ВЫБЕРИТЕ КОНКРЕТНЫЙ ТОПИК\n\n" +
                                   "Выберите топик из списка для получения информации";
                        }
                        else
                        {
                            result = await ProcessTopicInfo(selectedObject);
                        }
                        break;

                    case "Просмотр Топика (данные)":
                        if (selectedObject.StartsWith("---"))
                        {
                            result = "ВЫБЕРИТЕ КОНКРЕТНЫЙ ТОПИК\n\n" +
                                   "Выберите топик из списка для просмотра данных";
                        }
                        else
                        {
                            result = await ProcessTopicEcho(selectedObject);
                        }
                        break;

                    case "Остановить узел":
                        if (selectedObject.StartsWith("---"))
                        {
                            result = "ВЫБЕРИТЕ КОНКРЕТНЫЙ УЗЕЛ\n\n" +
                                   "Выберите узел из списка для остановки";
                        }
                        else
                        {
                            result = await ProcessNodeStop(selectedObject);
                        }
                        break;

                  
                    case "Просмотр Узла (информация)":
                        if (selectedObject.StartsWith("---"))
                        {
                            result = "ВЫБЕРИТЕ КОНКРЕТНЫЙ УЗЕЛ\n\n" +
                                   "Выберите узел для мониторинга параметров";
                        }
                        else
                        {
                            string nodeName = selectedObject.Replace(" (параметры)", "");
                            result = await ProcessNodeInfo(nodeName);
                        }
                        break;

                    case "Проверка работоспособности":
                        result = await ProcessDoctor();
                        break;

                    case "Просмотр логов узла":
                        if (selectedObject.StartsWith("---"))
                        {
                            result = "ВЫБЕРИТЕ КОНКРЕТНЫЙ УЗЕЛ\n\n" +
                                   "Выберите узел для просмотра логов";
                        }
                        else
                        {
                            result = await ProcessNodeLogs(selectedObject);
                        }
                        break;
                }

                NodesLog.Text = result;
            }
            catch (Exception ex)
            {
                NodesLog.Text = $"ОШИБКА ВЫПОЛНЕНИЯ КОМАНДЫ ROS 2:\n\n{ex.Message}\n\n" +
                              "Попробуйте:\n" +
                              "1. Проверить соединение\n" +
                              "2. Убедиться, что ROS 2 демон запущен\n" +
                              "3. Проверить правильность Domain ID\n" +
                              "4. Повторить запрос";
            }
            finally
            {
                BtnView.IsEnabled = true;
            }

        }
        private async Task<string> ProcessNodeInfo(string nodeName)
        {
            try
            {
                var nodeInfo = await _connection.GetNodeInfo(nodeName);
                return $"ИНФОРМАЦИЯ О УЗЛЕ: {nodeName}\n\n{nodeInfo}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПОЛУЧЕНИЯ ИНФОРМАЦИИ О УЗЛЕ {nodeName}:\n\n{ex.Message}";
            }
        }
        private async Task<string> ProcessTopicInfo(string topicName)
        {
            try
            {
                var topicInfo =  await _connection.GetRosTopicInfo(topicName);
                return $"ИНФОРМАЦИЯ О ТОПИКЕ: {topicName}\n\n{topicInfo}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПОЛУЧЕНИЯ ИНФОРМАЦИИ О ТОПИКЕ {topicName}:\n\n{ex.Message}";
            }
        }
        private async Task<string> ProcessNodesList()
        {
            _availableNodes = await _connection.GetRosNode();

            if (_availableNodes == null || _availableNodes.Count == 0)
            {
                return "СПИСОК УЗЛОВ ПУСТ\n\n" +
                       "Узлы ROS 2 не обнаружены\n" +
                       "Запустите launch файлы для создания узлов";
            }

            return $"СПИСОК АКТИВНЫХ УЗЛОВ ROS 2 ({_availableNodes.Count}):\n\n" +
                   string.Join("\n", _availableNodes.Select((n, i) => $"{i + 1}. {n}")) +
                   $"\n\nИТОГО: {_availableNodes.Count} узл(ов)";
        }
       
 
        private async Task<string> ProcessDoctor()
        {
            try
            {
                string result =  await _connection.CheckRosHealth();
                return $"ПРОВЕРКА РАБОТОСПОСОБНОСТИ ROS 2:\n\n{result}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПРОВЕРКИ РАБОТОСПОСОБНОСТИ:\n\n{ex.Message}";
            }
        }
        private async Task<string> ProcessNodeLogs(string nodeName)
        {
            try
            {
                string result =  await _connection.GetNodeLogs(nodeName, 5); // 5 последних сообщений
                return $"ЛОГИ УЗЛА: {nodeName}\n\n" +
                       $"Последние 5 сообщений:\n\n{result}";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПОЛУЧЕНИЯ ЛОГОВ УЗЛА {nodeName}:\n\n{ex.Message}";
            }
        }

     
        private async Task<string> ProcessTopicEcho(string topicName)
        {
            try
            {
                string echoData =  await _connection.EchoRosTopic(topicName, 5);
                return $"ДАННЫЕ ТОПИКА ROS 2: {topicName}\n\n" +
                       $"Последние 5 сообщений:\n\n" +
                       $"{echoData}\n\n" +
                       $"Для продолжения просмотра нажмите 'Посмотреть' снова";
            }
            catch (Exception ex)
            {
                return $"ОШИБКА ПРОСМОТРА ТОПИКА {topicName}:\n\n{ex.Message}";
            }
        }
        private async Task<string> ProcessNodeStop(string nodeName)
        {
            try
            {
                bool confirm = await DisplayAlert("ПОДТВЕРЖДЕНИЕ ОСТАНОВКИ",
                    $"Вы уверены, что хотите остановить узел:\n{nodeName}?\n\n" +
                    "Узел будет переведен в состояние shutdown",
                    "Да, остановить", "Отмена");

                if (!confirm)
                {
                    return "ОСТАНОВКА УЗЛА ОТМЕНЕНА";
                }

                string result = await _connection.StopRosNode(nodeName);

                if (result.StartsWith("Success"))
                {
                    // Обновляем список узлов
                    _availableNodes.Clear();

                    _availableNodes = await _connection.GetRosNode();
                    await UpdateSecondPicker();

                    return $"Проверьте удаление узла: {nodeName}\n\n" +
                           "Список активных узлов обновлен";
                }
                else
                {
                    return $"Проверьте список узлов ";
                }
            }
            catch (Exception ex)
            {
                return $"Проверьте список узлов {nodeName}:\n\n{ex.Message}";
            }
        }
        private async Task<string> ProcessTopicsList()
        {
            if (_availableTopics == null || _availableTopics.Count == 0)
            {
                return "СПИСОК ТОПИКОВ ПУСТ\n\n" +
                       "Ноды не публикуют топики";
            }

            return $"СПИСОК АКТИВНЫХ ТОПИКОВ ({_availableTopics.Count}):\n\n" +
                   string.Join("\n", _availableTopics.Select((t, i) => $"{i + 1}. {t}")) +
                   $"\n\nИТОГО: {_availableTopics.Count} топик(ов)";
        }
        private void ShowCommandDescription(string command)
        {
            string description = command switch
            {
                "Список узлов" => "Показывает список всех активных ROS 2 узлов\nКоманда: ros2 node list",
                "Список Топики" => "Показывает список всех активных топиков\nКоманда: ros2 topic list",
                "Просмотр Топика (информация)" => "Показывает информацию о топике (тип, издатели, подписчики)\nКоманда: ros2 topic info <topic>",
                "Просмотр Топика (данные)" => "Показывает сообщения топика в реальном времени\nКоманда: ros2 topic echo <topic>",
                "Остановить узел" => "Останавливает выбранный узел\nКоманда: ros2 lifecycle set <node> shutdown",
                "Проверка работоспособности" => "Проверяет работоспособность ROS 2 системы\nКоманда: ros2 doctor",
                "Просмотр логов узла" => "Показывает логи выбранного узла\nКоманда: ros2 topic echo /rosout",
                _ => "Выберите команду для работы с ROS 2"
            };

            NodesLog.Text = $"КОМАНДА: {command}\n\n{description}";
        }


        private async void OnDeleteNodesClicked(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                await DisplayAlert("Ошибка подключения",
                    "Нет подключения к устройству", "OK");
                return;
            }

            bool answer = await DisplayAlert("ПОДТВЕРЖДЕНИЕ УДАЛЕНИЯ",
                "Вы уверены, что хотите удалить ВСЕ ноды?\n\n" +
                "ВНИМАНИЕ:\n" +
                "• Все запущенные ноды будут остановлены\n" +
                "• Топики прекратят публикацию\n" +
                "• Запущенные процессы будут прерваны\n" +
                "• ВСЕ screen сессии будут закрыты\n" +
                "• Launch файлы будут остановлены",
                "Да, удалить все", "Отмена");

            if (!answer) return;

            try
            {
                BtnDeleteNodes.IsEnabled = false;
                NodesLog.Text = "УДАЛЕНИЕ ВСЕХ НОД И ПРОЦЕССОВ...\n\n" +
                              "Пожалуйста, подождите\n" +
                              "Это может занять несколько секунд";

                // 1. Сначала удаляем все через основной метод
                NodesLog.Text += "\n\n1. Удаление нод из графа ROS...";
                string deleteResult = await _connection.DeleteAllNodes();

                // 2. Внутри DeleteAllNodes теперь будет полная очистка
                bool success = !deleteResult.StartsWith("Ошибка:") &&
                              !deleteResult.StartsWith("Исключение:") &&
                              !deleteResult.StartsWith("ERROR:") &&
                              !string.IsNullOrWhiteSpace(deleteResult);

                if (success)
                {
                    NodesLog.Text = " ВСЕ НОДЫ И ПРОЦЕССЫ УДАЛЕНЫ УСПЕШНО\n\n" +
                                  "Результаты:\n" +
                                  $"{deleteResult}\n\n" +
                                  " Все запущенные ноды остановлены\n" +
                                  " Топики прекратили публикацию\n" +
                                  " Screen сессии закрыты\n" +
                                  " Launch файлы остановлены\n" +
                                  " Система полностью очищена\n\n" +
                                  "Для запуска новых нод используйте вкладку 'Запуск'";

                    // Обновляем списки
                    _availableNodes.Clear();
                    _availableTopics.Clear();

                    await DisplayAlert("Успех", "Все ноды и процессы успешно удалены", "OK");
                }
                else
                {
                    NodesLog.Text = "⚠ОШИБКА ПРИ УДАЛЕНИИ:\n\n" +
                                  $"{deleteResult}\n\n" +
                                  "Рекомендуется:\n" +
                                  "1. Проверить командой: ros2 node list\n" +
                                  "2. Проверить командой: screen -list\n" +
                                  "3. При необходимости перезагрузить систему";

                    await DisplayAlert("Внимание",
                        "Возникла ошибка при удалении.\nПроверьте лог для деталей.", "OK");
                }
            }
            catch (Exception ex)
            {
                NodesLog.Text = $"КРИТИЧЕСКАЯ ОШИБКА ПРИ УДАЛЕНИИ:\n\n{ex.Message}\n\n" +
                              "Рекомендуется:\n" +
                              "1. Перезагрузить приложение\n" +
                              "2. Проверить SSH соединение\n" +
                              "3. Перезагрузить устройство";

                await DisplayAlert("Критическая ошибка",
                    $"Ошибка при удалении нод: {ex.Message}", "OK");
            }
            finally
            {
                BtnDeleteNodes.IsEnabled = true;

                // Обновляем списки через несколько секунд
                await Task.Delay(2000);
                if (_isConnected)
                {
                    await LoadNodesAndTopics();
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // При необходимости можно добавить логику отключения
            // await _connection.Disconnect();
        }
    }
}