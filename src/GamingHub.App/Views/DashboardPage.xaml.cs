using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = App.GetService<DashboardViewModel>();
    public DashboardPage() { InitializeComponent(); Loaded += async (_, _) => await ViewModel.LoadAsync(); }
}
