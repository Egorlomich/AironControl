using AironControl.Model;
using AironControl.Services;
using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AironControl.Adapters
{
    public class SshProtocolAdapter : IRobotAdapter, IDisposable
    {
        private SshClient? _client;
        private ShellStream? _shell;

        private readonly ConcurrentQueue<string> _commandQueue = new();
        private readonly RobotInfo _robotInfo = new();
        private readonly IConnectionSettingsService _settingsService;
        private CancellationTokenSource _cts = new();
        private bool _isRunning;
        private DateTime _lastSendTime = DateTime.MinValue;

        private const int THROTTLE_MS = 60;
        private const int RECONNECT_DELAY_MS = 5000;

        private (double x, double y, int rotate) _lastSent;

        public string ProtocolName => "SSH";
        public RobotInfo RobotInfo => _robotInfo;

        public SshProtocolAdapter(IConnectionSettingsService settingsService)
        {
            _settingsService = settingsService;
            _robotInfo.ProtocolType = "SSH";
            _robotInfo.Name = "Airon Robot (SSH)";
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            _robotInfo.UpdateStatus("Connecting");
            await ReconnectAsync(ct);
        }

        public async Task ReconnectAsync(CancellationToken ct = default)
        {
            Cleanup();

            try
            {
                var (host, port, user, pass) = GetConnectionSettings();

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                {
                    _robotInfo.UpdateStatus("Disconnected", "No active connection selected");
                    return;
                }

                _client = new SshClient(host, port, user, pass)
                {
                    ConnectionInfo = { Timeout = TimeSpan.FromSeconds(5) }
                };

                await _client.ConnectAsync(ct);

                _shell = _client.CreateShellStream("xterm", 80, 24, 800, 600, 8192);

                _robotInfo.UpdateStatus("Connected");
                _isRunning = true;
                _ = ProcessQueueAsync(_cts.Token);

                Debug.WriteLine("✅ SSH Connected successfully");
            }
            catch (Exception ex)
            {
                _robotInfo.UpdateStatus("Disconnected", ex.Message);
                Debug.WriteLine($"❌ SSH Connection failed: {ex.Message}");
            }
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_commandQueue.TryDequeue(out var command))
                {
                    await SendCommandInternalAsync(command, ct);
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }
        }

        private async Task SendCommandInternalAsync(string command, CancellationToken ct)
        {
            if (_shell == null || !_shell.CanWrite) return;

            var now = DateTime.UtcNow;
            if ((now - _lastSendTime).TotalMilliseconds < THROTTLE_MS)
                return;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(command + "\n");
                await _shell.WriteAsync(bytes, 0, bytes.Length, ct);
                await _shell.FlushAsync(ct);
                _lastSendTime = now;

                Debug.WriteLine($"[SSH Sent] {command}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SSH Send Error] {ex.Message}");
                Cleanup();
            }
        }

        public Task SendCommandAsync(CommandRequest command, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(command.Command))
                _commandQueue.Enqueue(command.Command);

            return Task.CompletedTask;
        }

        public async Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default)
        {
            if (_client == null || !_client.IsConnected)
                return "Error: Not connected";
            try
            {
                using var cmd = _client.CreateCommand(command);
                return await Task.Run(() => cmd.Execute(), ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SSH Execute Error] {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public Task<bool> ChangeMode(int mode, CancellationToken ct = default)
        {
            var cmd = $"echo \"mode \n{mode}\" > ./DataApp/modeApp.csv";
            _commandQueue.Enqueue(cmd);
            return Task.FromResult(true);
        }

        public Task<bool> SendIP(CancellationToken ct = default)
        {
            var ip = GetLocalWifiIp();
            if (string.IsNullOrWhiteSpace(ip) || ip == "0.0.0.0")
                return Task.FromResult(false);

            var cmd = $"echo \"ip \n{ip}\" > ./DataApp/ip.csv";
            _commandQueue.Enqueue(cmd);
            return Task.FromResult(true);
        }

        public void SendJoystickValues(double x, double y, int rotate)
        {
            // Дедупликация
            if (Math.Abs(x - _lastSent.x) < 0.04 &&
                Math.Abs(y - _lastSent.y) < 0.04 &&
                rotate == _lastSent.rotate)
                return;

            _lastSent = (x, y, rotate);

            var cmd = string.Format(CultureInfo.InvariantCulture,
                "printf \"x,y,angle\\n{0:0.00},{1:0.00},{2}\\n\" > ~/DataApp/motorValues.csv",
                x, y, rotate);

            _commandQueue.Enqueue(cmd);
        }

        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            _isRunning = false;
            _cts.Cancel();
            Cleanup();
            _robotInfo.UpdateStatus("Disconnected");
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
            _cts.Cancel();
            Cleanup();
            _cts.Dispose();
        }

        // ====================== Вспомогательные методы ======================

        private static string GetLocalWifiIp()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetLocalWifiIp error: {ex.Message}");
            }
            return "0.0.0.0";
        }

        private (string Host, int Port, string Username, string Password) GetConnectionSettings()
        {
            var active = _settingsService.GetActive();
            if (string.IsNullOrWhiteSpace(active.settingsName))
                return (string.Empty, 22, string.Empty, string.Empty);
            return (active.host, active.port, active.user, active.password);
        }
    }
}