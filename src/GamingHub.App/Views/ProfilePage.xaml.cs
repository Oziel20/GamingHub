using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class ProfilePage : Page
{
    public ProfileViewModel ViewModel { get; } = App.GetService<ProfileViewModel>();
    public ProfilePage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }

    private async void Avatar_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickerHelper.PickFileAsync(".png", ".jpg", ".jpeg");
        if (path is not null) ViewModel.SetAvatarCommand.Execute(path);
    }
}
