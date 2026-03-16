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
using System.Net;
using System.Threading.Tasks;
namespace AironControl
{
    /// <summary>
    /// Оптимизированный SSH-клиент для частых команд джойстика
    /// Использует ShellStream + очередь + троттлинг + reconnect
    /// </summary>
    internal partial class SshConnection
    {
        public async Task<bool> ChangeMode(int mode, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string request = $"echo \"mode \n{mode}\" > ./DataApp/modeApp.csv";

                // Добавляем в очередь — мгновенная операция
                _commandQueue.Enqueue(request);

                // Если обработчик не запущен — запускаем
                if (!_isRunning)
                {
                    _isRunning = true;
                    _ = ProcessQueueAsync(cancellationToken);
                }

                Debug.WriteLine($"ChangeMode queued: mode={mode}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChangeMode error: {ex.Message}");
                Cleanup();
                return false;
            }
        }
        public async Task<bool> SendIP(CancellationToken token = default)
        {
            try
            {
                var phoneIp = GetLocalWifiIp();
                if (string.IsNullOrWhiteSpace(phoneIp) || phoneIp == "0.0.0.0")
                {
                    Debug.WriteLine("SendIP: Не удалось определить IP телефона");
                    return false;
                }

                string request = $"echo \"ip \n{phoneIp}\" > ./DataApp/ip.csv";

                // Добавляем в очередь
                _commandQueue.Enqueue(request);

                if (!_isRunning)
                {
                    _isRunning = true;
                    _ = ProcessQueueAsync(token);
                }

                Debug.WriteLine($"SendIP queued: IP={phoneIp}");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("SendIP: Операция отменена");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendIP failed: {ex.Message}");
                Cleanup();
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
    }
}
