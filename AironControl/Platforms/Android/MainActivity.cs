using Android.App;
using Android.Content.PM;
using Android.OS;

namespace AironControl
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
    ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
