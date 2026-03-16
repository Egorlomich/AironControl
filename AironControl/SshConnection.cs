using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AironControl
{
    internal partial class SshConnection
    {
        private Host _host;
        private SshClient _client;
        private ShellStream _shell;
        private readonly ConcurrentQueue<string> _commandQueue = new();
        private CancellationTokenSource _cts = new();
        private bool _isRunning;
        private DateTime _lastSendTime = DateTime.MinValue;
        private (double x, double y, int rotate) _lastSentValues;
        private const int THROTTLE_MS = 60;           // ограничение кооманд в секкунду (примерно 16-17 команд в секунду)
        private const int RECONNECT_DELAY_MS = 1500;


        private readonly string _filePath;
        readonly string _activeConnectionPath;
        const string _fileName = "connectSettings.json";
        const string _activeFileName = "activeConnection.json";
        string _activeConnectionName = "default";

        public bool IsConnected => _client?.IsConnected == true && _shell?.CanWrite == true;
        public SshConnection()
        {
            //пусть с данными сохранненных подключений
            string _path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            _filePath = Path.Combine(_path, _fileName);
            _activeConnectionPath = Path.Combine(_path, _activeFileName);

            _host = LoadActiveConnection();
            _ = StartProcessingLoopAsync();
        }
        private async Task StartProcessingLoopAsync()
        {
            _isRunning = true;

            while (_isRunning && !_cts.IsCancellationRequested)
            {
                try
                {
                    // Подключаемся, если потеряли связь
                    if (!IsConnected)
                    {
                        await ReconnectAsync();
                    }

                    if (_commandQueue.TryDequeue(out var command))
                    {
                        await SendCommandInternalAsync(command);
                    }
                    else
                    {
                        await Task.Delay(10, _cts.Token); // лёгкий breathing
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SSH Loop] Ошибка: {ex.Message}");
                    await Task.Delay(RECONNECT_DELAY_MS, _cts.Token);
                }
            }
        }
        private async Task ReconnectAsync(CancellationToken ct = default)
        {
            Debug.WriteLine($"[Reconnect START] Хост: {_host.host}:{_host.port}, user: {_host.user}");

            Cleanup();

            try
            {
                if (string.IsNullOrWhiteSpace(_host.host))
                {
                    Debug.WriteLine("[Reconnect] ОШИБКА: host пустой!");
                    return;
                }

                Debug.WriteLine("[Reconnect] Создаём SshClient...");

                _client = new SshClient(_host.host.Trim(), _host.port, _host.user.Trim(), _host.password.Trim())
                {
                    ConnectionInfo = { Timeout = TimeSpan.FromSeconds(12) }   // увеличил таймаут
                };

                Debug.WriteLine("[Reconnect] Вызываем ConnectAsync...");

                // ←←← ИЗМЕНЕНИЕ: используем ConnectAsync правильно
                await _client.ConnectAsync(ct);

                Debug.WriteLine($"[Reconnect] После ConnectAsync(): IsConnected = {_client.IsConnected}");

                if (!_client.IsConnected)
                {
                    Debug.WriteLine("[Reconnect] ConnectAsync вернул false");
                    throw new Exception("ConnectAsync вернул false");
                }

                Debug.WriteLine("[Reconnect] Подключение успешно, создаём ShellStream...");

                _shell = _client.CreateShellStream("joystick-shell", 80, 25, 800, 600, 8192);

                Debug.WriteLine("[Reconnect] Готово! ShellStream создан");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Reconnect ERROR] {ex.GetType().Name}: {ex.Message}");

                if (ex.InnerException != null)
                    Debug.WriteLine($"   Inner: {ex.InnerException.Message}");

                // Более точные сообщения
                if (ex is SocketException sockEx)
                {
                    Debug.WriteLine($"   → Сетевая ошибка: {sockEx.SocketErrorCode} - {sockEx.Message}");
                    if (sockEx.SocketErrorCode == SocketError.HostNotFound)
                        Debug.WriteLine("   → ИМЯ ХОСТА НЕ НАЙДЕНО! Замени 'airon0' на реальный IP (192.168.x.x)");
                }
                else if (ex is SshAuthenticationException)
                    Debug.WriteLine("   → Неверный логин или пароль");
                else if (ex is SshConnectionException)
                    Debug.WriteLine("   → Соединение отклонено (порт 22 закрыт или SSH не запущен)");

                Cleanup();
            }
        }
        private async Task SendCommandInternalAsync(string command, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastSendTime).TotalMilliseconds < THROTTLE_MS)
            {
                return;
            }
            if (_shell == null || !_shell.CanWrite)
            {
                await ReconnectAsync(cancellationToken);
                if (_shell == null || !_shell.CanWrite)
                {
                    Debug.WriteLine("[Send] Shell still not writable after reconnect → skipping command");
                    return;
                }
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(command + "\n");
                await _shell.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                await _shell.FlushAsync(cancellationToken);
                _lastSendTime = now;
                Debug.WriteLine($"[SSH Sent] {command.Trim()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SSH Send] Error: {ex.GetType().Name}: {ex.Message}");
                Cleanup();
            }
        }
        private void Cleanup()
        {
            try
            {
                _shell?.Dispose();
                _client?.Disconnect();
                _client?.Dispose();
            }
            catch { }
            _shell = null;
            _client = null;
        }
        public void Dispose()
        {
            _isRunning = false;
            _cts.Cancel();
            _cts.Dispose();
            Cleanup();
        }
    }
}
