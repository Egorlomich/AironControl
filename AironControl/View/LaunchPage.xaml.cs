using AironControl.ViewModels;

namespace AironControl
{
    public partial class LaunchPage : ContentPage
    {
        private readonly LaunchPageViewModel _vm;

        public LaunchPage() : this(
            IPlatformApplication.Current!.Services.GetRequiredService<LaunchPageViewModel>())
        { }

        public LaunchPage(LaunchPageViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            _vm.AlertAsync = DisplayAlertAsync;
            _vm.ConfirmAsync = DisplayAlertAsync;
            BindingContext = _vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitializeAsync();
        }

        private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
    }
}