using GamingHub.Core.Models;
using GamingHub.Core.Services;
using GamingHub.Core.Validation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GamingHub.App.Views;

public sealed partial class CheckoutDialog : ContentDialog
{
    private readonly StoreService _storeService;

    /// <summary>Ticket de la compra confirmada. Es null si el usuario canceló o el pago falló.</summary>
    public Ticket? CompletedTicket { get; private set; }

    public CheckoutDialog()
    {
        this.InitializeComponent();
        // Conecta con tu servicio de compras a través de App.xaml.cs
        _storeService = App.GetService<StoreService>();

        BankInput.ItemsSource = PaymentValidator.CommonBanks;
        ExpiryMonthInput.ItemsSource = Enumerable.Range(1, 12).Select(month => $"{month:00}").ToList();
        var currentYear = DateTime.UtcNow.Year;
        ExpiryYearInput.ItemsSource = Enumerable.Range(currentYear, 21).Select(year => year.ToString()).ToList();
        ExpiryMonthInput.SelectedIndex = DateTime.UtcNow.Month - 1;
        ExpiryYearInput.SelectedIndex = 0;
        MethodSelector.SelectionChanged += (_, _) => UpdateMethodPanels();
        Opened += async (_, _) => await ShowTotalAsync();
    }

    private bool IsTransfer => MethodSelector.SelectedIndex == 1;

    private void UpdateMethodPanels()
    {
        CardPanel.Visibility = IsTransfer ? Visibility.Collapsed : Visibility.Visible;
        TransferPanel.Visibility = IsTransfer ? Visibility.Visible : Visibility.Collapsed;
        ErrorBanner.IsOpen = false;
    }

    private async Task ShowTotalAsync()
    {
        var cart = await _storeService.GetCartAsync();
        if (cart.Success && cart.Value!.Count > 0)
            TotalText.Text = $"Total a pagar: {cart.Value.Sum(i => i.Price):0.00} {cart.Value[0].Currency}";
    }

    private PaymentInfo BuildPayment()
    {
        if (IsTransfer)
        {
            var bank = string.IsNullOrWhiteSpace(BankInput.Text) ? BankInput.SelectedItem as string : BankInput.Text;
            return new PaymentInfo(PaymentMethod.Transfer)
            {
                PayerName = PayerNameInput.Text,
                Bank = bank,
                AccountNumber = AccountInput.Text,
                Subject = SubjectInput.Text
            };
        }
        var month = ExpiryMonthInput.SelectedItem as string ?? "";
        var year = ExpiryYearInput.SelectedItem as string ?? "";
        var expiry = month.Length == 2 && year.Length == 4 ? $"{month}/{year[2..]}" : "";
        return new PaymentInfo(PaymentMethod.Card)
        {
            CardNumber = CardNumberInput.Text,
            CardHolder = CardHolderInput.Text,
            CardExpiry = expiry
        };
    }

    // EVENTO: Al darle clic al botón "Confirmar y Pagar"
    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 1. Evita que la ventana se cierre de golpe
        args.Cancel = true;

        // 2. Validación centralizada (misma que vuelve a aplicar el servicio)
        var payment = BuildPayment();
        var validation = PaymentValidator.Validate(payment, DateTime.UtcNow);
        if (!validation.IsValid)
        {
            ShowError(validation.FirstError);
            return;
        }

        // 3. El CVV solo se valida aquí: no se envía al servicio ni se guarda en ningún lado
        if (!IsTransfer)
        {
            var cvv = CvvInput.Password.Trim();
            if (cvv.Length is not (3 or 4) || !cvv.All(PaymentValidator.IsAsciiDigit))
            {
                ShowError("El CVV debe contener 3 o 4 números válidos.");
                return;
            }
        }

        // --- INICIA SIMULACIÓN DE PAGO ---
        ErrorBanner.IsOpen = false;
        LoadingText.Text = IsTransfer ? "Registrando la transferencia..." : "Conectando con el banco emisor...";
        SetBusy(true);

        // Retraso de 3 segundos
        await Task.Delay(3000);

        // Llamada a la capa Core para registrar la compra simulada
        var result = await _storeService.CheckoutAsync(payment);

        if (result.Success)
        {
            CompletedTicket = result.Value!.Ticket;
            // No dejamos datos de tarjeta en los controles
            CardNumberInput.Text = "";
            CvvInput.Password = "";
            // Compra exitosa: Cierra la ventana modal (StorePage mostrará el ticket)
            this.Hide();
        }
        else
        {
            // Error de negocio (ej. carrito vacío), muestra el error y reactiva controles
            ShowError(string.IsNullOrWhiteSpace(result.Message) ? "Error al procesar el pago." : result.Message);
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        LoadingPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        IsPrimaryButtonEnabled = !busy;
        MethodSelector.IsEnabled = !busy;
        CardHolderInput.IsEnabled = !busy;
        CardNumberInput.IsEnabled = !busy;
        ExpiryMonthInput.IsEnabled = !busy;
        ExpiryYearInput.IsEnabled = !busy;
        CvvInput.IsEnabled = !busy;
        PayerNameInput.IsEnabled = !busy;
        BankInput.IsEnabled = !busy;
        AccountInput.IsEnabled = !busy;
        SubjectInput.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        ErrorBanner.Message = message;
        ErrorBanner.IsOpen = true;
    }
}
