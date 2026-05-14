using AironControl.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AironControl.ViewModels
{
    public class ConnectionVM : INotifyPropertyChanged
    {
        private readonly RobotConnectionManager _manager;

        public bool IsConnected => _manager.IsConnected;
        public string Status => _manager.CurrentRobot?.Info.Status ?? "Disconnected";
        public string RobotName => _manager.CurrentRobot?.Info.Name ?? string.Empty;
        public string LastError => _manager.CurrentRobot?.Info.LastError ?? string.Empty;

        public ICommand ConnectCommand { get; }

        public ConnectionVM(RobotConnectionManager manager)
        {
            _manager = manager;
            ConnectCommand = new Command(async () => await ConnectAsync());
        }

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            await _manager.ConnectAsync(ct);
            RefreshStatus();
            return IsConnected;
        }

        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(RobotName));
            OnPropertyChanged(nameof(LastError));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
