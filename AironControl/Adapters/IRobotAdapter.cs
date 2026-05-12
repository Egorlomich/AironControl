using AironControl.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace AironControl.Adapters
{
    // Adapters/IRobotAdapter.cs
    public interface IRobotAdapter
    {
        Task ConnectAsync(CancellationToken ct = default);
        Task DisconnectAsync(CancellationToken ct = default);
        Task SendCommandAsync(CommandRequest command, CancellationToken ct = default);
        Task<bool> ChangeMode(int mode, CancellationToken cancellationToken = default);
        Task<bool> SendIP(CancellationToken token = default);
        Task ReconnectAsync(CancellationToken ct = default);

        string ProtocolName { get; }
        RobotInfo RobotInfo { get; }

        IObservable<object> SubscribeTelemetry();
    }
}
