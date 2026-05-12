// Adapters/SshProtocolAdapter.cs
using AironControl.Model;
using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace AironControl.Adapters
{
    public class SshProtocolAdapter : IRobotAdapter, IDisposable
    {
        private readonly SshConnection _sshConnection;
        private readonly Subject<object> _telemetrySubject = new();
        private readonly RobotInfo _robotInfo = new();

        public string ProtocolName => "SSH";
        public RobotInfo RobotInfo => _robotInfo;

        public SshProtocolAdapter()
        {
            _sshConnection = new SshConnection();
            _robotInfo.ProtocolType = "SSH";
            _robotInfo.Name = "Robot SSH";
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            _robotInfo.Status = "Connecting";
            bool success = await _sshConnection.ConnectAsync(ct); // Добавим этот метод ниже
            _robotInfo.Status = success ? "Connected" : "Disconnected";
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            _sshConnection.Dispose();
            _robotInfo.Status = "Disconnected";
            return Task.CompletedTask;
        }

        public async Task SendCommandAsync(CommandRequest command, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(command.Command))
                return;

            // Передаём в твою очередь
            _sshConnection.EnqueueCommand(command.Command);
            await Task.CompletedTask;
        }

        public Task<bool> ChangeMode(int mode, CancellationToken cancellationToken = default)
        {
            return _sshConnection.ChangeMode(mode, cancellationToken);
        }

        public Task<bool> SendIP(CancellationToken token = default)
        {
            return _sshConnection.SendIP(token);
        }

        public async Task ReconnectAsync(CancellationToken ct = default)
        {
            await DisconnectAsync(ct);
            await Task.Delay(500, ct);
            await ConnectAsync(ct);
        }

        public IObservable<object> SubscribeTelemetry()
        {
            return _telemetrySubject.AsObservable();
        }

        public void Dispose()
        {
            _sshConnection.Dispose();
            _telemetrySubject.Dispose();
        }
    }
}