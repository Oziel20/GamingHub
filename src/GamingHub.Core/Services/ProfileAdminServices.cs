using GamingHub.Core.Models;
using GamingHub.Core.Storage;
using GamingHub.Core.Validation;
using Microsoft.Extensions.Logging;

namespace GamingHub.Core.Services;

public sealed class ProfileService
{
    private readonly AuthService _auth; private readonly DataStores _s; private readonly StoragePaths _paths;
    private readonly ActivityLog _act; private readonly ILogger<ProfileService> _log;

    public ProfileService(AuthService auth, DataStores s, StoragePaths paths, ActivityLog act, ILogger<ProfileService> log)
    { _auth = auth; _s = s; _paths = paths; _act = act; _log = log; }

    public async Task<OperationResult<ProfileDto>> GetMyProfileAsync(CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult<ProfileDto>.AuthRequired();
        var entries = await _s.Library.ReadAsync(d => d.Entries.Where(e => e.UserId == me.Id).Select(e => e.Clone()).ToList(), ct);
        var favIds = entries.Where(e => e.Favorite).Select(e => e.GameId).ToHashSet();
        var favs = await _s.Games.ReadAsync(d => d.Games.Where(g => favIds.Contains(g.Id)).Select(g => g.Title).OrderBy(t => t).ToList(), ct);
        var secs = entries.Sum(e => e.PlayedSeconds);
        var level = 1 + entries.Count + (int)(secs / 36000); // local rule: 1 + games + 1 per 10 h played
        return OperationResult<ProfileDto>.Ok(new ProfileDto(me.UserName, me.DisplayName, me.Bio, level, entries.Count, secs, me.AvatarPath, favs, me.Role));
    }

    public async Task<OperationResult> UpdateProfileAsync(string? displayName, string? bio, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        var v = new ValidationResult();
        v.Merge(TextValidator.Required(displayName, "El nombre", 2, 32));
        v.Merge(TextValidator.Optional(bio, "La descripción", 300));
        if (!v.IsValid) return OperationResult.Fail(ErrorCodes.Invalid, v.AllErrors);
        var result = await _s.Users.UpdateAsync(d =>
        {
            var u = d.Users.FirstOrDefault(x => x.Id == me.Id);
            if (u is null) return (false, OperationResult.Fail(ErrorCodes.NotFound, "Usuario no encontrado."));
            u.DisplayName = displayName!.Trim(); u.Bio = bio ?? "";
            return (true, OperationResult.Ok("Perfil actualizado."));
        }, ct);
        if (result.Success) await _act.LogAsync(me.Id, "Perfil", "Perfil actualizado", ct);
        return result;
    }

    public async Task<OperationResult<string>> SetAvatarAsync(string? sourcePath, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult<string>.AuthRequired();
        var v = ImageValidator.ValidateAvatar(sourcePath);
        if (!v.IsValid) return OperationResult<string>.Fail(ErrorCodes.Invalid, v.FirstError);
        try
        {
            Directory.CreateDirectory(_paths.AvatarsDir);
            var dest = Path.Combine(_paths.AvatarsDir, me.Id.ToString("N") + Path.GetExtension(sourcePath!).ToLowerInvariant());
            File.Copy(sourcePath!, dest, true);
            await _s.Users.UpdateAsync(d =>
            {
                var u = d.Users.FirstOrDefault(x => x.Id == me.Id);
                if (u is null) return (false, 0);
                u.AvatarPath = dest; return (true, 0);
            }, ct);
            return OperationResult<string>.Ok(dest, "Avatar actualizado.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogError("Avatar copy failed: {Type}", ex.GetType().Name);
            return OperationResult<string>.Fail(ErrorCodes.Io, "No se pudo guardar la imagen.");
        }
    }
}

public sealed class AdminService
{
    private readonly AuthService _auth; private readonly DataStores _s; private readonly TimeProvider _time; private readonly ActivityLog _act;
    public AdminService(AuthService auth, DataStores s, TimeProvider time, ActivityLog act) { _auth = auth; _s = s; _time = time; _act = act; }

    public async Task<OperationResult<List<UserAdminDto>>> ListUsersAsync(CancellationToken ct = default)
    {
        if (await _auth.GetCurrentAdminAsync(ct) is null) return OperationResult<List<UserAdminDto>>.Forbidden();
        var now = _time.GetUtcNow().UtcDateTime;
        var list = await _s.Users.ReadAsync(d => d.Users.OrderBy(u => u.UserName)
            .Select(u => new UserAdminDto(u.Id, u.UserName, u.Email, u.Role, u.IsBlocked, u.IsOnline)).ToList(), ct);
        return OperationResult<List<UserAdminDto>>.Ok(list);
    }

    public async Task<OperationResult> ToggleUserBlockAsync(Guid userId, CancellationToken ct = default)
    {
        var admin = await _auth.GetCurrentAdminAsync(ct);
        if (admin is null) return OperationResult.Fail(ErrorCodes.Forbidden, "No tienes permisos de administrador.");
        if (admin.Id == userId) return OperationResult.Fail(ErrorCodes.Invalid, "No puedes bloquear tu propia cuenta.");
        var r = await _s.Users.UpdateAsync(d =>
        {
            var u = d.Users.FirstOrDefault(x => x.Id == userId);
            if (u is null) return (false, OperationResult.Fail(ErrorCodes.NotFound, "Usuario no encontrado."));
            u.IsBlocked = !u.IsBlocked;
            if (!u.IsBlocked) { u.FailedAttempts = 0; u.LockedUntilUtc = null; }
            return (true, OperationResult.Ok(u.IsBlocked ? "Cuenta bloqueada." : "Cuenta desbloqueada."));
        }, ct);
        if (r.Success)
        {
            var blocked = await _s.Users.ReadAsync(d => d.Users.First(u => u.Id == userId).IsBlocked, ct);
            if (blocked) await _auth.RevokeSessionsAsync(userId, ct);
            await _act.LogAsync(admin.Id, "Administración", $"Cuenta {(blocked ? "bloqueada" : "desbloqueada")}: {userId}", ct);
        }
        return r;
    }

    public Task<OperationResult> UnlockUserAsync(Guid userId, CancellationToken ct = default) =>
        ToggleUserBlockAsync(userId, ct);

    public async Task<OperationResult> RevokeSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var admin = await _auth.GetCurrentAdminAsync(ct);
        if (admin is null) return OperationResult.Fail(ErrorCodes.Forbidden, "No tienes permisos de administrador.");
        await _auth.RevokeSessionsAsync(userId, ct);
        await _act.LogAsync(admin.Id, "Administración", $"Sesiones revocadas: {userId}", ct);
        return OperationResult.Ok("Sesiones revocadas.");
    }
}
