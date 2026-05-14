using AironControl.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AironControl.ViewModels
{
    public class JoystickVM : INotifyPropertyChanged
    {
        private readonly ConnectionVM _connectionVm;
        private readonly RobotConnectionManager _manager;

        private string _statusSource = "red_light.svg";
        public string StatusSource { get => _statusSource; set => SetField(ref _statusSource, value); }

        public bool IsConnected => _connectionVm.IsConnected;

        public ICommand ChangeModeCommand { get; }

        public JoystickVM(ConnectionVM connectionVm, RobotConnectionManager manager)
        {
            _connectionVm = connectionVm;
            _manager = manager;
            ChangeModeCommand = new Command<int>(async mode => await ChangeModeAsync(mode));
        }

        public async Task ConnectAsync()
        {
            await _connectionVm.ConnectAsync();

            if (IsConnected)
            {
                StatusSource = "green_light.svg";
                await Task.WhenAll(
                    _manager.ChangeModeAsync(22),
                    _manager.ChangeModeAsync(2),
                    _manager.SendIPAsync()
                );
            }
            else
            {
                StatusSource = "red_light.svg";
            }
        }

        public void SendJoystickValues(double x, double y, int rotate)
            => _manager.SendJoystickValues(x, y, rotate);

        public async Task ChangeModeAsync(int mode)
            => await _manager.ChangeModeAsync(mode);

        public async Task SendIPAsync()
            => await _manager.SendIPAsync();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
