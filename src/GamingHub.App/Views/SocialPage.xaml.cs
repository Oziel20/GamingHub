using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class SocialPage : Page
{
    public SocialViewModel ViewModel { get; } = App.GetService<SocialViewModel>();
    public SocialPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }

    private static Guid Id(object sender) => (sender as Button)?.Tag is Guid g ? g : Guid.Empty;
    private void Accept_Click(object sender, RoutedEventArgs e) => ViewModel.AcceptCommand.Execute(Id(sender));
    private void Reject_Click(object sender, RoutedEventArgs e) => ViewModel.RejectCommand.Execute(Id(sender));
    private void Remove_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveCommand.Execute(Id(sender));
    private void Block_Click(object sender, RoutedEventArgs e) => ViewModel.BlockCommand.Execute(Id(sender));
}
