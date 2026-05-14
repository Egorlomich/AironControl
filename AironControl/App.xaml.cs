namespace AironControl
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Light;
            MainPage = new NavigationPage(new MainPage());
        }
    }
}
