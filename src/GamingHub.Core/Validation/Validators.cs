using System.Text.RegularExpressions;
using GamingHub.Core.Models;

namespace GamingHub.Core.Validation;

public sealed class ValidationResult
{
    private readonly List<string> _errors = new();
    public IReadOnlyList<string> Errors => _errors;
    public bool IsValid => _errors.Count == 0;
    public string FirstError => _errors.Count > 0 ? _errors[0] : "";
    public string AllErrors => string.Join(" ", _errors);
    public void Add(string error) => _errors.Add(error);
    public void Merge(ValidationResult other) => _errors.AddRange(other._errors);
    public static ValidationResult Fail(string error) { var r = new ValidationResult(); r.Add(error); return r; }
}

internal static class TextRules
{
    public static bool HasControlChars(string s, bool allowWhitespaceControls = false) =>
        s.Any(c => char.IsControl(c) && !(allowWhitespaceControls && (c == '\n' || c == '\r' || c == '\t')));
}

public static class UserNameValidator
{
    public const int MinLength = 4, MaxLength = 24;
    private static readonly HashSet<string> Reserved = new() { "admin", "administrator", "system", "root" };

    public static string Normalize(string? s) => (s ?? "").Trim().ToLowerInvariant();

    public static ValidationResult Validate(string? userName)
    {
        var r = new ValidationResult();
        if (string.IsNullOrEmpty(userName)) { r.Add("El nombre de usuario es obligatorio."); return r; }
        if (userName.Length > 1000) { r.Add("El nombre de usuario es excesivamente largo."); return r; }
        if (TextRules.HasControlChars(userName)) r.Add("El nombre de usuario contiene caracteres de control.");
        if (userName.Length < MinLength || userName.Length > MaxLength)
            r.Add($"El nombre de usuario debe tener entre {MinLength} y {MaxLength} caracteres.");
        if (!userName.All(c => (c < 128 && char.IsLetterOrDigit(c)) || c == '_'))
            r.Add("El nombre de usuario solo admite letras, números y guion bajo (sin espacios).");
        if (Reserved.Contains(Normalize(userName))) r.Add("Ese nombre de usuario está reservado.");
        return r;
    }
}

public static class PasswordValidator
{
    public const int MinLength = 12, MaxLength = 64;
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1234", "password123!", "password1234!", "123456789012", "qwertyuiop12", "qwerty123456!",
        "contraseña123", "contraseña123!", "welcome12345", "letmein12345", "admin1234567", "admin123456!",
        "passw0rd1234", "iloveyou1234", "changeme1234", "abc123456789"
    };

    public static ValidationResult Validate(string? password, string? userName)
    {
        var r = new ValidationResult();
        if (string.IsNullOrEmpty(password)) { r.Add("La contraseña es obligatoria."); return r; }
        if (password.Length > MaxLength) { r.Add($"La contraseña no puede superar {MaxLength} caracteres."); return r; }
        if (password.Length < MinLength) r.Add($"La contraseña debe tener al menos {MinLength} caracteres.");
        if (char.IsWhiteSpace(password[0]) || char.IsWhiteSpace(password[^1]))
            r.Add("La contraseña no puede empezar ni terminar con espacios.");
        if (TextRules.HasControlChars(password)) r.Add("La contraseña contiene caracteres de control.");
        if (!password.Any(char.IsUpper)) r.Add("La contraseña necesita una letra mayúscula.");
        if (!password.Any(char.IsLower)) r.Add("La contraseña necesita una letra minúscula.");
        if (!password.Any(char.IsDigit)) r.Add("La contraseña necesita un número.");
        if (!password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c)))
            r.Add("La contraseña necesita un carácter especial.");
        if (Common.Contains(password) || password.Distinct().Count() == 1)
            r.Add("La contraseña es demasiado común o insegura.");
        if (!string.IsNullOrEmpty(userName) && password.Contains(userName, StringComparison.OrdinalIgnoreCase))
            r.Add("La contraseña no puede contener el nombre de usuario.");
        return r;
    }
}

public static class EmailValidator
{
    public const int MaxLength = 254;
    private static readonly Regex Pattern = new(
        @"^[A-Za-z0-9._%+\-]{1,64}@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public static string Normalize(string? s) => (s ?? "").Trim().ToLowerInvariant();

    public static ValidationResult Validate(string? email)
    {
        var r = new ValidationResult();
        if (string.IsNullOrWhiteSpace(email)) { r.Add("El correo es obligatorio."); return r; }
        if (email.Length > MaxLength) { r.Add($"El correo no puede superar {MaxLength} caracteres."); return r; }
        if (TextRules.HasControlChars(email)) { r.Add("El correo contiene caracteres de control."); return r; }
        if (!Pattern.IsMatch(email.Trim())) r.Add("El formato del correo no es válido.");
        return r;
    }
}

public static class SearchValidator
{
    public const int MinLength = 2, MaxLength = 100;

    /// <summary>An empty (or whitespace) query is valid and means "show everything".</summary>
    public static ValidationResult Validate(string? query, out string normalized)
    {
        normalized = "";
        var r = new ValidationResult();
        if (query is null) { r.Add("La búsqueda no puede ser nula."); return r; }
        if (query.Length > MaxLength) { r.Add($"La búsqueda no puede superar {MaxLength} caracteres."); return r; }
        if (TextRules.HasControlChars(query)) { r.Add("La búsqueda contiene caracteres no permitidos."); return r; }
        var t = query.Trim();
        if (t.Length == 0) return r;
        if (t.Length < MinLength) { r.Add($"Escribe al menos {MinLength} caracteres para buscar."); return r; }
        normalized = t;
        return r;
    }
}

public static class TextValidator
{
    public static ValidationResult Required(string? value, string field, int min, int max, bool allowNewlines = false)
    {
        var r = new ValidationResult();
        if (value is null || value.Trim().Length == 0) { r.Add($"{field} es obligatorio."); return r; }
        if (value.Length > max * 4) { r.Add($"{field} es excesivamente largo."); return r; }
        if (TextRules.HasControlChars(value, allowNewlines)) r.Add($"{field} contiene caracteres no permitidos.");
        var len = value.Trim().Length;
        if (len < min || len > max) r.Add($"{field} debe tener entre {min} y {max} caracteres.");
        return r;
    }

    public static ValidationResult Optional(string? value, string field, int max)
    {
        if (string.IsNullOrEmpty(value)) return new ValidationResult();
        var r = new ValidationResult();
        if (value.Length > max) r.Add($"{field} no puede superar {max} caracteres.");
        if (TextRules.HasControlChars(value, true)) r.Add($"{field} contiene caracteres no permitidos.");
        return r;
    }
}

public static class PriceValidator
{
    public static readonly string[] Currencies = { "USD", "EUR", "MXN" };
    public static ValidationResult Validate(decimal price, string? currency)
    {
        var r = new ValidationResult();
        if (price < 0) r.Add("El precio no puede ser negativo.");
        if (price > 1000) r.Add("El precio supera el máximo permitido.");
        if (decimal.Round(price, 2) != price) r.Add("El precio admite como máximo 2 decimales.");
        if (currency is null || !Currencies.Contains(currency)) r.Add("Moneda no soportada.");
        return r;
    }
}

public static class PathValidator
{
    public static ValidationResult ValidateGameExecutable(string? path, IEnumerable<string> authorizedRoots)
    {
        var r = new ValidationResult();
        if (string.IsNullOrWhiteSpace(path)) { r.Add("La ruta es obligatoria."); return r; }
        if (path.Length > 260 || TextRules.HasControlChars(path) || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        { r.Add("La ruta no es válida."); return r; }
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception) { r.Add("La ruta no es válida."); return r; }
        if (!string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase))
            r.Add("Solo se admiten archivos .exe.");
        if (!authorizedRoots.Any(root => IsUnder(full, root)))
        { r.Add("La ruta está fuera de los directorios autorizados."); return r; }
        if (!File.Exists(full)) { r.Add("El archivo no existe."); return r; }
        var fi = new FileInfo(full);
        if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint)) r.Add("No se admiten enlaces simbólicos.");
        if (fi.Length < 64 || fi.Length > 8L * 1024 * 1024 * 1024) r.Add("El tamaño del archivo no es válido.");
        else if (!StartsWith(full, new byte[] { 0x4D, 0x5A })) r.Add("El archivo no es un ejecutable válido.");
        return r;
    }

    public static bool IsUnder(string fullPath, string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;
        var rf = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rf, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool StartsWith(string path, byte[] magic)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buf = new byte[magic.Length];
            return fs.Read(buf, 0, buf.Length) == buf.Length && buf.SequenceEqual(magic);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}

public static class ImageValidator
{
    public const long MaxBytes = 2L * 1024 * 1024;
    public static ValidationResult ValidateAvatar(string? path)
    {
        var r = new ValidationResult();
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 || TextRules.HasControlChars(path))
        { r.Add("La ruta de la imagen no es válida."); return r; }
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg")) { r.Add("Solo se admiten imágenes PNG o JPG."); return r; }
        if (!File.Exists(path)) { r.Add("La imagen no existe."); return r; }
        if (new FileInfo(path).Length is 0 or > MaxBytes) { r.Add("La imagen debe pesar entre 1 byte y 2 MB."); return r; }
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var jpg = new byte[] { 0xFF, 0xD8, 0xFF };
        var okContent = ext == ".png" ? PathValidator.StartsWith(path, png) : PathValidator.StartsWith(path, jpg);
        if (!okContent) r.Add("El contenido del archivo no coincide con una imagen válida.");
        return r;
    }
}

/// <summary>Validación centralizada de los datos de pago (tarjeta o transferencia). Compras simuladas.</summary>
public static class PaymentValidator
{
    public const int AccountLength = 18;
    public static readonly string[] CommonBanks =
    {
        "BBVA", "Banorte", "Santander", "HSBC", "Citibanamex", "Scotiabank", "Inbursa",
        "BanCoppel", "Banco Azteca", "Nu México", "Otro"
    };

    private static readonly Regex ExpiryPattern = new(@"^(0[1-9]|1[0-2])/[0-9]{2}$", RegexOptions.Compiled);

    public static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    /// <summary>Quita espacios y guiones (formato típico de tarjetas y CLABE).</summary>
    public static string StripSeparators(string? s) => (s ?? "").Replace(" ", "").Replace("-", "");

    /// <summary>Recorta y colapsa espacios repetidos.</summary>
    public static string Clean(string? s) => Regex.Replace((s ?? "").Trim(), @"\s+", " ");

    public static string Last4(string? digits)
    {
        var d = StripSeparators(digits);
        return d.Length >= 4 ? d[^4..] : d;
    }

    public static string CardBrandOf(string? cardNumber)
    {
        var d = StripSeparators(cardNumber);
        if (d.StartsWith('4')) return "Visa";
        if (d.Length >= 2 && int.TryParse(d[..2], out var p2) && p2 is >= 51 and <= 55) return "Mastercard";
        if (d.Length >= 4 && int.TryParse(d[..4], out var p4) && p4 is >= 2221 and <= 2720) return "Mastercard";
        return "Tarjeta";
    }

    public static ValidationResult Validate(PaymentInfo? p, DateTime nowUtc)
    {
        var r = new ValidationResult();
        if (p is null || p.Method == PaymentMethod.None) { r.Add("Selecciona un método de pago."); return r; }
        if (p.Method == PaymentMethod.Card) ValidateCard(p, nowUtc, r); else ValidateTransfer(p, r);
        return r;
    }

    private static void ValidateCard(PaymentInfo p, DateTime now, ValidationResult r)
    {
        var number = StripSeparators(p.CardNumber);
        if (number.Length == 0) r.Add("El número de tarjeta es obligatorio.");
        else if (number.Length != 16 || !number.All(IsAsciiDigit)) r.Add("El número de tarjeta debe tener exactamente 16 dígitos.");

        ValidateName(p.CardHolder, "El nombre del titular de la tarjeta", r);

        var expiry = (p.CardExpiry ?? "").Trim();
        if (expiry.Length == 0) r.Add("La fecha de vencimiento es obligatoria.");
        else if (!ExpiryPattern.IsMatch(expiry)) r.Add("La fecha de vencimiento debe tener el formato MM/AA (ej. 12/28).");
        else
        {
            var month = int.Parse(expiry[..2]);
            var year = 2000 + int.Parse(expiry[3..]);
            if (year < now.Year || (year == now.Year && month < now.Month)) r.Add("La tarjeta está vencida.");
            else if (year > now.Year + 20) r.Add("La fecha de vencimiento no es válida.");
        }
    }

    private static void ValidateTransfer(PaymentInfo p, ValidationResult r)
    {
        ValidateName(p.PayerName, "El nombre de quien realiza la transferencia", r);

        var bank = Clean(p.Bank);
        if (bank.Length == 0) r.Add("El banco de origen es obligatorio.");
        else if (bank.Length < 2 || bank.Length > 40) r.Add("El banco debe tener entre 2 y 40 caracteres.");
        else if (TextRules.HasControlChars(bank)) r.Add("El banco contiene caracteres no válidos.");

        var account = StripSeparators(p.AccountNumber);
        if (account.Length == 0) r.Add("La cuenta CLABE de origen es obligatoria.");
        else if (account.Length != AccountLength || !account.All(IsAsciiDigit))
            r.Add($"La cuenta CLABE debe tener exactamente {AccountLength} dígitos.");

        var subject = Clean(p.Subject);
        if (subject.Length == 0) r.Add("El asunto de la transferencia es obligatorio.");
        else if (subject.Length < 3 || subject.Length > 60) r.Add("El asunto debe tener entre 3 y 60 caracteres.");
        else if (TextRules.HasControlChars(subject)) r.Add("El asunto contiene caracteres no válidos.");
    }

    private static void ValidateName(string? raw, string label, ValidationResult r)
    {
        var n = Clean(raw);
        if (n.Length == 0) { r.Add($"{label} es obligatorio."); return; }
        if (n.Length < 3 || n.Length > 60) r.Add($"{label} debe tener entre 3 y 60 caracteres.");
        else if (!n.All(c => char.IsLetter(c) || c == ' ' || c == '.' || c == '\'' || c == '-'))
            r.Add($"{label} solo admite letras, espacios, punto, apóstrofo y guion.");
    }
}
