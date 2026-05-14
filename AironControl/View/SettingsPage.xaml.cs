using AironControl.ViewModels;

namespace AironControl;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsVM _vm;

    public SettingsPage() : this(IPlatformApplication.Current!.Services.GetRequiredService<SettingsVM>()) { }

    public SettingsPage(SettingsVM vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.AlertAsync = DisplayAlertAsync;
        _vm.ConfirmAsync = DisplayAlertAsync;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Initialize();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
}
