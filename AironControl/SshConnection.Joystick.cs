using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        private (double x, double y, int rotate) _lastSent;
        public Task SendJoystickValuesAsync(double x, double y, int rotate)
        {
            if (Math.Abs(x - _lastSentValues.x) < 0.1 &&
                Math.Abs(y - _lastSentValues.y) < 0.1 &&
                rotate == _lastSentValues.rotate)
            {
                return Task.CompletedTask;
            }

            _lastSentValues = (x, y, rotate);

            var cmd = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "printf \"x,y,angle\\n{0:0.00},{1:0.00},{2}\\n\" > ~/DataApp/motorValues.csv",
                x, y, rotate);

            _commandQueue.Enqueue(cmd);
            return Task.CompletedTask;
        }
        public async Task<string> GoJoystickValues(double x, double y, int rotate, CancellationToken cancellationToken = default)
        {
            bool skipDeduplication = false;

            // Если x и y равны 0 - пропускаем дедупликацию
            if (x == 0 && y == 0)
            {
                skipDeduplication = true;
            }

            // Дедупликация (пропускаем если skipDeduplication = true)
            if (!skipDeduplication && 
                Math.Abs(x - _lastSent.x) < 0.04 &&
                Math.Abs(y - _lastSent.y) < 0.04 &&
                rotate == _lastSent.rotate)
            {
                return "No change (deduplicated)";
            }

            _lastSent = (x, y, rotate);

            // 2. Формируем команду (точно такая же, как была раньше)
            var request = string.Format(
                CultureInfo.InvariantCulture,
                "printf \"x,y,angle\\n{0:0.00},{1:0.00},{2}\\n\" > ~/DataApp/motorValues.csv",
                x, y, rotate);

            // 3. Добавляем в очередь (это мгновенная операция)
            _commandQueue.Enqueue(request);

            // 4. Если обработчик очереди ещё не запущен — запускаем его
            if (!_isRunning)
            {
                _isRunning = true;
                _ = ProcessQueueAsync(cancellationToken);
            }

            Debug.WriteLine($"Queued: X={x:F2}, Y={y:F2}, R={rotate}");

            // Возвращаем быстро — реальная отправка идёт в фоне
            return "Queued";
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
                    await Task.Delay(10, ct); // лёгкий breathing, чтобы не жрать CPU
                }
            }
        }
    }
}
