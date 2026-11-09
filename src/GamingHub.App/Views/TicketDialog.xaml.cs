using System.Diagnostics;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

/// <summary>Muestra el ticket, guarda una copia (.txt y .html) y permite imprimirlo.</summary>
public sealed partial class TicketDialog : ContentDialog
{
    private readonly Ticket _ticket;
    private readonly TicketFiles _files;

    public TicketDialog(Ticket ticket)
    {
        this.InitializeComponent();
        _ticket = ticket;
        _files = App.GetService<TicketFiles>();
        TicketText.Text = TicketRenderer.ToText(ticket);
        Opened += async (_, _) => await SaveCopyAsync();
    }

    private async Task SaveCopyAsync()
    {
        try
        {
            await _files.SaveAsync(_ticket);
            SavedText.Text = $"Copia guardada en: {_files.TicketsDir}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SavedText.Text = "No se pudo guardar una copia del ticket en disco.";
        }
    }

    // Abre el ticket en el navegador predeterminado y lanza el cuadro de impresión
    // (desde ahí también se puede elegir «Guardar como PDF»).
    private async void Print_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // el diálogo se queda abierto
        try
        {
            var path = await _files.WritePrintFileAsync(_ticket);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ShowError("No se pudo abrir el ticket para imprimir. Usa «Abrir carpeta» y abre el archivo .html.");
        }
    }

    private void OpenFolder_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        try
        {
            Directory.CreateDirectory(_files.TicketsDir);
            Process.Start(new ProcessStartInfo(_files.TicketsDir) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ShowError("No se pudo abrir la carpeta de tickets.");
        }
    }

    private void ShowError(string message)
    {
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
