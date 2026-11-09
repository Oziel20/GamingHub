using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; } = App.GetService<ShellViewModel>();
    private readonly DispatcherTimer _heartbeat = new() { Interval = TimeSpan.FromSeconds(60) };

    public ShellPage()
    {
        InitializeComponent();
        ViewModel.LoggedOut += OnLoggedOut;
        _heartbeat.Tick += async (_, _) => await ViewModel.RefreshAsync();
        Loaded += async (_, _) =>
        {
            Nav.SelectedItem = Nav.MenuItems[0];
            ContentFrame.Navigate(typeof(DashboardPage));
            await ViewModel.RefreshAsync();
            _heartbeat.Start();
        };
        Unloaded += (_, _) => { _heartbeat.Stop(); ViewModel.LoggedOut -= OnLoggedOut; };
    }

    private void OnLoggedOut() => App.Main.Root.Navigate(typeof(LoginPage));

    private async void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var target = (args.InvokedItemContainer?.Tag as string) switch
        {
            "dashboard" => typeof(DashboardPage),
            "library" => typeof(LibraryPage),
            "store" => typeof(StorePage),
            "social" => typeof(SocialPage),
            "profile" => typeof(ProfilePage),
            "settings" => typeof(SettingsPage),
            _ => null
        };
        if (target is not null) ContentFrame.Navigate(target);
        await ViewModel.RefreshAsync();
    }
}
