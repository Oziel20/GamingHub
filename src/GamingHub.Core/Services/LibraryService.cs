using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using GamingHub.Core.Models;
using GamingHub.Core.Storage;
using GamingHub.Core.Validation;
using Microsoft.Extensions.Logging;

namespace GamingHub.Core.Services;

public sealed class SettingsService
{
    private readonly AuthService _auth; private readonly DataStores _s;
    public SettingsService(AuthService auth, DataStores s) { _auth = auth; _s = s; }

    public async Task<List<string>> GetRootsInternalAsync(CancellationToken ct = default)
    {
        var roots = await _s.Settings.ReadAsync(d => d.Settings.AuthorizedGameRoots.ToList(), ct);
        if (roots.Count > 0) return roots;
        var defaults = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        await _s.Settings.UpdateAsync(d => { d.Settings.AuthorizedGameRoots = defaults.ToList(); return (true, 0); }, ct);
        return defaults;
    }

    public async Task<OperationResult<List<string>>> GetRootsAsync(CancellationToken ct = default) =>
        await _auth.GetCurrentUserAsync(ct) is null ? OperationResult<List<string>>.AuthRequired()
            : OperationResult<List<string>>.Ok(await GetRootsInternalAsync(ct));

    public async Task<OperationResult> AddRootAsync(string? path, CancellationToken ct = default)
    {
        if (await _auth.GetCurrentAdminAsync(ct) is null) return OperationResult.Fail(ErrorCodes.Forbidden, "Solo un administrador puede cambiar los directorios autorizados.");
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Directory.Exists(path))
            return OperationResult.Fail(ErrorCodes.Invalid, "El directorio no existe o no es válido.");
        var full = Path.GetFullPath(path);
        if (Path.GetPathRoot(full) == full) return OperationResult.Fail(ErrorCodes.Invalid, "No se permite autorizar una unidad completa.");
        await GetRootsInternalAsync(ct);
        return await _s.Settings.UpdateAsync(d =>
        {
            if (d.Settings.AuthorizedGameRoots.Contains(full, StringComparer.OrdinalIgnoreCase))
                return (false, OperationResult.Fail(ErrorCodes.Duplicate, "Ese directorio ya está autorizado."));
            d.Settings.AuthorizedGameRoots.Add(full);
            return (true, OperationResult.Ok("Directorio autorizado."));
        }, ct);
    }

    public async Task<OperationResult> RemoveRootAsync(string? path, CancellationToken ct = default)
    {
        if (await _auth.GetCurrentAdminAsync(ct) is null) return OperationResult.Fail(ErrorCodes.Forbidden, "Solo un administrador puede cambiar los directorios autorizados.");
        return await _s.Settings.UpdateAsync(d =>
        {
            var n = d.Settings.AuthorizedGameRoots.RemoveAll(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
            return n == 0 ? (false, OperationResult.Fail(ErrorCodes.NotFound, "Directorio no encontrado."))
                          : (true, OperationResult.Ok("Directorio eliminado."));
        }, ct);
    }
}

public sealed class LibraryService
{
    public const string AllCategories = "Todas";
    private readonly AuthService _auth; private readonly DataStores _s; private readonly SettingsService _settings;
    private readonly ActivityLog _act; private readonly TimeProvider _time; private readonly ILogger<LibraryService> _log;

    public LibraryService(AuthService auth, DataStores s, SettingsService settings, ActivityLog act, TimeProvider time, ILogger<LibraryService> log)
    { _auth = auth; _s = s; _settings = settings; _act = act; _time = time; _log = log; }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<OperationResult<PagedResult<LibraryItemDto>>> SearchAsync(string? query, string? category,
        LibrarySort sort, int page, int pageSize = 12, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<PagedResult<LibraryItemDto>>.AuthRequired();
        var v = SearchValidator.Validate(query, out var term);
        if (!v.IsValid) return OperationResult<PagedResult<LibraryItemDto>>.Fail(ErrorCodes.Invalid, v.FirstError);
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(1, page);

        var entries = await _s.Library.ReadAsync(d => d.Entries.Where(e => e.UserId == user.Id).Select(e => e.Clone()).ToList(), ct);
        var games = await _s.Games.ReadAsync(d => d.Games.GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First().Clone()), ct);

        var items = entries.Where(e => games.ContainsKey(e.GameId)).Select(e =>
        {
            var g = games[e.GameId];
            return new LibraryItemDto(g.Id, g.Title, g.Category, g.SizeBytes, e.Installed, e.PlayedSeconds,
                e.LastPlayedUtc, e.AddedUtc, g.OwnerUserId is not null, e.Favorite, g.CoverPath);
        });
        if (term.Length > 0) items = items.Where(i => i.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category) && category != AllCategories)
            items = items.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));

        items = sort switch
        {
            LibrarySort.TitleDesc => items.OrderByDescending(i => i.Title, StringComparer.OrdinalIgnoreCase),
            LibrarySort.DateAddedDesc => items.OrderByDescending(i => i.AddedUtc),
            LibrarySort.DateAddedAsc => items.OrderBy(i => i.AddedUtc),
            LibrarySort.PlayTimeDesc => items.OrderByDescending(i => i.PlayedSeconds),
            _ => items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
        };
        var all = items.ToList();
        var pageItems = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return OperationResult<PagedResult<LibraryItemDto>>.Ok(new PagedResult<LibraryItemDto>(pageItems, page, pageSize, all.Count));
    }

    public async Task<OperationResult<List<string>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult<List<string>>.AuthRequired();
        var ids = await _s.Library.ReadAsync(d => d.Entries.Where(e => e.UserId == user.Id).Select(e => e.GameId).ToHashSet(), ct);
        var cats = await _s.Games.ReadAsync(d => d.Games.Where(g => ids.Contains(g.Id)).Select(g => g.Category).Distinct().OrderBy(c => c).ToList(), ct);
        cats.Insert(0, AllCategories);
        return OperationResult<List<string>>.Ok(cats);
    }

    public async Task<OperationResult> AddLocalGameAsync(string? title, string? category, string? exePath, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        var v = new ValidationResult();
        v.Merge(TextValidator.Required(title, "El título", 2, 60));
        v.Merge(TextValidator.Required(category, "La categoría", 2, 30));
        var roots = await _settings.GetRootsInternalAsync(ct);
        v.Merge(PathValidator.ValidateGameExecutable(exePath, roots));
        if (!v.IsValid) return OperationResult.Fail(ErrorCodes.Invalid, v.AllErrors);

        var full = Path.GetFullPath(exePath!);
        var id = "local-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.Id + full.ToLowerInvariant())))[..16].ToLowerInvariant();
        var size = new FileInfo(full).Length;
        var now = Now;

        await _s.Games.UpdateAsync(d =>
        {
            if (d.Games.Any(g => g.Id == id)) return (false, 0);
            d.Games.Add(new Game { Id = id, Title = title!.Trim(), Category = category!.Trim(), Price = 0, ReleaseDate = now,
                SizeBytes = size, OwnerUserId = user.Id, ExecutablePath = full });
            return (true, 0);
        }, ct);

        var result = await _s.Library.UpdateAsync(d =>
        {
            if (d.Entries.Any(e => e.UserId == user.Id && e.GameId == id))
                return (false, OperationResult.Fail(ErrorCodes.Duplicate, "Ese juego ya está en tu biblioteca."));
            d.Entries.Add(new LibraryEntry { UserId = user.Id, GameId = id, AddedUtc = now, Installed = true });
            return (true, OperationResult.Ok("Juego agregado."));
        }, ct);
        if (result.Success) await _act.LogAsync(user.Id, "Biblioteca", $"Juego local agregado: {title!.Trim()}", ct);
        return result;
    }

    public async Task<OperationResult> RemoveAsync(string? gameId, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 64) return OperationResult.Fail(ErrorCodes.Invalid, "Identificador no válido.");
        var result = await _s.Library.UpdateAsync(d =>
        {
            var n = d.Entries.RemoveAll(e => e.UserId == user.Id && e.GameId == gameId);
            return n == 0 ? (false, OperationResult.Fail(ErrorCodes.NotFound, "El juego no está en tu biblioteca."))
                          : (true, OperationResult.Ok("Juego eliminado de tu biblioteca."));
        }, ct);
        if (result.Success) await _act.LogAsync(user.Id, "Biblioteca", $"Juego eliminado: {gameId}", ct);
        return result;
    }

    public Task<OperationResult> ToggleFavoriteAsync(string? gameId, CancellationToken ct = default) =>
        MutateEntryAsync(gameId, e => e.Favorite = !e.Favorite, ct);

    /// <summary>Simulated install state for catalog games (no real downloads exist).</summary>
    public async Task<OperationResult> ToggleInstalledAsync(string? gameId, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        var isLocal = await _s.Games.ReadAsync(d => d.Games.Any(g => g.Id == gameId && g.OwnerUserId != null), ct);
        if (isLocal) return OperationResult.Fail(ErrorCodes.State, "Los juegos locales siempre están instalados.");
        return await MutateEntryAsync(gameId, e => e.Installed = !e.Installed, ct);
    }

    private async Task<OperationResult> MutateEntryAsync(string? gameId, Action<LibraryEntry> change, CancellationToken ct)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 64) return OperationResult.Fail(ErrorCodes.Invalid, "Identificador no válido.");
        return await _s.Library.UpdateAsync(d =>
        {
            var e = d.Entries.FirstOrDefault(x => x.UserId == user.Id && x.GameId == gameId);
            if (e is null) return (false, OperationResult.Fail(ErrorCodes.NotFound, "El juego no está en tu biblioteca."));
            change(e);
            return (true, OperationResult.Ok());
        }, ct);
    }

    public async Task<OperationResult> PlayAsync(string? gameId, CancellationToken ct = default)
    {
        var user = await _auth.GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        var game = await _s.Games.ReadAsync(d => d.Games.FirstOrDefault(g => g.Id == gameId && g.OwnerUserId == user.Id)?.Clone(), ct);
        var owned = game is not null && await _s.Library.ReadAsync(d => d.Entries.Any(e => e.UserId == user.Id && e.GameId == game.Id), ct);
        if (game is null || !owned || game.ExecutablePath is null)
            return OperationResult.Fail(ErrorCodes.State, "Este juego no tiene un ejecutable local que iniciar.");
        var roots = await _settings.GetRootsInternalAsync(ct);
        var v = PathValidator.ValidateGameExecutable(game.ExecutablePath, roots);
        if (!v.IsValid) return OperationResult.Fail(ErrorCodes.Invalid, v.FirstError);
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = game.ExecutablePath, UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath)!
            });
            if (p is null) return OperationResult.Fail(ErrorCodes.Io, "No se pudo iniciar el juego.");
            _ = TrackAsync(p, user.Id, game.Id, Now);
            await _act.LogAsync(user.Id, "Juego", $"Iniciado: {game.Title}", ct);
            return OperationResult.Ok("Juego iniciado.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _log.LogError("Could not start game {GameId}: {Type}", game.Id, ex.GetType().Name);
            return OperationResult.Fail(ErrorCodes.Io, "No se pudo iniciar el juego.");
        }
    }

    private async Task TrackAsync(Process p, Guid userId, string gameId, DateTime started)
    {
        try
        {
            await p.WaitForExitAsync();
            var secs = (long)Math.Max(0, (Now - started).TotalSeconds);
            await _s.Library.UpdateAsync(d =>
            {
                var e = d.Entries.FirstOrDefault(x => x.UserId == userId && x.GameId == gameId);
                if (e is null) return (false, 0);
                e.PlayedSeconds += secs; e.LastPlayedUtc = started;
                return (true, 0);
            });
        }
        catch (Exception ex) { _log.LogError("Playtime tracking failed: {Type}", ex.GetType().Name); }
        finally { p.Dispose(); }
    }
}
