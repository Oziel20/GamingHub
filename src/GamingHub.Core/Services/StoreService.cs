using GamingHub.Core.Models;
using GamingHub.Core.Storage;
using GamingHub.Core.Validation;
using Microsoft.Extensions.Logging;

namespace GamingHub.Core.Services;

/// <summary>Catalog, cart and SIMULATED purchases (there is no payment gateway).</summary>
public sealed class StoreService
{
    private readonly AuthService _auth; private readonly DataStores _s; private readonly ActivityLog _act;
    private readonly NotificationHub _notes; private readonly TimeProvider _time; private readonly ILogger<StoreService> _log;
    private readonly Dictionary<Guid, HashSet<string>> _carts = new();
    private readonly object _cartLock = new();

    public StoreService(AuthService auth, DataStores s, ActivityLog act, NotificationHub notes, TimeProvider time, ILogger<StoreService> log)
    { _auth = auth; _s = s; _act = act; _notes = notes; _time = time; _log = log; }

    private static readonly (string Id, string Title, string Cat, decimal Price, string Desc, long Size)[] Seed =
    {
        ("minecraft", "Minecraft", "Supervivencia", 599.00m, "Construye, explora y sobrevive en un mundo de bloques.", 1_000_000_000),
        ("fortnite", "Fortnite", "Acción", 0m, "Battle royale gratuito con construcción y eventos en línea.", 45_000_000_000),
        ("gta-v", "Grand Theft Auto V", "Acción", 499.00m, "Aventura de mundo abierto ambientada en Los Santos.", 110_000_000_000),
        ("ea-sports-fc-25", "EA Sports FC 25", "Deportes", 999.00m, "Fútbol con clubes, ligas y modos competitivos.", 80_000_000_000),
        ("forza-horizon-5", "Forza Horizon 5", "Carreras", 899.00m, "Carreras de mundo abierto por los paisajes de México.", 110_000_000_000),
        ("elden-ring", "Elden Ring", "RPG", 999.00m, "RPG de acción y exploración en un mundo de fantasía oscura.", 60_000_000_000),
        ("the-witcher-3", "The Witcher 3: Wild Hunt", "RPG", 499.00m, "Aventura de rol con decisiones y un enorme mundo abierto.", 50_000_000_000),
        ("baldurs-gate-3", "Baldur's Gate 3", "RPG", 999.00m, "RPG por turnos basado en el universo de Dungeons & Dragons.", 150_000_000_000),
        ("hogwarts-legacy", "Hogwarts Legacy", "Aventura", 899.00m, "Explora el mundo mágico y estudia en Hogwarts.", 85_000_000_000),
        ("cyberpunk-2077", "Cyberpunk 2077", "RPG", 799.00m, "RPG futurista de mundo abierto en Night City.", 70_000_000_000),
        ("red-dead-redemption-2", "Red Dead Redemption 2", "Aventura", 899.00m, "Historia de forajidos en el oeste americano.", 150_000_000_000),
        ("god-of-war-ragnarok", "God of War Ragnarök", "Acción", 999.00m, "Aventura de acción mitológica con Kratos y Atreus.", 90_000_000_000),
        ("resident-evil-4", "Resident Evil 4", "Terror", 799.00m, "Supervivencia y terror en una misión de rescate.", 65_000_000_000),
        ("assassins-creed-shadows", "Assassin's Creed Shadows", "Acción", 999.00m, "Aventura de sigilo ambientada en el Japón feudal.", 95_000_000_000),
        ("civilization-vi", "Sid Meier's Civilization VI", "Estrategia", 399.00m, "Construye una civilización desde la antigüedad hasta el futuro.", 15_000_000_000),
        ("age-of-empires-iv", "Age of Empires IV", "Estrategia", 699.00m, "Estrategia en tiempo real con grandes civilizaciones históricas.", 50_000_000_000),
        ("the-sims-4", "The Sims 4", "Simulación", 0m, "Crea personajes, construye hogares y dirige sus vidas.", 25_000_000_000),
        ("hogwarts-legacy-platform", "Hollow Knight", "Plataformas", 299.00m, "Aventura de plataformas en un reino subterráneo dibujado a mano.", 9_000_000_000),
        ("stardew-valley", "Stardew Valley", "Simulación", 249.00m, "Cultiva tu granja, conoce a sus habitantes y explora el valle.", 1_000_000_000),
        ("rocket-league", "Rocket League", "Deportes", 0m, "Fútbol con coches propulsados y partidas competitivas.", 25_000_000_000),
    };

    // Ficha técnica de demostración (valores inventados, igual que el catálogo). Se imprime en el ticket.
    private const string DefaultPlatform = "Windows 10/11 (64 bits)";
    private static readonly Dictionary<string, (string Dev, string Req)> Specs = new()
    {
        ["minecraft"] = ("Mojang Studios", "CPU i3 · RAM 4 GB · GPU integrada"),
        ["fortnite"] = ("Epic Games", "CPU i5 · RAM 8 GB · GPU GTX 960"),
        ["gta-v"] = ("Rockstar Games", "CPU i5-3470 · RAM 8 GB · GPU GTX 660"),
        ["ea-sports-fc-25"] = ("EA Sports", "CPU i5-6600K · RAM 8 GB · GPU GTX 1050 Ti"),
        ["forza-horizon-5"] = ("Playground Games", "CPU i5-4460 · RAM 8 GB · GPU GTX 970"),
        ["elden-ring"] = ("FromSoftware", "CPU i5-8400 · RAM 12 GB · GPU GTX 1060"),
        ["the-witcher-3"] = ("CD Projekt Red", "CPU i5-2500K · RAM 6 GB · GPU GTX 770"),
        ["baldurs-gate-3"] = ("Larian Studios", "CPU i5-4690 · RAM 8 GB · GPU GTX 970"),
        ["hogwarts-legacy"] = ("Avalanche Software", "CPU i5-6600 · RAM 16 GB · GPU GTX 960"),
        ["cyberpunk-2077"] = ("CD Projekt Red", "CPU i7-6700 · RAM 12 GB · GPU GTX 1060"),
        ["red-dead-redemption-2"] = ("Rockstar Games", "CPU i7-4770K · RAM 12 GB · GPU GTX 1060"),
        ["god-of-war-ragnarok"] = ("Santa Monica Studio", "CPU i5-4670K · RAM 8 GB · GPU GTX 1060"),
        ["resident-evil-4"] = ("Capcom", "CPU i5-7500 · RAM 8 GB · GPU GTX 1050 Ti"),
        ["assassins-creed-shadows"] = ("Ubisoft", "CPU i7-8700K · RAM 16 GB · GPU RTX 2070"),
        ["civilization-vi"] = ("Firaxis Games", "CPU i3-530 · RAM 8 GB · GPU GTX 650"),
        ["age-of-empires-iv"] = ("Relic Entertainment", "CPU i5-6300U · RAM 8 GB · GPU GTX 970"),
        ["the-sims-4"] = ("Maxis", "CPU i5 · RAM 8 GB · GPU GTX 650"),
        ["hogwarts-legacy-platform"] = ("Team Cherry", "CPU i3 · RAM 4 GB · GPU GTX 560"),
        ["stardew-valley"] = ("ConcernedApe", "CPU 2 GHz · RAM 2 GB · GPU 256 MB"),
        ["rocket-league"] = ("Psyonix", "CPU 2.5 GHz · RAM 4 GB · GPU GTX 760"),
    };

    private static bool ApplySpecs(Game g)
    {
        if (!Specs.TryGetValue(g.Id, out var sp)) return false;
        g.Developer = sp.Dev; g.Platform = DefaultPlatform; g.Requirements = sp.Req;
        return true;
    }

    private async Task EnsureCatalogAsync(CancellationToken ct)
    {
        await _s.Games.UpdateAsync(d =>
        {
            var changed = false;
            var catalogIds = d.Games.Where(g => g.OwnerUserId is null).Select(g => g.Id).ToHashSet();
            var seedIds = Seed.Select(x => x.Id).ToHashSet();
            if (!catalogIds.SetEquals(seedIds))
            {
                d.Games.RemoveAll(g => g.OwnerUserId is null);
                var now = _time.GetUtcNow().UtcDateTime;
                var i = 0;
                foreach (var x in Seed)
                {
                    if (!PriceValidator.Validate(x.Price, "MXN").IsValid) continue;
                    var game = new Game { Id = x.Id, Title = x.Title, Category = x.Cat, Price = x.Price, Currency = "MXN",
                        Description = x.Desc, SizeBytes = x.Size, ReleaseDate = now.AddDays(-30 * (++i)) };
                    ApplySpecs(game);
                    d.Games.Add(game);
                }
                changed = true;
            }
            else
            {
                // Catálogos creados antes de existir los tickets: se completa la ficha técnica una sola vez.
                foreach (var g in d.Games)
                    if (g.OwnerUserId is null && g.Developer.Length == 0 && ApplySpecs(g)) changed = true;
            }
            return (changed, 0);
        }, ct);
    }

    private HashSet<string> CartOf(Guid userId)
    {
        lock (_cartLock) return _carts.TryGetValue(userId, out var c) ? c.ToHashSet() : new HashSet<string>();
    }

    public async Task<OperationResult<PagedResult<CatalogItemDto>>> SearchCatalogAsync(string? query, string? category,
        int page, int pageSize = 12, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<PagedResult<CatalogItemDto>>.AuthRequired();
        var v = SearchValidator.Validate(query, out var term);
        if (!v.IsValid) return OperationResult<PagedResult<CatalogItemDto>>.Fail(ErrorCodes.Invalid, v.FirstError);
        await EnsureCatalogAsync(ct);
        pageSize = Math.Clamp(pageSize, 1, 50); page = Math.Max(1, page);
        var owned = await _s.Library.ReadAsync(d => d.Entries.Where(e => e.UserId == user.Id).Select(e => e.GameId).ToHashSet(), ct);
        var cart = CartOf(user.Id);
        var games = await _s.Games.ReadAsync(d => d.Games.Where(g => g.OwnerUserId is null && g.Available).Select(g => g.Clone()).ToList(), ct);
        var q = games.AsEnumerable();
        if (term.Length > 0) q = q.Where(g => g.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category) && category != LibraryService.AllCategories)
            q = q.Where(g => string.Equals(g.Category, category, StringComparison.OrdinalIgnoreCase));
        var all = q.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(g => new CatalogItemDto(g.Id, g.Title, g.Category, g.Price, g.Currency, g.Description, g.SizeBytes,
                owned.Contains(g.Id), cart.Contains(g.Id), g.CoverPath)).ToList();
        return OperationResult<PagedResult<CatalogItemDto>>.Ok(new PagedResult<CatalogItemDto>(items, page, pageSize, all.Count));
    }

    public async Task<OperationResult<List<string>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (await _auth.GetCurrentUserAsync(ct) is null) return OperationResult<List<string>>.AuthRequired();
        await EnsureCatalogAsync(ct);
        var cats = await _s.Games.ReadAsync(d => d.Games.Where(g => g.OwnerUserId is null).Select(g => g.Category).Distinct().OrderBy(c => c).ToList(), ct);
        cats.Insert(0, LibraryService.AllCategories);
        return OperationResult<List<string>>.Ok(cats);
    }

    public async Task<OperationResult> AddToCartAsync(string? gameId, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 64) return OperationResult.Fail(ErrorCodes.Invalid, "Identificador no válido.");
        var game = await _s.Games.ReadAsync(d => d.Games.FirstOrDefault(g => g.Id == gameId && g.OwnerUserId == null)?.Clone(), ct);
        if (game is null || !game.Available) return OperationResult.Fail(ErrorCodes.NotFound, "El juego no está disponible.");
        if (await _s.Library.ReadAsync(d => d.Entries.Any(e => e.UserId == user.Id && e.GameId == gameId), ct))
            return OperationResult.Fail(ErrorCodes.Duplicate, "Ya tienes este juego.");
        lock (_cartLock)
        {
            if (!_carts.TryGetValue(user.Id, out var c)) _carts[user.Id] = c = new HashSet<string>();
            if (!c.Add(gameId)) return OperationResult.Fail(ErrorCodes.Duplicate, "El juego ya está en el carrito.");
        }
        return OperationResult.Ok("Agregado al carrito.");
    }

    public async Task<OperationResult> RemoveFromCartAsync(string? gameId, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        lock (_cartLock) { if (_carts.TryGetValue(user.Id, out var c)) c.Remove(gameId ?? ""); }
        return OperationResult.Ok("Quitado del carrito.");
    }

    public async Task<OperationResult<List<CatalogItemDto>>> GetCartAsync(CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<List<CatalogItemDto>>.AuthRequired();
        var ids = CartOf(user.Id);
        var items = await _s.Games.ReadAsync(d => d.Games.Where(g => g.OwnerUserId == null && ids.Contains(g.Id))
            .Select(g => new CatalogItemDto(g.Id, g.Title, g.Category, g.Price, g.Currency, g.Description, g.SizeBytes,
                false, true, g.CoverPath)).ToList(), ct);
        return OperationResult<List<CatalogItemDto>>.Ok(items);
    }

    /// <summary>
    /// Prices are always read from games.json, never from the UI.
    /// <paramref name="payment"/> se valida aquí (no solo en la UI); del pago solo se guardan marca y 4 últimos dígitos.
    /// Con <c>null</c> se conserva el comportamiento anterior (compra simulada sin datos de pago).
    /// </summary>
    public async Task<OperationResult<CheckoutResult>> CheckoutAsync(PaymentInfo? payment = null, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<CheckoutResult>.AuthRequired();
        var ids = CartOf(user.Id);
        if (ids.Count == 0) return OperationResult<CheckoutResult>.Fail(ErrorCodes.State, "El carrito está vacío.");
        if (payment is not null)
        {
            var pv0 = PaymentValidator.Validate(payment, _time.GetUtcNow().UtcDateTime);
            if (!pv0.IsValid) return OperationResult<CheckoutResult>.Fail(ErrorCodes.Invalid, pv0.FirstError);
        }
        await EnsureCatalogAsync(ct);
        var games = await _s.Games.ReadAsync(d => d.Games.Where(g => g.OwnerUserId == null && ids.Contains(g.Id)).Select(g => g.Clone()).ToList(), ct);
        if (games.Count != ids.Count || games.Any(g => !g.Available))
            return OperationResult<CheckoutResult>.Fail(ErrorCodes.State, "Algún juego del carrito ya no está disponible.");
        foreach (var g in games)
        {
            var pv = PriceValidator.Validate(g.Price, g.Currency);
            if (!pv.IsValid) return OperationResult<CheckoutResult>.Fail(ErrorCodes.Invalid, pv.FirstError);
        }
        if (games.Select(g => g.Currency).Distinct().Count() > 1)
            return OperationResult<CheckoutResult>.Fail(ErrorCodes.State, "El carrito mezcla monedas distintas.");

        var now = _time.GetUtcNow().UtcDateTime;
        var orderId = Guid.NewGuid();
        var purchases = games.Select(g => BuildPurchase(user.Id, orderId, g, now, payment)).ToList();
        var customer = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        var ticket = TicketBuilder.FromPurchases(purchases, games.ToDictionary(g => g.Id), customer);

        var added = await _s.Library.UpdateAsync(d =>
        {
            if (d.Entries.Any(e => e.UserId == user.Id && ids.Contains(e.GameId))) return (false, false);
            foreach (var g in games) d.Entries.Add(new LibraryEntry { UserId = user.Id, GameId = g.Id, AddedUtc = now });
            return (true, true);
        }, ct);
        if (!added)
        {
            lock (_cartLock) _carts.Remove(user.Id);
            return OperationResult<CheckoutResult>.Fail(ErrorCodes.Duplicate, "Ya posees alguno de estos juegos; el carrito se vació.");
        }
        try
        {
            await _s.Purchases.UpdateAsync(d => { d.Purchases.AddRange(purchases); return (true, 0); }, ct);
        }
        catch (IOException ex) { _log.LogError("Purchase history write failed after library update: {Type}", ex.GetType().Name); }

        lock (_cartLock) _carts.Remove(user.Id);
        var total = games.Sum(g => g.Price);
        var cur = games[0].Currency;
        var how = payment is null ? "" : $" · {TicketBuilder.MethodName(payment.Method)}";
        await _act.LogAsync(user.Id, "Tienda", $"Compra simulada de {games.Count} juego(s) por {total:0.00} {cur}{how} · Ticket {ticket.Number}", ct);
        await _notes.NotifyAsync(user.Id, "Compra simulada", $"Se agregaron {games.Count} juego(s) a tu biblioteca (ticket {ticket.Number}). No se realizó ningún pago real.", ct);
        return OperationResult<CheckoutResult>.Ok(new CheckoutResult(games.Count, total, cur, ticket));
    }

    private static Purchase BuildPurchase(Guid userId, Guid orderId, Game g, DateTime now, PaymentInfo? pay)
    {
        var p = new Purchase { UserId = userId, OrderId = orderId, GameId = g.Id, GameTitle = g.Title, Amount = g.Price, Currency = g.Currency, Utc = now };
        if (pay is null) return p;
        p.PaymentMethod = pay.Method;
        if (pay.Method == PaymentMethod.Card)
        {
            p.PaymentHolder = PaymentValidator.Clean(pay.CardHolder);
            p.CardBrand = PaymentValidator.CardBrandOf(pay.CardNumber);
            p.CardLast4 = PaymentValidator.Last4(pay.CardNumber);   // el número completo no se guarda
        }
        else if (pay.Method == PaymentMethod.Transfer)
        {
            p.PaymentHolder = PaymentValidator.Clean(pay.PayerName);
            p.Bank = PaymentValidator.Clean(pay.Bank);
            p.AccountLast4 = PaymentValidator.Last4(pay.AccountNumber);
            p.TransferSubject = PaymentValidator.Clean(pay.Subject);
            p.TrackingKey = TicketBuilder.TrackingKeyFor(orderId, now);
        }
        return p;
    }

    /// <summary>Reconstruye el ticket de una compra (solo del usuario actual) para verlo o reimprimirlo.</summary>
    public async Task<OperationResult<Ticket>> GetTicketAsync(Guid orderId, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<Ticket>.AuthRequired();
        if (orderId == Guid.Empty)
            return OperationResult<Ticket>.Fail(ErrorCodes.NotFound, "Esta compra es anterior al sistema de tickets y no tiene comprobante.");
        var lines = await _s.Purchases.ReadAsync(d => d.Purchases.Where(p => p.UserId == user.Id && p.OrderId == orderId)
            .OrderBy(p => p.GameTitle).Select(p => p.Clone()).ToList(), ct);
        if (lines.Count == 0) return OperationResult<Ticket>.Fail(ErrorCodes.NotFound, "No se encontró el ticket.");
        await EnsureCatalogAsync(ct);
        var gameIds = lines.Select(l => l.GameId).ToHashSet();
        var games = await _s.Games.ReadAsync(d => d.Games.Where(g => g.OwnerUserId == null && gameIds.Contains(g.Id))
            .Select(g => g.Clone()).ToDictionary(g => g.Id), ct);
        var customer = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        return OperationResult<Ticket>.Ok(TicketBuilder.FromPurchases(lines, games, customer));
    }

    public async Task<OperationResult<List<PurchaseDto>>> GetPurchasesAsync(CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<List<PurchaseDto>>.AuthRequired();
        var list = await _s.Purchases.ReadAsync(d => d.Purchases.Where(p => p.UserId == user.Id).OrderByDescending(p => p.Utc)
            .Select(p => new PurchaseDto(p.Id, p.GameTitle, p.Amount, p.Currency, p.Utc, p.OrderId, TicketBuilder.PaymentLabel(p))).ToList(), ct);
        return OperationResult<List<PurchaseDto>>.Ok(list);
    }
}
