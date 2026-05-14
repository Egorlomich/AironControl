using AironControl.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace AironControl.Adapters
{
    public class Ros2ProtocolAdapter : IRobotAdapter
    {
        private readonly string _rcics; // из диаграммы
        public string ProtocolName => "ROS2";
        public RobotInfo RobotInfo { get; } = new();

        public Task ConnectAsync(CancellationToken ct = default)
        {
            // TODO: ROS2 реализация
            //RobotInfo.UpdateStatus("Connected");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SendCommandAsync(CommandRequest command, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<bool> ChangeMode(int mode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> SendIP(CancellationToken token = default) => Task.FromResult(true);
        public Task ReconnectAsync(CancellationToken ct = default) => ConnectAsync(ct);

// public IObservable<object> SubscribeTelemetry() => Observable.Empty<object>();
    }

    // Аналогично можно сделать TcpProtocolAdapter, MqttProtocolAdapter, SshProtocolAdapter, UdpProtocolAdapter
}
