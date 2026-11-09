namespace GamingHub.Core.Models;

/// <summary>
/// Datos de pago capturados en la UI. El número de tarjeta completo solo existe en memoria durante la
/// validación: nunca se persiste (solo marca y 4 últimos dígitos). El CVV no entra en este objeto.
/// </summary>
public sealed record PaymentInfo(PaymentMethod Method)
{
    // Tarjeta
    public string? CardNumber { get; init; }
    public string? CardHolder { get; init; }
    public string? CardExpiry { get; init; }   // MM/AA

    // Transferencia
    public string? PayerName { get; init; }
    public string? Bank { get; init; }
    public string? AccountNumber { get; init; } // CLABE de origen (18 dígitos)
    public string? Subject { get; init; }       // asunto / concepto

    /// <summary>Evita que el número de tarjeta termine en logs o en el depurador por accidente.</summary>
    public override string ToString() => $"PaymentInfo {{ Method = {Method} }}";
}

public sealed record TicketLine(string GameId, string Title, string Category, decimal Price, string Currency,
    long SizeBytes, DateTime ReleaseDate, string Developer, string Platform, string Requirements);

/// <summary>Comprobante de una compra simulada. Se reconstruye siempre desde purchases.json + games.json.</summary>
public sealed record Ticket(string Number, DateTime Utc, string CustomerName, IReadOnlyList<TicketLine> Lines,
    decimal Total, string Currency, PaymentMethod Method)
{
    public string? CardBrand { get; init; }
    public string? CardLast4 { get; init; }
    public string? PaymentHolder { get; init; }
    public string? Bank { get; init; }
    public string? AccountLast4 { get; init; }
    public string? Subject { get; init; }
    public string? TrackingKey { get; init; }
}
