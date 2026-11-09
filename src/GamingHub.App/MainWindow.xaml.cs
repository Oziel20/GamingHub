using GamingHub.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App;

public sealed partial class MainWindow : Window
{
    public Frame Root => RootFrame;

    public MainWindow()
    {
        InitializeComponent();
        Title = "GamingHub";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
        RootFrame.Navigate(typeof(LoginPage));
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon (1).ico"));
    }
}
