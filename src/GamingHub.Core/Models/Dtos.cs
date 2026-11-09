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
        ["minecraft"] = "https://assets.nintendo.com/image/upload/q_auto/f_auto/store/software/switch/70010000000964/a28a81253e919298beab2295e39a56b7a5140ef15abdb56135655e5c221b2a3a",
        ["fortnite"] = "https://cdn1.epicgames.com/offer/fn/FNBR_43-00_MSEE_KeyArt_FN_Logo_EGS_Launcher_Blade_2560x1440_2560x1440-b575b2bd2d0d4892b520b4262617de93",
        ["gta-v"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/271590/header.jpg",
        ["ea-sports-fc-25"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/2669320/header.jpg",
        ["forza-horizon-5"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1551360/header.jpg",
        ["elden-ring"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1245620/header.jpg",
        ["the-witcher-3"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/292030/header.jpg",
        ["baldurs-gate-3"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1086940/header.jpg",
        ["hogwarts-legacy"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/990080/header.jpg",
        ["cyberpunk-2077"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1091500/header.jpg",
        ["red-dead-redemption-2"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1174180/header.jpg",
        ["god-of-war-ragnarok"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/2322010/header.jpg",
        ["resident-evil-4"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/2050650/header.jpg",
        ["assassins-creed-shadows"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/3159330/header.jpg",
        ["civilization-vi"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/289070/header.jpg",
        ["age-of-empires-iv"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1466860/header.jpg",
        ["the-sims-4"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/1222670/header.jpg",
        ["hogwarts-legacy-platform"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/367520/header.jpg",
        ["stardew-valley"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/413150/header.jpg",
        ["rocket-league"] = "https://cdn.cloudflare.steamstatic.com/steam/apps/252950/header.jpg"
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
    public string Text => $"{UserName} · {Email} · {Role}{(Locked ? " · BLOQUEADO TEMPORALMENTE" : "")}";
}
