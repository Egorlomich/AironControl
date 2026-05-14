using AironControl.Core;
using AironControl.Services;
using AironControl.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace AironControl
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>().UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                }).ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android => android
                        .OnCreate((activity, bundle) =>
                        {
                            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;
                        })
                        .OnResume(activity =>
                        {
                            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;
                        }));
#endif
                });

            // Core & Services (singletons — shared state across app lifetime)
            builder.Services.AddSingleton<RobotConnectionManager>();
            builder.Services.AddSingleton<IConnectionSettingsService, ConnectionSettingsService>();
            builder.Services.AddSingleton<IRos2Service, Ros2Service>();

            // ViewModels
            builder.Services.AddSingleton<ConnectionVM>();
            builder.Services.AddSingleton<JoystickVM>();
            builder.Services.AddTransient<LaunchPageViewModel>();
            builder.Services.AddTransient<SettingsVM>();

            // Pages
            builder.Services.AddTransient<LaunchPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<JoystickPageWithVideo>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
