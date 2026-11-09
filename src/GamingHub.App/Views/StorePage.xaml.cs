using GamingHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System; // Necesario para EventArgs
using GamingHub.Core.Models;

namespace GamingHub.App.Views;

public sealed partial class StorePage : Page
{
    public StoreViewModel ViewModel { get; } = App.GetService<StoreViewModel>();

    public StorePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private static string? Id(object sender) => (sender as Button)?.Tag as string;

    private void AddCart_Click(object sender, RoutedEventArgs e) => ViewModel.AddToCartCommand.Execute(Id(sender));

    private void RemoveCart_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveFromCartCommand.Execute(Id(sender));

    private void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) ViewModel.SearchCommand.Execute(null);
    }

    // Abre la ventana de pago (tarjeta / transferencia)
    private async void PagarButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CheckoutDialog();
        dialog.XamlRoot = this.XamlRoot;

        // Muestra el cuadro de diálogo
        await dialog.ShowAsync();

        // Al terminar el pago (si fue exitoso y se cerró), recarga los datos de la tienda y el carrito
        await ViewModel.LoadAsync();

        // Si hubo compra, muestra el ticket (un ContentDialog a la vez: el de pago ya se cerró)
        if (dialog.CompletedTicket is { } ticket) await ShowTicketAsync(ticket);
    }

    // Reimprimir / ver el ticket de una compra del historial
    private async void ViewTicket_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not Guid orderId) return;
        var ticket = await ViewModel.GetTicketAsync(orderId);
        if (ticket is not null) await ShowTicketAsync(ticket);
    }

    private async System.Threading.Tasks.Task ShowTicketAsync(Ticket ticket)
    {
        var dialog = new TicketDialog(ticket) { XamlRoot = this.XamlRoot };
        await dialog.ShowAsync();
    }
}
