using AironControl.Adapters;
using AironControl.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace AironControl.Core
{
    // Core/RobotConnectionManager.cs
    public class RobotConnectionManager
    {
        public ObservableCollection<RobotConnection> Connections { get; } = new();

        public event Action<RobotConnection>? CurrentRobotChanged;
        private readonly SshProtocolAdapter _sshAdapter;
        public RobotConnection? CurrentRobot { get; private set; }

        public RobotConnectionManager()
        {
            _sshAdapter = new SshProtocolAdapter();
            CurrentRobot = new RobotConnection(_sshAdapter);
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            if (CurrentRobot?.Adapter != null)
                await CurrentRobot.Adapter.ConnectAsync(ct);
        }

        public async Task SendCommandAsync(CommandRequest cmd, CancellationToken ct = default)
        {
            await CurrentRobot?.Adapter.SendCommandAsync(cmd, ct)!;
        }
    }

    // Вспомогательный класс
    public class RobotConnection
    {
        public IRobotAdapter Adapter { get; }
        public RobotInfo Info => Adapter.RobotInfo;

        public RobotConnection(IRobotAdapter adapter)
        {
            Adapter = adapter;
        }
    }
}
