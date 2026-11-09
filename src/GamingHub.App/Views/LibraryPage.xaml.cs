using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GamingHub.App.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; } = App.GetService<LibraryViewModel>();
    public LibraryPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }

    private static string? Id(object sender) => (sender as Button)?.Tag as string;
    private void Play_Click(object sender, RoutedEventArgs e) => ViewModel.PlayCommand.Execute(Id(sender));
    private void Install_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleInstallCommand.Execute(Id(sender));
    private void Favorite_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleFavoriteCommand.Execute(Id(sender));
    private void Remove_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveCommand.Execute(Id(sender));
    private void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) ViewModel.SearchCommand.Execute(null);
    }
    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickerHelper.PickFileAsync(".exe");
        if (path is not null) ViewModel.NewPath = path;
    }
}
