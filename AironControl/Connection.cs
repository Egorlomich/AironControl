using Android.Content;
using Bumptech.Glide.Load.Resource.Bitmap;
using global::Renci.SshNet;
using Java.Util;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Microsoft.VisualBasic;
using Org.W3c.Dom;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace AironControl
{
    public struct Host
    {
        public Host()
        {
            settingsName = "default";
            host = "airon0";
            port = 22;
            user = "me"; 
            password = "10518psw";
            deviceName = "raspberry pi";
       
        }
        public string settingsName { get; set; }
        public string host { get; set; }
        public string user { get; set; }
        public string password { get; set; }
        public string deviceName { get; set; }
        public int port { get; set; }
        public override string ToString()
        {
            return settingsName;
        }
    }
    public class Connection
    {
        Host _host;
        SshClient _client;
        bool _isConnected = false;
        private readonly string _filePath;
        readonly string _activeConnectionPath;
        const string _fileName = "connectSettings.json";
        const string _activeFileName = "activeConnection.json";
        string _activeConnectionName = "default";

        public Host CurrentHost => _host;

        public Connection()
        {
            string _path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            _filePath = Path.Combine(_path, _fileName);
            _activeConnectionPath = Path.Combine(_path, _activeFileName);

            _ = InitializeAsync();

            Directory.CreateDirectory(_path);

            _host = LoadActiveConnection();
            if (string.IsNullOrWhiteSpace(_host.host))
            {
                _host = new Host(); 
            }

            _client = new SshClient(_host.host.Trim(),
                                    _host.port,
                                    _host.user.Trim(),
                                    _host.password.Trim());

        }

        public bool IsConnected => _isConnected && _client != null && _client.IsConnected;
        public void Disconnect()
        {
            try
            {
                if (_client != null && _client.IsConnected)
                {
                    _client.Disconnect();
                }
                _isConnected = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disconnecting: {ex.Message}");
            }
        }
        private async Task InitializeAsync()
        {
            try
            {
                await CheckDirAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialization error: {ex.Message}");
            }
        }
        public string GetActiveConnectionName()
        {
            return _activeConnectionName;
        }
        public Host LoadActiveConnection()
        {
            try
            {
                if (!File.Exists(_activeConnectionPath))
                    return new Host(); 

                string json = File.ReadAllText(_activeConnectionPath);
                var host = JsonSerializer.Deserialize<Host>(json);

                // Правильная проверка на null
                if (host.user.Equals("") || string.IsNullOrWhiteSpace(host.settingsName))
                {
                    try { File.Delete(_activeConnectionPath); } catch { }
                    return new Host();
                }

                _activeConnectionName = host.settingsName;
                return host;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadActiveConnection error: {ex}");
                try { File.Delete(_activeConnectionPath); } catch { }
                return new Host(); // Возвращаем null при ошибке
            }
        }
        public void DeleteConnection(string settingsName)
        {
            try
            {
                var hosts = GetAllConnections();
                int removed = hosts.RemoveAll(h => h.settingsName == settingsName);

                if (removed > 0)
                {
                    File.WriteAllText(_filePath,
                        JsonSerializer.Serialize(hosts,
                            new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to delete connection", ex);
            }
        }
        public async Task SaveConnection(Host host, bool setAsActive = false)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var hosts = GetAllConnections();
                var existing = hosts.FindIndex(h => h.settingsName == host.settingsName);

                if (existing >= 0)
                    hosts[existing] = host;
                else
                    hosts.Add(host);

                File.WriteAllText(_filePath, JsonSerializer.Serialize(hosts, options));

                if (setAsActive)
                   await SetActiveConnection(host);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to save connection", ex);
            }
        }
        public List<Host> GetAllConnections()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new List<Host>();

                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<Host>>(json) ?? new List<Host>();
            }
            catch
            {
                return new List<Host>();
            }
        }
        private async Task ExecuteInitialCommands(CancellationToken cancellationToken)
        {
            try
            {
                // Выполняем начальные команды только если они нужны
                await ChangeMode(150, cancellationToken);
                await ChangeMode(1, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initial commands error (non-critical): {ex.Message}");
                // Продолжаем работу даже если команды не выполнились
            }
        }

        private void CleanupClient()
        {
            try
            {
                if (_client != null)
                {
                    if (_client.IsConnected)
                    {
                        _client.Disconnect();
                    }
                    _client.Dispose();
                    _client = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupClient error: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
            }
        }
        public async Task<List<string>> GetRosNode(CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"GetRosNode START");

            try
            {
                string command = "ros2 node list";

                Debug.WriteLine($"Executing command: {command}");

                // Выполняем команду через общий метод
                string result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);

                Debug.WriteLine($"Command completed");
                Debug.WriteLine($"Result length: {result?.Length ?? 0}");
                Debug.WriteLine($"Raw result: '{result}'");

                // Проверяем, есть ли ошибка в результате
                if (string.IsNullOrEmpty(result))
                {
                    Debug.WriteLine($"Empty result");
                    return new List<string> { "Пустой ответ от ROS 2" };
                }

                if (result.StartsWith("Ошибка:") || result.StartsWith("Исключение:") || result.StartsWith("Операция отменена"))
                {
                    Debug.WriteLine($"Error in result: {result}");

                    // Проверяем типичные ошибки ROS
                    if (result.Contains("command not found") || result.Contains("ros2: not found"))
                    {
                        return new List<string> { "ROS 2 не установлен или не настроен" };
                    }

                    if (result.Contains("Unable to communicate with master") ||
                        result.Contains("could not communicate") ||
                        result.Contains("daemon may not be running"))
                    {
                        return new List<string> { "ROS 2 демон не запущен. Используйте 'ros2 daemon start'" };
                    }

                    return new List<string> { result };
                }

                var nodes = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(n => n.Trim())
                                 .Where(n => !string.IsNullOrWhiteSpace(n))
                                 .ToList();

                Debug.WriteLine($"Parsed {nodes.Count} nodes");

                foreach (var node in nodes)
                {
                    Debug.WriteLine($"  Node: {node}");
                }

                if (!nodes.Any())
                {
                    Debug.WriteLine($"No nodes found");
                    return new List<string> { "Нет запущенных узлов ROS 2" };
                }

                Debug.WriteLine($"GetRosNode SUCCESS: {nodes.Count} nodes");
                return nodes;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"GetRosNode cancelled");
                return new List<string> { "Операция отменена" };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRosNode UNEXPECTED error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.Message.Contains("channel") || ex.Message.Contains("session") || ex.Message.Contains("SSH"))
                {
                    return new List<string> { "Ошибка SSH соединения. Проверьте подключение." };
                }

                return new List<string> { $"Неожиданная ошибка: {ex.Message}" };
            }
            finally
            {
                Debug.WriteLine($"GetRosNode END");
            }
        }

        public async Task SetActiveConnection(Host newHost)
        {
            _host = newHost;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNameCaseInsensitive = true
                };
                string json = JsonSerializer.Serialize(_host, options);
                string directory = Path.GetDirectoryName(_activeConnectionPath) ??
                                  Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(_activeConnectionPath, JsonSerializer.Serialize(_host, options));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to save active connection", ex);
            }
        }
        public string GetInfo()
        {
            return $"{_host.settingsName} {_host.deviceName} {_host.user}@{_host.host}";
        }
        public async Task<bool> ConnectToDevice(CancellationToken cancellationToken = default)
        {
            try
            {
                Debug.WriteLine($"Attempting to connect to {_host.host}:{_host.port} as {_host.user}");

                _client?.Dispose();

                _client = new SshClient(_host.host.Trim(), _host.port, _host.user.Trim(), _host.password.Trim());

                await _client.ConnectAsync(cancellationToken);

                _isConnected = _client.IsConnected;

                Debug.WriteLine($"Connection result: {_isConnected}");
                return _isConnected;
            }
            catch (SshAuthenticationException ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error 304: Authentication failed - {ex.Message}");
                return false;
            }
            catch (SshConnectionException ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error 305: Connection refused - {ex.Message}");
                return false;
            }
            catch (SocketException ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error 306: Network error - {ex.SocketErrorCode}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error 303: ConnectToDevice failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }


        public static string GetLocalWifiIp()
        {
            try
            {
                // Получаем все сетевые интерфейсы
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface ni in interfaces)
                {
                    // Фильтруем WiFi интерфейсы
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                        ni.OperationalStatus == OperationalStatus.Up)
                    {
                        // Получаем IP свойства интерфейса
                        IPInterfaceProperties ipProps = ni.GetIPProperties();

                        // Ищем IPv4 адрес
                        foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                System.Diagnostics.Debug.WriteLine($"Адресс в сети WiFi IP: {addr.Address}");
                                return addr.Address.ToString();
                            }
                        }
                    }
                }

                // Если WiFi не найден, ищем любой активный IPv4 интерфейс
                foreach (NetworkInterface ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        IPInterfaceProperties ipProps = ni.GetIPProperties();

                        foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !IPAddress.IsLoopback(addr.Address))
                            {
                                System.Diagnostics.Debug.WriteLine($"Адресс в сети WiFi IP: {addr.Address}");
                                return addr.Address.ToString();
                            }
                        }
                    }
                }

                return "0.0.0.0";
            }
            catch (Exception ex)
            {
                // Логирование ошибки при необходимости
                System.Diagnostics.Debug.WriteLine($"Ошибка получения WiFi IP: {ex.Message}");
                return "0.0.0.0";
            }
        }
        public async Task<bool> SendIP(CancellationToken token = default)
        {
            try
            {
                var phoneIp = GetLocalWifiIp();

                 using var saveIpCmd = _client.CreateCommand(
                    $"echo \"ip\n{phoneIp}\" > ./DataApp/ip.csv"
                );

                await Task.Run(() => saveIpCmd.Execute(), token);
                
                if (saveIpCmd.ExitStatus != 0)
                    return false;

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SendIP failed: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> CheckDirAsync(CancellationToken token = default)
        {
            try
            {
                using var client = await CreateConnectedClientAsync(token);
                await EnsureDirectoryExistsAsync(client, token);
                await EnsureFilesExistAsync(client, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Error 225: CheckDirAsync canceled.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error 226: CheckDirAsync failed: {ex.Message}");
                return false;
            }
        }

        private async Task<SshClient> CreateConnectedClientAsync(CancellationToken token)
        {
            SshClient client;
            if (_host.user is null)
            { 
                throw new SshConnectionException("Error 229: Invalid SSH credentials");
            }
            else 
            {
                 client = new SshClient(_host.host.Trim(), _host.port, _host.user.Trim(), _host.password.Trim());
            }
            try
            {
                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    client.Connect();
                }, token);

                if (!client.IsConnected)
                    throw new SshConnectionException("Error 227: Failed to connect");

                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private async Task EnsureDirectoryExistsAsync(SshClient client, CancellationToken token)
        {
            using SshCommand command = client.CreateCommand("[ -d ./DataApp ] || mkdir -p ./DataApp");

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                command.Execute();
            }, token);

            if (command.ExitStatus != 0)
                throw new Exception("Error 228: Failed to create directory");
        }

        private async Task EnsureFilesExistAsync(SshClient client, CancellationToken token)
        {
            string[] files = ["motorValues.csv", "modeApp.csv", "ip.csv"];

            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();

                using SshCommand command = client.CreateCommand($"[ -f ./DataApp/{file} ] || touch ./DataApp/{file}");

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    command.Execute();
                }, token);

                if (command.ExitStatus != 0)
                    throw new Exception($"Error 10: Failed to create file: {file}");
            }
        }
        public async Task<string> ChangeMode(int mode, CancellationToken cancellationToken = default)//вроде збс
        {
            string request = $"echo \"mode\n{mode}\" > ./DataApp/modeApp.csv";
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_isConnected || !_client.IsConnected)
                {
                    await _client.ConnectAsync(cancellationToken);
                    _isConnected = true;
                    
                }

                using SshCommand cmd = _client.CreateCommand(request);
                string result = await Task.Run(() => cmd.Execute(), cancellationToken);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error 2: occurred: {ex.Message}");
                
            }
            return "Error 1: mode don`t change";
        }
        public async Task<string> GoJoystickValues(double x, double y, int rotate, CancellationToken cancellationToken = default)
        {
            if (!_client.IsConnected)
            {
                return "Error: Not connected to device";
            }
            var request = string.Format(
                CultureInfo.InvariantCulture,
                "printf \"x,y,angle\n{0:0.00},{1:0.00},{2}\n\" > ~/DataApp/motorValues.csv",
                x, y, rotate);
            //string request = $"echo \"x,y,angle\n{x:0.00},{y:0.00},{rotate}\" > ./DataApp/motorValues.csv"; 
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using SshCommand cmd = _client.CreateCommand(request);
                await Task.Run(() => cmd.Execute(), cancellationToken);
                return cmd.ExitStatus == 0 ? "Command send" : "Command failed";
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error: {ex.Message}");
                return $"Error 100";
            }
        }
        public async Task<List<string>> GetRosLocalWorkespace(CancellationToken cancellationToken = default)
        {
            List<string> packages = new List<string>();
            if (!_client.IsConnected)
            {
                packages.Add("Error: Not connected to device");
                return packages;
            }
            string command = "colcon list -n";
            //string command = "bash -lc \"colcon list -n\""

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using SshCommand cmd = _client.CreateCommand(command);

                string result = await Task.Run(() =>
                {
                    cmd.Execute();
                    return cmd.Result;
                }, cancellationToken);
                Debug.WriteLine("STDOUT: " + cmd.Result);
                Debug.WriteLine("STDERR: " + cmd.Error);
                if (cmd.ExitStatus == 0 && !string.IsNullOrEmpty(result))
                {
                    packages.AddRange(result.Split('\n')
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim()));
                }
                else
                {
                    packages.Add("Command failed or no output");
                }

                return packages;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Debug.WriteLine($"Error: {ex.Message}");
                packages.Add("Error 99");
                return packages;
            }
        }

        public async Task<List<string>> GetRosNodeList(CancellationToken cancellationToken = default)
        {
            string command = "ros2 node list";
            string result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(5), cancellationToken);

            if (string.IsNullOrEmpty(result) || result.StartsWith("Ошибка:"))
            {
                return new List<string> { "Узлы не найдены" };
            }

            return result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.Trim())
                         .Where(t => !string.IsNullOrEmpty(t))
                         .ToList();
        }
        public async Task<List<string>> GetRosLaunchName(string packName, CancellationToken cancellationToken = default)
        {
            {
                List<string> launchFiles = new List<string>();

                try
                {
                    if (_client == null || !_client.IsConnected)
                    {
                        launchFiles.Add("Нет подключения");
                        return launchFiles;
                    }

                    string testCmd = $"bash -lc \"ls  /*/*/*/install/{packName}/share/{packName}/launch/*.py 2>/dev/null | head -15\"";

                    string result = string.Empty;

                    await Task.Run(() =>
                    {
                        using var cmd = _client.CreateCommand(testCmd);
                        cmd.CommandTimeout = TimeSpan.FromSeconds(10);
                        cmd.Execute();
                        result = cmd.Result;
                    }, cancellationToken);

                    if (!string.IsNullOrEmpty(result.Trim()))
                    {
                        // Извлекаем только имена файлов
                        var files = result.Split('\n')
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => Path.GetFileName(line.Trim()))
                            .Select(name => name.Replace(".launch.py", ""))
                            .ToList();

                        launchFiles.AddRange(files);
                    }

                    if (!launchFiles.Any())
                    {
                        launchFiles.Add("launch файлы не найдены");
                    }

                    return launchFiles;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GetRosLaunchNameSimple error: {ex.Message}");
                    launchFiles.Add("launch файлы не найдены");
                    return launchFiles;
                }
            }
        }
        //------------------------------------------------------------------ros2
        public async Task<List<string>> GetRosNodes(CancellationToken cancellationToken = default)
        {
            return await GetRosNode(cancellationToken);
        }

        // Для "Список Топики"
        public async Task<List<string>> GetRosTopics(CancellationToken cancellationToken = default)
        {
            var topics = new List<string>();

            try
            {
                Debug.WriteLine("GetRosTopics START");

                if (_client == null || !_client.IsConnected)
                {
                    return new List<string> { "Нет подключения к SSH" };
                }

                string result = string.Empty;
                string error = string.Empty;

                try
                {
                    result = await Task.Run(() =>
                    {
                        using var cmd = _client.CreateCommand("ros2 topic list");
                        cmd.CommandTimeout = TimeSpan.FromSeconds(15);
                        cmd.Execute();
                        error = cmd.Error;
                        return cmd.Result;
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SSH command execution exception: {ex.Message}");
                    return new List<string> { $"Исключение SSH: {ex.Message}" };
                }

                if (!string.IsNullOrEmpty(error) && string.IsNullOrEmpty(result))
                {
                    if (error.Contains("command not found") || error.Contains("ros2: not found"))
                    {
                        return new List<string> { "ROS 2 не установлен или не настроен" };
                    }
                    return new List<string> { $"Ошибка SSH: {error}" };
                }

                if (!string.IsNullOrEmpty(result))
                {
                    result = result.Replace("\r", "");
                    topics = result.Split('\n')
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim())
                        .ToList();

                    Debug.WriteLine($"Parsed {topics.Count} topics");
                }

                if (!topics.Any())
                {
                    if (!string.IsNullOrEmpty(error) && error.Contains("Unable to communicate with master"))
                    {
                        return new List<string> { "ROS 2 демон не запущен" };
                    }
                    return new List<string> { "Нет активных топиков ROS 2" };
                }

                return topics;
            }
            catch (OperationCanceledException)
            {
                return new List<string> { "Операция отменена" };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRosTopics error: {ex.Message}");
                return new List<string> { $"Ошибка: {ex.GetType().Name}: {ex.Message}" };
            }
        }

        // Для "Список сервисов"
        public async Task<List<string>> GetRosServices(CancellationToken cancellationToken = default)
        {
            var services = new List<string>();

            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return new List<string> { "Нет подключения к SSH" };
                }

                string result = string.Empty;
                string error = string.Empty;

                try
                {
                    result = await Task.Run(() =>
                    {
                        using var cmd = _client.CreateCommand("ros2 service list");
                        cmd.CommandTimeout = TimeSpan.FromSeconds(15);
                        cmd.Execute();
                        error = cmd.Error;
                        return cmd.Result;
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SSH command execution exception: {ex.Message}");
                    return new List<string> { $"Исключение SSH: {ex.Message}" };
                }

                if (!string.IsNullOrEmpty(result))
                {
                    result = result.Replace("\r", "");
                    services = result.Split('\n')
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim())
                        .ToList();
                }

                if (!services.Any())
                {
                    return new List<string> { "Нет доступных сервисов ROS 2" };
                }

                return services;
            }
            catch (OperationCanceledException)
            {
                return new List<string> { "Операция отменена" };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRosServices error: {ex.Message}");
                return new List<string> { $"Ошибка: {ex.Message}" };
            }
        }

        // Для "Список параметров"
        public async Task<List<string>> GetRosParameters(CancellationToken cancellationToken = default)
        {
            var parameters = new List<string>();

            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return new List<string> { "Нет подключения к SSH" };
                }

                string result = string.Empty;
                string error = string.Empty;

                try
                {
                    string command = "ros2 param list";
                  //  return result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);
                    result = await Task.Run(() =>
                    {
                        using var cmd = _client.CreateCommand("ros2 param list");
                        cmd.CommandTimeout = TimeSpan.FromSeconds(15);
                        cmd.Execute();
                        error = cmd.Error;
                        return cmd.Result;
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SSH command execution exception: {ex.Message}");
                    return new List<string> { $"Исключение SSH: {ex.Message}" };
                }

                if (!string.IsNullOrEmpty(result))
                {
                    result = result.Replace("\r", "");
                    parameters = result.Split('\n')
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim())
                        .ToList();
                }

                if (!parameters.Any())
                {
                    return new List<string> { "Нет доступных параметров ROS 2" };
                }

                return parameters;
            }
            catch (OperationCanceledException)
            {
                return new List<string> { "Операция отменена" };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRosParameters error: {ex.Message}");
                return new List<string> { $"Ошибка: {ex.Message}" };
            }
        }

        // Для "Просмотр Топика (информация)"
        public async Task<string> GetRosTopicInfo(string topicName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return "Нет подключения к SSH";
                }

                string result = string.Empty;
                string error = string.Empty;

                try
                {
                    string command = $"ros2 topic info {topicName} --verbose";
                    return result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);
                  
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SSH command execution exception: {ex.Message}");
                    return $"Исключение SSH: {ex.Message}";
                }

            }
            catch (OperationCanceledException)
            {
                return "Операция отменена";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRosTopicInfo error: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }
        }

        // Для "Просмотр Топика (данные)"
        public async Task<string> EchoRosTopic(string topicName, int messageCount, CancellationToken cancellationToken = default)
        {
            string command = $"ros2 topic echo {topicName} | head -{Math.Max(messageCount, 1) * 5}";
            return await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);
        }

        // Для "Остановить узел"

        public async Task<string> StopRosNode(string nodeName, CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"Остановка узла: {nodeName}");

            // Убираем слэш и готовим команду
            string nodeCut = nodeName.TrimStart('/');
            string command = $"pkill -9 -f \"{nodeCut}\"";
            command = WrapSudoCommand(command, _host.password);

            Debug.WriteLine($"Команда: {command}");

            using var cmd = _client.CreateCommand(command);

            string command1 = $"pkill -9 -f \"{nodeName}\"";
            using var cmd1 = _client.CreateCommand(command1);
            // Первое выполнение
            string result = "";
            try
            {
                cmd.Execute();
                await Task.Delay(500, cancellationToken); // Даем время на завершение

                // Второе выполнение (для уверенности)
                cmd1.Execute();
                await Task.Delay(500, cancellationToken);

                result = cmd.Result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при выполнении команды: {ex.Message}");
                result = $"Error: {ex.Message}";
            }

            return result;
        }
        // Для "Перезапустить узел"
        public async Task<string> RestartRosNode(string nodeName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return "Нет подключения к SSH";
                }

                string result = string.Empty;
                string error = string.Empty;

                try
                {
                    result = await Task.Run(() =>
                    {
                        // Сначала получаем информацию об узле, чтобы узнать пакет
                        using var cmd = _client.CreateCommand($"ros2 node info {nodeName} | grep 'Package:' | awk '{{print $2}}'");
                        cmd.Execute();
                        string package = cmd.Result.Trim();

                        if (string.IsNullOrEmpty(package))
                        {
                            error = "Не удалось определить пакет узла";
                            return string.Empty;
                        }

                        // Останавливаем узел
                        using var stopCmd = _client.CreateCommand($"ros2 lifecycle set {nodeName} shutdown || pkill -f \"ros2 run.*{nodeName}\"");
                        stopCmd.Execute();

                        // Ждем немного
                        Thread.Sleep(1000);

                        // Перезапускаем узел (это сложно сделать без знания точной команды запуска)
                        // Вместо этого просто возвращаем сообщение
                        error = "Узел остановлен. Для запуска используйте соответствующую launch команду";
                        return string.Empty;

                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SSH command execution exception: {ex.Message}");
                    return $"Исключение SSH: {ex.Message}";
                }

                if (!string.IsNullOrEmpty(error))
                {
                    return error;
                }

                return $"Узел '{nodeName}' перезапущен";
            }
            catch (OperationCanceledException)
            {
                return "Операция отменена";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestartRosNode error: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }
        }


        // Для "Проверка работоспособности"
        public async Task<string> CheckRosHealth(CancellationToken cancellationToken = default)
        {
            string command = "ros2 doctor --report";
            string result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(30), cancellationToken);

            if (string.IsNullOrEmpty(result) || result.StartsWith("Ошибка:"))
            {
                return "Не удалось проверить работоспособность ROS 2";
            }

            return $"Проверка работоспособности ROS 2:\n\n{result}";
        }


        public async Task<string> GetNodeLogs(string nodeName, int messageCount, CancellationToken cancellationToken = default)
        {
            string command = $"ros2 topic echo /rosout --once | grep -i '{nodeName}' | head -{Math.Max(messageCount, 1)}";
            string result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);

            if (string.IsNullOrEmpty(result) || result.StartsWith("Ошибка:"))
            {
                return $"Нет логов для узла '{nodeName}'";
            }

            return $"Логи узла '{nodeName}':\n\n{result}";
        }

        public async Task<string> DeleteAllNodes(CancellationToken cancellationToken = default)
        {
            string command = @"
        nodes=$(ros2 node list);
        if [ -n ""$nodes"" ]; then
            for node in $nodes; do
                ros2 lifecycle set $node shutdown 2>/dev/null || true;
                pkill -f ""ros2 run.*$node"" 2>/dev/null || true;
            done;
            echo ""Все узлы остановлены"";
        else
            echo ""Нет запущенных узлов"";
        fi
    ";

            return await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(10), cancellationToken);
        }


        // Дополнительный метод для получения информации о системе
        public async Task<string> GetSystemInfo(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return "Нет подключения к SSH";
                }

                StringBuilder info = new StringBuilder();
                string error = string.Empty;

                try
                {
                    await Task.Run(() =>
                    {
                        // Получаем различные системные данные
                        using var cmd1 = _client.CreateCommand("uname -a");
                        cmd1.Execute();
                        info.AppendLine("ИНФОРМАЦИЯ О СИСТЕМЕ");
                        info.AppendLine($"ОС: {cmd1.Result.Trim()}");

                        using var cmd2 = _client.CreateCommand("uptime");
                        cmd2.Execute();
                        info.AppendLine($"Аптайм: {cmd2.Result.Trim()}");

                        using var cmd3 = _client.CreateCommand("free -h | head -2");
                        cmd3.Execute();
                        info.AppendLine($"Память:\n{cmd3.Result.Trim()}");

                        using var cmd4 = _client.CreateCommand("df -h / | tail -1");
                        cmd4.Execute();
                        info.AppendLine($"Диск: {cmd4.Result.Trim()}");

                       
                        using var cmd6 = _client.CreateCommand("hostname -I");
                        cmd6.Execute();
                        info.AppendLine($"IP адреса: {cmd6.Result.Trim()}");

                        using var cmd7 = _client.CreateCommand("date");
                        cmd7.Execute();
                        info.AppendLine($"Дата и время: {cmd7.Result.Trim()}");

                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SSH command execution exception: {ex.Message}");
                    return $"Исключение SSH: {ex.Message}";
                }

                return info.ToString();
            }
            catch (OperationCanceledException)
            {
                return "Операция отменена";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetSystemInfo error: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }
        }
        /// <summary>
        /// ////////////////////////////////////
        /// </summary>
        /// <returns></returns>
        /// <returns></returns>
        public async Task<string> RunLaunchFile(string package, string launchFile, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_client.IsConnected)
                        _client.Connect();

                    if (!_client.IsConnected)
                        return "Failed to connect to SSH server";

                    // Проверяем количество запущенных ROS нод
                    string checkNodesCommand = WrapRos2Command("ros2 node list 2>/dev/null | wc -l");
                    string nodeCountStr;
                    using (var checkCmd = _client.CreateCommand(checkNodesCommand))
                    {
                        checkCmd.CommandTimeout = TimeSpan.FromSeconds(5);
                        nodeCountStr = checkCmd.Execute().Trim();
                    }

                    int nodeCount = 0;
                    int.TryParse(nodeCountStr, out nodeCount);

                    // Если нод больше 2 - ошибка
                    if (nodeCount > 2)
                    {
                        return "❌ ERROR: Обнаружены запущенные ROS ноды. Пожалуйста, остановите все программы перед запуском.\n" +
                               $"Текущее количество нод: {nodeCount}\n" +
                               "Используйте: ros2 node list для просмотра активных нод";
                    }

                    string screenSessionName = $"{package}_{launchFile}";

                    // Убиваем старую
                    _client.CreateCommand($"screen -S {screenSessionName} -X quit 2>/dev/null || true").Execute();

                    // ОДИН запуск - всё в одной команде
                    string wrappedCommand = WrapRos2Command(
                        $"screen -dmS {screenSessionName} bash -c 'ros2 launch {package} {launchFile}.launch.py; exec bash'"
                    );

                    using (var cmd = _client.CreateCommand(wrappedCommand))
                    {
                        cmd.CommandTimeout = TimeSpan.FromSeconds(10);
                        string result = cmd.Execute();

                        Thread.Sleep(2000);

                        string checkScreen = _client.CreateCommand($"screen -ls | grep {screenSessionName} || echo 'NO'")
                                                   .Execute()
                                                   .Trim();

                        return checkScreen.Contains("NO")
                            ? $"Failed: {result}"
                            : $"Success: {screenSessionName} started";
                    }
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }, cancellationToken);
        }
        private string GetRecentLogs(string logDir, int lines = 10)
        {
            try
            {
                string command = $"tail -n {lines} {logDir}/launch.log 2>/dev/null || echo 'Log file not found'";

                using (var cmd = _client.CreateCommand(command))
                {
                    cmd.CommandTimeout = TimeSpan.FromSeconds(3);
                    return cmd.Execute();
                }
            }
            catch
            {
                return "Could not retrieve logs";
            }
        }
        public async Task<string> DeleteAllNodes()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_client.IsConnected)
                    {
                        _client.Connect();
                    }

                    // Команда для полной очистки всех процессов
                    string command = WrapRos2Command(
                        "echo '1. ОСТАНОВКА ВСЕХ SCREEN СЕССИЙ ' && " +
                        "screen -wipe 2>/dev/null || true && " +
                        "screen -list 2>/dev/null | grep -oE '[0-9]+\\.[a-zA-Z0-9_]+' | " +
                        "while read session; do " +
                        "  echo 'Останавливаю: $session' && " +
                        "  screen -S \"$session\" -X quit 2>/dev/null || true; " +
                        "done && " +
                        "echo '✓ Screen сессии остановлены' && " +
                        "echo '' && " +

                        "echo ' 2. ОСТАНОВКА ROS2 ПРОЦЕССОВ ' && " +
                        "pkill -f 'ros2 launch' 2>/dev/null || true && " +
                        "pkill -f 'ros2 run' 2>/dev/null || true && " +
                        "pkill -f 'python.*airon_pkg' 2>/dev/null || true && " +
                        "pkill -f 'python.*main_node' 2>/dev/null || true && " +
                        "sleep 1 && " +
                        "echo '✓ ROS процессы остановлены' && " +
                        "echo '' && " +

                        "echo ' 3. УДАЛЕНИЕ НОД ИЗ ROS ГРАФА' && " +
                        "ros2 node list 2>/dev/null | " +
                        "while read node; do " +
                        "  echo 'Удаляю ноду: $node' && " +
                        "  ros2 lifecycle set \"$node\" shutdown 2>/dev/null || " +
                        "  ros2 service call \"$node/_node/deactivate\" std_srvs/srv/Trigger 2>/dev/null || " +
                        "  ros2 service call \"$node/~/shutdown\" std_srvs/srv/Empty 2>/dev/null || true; " +
                        "done && " +
                        "sleep 1 && " +
                        "NODE_COUNT=$(ros2 node list 2>/dev/null | wc -l) && " +
                        "echo '✓ Удаление нод завершено. Осталось: $NODE_COUNT' && " +
                        "echo '' && " +

                        "echo ' 4. ОЧИСТКА ВРЕМЕННЫХ ФАЙЛОВ ' && " +
                        "rm -rf /tmp/ros_logs/* 2>/dev/null || true && " +
                        "echo '✓ Временные файлы очищены' && " +
                        "echo '' && " +

                        "echo ' 5. ФИНАЛЬНАЯ ПРОВЕРКА ' && " +
                        "echo 'Активных screen сессий:' && " +
                        "screen -list 2>/dev/null | head -5 || echo '  нет' && " +
                        "echo '' && " +
                        "echo 'Активных ROS нод:' && " +
                        "ros2 node list 2>/dev/null || echo '  нет' && " +
                        "echo '' && " +
                        "echo ' ВСЕ ПРОЦЕССЫ ОСТАНОВЛЕНЫ И СИСТЕМА ОЧИЩЕНА'"
                    );

                    using (var cmd = _client.CreateCommand(command))
                    {
                        cmd.CommandTimeout = TimeSpan.FromSeconds(30);
                        return cmd.Execute();
                    }
                }
                catch (Exception ex)
                {
                    return $"❌ Ошибка при удалении: {ex.Message}";
                }
            });
        }

        private string ExecuteSimpleCommand(string command)
        {
            try
            {
                using (var cmd = _client.CreateCommand(command))
                {
                    cmd.CommandTimeout = TimeSpan.FromSeconds(3);
                    return cmd.Execute();
                }
            }
            catch
            {
                return "";
            }
        }
        //////////////////////////////////////////////
        private string WrapRos2Command(string command)
        {
            // Пробуем несколько возможных путей к workspace
            return $"/bin/bash -c \"" +
                   $"source /opt/ros/humble/setup.bash && " +
                   $"source $HOME/ros2_ws/install/setup.bash 2>/dev/null || " +
                   $"source $HOME/*/install/setup.bash 2>/dev/null || " +
                   $"source /opt/ros/*/setup.bash 2>/dev/null || true && " +
                   $"cd ~ && {command}\"";
        }
        private string WrapSudoCommand(string command, string sudoPassword)
        {
            return $"/bin/bash -c \"echo '{sudoPassword}' | sudo -S {command}\"";
        }

        private async Task<string> ExecuteRos2CommandAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return "Нет подключения к SSH";
                }

                string wrappedCommand = WrapRos2Command(command);
                string result = string.Empty;
                string error = string.Empty;

                await Task.Run(() =>
                {
                    using var cmd = _client.CreateCommand(wrappedCommand);
                    cmd.CommandTimeout = timeout ?? TimeSpan.FromSeconds(10);
                    cmd.Execute();
                    result = cmd.Result;
                    error = cmd.Error;
                }, cancellationToken);

                if (!string.IsNullOrEmpty(error) && string.IsNullOrEmpty(result))
                {
                    return $"Ошибка: {error.Trim()}";
                }

                return result?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                return "Операция отменена";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExecuteRos2CommandAsync error: {ex.Message}");
                return $"Исключение: {ex.Message}";
            }
        }

        public async Task<string> ReloadProgram(CancellationToken cancellationToken = default)
        {
            string command = @"
                ros2 daemon stop 2>/dev/null || true && 
                sleep 2 && 
                ros2 daemon start 2>/dev/null && 
                echo 'Программа перезагружена'";

            return await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(30), cancellationToken);
        }

        public async Task<string> RebootRobot(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    return "Нет подключения к SSH";
                }

                string sudoPassword = _host.password;
                if (string.IsNullOrEmpty(sudoPassword))
                {
                    return "Требуется пароль sudo";
                }

                string wrappedCommand = WrapSudoCommand("reboot", sudoPassword);
                string result = string.Empty;

                await Task.Run(() =>
                {
                    using var cmd = _client.CreateCommand(wrappedCommand);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(5);
                    cmd.Execute();
                    result = cmd.Result;
                }, cancellationToken);

                return "Команда перезагрузки отправлена";
            }
            catch (SshConnectionException)
            {
                return "Устройство перезагружается (соединение потеряно)";
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}. Неверный пароль?";
            }
        }

        public async Task<List<string>> GetAllTopics(CancellationToken cancellationToken = default)
        {
            string command = "ros2 topic list";
            string result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(5), cancellationToken);

            if (string.IsNullOrEmpty(result) || result.StartsWith("Ошибка:"))
            {
                return new List<string> { "Топики не найдены" };
            }

            return result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.Trim())
                         .Where(t => !string.IsNullOrEmpty(t))
                         .ToList();
        }

        public async Task<string> KillNode(string nodeName, CancellationToken cancellationToken = default)
        {
            string command = $"ros2 lifecycle set {nodeName} shutdown || ros2 lifecycle set {nodeName} cleanup";
            return await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(10), cancellationToken);
        }

        public async Task<string> EchoTopic(string topicName, int messageCount, CancellationToken cancellationToken = default)
        {
            string command = $"ros2 topic echo {topicName} --once 2>&1 | head -{Math.Max(messageCount, 1) * 5}";
            return await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);
        }

        public async Task<string> GetNodeInfo(string nodeName, CancellationToken cancellationToken = default)
        {
            string command = $@"
                    echo 'ИНФОРМАЦИЯ ОБ УЗЛЕ: {nodeName}' && 
                    echo '' && 
                    echo 'Выполняется команда: ros2 node info {nodeName}' && 
                    echo '' && 
                    ros2 node info {nodeName}  2>&1 || echo 'Узел не найден'
                ";

                string result = await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(15), cancellationToken);

                if (string.IsNullOrEmpty(result) ||
                    result.Contains("Node not found") ||
                    result.Contains("node does not exist") ||
                    result.StartsWith("Ошибка:"))
                {
                    return $"Узел '{nodeName}' не найден в системе";
                }

            return result;
        }
        

        public async Task<string> GetTopicInfo(string topicName, CancellationToken cancellationToken = default)
        {
            string command = $"ros2 topic info {topicName} --verbose";
            return await ExecuteRos2CommandAsync(command, TimeSpan.FromSeconds(10), cancellationToken);
        }

     
    }
}
