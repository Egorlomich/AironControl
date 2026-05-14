using AironControl.Adapters;
using AironControl.Model;
using AironControl.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace AironControl.Core
{
    public class RobotConnectionManager
    {
        public ObservableCollection<RobotConnection> Connections { get; } = new();

        public event Action<RobotConnection>? CurrentRobotChanged;
        private readonly SshProtocolAdapter _sshAdapter;
        public RobotConnection? CurrentRobot { get; private set; }

        public RobotConnectionManager(IConnectionSettingsService settingsService)
        {
            _sshAdapter = new SshProtocolAdapter(settingsService);
            CurrentRobot = new RobotConnection(_sshAdapter);
        }

        public bool IsConnected => CurrentRobot?.Info.Status == "Connected";

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            if (CurrentRobot?.Adapter != null)
                await CurrentRobot.Adapter.ConnectAsync(ct);
        }

        public async Task SendCommandAsync(CommandRequest cmd, CancellationToken ct = default)
        {
            if (CurrentRobot?.Adapter != null)
                await CurrentRobot.Adapter.SendCommandAsync(cmd, ct);
        }

        public async Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default)
        {
            if (CurrentRobot?.Adapter == null) return "Error: No robot connected";
            return await CurrentRobot.Adapter.ExecuteCommandAsync(command, ct);
        }

        public async Task<bool> ChangeModeAsync(int mode, CancellationToken ct = default)
        {
            if (CurrentRobot?.Adapter == null) return false;
            return await CurrentRobot.Adapter.ChangeMode(mode, ct);
        }

        public async Task<bool> SendIPAsync(CancellationToken ct = default)
        {
            if (CurrentRobot?.Adapter == null) return false;
            return await CurrentRobot.Adapter.SendIP(ct);
        }

        public void SendJoystickValues(double x, double y, int rotate)
        {
            if (CurrentRobot?.Adapter is SshProtocolAdapter ssh)
                ssh.SendJoystickValues(x, y, rotate);
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
