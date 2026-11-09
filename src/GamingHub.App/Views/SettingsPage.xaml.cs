using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel.LoggedOut += OnLoggedOut;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
        Unloaded += (_, _) => ViewModel.LoggedOut -= OnLoggedOut;
    }

    private void OnLoggedOut() => App.Main.Root.Navigate(typeof(LoginPage));
    private static Guid Id(object sender) => (sender as Button)?.Tag is Guid g ? g : Guid.Empty;
    private void Unlock_Click(object sender, RoutedEventArgs e) => ViewModel.UnlockCommand.Execute(Id(sender));
    private void Revoke_Click(object sender, RoutedEventArgs e) => ViewModel.RevokeCommand.Execute(Id(sender));
    private void RemoveRoot_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveRootCommand.Execute((sender as Button)?.Tag as string);
    private void RemoveTyped_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveRootCommand.Execute(ViewModel.NewRoot);
    private async void BrowseRoot_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickerHelper.PickFolderAsync();
        if (path is not null) ViewModel.NewRoot = path;
    }
}
