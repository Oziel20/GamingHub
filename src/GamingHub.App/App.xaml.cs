using GamingHub.App.Logging;
using GamingHub.App.ViewModels;
using GamingHub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace GamingHub.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow Main { get; private set; } = null!;
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    public App()
    {
        InitializeComponent();
        Services = BuildServices();
        UnhandledException += (_, e) =>
        {
            // Recoverable UI errors must not close the app; log only the type (no stack traces, no data).
            Services.GetRequiredService<ILoggerFactory>().CreateLogger("App").LogError("Unhandled UI exception: {Type}", e.Exception.GetType().Name);
            e.Handled = true;
        };
    }

    private static IServiceProvider BuildServices()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamingHub");
        var sc = new ServiceCollection();
        sc.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Information);
            b.AddDebug();
            b.AddProvider(new FileLoggerProvider(Path.Combine(root, "logs")));
        });
        sc.AddGamingHubCore(Path.Combine(root, "data"));
        sc.AddTransient<LoginViewModel>();
        sc.AddTransient<ShellViewModel>();
        sc.AddTransient<DashboardViewModel>();
        sc.AddTransient<LibraryViewModel>();
        sc.AddTransient<StoreViewModel>();
        sc.AddTransient<SocialViewModel>();
        sc.AddTransient<ProfileViewModel>();
        sc.AddTransient<SettingsViewModel>();
        return sc.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Main = new MainWindow();
        Main.Activate();
    }
}
