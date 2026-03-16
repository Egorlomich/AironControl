using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net.Sockets;
namespace AironControl
{
    /// <summary>
    /// Оптимизированный SSH-клиент для частых команд джойстика
    /// Использует ShellStream + очередь + троттлинг + reconnect
    /// </summary>
    internal partial class SshConnection
    {
        public Host LoadActiveConnection()
        {
            try
            {
                if (!File.Exists(_activeConnectionPath))
                {
                    Debug.WriteLine($"Файл {_activeConnectionPath} не найден → дефолтный Host");
                    return new Host();
                }

                string json = File.ReadAllText(_activeConnectionPath);
                Debug.WriteLine($"Прочитан JSON ({json.Length} символов):\n{json}");

                var host = JsonSerializer.Deserialize<Host>(json);


                _activeConnectionName = host.settingsName;
                Debug.WriteLine($"Успешно загружен: {host.settingsName} → {host.host}:{host.port}");
                return host;
            }
            catch (JsonException jex)
            {
                Debug.WriteLine($"Ошибка десериализации JSON: {jex.Message}\nJSON: {File.ReadAllText(_activeConnectionPath)}");
                try { File.Delete(_activeConnectionPath); } catch { }
                return new Host();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadActiveConnection общая ошибка: {ex.Message}");
                try { File.Delete(_activeConnectionPath); } catch { }
                return new Host();
            }
        }
        public async Task<bool> ConnectToDevice(CancellationToken cancellationToken = default)
        {
            try
            {
                Debug.WriteLine($"Попытка подключения к {_host.host}:{_host.port} как {_host.user}");

                // Если уже подключено — сразу возвращаем true
                if (IsConnected)
                {
                    Debug.WriteLine("Уже подключено");
                    return true;
                }

                // Инициируем подключение (внутри SshConnection это безопасно и с таймаутом)
                // Можно вызвать приватный метод ReconnectAsync, но для совместимости делаем публичный вызов
                await Task.Run(() =>
                {
                    // Здесь просто заставляем класс попытаться подключиться
                    // (в реальности он сам подключится при первой команде, но мы форсируем)
                    var dummyTask = SendJoystickValuesAsync(0, 0, 0); // или любой другой метод
                }, cancellationToken);

                bool connected = IsConnected;

                Debug.WriteLine($"Результат подключения: {connected}");

                return connected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConnectToDevice failed: {ex.GetType().Name}: {ex.Message}");
                return false;
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
    }
}
