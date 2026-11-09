using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; } = App.GetService<LoginViewModel>();

    public LoginPage()
    {
        InitializeComponent();
        ViewModel.LoggedIn += OnLoggedIn;
        ViewModel.CredentialsCleared += OnCleared;
        Unloaded += (_, _) => { ViewModel.LoggedIn -= OnLoggedIn; ViewModel.CredentialsCleared -= OnCleared; };
    }

    // PasswordBox cannot be data-bound, so UI events forward the value to the view model.
    private void Password_Changed(object sender, RoutedEventArgs e) => ViewModel.Password = ((PasswordBox)sender).Password;
    private void Confirm_Changed(object sender, RoutedEventArgs e) => ViewModel.ConfirmPassword = ((PasswordBox)sender).Password;
    private void OnCleared() { PasswordInput.Password = ""; ConfirmInput.Password = ""; }
    private void OnLoggedIn() => App.Main.Root.Navigate(typeof(ShellPage));
}
