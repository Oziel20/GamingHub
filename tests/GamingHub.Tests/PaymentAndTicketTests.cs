using FluentAssertions;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using GamingHub.Core.Validation;
using Xunit;

namespace GamingHub.Tests;

public class PaymentValidatorTests
{
    // Mismo instante que TestClock: 1 de junio de 2026.
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PaymentInfo Card(string number = "4111 1111 1111 1111", string holder = "Ana Pérez", string expiry = "12/28") =>
        new(PaymentMethod.Card) { CardNumber = number, CardHolder = holder, CardExpiry = expiry };

    private static PaymentInfo Transfer(string payer = "Luis Gómez", string bank = "BBVA",
        string account = "012345678901234567", string subject = "Compra en GamingHub") =>
        new(PaymentMethod.Transfer) { PayerName = payer, Bank = bank, AccountNumber = account, Subject = subject };

    [Fact] public void Valid_card_and_transfer_pass()
    {
        PaymentValidator.Validate(Card(), Now).IsValid.Should().BeTrue();
        PaymentValidator.Validate(Transfer(), Now).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("4111 1111 1111 111")]      // 15 dígitos
    [InlineData("4111 1111 1111 111a")]     // letra
    [InlineData("")]
    public void Bad_card_numbers_are_rejected(string number) =>
        PaymentValidator.Validate(Card(number: number), Now).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("13/28")]   // mes inválido
    [InlineData("1228")]    // sin diagonal
    [InlineData("05/26")]   // vencida (hoy es junio 2026)
    [InlineData("")]
    public void Bad_expiry_is_rejected(string expiry) =>
        PaymentValidator.Validate(Card(expiry: expiry), Now).IsValid.Should().BeFalse();

    [Fact] public void Current_month_is_not_expired() =>
        PaymentValidator.Validate(Card(expiry: "06/26"), Now).IsValid.Should().BeTrue();

    [Fact] public void Card_holder_is_required_and_letters_only()
    {
        PaymentValidator.Validate(Card(holder: ""), Now).IsValid.Should().BeFalse();
        PaymentValidator.Validate(Card(holder: "Ana <script>"), Now).IsValid.Should().BeFalse();
    }

    [Fact] public void Transfer_requires_all_fields()
    {
        PaymentValidator.Validate(Transfer(payer: ""), Now).IsValid.Should().BeFalse();
        PaymentValidator.Validate(Transfer(bank: ""), Now).IsValid.Should().BeFalse();
        PaymentValidator.Validate(Transfer(account: "1234"), Now).IsValid.Should().BeFalse();
        PaymentValidator.Validate(Transfer(subject: ""), Now).IsValid.Should().BeFalse();
        PaymentValidator.Validate(Transfer(subject: new string('x', 61)), Now).IsValid.Should().BeFalse();
    }

    [Fact] public void Missing_or_none_method_is_rejected()
    {
        PaymentValidator.Validate(null, Now).IsValid.Should().BeFalse();
        PaymentValidator.Validate(new PaymentInfo(PaymentMethod.None), Now).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("4111111111111111", "Visa")]
    [InlineData("5100000000000000", "Mastercard")]
    [InlineData("2221000000000000", "Mastercard")]
    [InlineData("9999999999999999", "Tarjeta")]
    public void Card_brand_detection(string number, string brand) =>
        PaymentValidator.CardBrandOf(number).Should().Be(brand);
}

public class TicketTests
{
    private const string FullCard = "4111111111111111";

    private static async Task<(TestEnv env, StoreService store, CatalogItemDto game)> SetupAsync(string user = "buyer_one")
    {
        var e = new TestEnv(); await e.RegisterAndLoginAsync(user);
        var store = e.Store();
        var game = (await store.SearchCatalogAsync("", null, 1, 50)).Value!.Items.First(i => i.Price > 0);
        (await store.AddToCartAsync(game.Id)).Success.Should().BeTrue();
        return (e, store, game);
    }

    private static PaymentInfo Card() =>
        new(PaymentMethod.Card) { CardNumber = FullCard, CardHolder = "Ana Pérez", CardExpiry = "12/28" };

    private static PaymentInfo Transfer(string subject = "Compra en GamingHub") =>
        new(PaymentMethod.Transfer) { PayerName = "Luis Gómez", Bank = "BBVA", AccountNumber = "012345678901234567", Subject = subject };

    [Fact] public async Task Card_checkout_returns_ticket_without_full_card_number()
    {
        var (e, store, game) = await SetupAsync(); using var _ = e;
        var res = await store.CheckoutAsync(Card());
        res.Success.Should().BeTrue();
        var t = res.Value!.Ticket!;
        t.Number.Should().StartWith("GH-");
        t.Method.Should().Be(PaymentMethod.Card);
        t.Total.Should().Be(game.Price);
        t.Lines.Should().ContainSingle().Which.Developer.Should().NotBeNullOrEmpty();   // ficha técnica del juego

        var text = TicketRenderer.ToText(t);
        text.Should().Contain(game.Title).And.Contain("Visa").And.Contain("1111").And.Contain("Ana Pérez");
        text.Should().NotContain(FullCard);
        TicketRenderer.ToHtml(t, false).Should().NotContain(FullCard);
    }

    [Fact] public async Task Card_number_is_never_written_to_disk()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        (await store.CheckoutAsync(Card())).Success.Should().BeTrue();
        foreach (var f in Directory.EnumerateFiles(e.Dir, "*", SearchOption.AllDirectories))
            File.ReadAllText(f).Should().NotContain(FullCard, $"{Path.GetFileName(f)} no debe contener el número completo");
    }

    [Fact] public async Task Transfer_checkout_includes_payer_bank_subject_and_tracking_key()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        var t = (await store.CheckoutAsync(Transfer("Pago de juego"))).Value!.Ticket!;
        t.Method.Should().Be(PaymentMethod.Transfer);
        t.TrackingKey.Should().NotBeNullOrEmpty();
        var text = TicketRenderer.ToText(t);
        text.Should().Contain("Luis Gómez").And.Contain("BBVA").And.Contain("Pago de juego").And.Contain(t.TrackingKey!);
        text.Should().Contain("4567");                         // últimos 4 de la CLABE
        text.Should().NotContain("012345678901234567");
    }

    [Fact] public async Task Invalid_payment_is_rejected_and_nothing_is_bought()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        var bad = Card() with { CardNumber = "1234" };
        (await store.CheckoutAsync(bad)).Code.Should().Be(ErrorCodes.Invalid);
        (await store.GetPurchasesAsync()).Value!.Should().BeEmpty();
        (await store.GetCartAsync()).Value!.Should().HaveCount(1);          // el carrito sigue intacto
        (await store.CheckoutAsync(Card())).Success.Should().BeTrue();       // y se puede reintentar
    }

    [Fact] public async Task Ticket_can_be_rebuilt_from_history_and_matches()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        var original = (await store.CheckoutAsync(Card())).Value!.Ticket!;
        var purchase = (await store.GetPurchasesAsync()).Value!.Single();
        purchase.HasTicket.Should().BeTrue();
        purchase.PaymentText.Should().Be("Visa ****1111");

        var again = (await store.GetTicketAsync(purchase.OrderId)).Value!;
        again.Number.Should().Be(original.Number);
        TicketRenderer.ToText(again).Should().Be(TicketRenderer.ToText(original));
    }

    [Fact] public async Task Tickets_are_private_to_their_owner()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        await store.CheckoutAsync(Card());
        var orderId = (await store.GetPurchasesAsync()).Value!.Single().OrderId;
        await e.Auth.LogoutAsync(); await e.RegisterAndLoginAsync("other_user");
        (await store.GetTicketAsync(orderId)).Code.Should().Be(ErrorCodes.NotFound);
        (await store.GetTicketAsync(Guid.Empty)).Success.Should().BeFalse();
    }

    [Fact] public async Task Checkout_without_payment_still_works_for_legacy_callers()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        var res = await store.CheckoutAsync();
        res.Success.Should().BeTrue();
        res.Value!.Ticket!.Method.Should().Be(PaymentMethod.None);
    }

    [Fact] public async Task Text_ticket_fits_42_columns_and_html_escapes_user_text()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        var t = (await store.CheckoutAsync(Transfer("<script>alert(1)</script> & más"))).Value!.Ticket!;
        foreach (var line in TicketRenderer.ToText(t).Split('\n'))
            line.TrimEnd('\r').Length.Should().BeLessThanOrEqualTo(42, $"«{line}» se sale del ancho del ticket");
        var html = TicketRenderer.ToHtml(t, true);
        html.Should().NotContain("<script>alert(1)");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact] public async Task Ticket_files_are_saved_with_safe_names()
    {
        var (e, store, _) = await SetupAsync(); using var env = e;
        var t = (await store.CheckoutAsync(Card())).Value!.Ticket!;
        var files = new TicketFiles(e.Paths);
        var html = await files.SaveAsync(t);
        File.Exists(html).Should().BeTrue();
        File.Exists(Path.ChangeExtension(html, ".txt")).Should().BeTrue();
        Path.GetDirectoryName(html).Should().Be(files.TicketsDir);
        var print = await files.WritePrintFileAsync(t);
        File.ReadAllText(print).Should().Contain("window.print()");
    }
}
