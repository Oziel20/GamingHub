
namespace GamingHub.Core.Models;

public static class Formatters
{
    public static string Bytes(long b)
    {
        if (b <= 0) return "—";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }
    public static string Duration(long seconds) => $"{seconds / 3600} h {seconds % 3600 / 60} min";
}

public enum LibrarySort { TitleAsc, TitleDesc, DateAddedDesc, DateAddedAsc, PlayTimeDesc }

public static class GameImageUrls
{
    private static readonly IReadOnlyDictionary<string, string> Covers = new Dictionary<string, string>
    {
        ["minecraft"] = "ms-appx:///Assets/Covers/Minecraft.jpg",
        ["fortnite"] = "ms-appx:///Assets/Covers/Fortnite.jpg",
        ["gta-v"] = "ms-appx:///Assets/Covers/Gta%20V.jpg",
        ["ea-sports-fc-25"] = "ms-appx:///Assets/Covers/ea-sports-fc-25.jpg",
        ["forza-horizon-5"] = "ms-appx:///Assets/Covers/forza-horizon-5.jpg",
        ["elden-ring"] = "ms-appx:///Assets/Covers/elden-ring.jpg",
        ["the-witcher-3"] = "ms-appx:///Assets/Covers/the-witcher-3.jpg",
        ["baldurs-gate-3"] = "ms-appx:///Assets/Covers/baldurs-gate-3.jpg",
        ["hogwarts-legacy"] = "ms-appx:///Assets/Covers/hogwarts-legacy.jpg",
        ["cyberpunk-2077"] = "ms-appx:///Assets/Covers/cyberpunk-2077.jpg",
        ["red-dead-redemption-2"] = "ms-appx:///Assets/Covers/red-dead-redemption-2.jpg",
        ["god-of-war-ragnarok"] = "ms-appx:///Assets/Covers/god-of-war-ragnarok.jpg",
        ["resident-evil-4"] = "ms-appx:///Assets/Covers/resident-evil-4.jpg",
        ["assassins-creed-shadows"] = "ms-appx:///Assets/Covers/assassins-creed-shadows.jpg",
        ["civilization-vi"] = "ms-appx:///Assets/Covers/civilization-vi.jpg",
        ["age-of-empires-iv"] = "ms-appx:///Assets/Covers/age-of-empires-iv.jpg",
        ["the-sims-4"] = "ms-appx:///Assets/Covers/the-sims-4.jpg",
        ["hogwarts-legacy-platform"] = "ms-appx:///Assets/Covers/Hollow%20Knight.jpg",
        ["stardew-valley"] = "ms-appx:///Assets/Covers/stardew-valley.jpg",
        ["rocket-league"] = "ms-appx:///Assets/Covers/rocket-league.jpg"
    };

    public static string For(string gameId) => Covers.TryGetValue(gameId, out var cover)
        ? cover
        : $"https://picsum.photos/seed/{Uri.EscapeDataString(gameId)}/600/338";
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record LibraryItemDto(string GameId, string Title, string Category, long SizeBytes, bool Installed,
    long PlayedSeconds, DateTime? LastPlayedUtc, DateTime AddedUtc, bool IsLocal, bool Favorite, string? CoverPath = null)
{
    public string SizeText => Formatters.Bytes(SizeBytes);
    public string PlayedText => Formatters.Duration(PlayedSeconds);
    public string LastPlayedText => LastPlayedUtc is { } d ? d.ToLocalTime().ToString("g") : "Nunca";
    public string InstalledText => Installed ? "Instalado" : "No instalado";
    public string InstallButtonText => Installed ? "Desinstalar (simulado)" : "Instalar (simulado)";
    public string FavoriteText => Favorite ? "★ Favorito" : "☆ Favorito";
    public bool IsStoreGame => !IsLocal;
    public string Initial => Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";
    public string ImageUrl => string.IsNullOrWhiteSpace(CoverPath) ? GameImageUrls.For(GameId) : CoverPath;
    public Uri ImageUri => new(ImageUrl, UriKind.RelativeOrAbsolute);
}

public sealed record CatalogItemDto(string Id, string Title, string Category, decimal Price, string Currency,
    string Description, long SizeBytes, bool Owned, bool InCart, string? CoverPath = null)
{
    public string PriceText => Price == 0 ? "Gratis" : $"{Price:0.00} {Currency}";
    public string SizeText => Formatters.Bytes(SizeBytes);
    public bool CanAdd => !Owned && !InCart;
    public string StatusText => Owned ? "En tu biblioteca" : InCart ? "En el carrito" : "Disponible";
    public string Initial => Title.Length > 0 ? Title[..1].ToUpperInvariant() : "?";
    public string ImageUrl => string.IsNullOrWhiteSpace(CoverPath) ? GameImageUrls.For(Id) : CoverPath;
    public Uri ImageUri => new(ImageUrl, UriKind.RelativeOrAbsolute);
}

public sealed record PurchaseDto(Guid Id, string Title, decimal Amount, string Currency, DateTime Utc,
    Guid OrderId = default, string PaymentText = "")
{
    public bool HasTicket => OrderId != Guid.Empty;
    public string Text => $"{Utc.ToLocalTime():g} · {Title} · {Amount:0.00} {Currency}" +
                          $"{(PaymentText.Length > 0 ? " · " + PaymentText : "")} (compra simulada)";
}

public sealed record CheckoutResult(int Count, decimal Total, string Currency, Ticket? Ticket = null);

public sealed record FriendDto(Guid FriendshipId, Guid UserId, string UserName, string DisplayName, bool IsOnline,
    FriendStatus Status, bool IncomingRequest)
{
    public bool IsAccepted => Status == FriendStatus.Accepted;
    public bool IsIncoming => Status == FriendStatus.Pending && IncomingRequest;
    public bool IsOutgoing => Status == FriendStatus.Pending && !IncomingRequest;
    public bool IsBlocked => Status == FriendStatus.Blocked;
    public string StatusText => Status switch
    {
        FriendStatus.Accepted => IsOnline ? "En línea (local)" : "Desconectado",
        FriendStatus.Pending => IncomingRequest ? "Solicitud recibida" : "Solicitud enviada",
        _ => "Bloqueado"
    };
}

public sealed record MessageDto(Guid Id, bool Mine, string Text, DateTime Utc)
{
    public string Header => $"{(Mine ? "Tú" : "Amigo")} · {Utc.ToLocalTime():t}";
}

public sealed record ProfileDto(string UserName, string DisplayName, string Bio, int Level, int GamesCount,
    long PlayedSeconds, string? AvatarPath, IReadOnlyList<string> FavoriteTitles, UserRole Role)
{
    public string PlayedText => Formatters.Duration(PlayedSeconds);
}

public sealed record ActivityDto(DateTime Utc, string Action, string Details)
{
    public string Text => $"{Utc.ToLocalTime():g} · {Action} · {Details}";
}

public sealed record NotificationDto(Guid Id, DateTime Utc, string Title, string Message, bool Read)
{
    public string Text => $"{(Read ? "" : "● ")}{Title}: {Message}";
}

public sealed record DashboardSummary(string DisplayName, int GamesCount, long PlayedSeconds, int Friends, int UnreadNotifications)
{
    public string PlayedText => Formatters.Duration(PlayedSeconds);
}

public sealed record UserAdminDto(Guid Id, string UserName, string Email, UserRole Role, bool Locked, bool Online)
{
    public string Text => $"{UserName} · {Email} · {Role}{(Locked ? " · BLOQUEADO" : "")}";
    public string BlockActionText => Locked ? "Desbloquear" : "Bloquear";
}
