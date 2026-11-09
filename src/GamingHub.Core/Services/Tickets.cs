using System.Globalization;
using System.Net;
using System.Text;
using GamingHub.Core.Models;
using GamingHub.Core.Storage;

namespace GamingHub.Core.Services;

/// <summary>Construye el ticket a partir de las líneas de compra guardadas (siempre desde datos del servicio, no de la UI).</summary>
public static class TicketBuilder
{
    public const string Beneficiary = "GamingHub (demo)";

    public static string NumberFor(Guid orderId, DateTime utc) =>
        $"GH-{utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{orderId.ToString("N")[..6].ToUpperInvariant()}";

    public static string TrackingKeyFor(Guid orderId, DateTime utc) =>
        $"GH{utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}{orderId.ToString("N")[..8].ToUpperInvariant()}";

    public static string MethodName(PaymentMethod m) => m switch
    {
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Transfer => "Transferencia bancaria",
        _ => "No registrado"
    };

    /// <summary>Texto corto para el historial: «Visa ****1234» / «Transferencia (BBVA)».</summary>
    public static string PaymentLabel(Purchase p) => p.PaymentMethod switch
    {
        PaymentMethod.Card => $"{(string.IsNullOrEmpty(p.CardBrand) ? "Tarjeta" : p.CardBrand)} ****{p.CardLast4}",
        PaymentMethod.Transfer => string.IsNullOrEmpty(p.Bank) ? "Transferencia" : $"Transferencia ({p.Bank})",
        _ => ""
    };

    /// <param name="lines">Líneas de UNA misma compra (mismo OrderId). No puede estar vacía.</param>
    public static Ticket FromPurchases(IReadOnlyList<Purchase> lines, IReadOnlyDictionary<string, Game> games, string customer)
    {
        var first = lines[0];
        var ticketLines = lines.Select(p =>
        {
            games.TryGetValue(p.GameId, out var g);
            return new TicketLine(p.GameId, p.GameTitle, g?.Category ?? "", p.Amount, p.Currency,
                g?.SizeBytes ?? 0, g?.ReleaseDate ?? default, g?.Developer ?? "", g?.Platform ?? "", g?.Requirements ?? "");
        }).ToList();

        return new Ticket(NumberFor(first.OrderId, first.Utc), first.Utc, customer, ticketLines,
            lines.Sum(p => p.Amount), first.Currency, first.PaymentMethod)
        {
            CardBrand = first.CardBrand,
            CardLast4 = first.CardLast4,
            PaymentHolder = first.PaymentHolder,
            Bank = first.Bank,
            AccountLast4 = first.AccountLast4,
            Subject = first.TransferSubject,
            TrackingKey = first.TrackingKey
        };
    }
}

/// <summary>Convierte un ticket a texto de ancho fijo (42 columnas, tipo impresora térmica) o a HTML imprimible.</summary>
public static class TicketRenderer
{
    private const int W = 42;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Money(decimal v, string cur) => $"{v.ToString("0.00", Inv)} {cur}";
    private static string Date(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Inv);
    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

    /// <summary>Campos de pago en orden de impresión; compartido por el texto y el HTML.</summary>
    private static List<(string Label, string Value)> PaymentFields(Ticket t)
    {
        var f = new List<(string, string)> { ("Método", TicketBuilder.MethodName(t.Method)) };
        switch (t.Method)
        {
            case PaymentMethod.Card:
                f.Add(("Tarjeta", $"{(string.IsNullOrEmpty(t.CardBrand) ? "Tarjeta" : t.CardBrand)} **** **** **** {t.CardLast4}"));
                f.Add(("Titular", Dash(t.PaymentHolder)));
                f.Add(("Estado", "Aprobado (simulado)"));
                break;
            case PaymentMethod.Transfer:
                f.Add(("Ordenante", Dash(t.PaymentHolder)));
                f.Add(("Banco origen", Dash(t.Bank)));
                f.Add(("Cuenta origen", $"**** **** **** {t.AccountLast4}"));
                f.Add(("Beneficiario", TicketBuilder.Beneficiary));
                f.Add(("Asunto", Dash(t.Subject)));
                f.Add(("Clave rastreo", Dash(t.TrackingKey)));
                f.Add(("Estado", "Aplicada (simulada)"));
                break;
            default:
                f.Add(("Estado", "Sin datos de pago"));
                break;
        }
        return f;
    }

    private static List<(string Label, string Value)> GameFields(TicketLine l)
    {
        var f = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(l.Category)) f.Add(("Categoría", l.Category));
        if (!string.IsNullOrWhiteSpace(l.Developer)) f.Add(("Desarrollador", l.Developer));
        if (!string.IsNullOrWhiteSpace(l.Platform)) f.Add(("Plataforma", l.Platform));
        if (!string.IsNullOrWhiteSpace(l.Requirements)) f.Add(("Requisitos", l.Requirements));
        f.Add(("Tamaño", Formatters.Bytes(l.SizeBytes)));
        if (l.ReleaseDate != default) f.Add(("Lanzamiento", l.ReleaseDate.ToString("dd/MM/yyyy", Inv)));
        return f;
    }

    // ------------------------------------------------------------------ TEXTO
    public static string ToText(Ticket t)
    {
        var sb = new StringBuilder();
        string eq = new('=', W), dash = new('-', W);

        void Center(string s) => sb.AppendLine(s.PadLeft((W + s.Length) / 2));
        void Row(string left, string right)
        {
            var max = W - right.Length - 1;
            if (left.Length > max) left = left[..Math.Max(0, max - 1)] + "…";
            sb.Append(left).Append(' ', Math.Max(1, W - left.Length - right.Length)).AppendLine(right);
        }
        void Field(string label, string value, int indent = 3)
        {
            var prefix = new string(' ', indent) + label + ": ";
            var pad = new string(' ', prefix.Length);
            var first = true;
            foreach (var part in Wrap(value, W - prefix.Length))
            {
                sb.Append(first ? prefix : pad).AppendLine(part);
                first = false;
            }
        }

        sb.AppendLine(eq);
        Center("GAMINGHUB");
        Center("TICKET DE COMPRA (SIMULADA)");
        sb.AppendLine(eq);
        Field("Ticket", t.Number, 0);
        Field("Fecha", Date(t.Utc), 0);
        Field("Cliente", Dash(t.CustomerName), 0);
        sb.AppendLine(dash);

        sb.AppendLine("JUEGOS");
        var n = 0;
        foreach (var l in t.Lines)
        {
            Row($"{++n}) {l.Title}", Money(l.Price, l.Currency));
            foreach (var (label, value) in GameFields(l)) Field(label, value);
            sb.AppendLine();
        }
        sb.AppendLine(dash);
        Row("TOTAL", Money(t.Total, t.Currency));
        sb.AppendLine(dash);

        sb.AppendLine("PAGO");
        foreach (var (label, value) in PaymentFields(t)) Field(label, value, 0);
        sb.AppendLine(eq);
        Center("*** COMPRA SIMULADA ***");
        Center("No se realizó ningún cargo real.");
        Center("Conserva este ticket como comprobante.");
        sb.AppendLine(eq);
        return sb.ToString();
    }

    private static IEnumerable<string> Wrap(string? s, int width)
    {
        if (string.IsNullOrWhiteSpace(s)) { yield return "—"; yield break; }
        var cur = new StringBuilder();
        foreach (var raw in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw;
            while (word.Length > width)
            {
                if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); }
                yield return word[..width];
                word = word[width..];
            }
            if (cur.Length > 0 && cur.Length + 1 + word.Length > width) { yield return cur.ToString(); cur.Clear(); }
            if (cur.Length > 0) cur.Append(' ');
            cur.Append(word);
        }
        if (cur.Length > 0) yield return cur.ToString();
    }

    // ------------------------------------------------------------------ HTML
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>HTML autocontenido (sin recursos externos). Todo texto del usuario va codificado.</summary>
    public static string ToHtml(Ticket t, bool autoPrint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Ticket {E(t.Number)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Consolas,'Courier New',monospace;background:#e9edf3;margin:0;padding:24px;color:#111}");
        sb.AppendLine(".ticket{width:80mm;max-width:100%;margin:0 auto;background:#fff;padding:5mm;box-sizing:border-box;box-shadow:0 2px 12px rgba(0,0,0,.2);font-size:12px;line-height:1.35}");
        sb.AppendLine("h1{font-size:16px;text-align:center;margin:0}h2{font-size:12px;text-align:center;margin:2px 0 6px;font-weight:normal}");
        sb.AppendLine("hr{border:0;border-top:1px dashed #000;margin:8px 0}.sec{font-weight:bold;margin:4px 0}");
        sb.AppendLine(".row{display:flex;justify-content:space-between;gap:8px}.row span:first-child{word-break:break-word}");
        sb.AppendLine(".kv{display:flex;gap:6px}.kv b{min-width:34%;font-weight:normal;color:#444}.kv span{word-break:break-word}");
        sb.AppendLine(".game{margin-bottom:8px}.game .kv{padding-left:10px}.total{font-weight:bold;font-size:14px}");
        sb.AppendLine(".foot{text-align:center;font-size:11px}.noprint{text-align:center;margin-top:14px}");
        sb.AppendLine("button{font:inherit;padding:8px 18px;cursor:pointer}");
        sb.AppendLine("@page{size:80mm auto;margin:3mm}");
        sb.AppendLine("@media print{body{background:#fff;padding:0}.ticket{box-shadow:none;width:auto;padding:0}.noprint{display:none}}");
        sb.AppendLine("</style></head><body><div class=\"ticket\">");
        sb.AppendLine("<h1>GAMINGHUB</h1><h2>TICKET DE COMPRA (SIMULADA)</h2><hr>");
        sb.AppendLine($"<div class=\"kv\"><b>Ticket</b><span>{E(t.Number)}</span></div>");
        sb.AppendLine($"<div class=\"kv\"><b>Fecha</b><span>{E(Date(t.Utc))}</span></div>");
        sb.AppendLine($"<div class=\"kv\"><b>Cliente</b><span>{E(Dash(t.CustomerName))}</span></div><hr>");

        sb.AppendLine("<div class=\"sec\">JUEGOS</div>");
        var n = 0;
        foreach (var l in t.Lines)
        {
            sb.AppendLine($"<div class=\"game\"><div class=\"row\"><span>{++n}) {E(l.Title)}</span><span>{E(Money(l.Price, l.Currency))}</span></div>");
            foreach (var (label, value) in GameFields(l))
                sb.AppendLine($"<div class=\"kv\"><b>{E(label)}</b><span>{E(value)}</span></div>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine($"<hr><div class=\"row total\"><span>TOTAL</span><span>{E(Money(t.Total, t.Currency))}</span></div><hr>");

        sb.AppendLine("<div class=\"sec\">PAGO</div>");
        foreach (var (label, value) in PaymentFields(t))
            sb.AppendLine($"<div class=\"kv\"><b>{E(label)}</b><span>{E(value)}</span></div>");

        sb.AppendLine("<hr><div class=\"foot\"><div>*** COMPRA SIMULADA ***</div><div>No se realizó ningún cargo real.</div>");
        sb.AppendLine("<div>Conserva este ticket como comprobante.</div></div></div>");
        sb.AppendLine("<div class=\"noprint\"><button onclick=\"window.print()\">Imprimir ticket</button></div>");
        if (autoPrint)
            sb.AppendLine("<script>window.addEventListener('load',function(){setTimeout(function(){window.print();},300);});</script>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

/// <summary>Guarda copias del ticket (.txt y .html) en %LocalAppData%\GamingHub\data\tickets y prepara el archivo para imprimir.</summary>
public sealed class TicketFiles
{
    private readonly StoragePaths _paths;
    public TicketFiles(StoragePaths paths) { _paths = paths; }

    public string TicketsDir => Path.Combine(_paths.Root, "tickets");

    /// <summary>Solo letras ASCII, dígitos y guion: el nombre sale del número de ticket, nunca de texto del usuario.</summary>
    private static string Safe(string s) => new(s.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());

    /// <returns>Ruta del .html guardado.</returns>
    public async Task<string> SaveAsync(Ticket t, CancellationToken ct = default)
    {
        Directory.CreateDirectory(TicketsDir);
        var baseName = Path.Combine(TicketsDir, Safe(t.Number));
        await File.WriteAllTextAsync(baseName + ".txt", TicketRenderer.ToText(t), new UTF8Encoding(true), ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(baseName + ".html", TicketRenderer.ToHtml(t, autoPrint: false), new UTF8Encoding(false), ct).ConfigureAwait(false);
        return baseName + ".html";
    }

    /// <summary>HTML temporal que abre el diálogo de impresión del navegador al cargar.</summary>
    public async Task<string> WritePrintFileAsync(Ticket t, CancellationToken ct = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "GamingHub-print");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Safe(t.Number) + "-print.html");
        await File.WriteAllTextAsync(path, TicketRenderer.ToHtml(t, autoPrint: true), new UTF8Encoding(false), ct).ConfigureAwait(false);
        return path;
    }
}
