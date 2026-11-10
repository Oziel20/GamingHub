using GamingHub.Core.Models;
using GamingHub.Core.Security;
using GamingHub.Core.Storage;
using GamingHub.Core.Validation;
using Microsoft.Extensions.Logging;

namespace GamingHub.Core.Services;

public sealed record RegisterRequest(string? UserName, string? Email, string? Password, string? ConfirmPassword,
    DateOnly? BirthDate, bool AcceptedTerms);

public sealed record AuthSession(Guid SessionId, string Token, Guid UserId, string UserName, UserRole Role, DateTime ExpiresUtc);

public sealed class AuthService
{
    private const string Generic = "Credenciales incorrectas o cuenta temporalmente bloqueada.";
    private readonly DataStores _s;
    private readonly IPasswordHasher _hasher;
    private readonly SecurityOptions _o;
    private readonly TimeProvider _time;
    private readonly ActivityLog _activity;
    private readonly ILogger<AuthService> _log;
    private readonly Lazy<string> _dummyHash;
    private string? _currentToken;

    public AuthService(DataStores s, IPasswordHasher hasher, SecurityOptions o, TimeProvider time,
        ActivityLog activity, ILogger<AuthService> log)
    {
        _s = s; _hasher = hasher; _o = o; _time = time; _activity = activity; _log = log;
        _dummyHash = new Lazy<string>(() => _hasher.Hash("dummy-Password-for-timing-1!"));
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;
    public bool IsAuthenticated => _currentToken is not null;

    public async Task<OperationResult<User>> RegisterAsync(RegisterRequest r, CancellationToken ct = default)
    {
        var v = new ValidationResult();
        v.Merge(UserNameValidator.Validate(r.UserName));
        v.Merge(EmailValidator.Validate(r.Email));
        v.Merge(PasswordValidator.Validate(r.Password, r.UserName));
        if (r.Password != r.ConfirmPassword) v.Add("Las contraseñas no coinciden.");
        if (r.BirthDate is null) v.Add("La fecha de nacimiento es obligatoria.");
        else
        {
            var today = DateOnly.FromDateTime(Now);
            var b = r.BirthDate.Value;
            if (b > today) v.Add("La fecha de nacimiento no puede ser futura.");
            else if (b.Year < 1900) v.Add("La fecha de nacimiento no es válida.");
            else if (b > today.AddYears(-_o.MinimumAge)) v.Add($"La edad mínima es {_o.MinimumAge} años.");
        }
        if (!r.AcceptedTerms) v.Add("Debes aceptar los términos.");
        if (!v.IsValid) return OperationResult<User>.Fail(ErrorCodes.Invalid, v.AllErrors);

        var hash = await Task.Run(() => _hasher.Hash(r.Password!), ct);
        var key = UserNameValidator.Normalize(r.UserName);
        var email = EmailValidator.Normalize(r.Email);
        var now = Now;

        var result = await _s.Users.UpdateAsync(d =>
        {
            if (d.Users.Any(u => u.UserNameKey == key || u.Email == email))
                return (false, OperationResult<User>.Fail(ErrorCodes.Duplicate, "El usuario o el correo ya están registrados."));
            var user = new User
            {
                UserName = r.UserName!, UserNameKey = key, Email = email, PasswordHash = hash,
                BirthDate = r.BirthDate!.Value, CreatedUtc = now, DisplayName = r.UserName!,
                Role = d.Users.Count == 0 ? UserRole.Admin : UserRole.User
            };
            d.Users.Add(user);
            return (true, OperationResult<User>.Ok(user.Clone()));
        }, ct);

        if (result.Success)
        {
            _log.LogInformation("User registered {UserId}", result.Value!.Id);
            await _activity.LogAsync(result.Value.Id, "Registro", "Cuenta creada", ct);
        }
        return result;
    }

    public async Task<OperationResult<AuthSession>> LoginAsync(string? userName, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) ||
            userName.Length > 64 || password.Length > PasswordValidator.MaxLength)
            return OperationResult<AuthSession>.Fail(ErrorCodes.AuthFailed, Generic);

        var key = UserNameValidator.Normalize(userName);
        var user = await _s.Users.ReadAsync(d => d.Users.FirstOrDefault(u => u.UserNameKey == key)?.Clone(), ct);
        var now = Now;
        if (user is null)
        {
            await Task.Run(() => _hasher.Verify(password, _dummyHash.Value), ct); // equalize timing
            _log.LogWarning("Login attempt for unknown user");
            return OperationResult<AuthSession>.Fail(ErrorCodes.AuthFailed, Generic);
        }
        if (user.IsBlocked)
        {
            _log.LogWarning("Login attempt on administratively blocked account {UserId}", user.Id);
            return OperationResult<AuthSession>.Fail(ErrorCodes.AuthFailed, Generic);
        }
        if (user.LockedUntilUtc > now)
        {
            _log.LogWarning("Login attempt on temporarily locked account {UserId}", user.Id);
            return OperationResult<AuthSession>.Fail(ErrorCodes.AuthFailed, Generic);
        }

        var ok = await Task.Run(() => _hasher.Verify(password, user.PasswordHash), ct);
        if (!ok)
        {
            await _s.Users.UpdateAsync(d =>
            {
                var u = d.Users.FirstOrDefault(x => x.Id == user.Id);
                if (u is null) return (false, 0);
                u.FailedAttempts++;
                u.LastFailedUtc = now;
                if (u.FailedAttempts >= _o.MaxFailedBeforeLock)
                {
                    var exp = Math.Min(u.FailedAttempts - _o.MaxFailedBeforeLock, 10);
                    var secs = Math.Min(_o.BaseLockSeconds * Math.Pow(2, exp), _o.MaxLockMinutes * 60);
                    u.LockedUntilUtc = now.AddSeconds(secs);
                }
                return (true, 0);
            }, ct);
            _log.LogWarning("Failed login for {UserId}", user.Id);
            return OperationResult<AuthSession>.Fail(ErrorCodes.AuthFailed, Generic);
        }

        var token = TokenHelper.NewToken();
        var session = new SessionRecord { UserId = user.Id, TokenHash = TokenHelper.Hash(token), CreatedUtc = now, ExpiresUtc = now.AddHours(_o.SessionHours) };
        await _s.Sessions.UpdateAsync(d =>
        {
            d.Sessions.RemoveAll(x => x.ExpiresUtc < now.AddDays(-1));
            d.Sessions.Add(session);
            return (true, 0);
        }, ct);
        await _s.Users.UpdateAsync(d =>
        {
            var u = d.Users.FirstOrDefault(x => x.Id == user.Id);
            if (u is null) return (false, 0);
            u.FailedAttempts = 0; u.LockedUntilUtc = null; u.IsOnline = true;
            return (true, 0);
        }, ct);
        _currentToken = token;
        await _activity.LogAsync(user.Id, "Inicio de sesión", "Sesión iniciada", ct);
        return OperationResult<AuthSession>.Ok(new AuthSession(session.Id, token, user.Id, user.UserName, user.Role, session.ExpiresUtc));
    }

    public async Task<User?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var token = _currentToken;
        if (token is null) return null;
        var h = TokenHelper.Hash(token);
        var now = Now;
        var userId = await _s.Sessions.ReadAsync(d =>
            d.Sessions.FirstOrDefault(x => x.TokenHash == h && !x.Revoked && x.ExpiresUtc > now)?.UserId, ct);
        if (userId is null) { _currentToken = null; return null; }
        return await _s.Users.ReadAsync(d => d.Users.FirstOrDefault(u => u.Id == userId)?.Clone(), ct);
    }

    public async Task<User?> GetCurrentAdminAsync(CancellationToken ct = default)
    {
        var u = await GetCurrentUserAsync(ct);
        return u is { Role: UserRole.Admin } ? u : null;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        var token = _currentToken;
        _currentToken = null;
        if (user is null || token is null) return;
        var h = TokenHelper.Hash(token);
        await _s.Sessions.UpdateAsync(d =>
        {
            foreach (var x in d.Sessions.Where(x => x.TokenHash == h)) x.Revoked = true;
            return (true, 0);
        }, ct);
        await SetOnlineAsync(user.Id, ct);
        await _activity.LogAsync(user.Id, "Cierre de sesión", "Sesión cerrada", ct);
    }

    public async Task<OperationResult> LogoutEverywhereAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        if (user is null) return OperationResult.AuthRequired();
        await RevokeSessionsAsync(user.Id, ct);
        _currentToken = null;
        await SetOnlineAsync(user.Id, ct);
        return OperationResult.Ok("Todas las sesiones fueron cerradas.");
    }

    internal Task RevokeSessionsAsync(Guid userId, CancellationToken ct) =>
        _s.Sessions.UpdateAsync(d =>
        {
            foreach (var x in d.Sessions.Where(x => x.UserId == userId)) x.Revoked = true;
            return (true, 0);
        }, ct);

    private Task SetOnlineAsync(Guid userId, CancellationToken ct) =>
        _s.Users.UpdateAsync(d =>
        {
            var u = d.Users.FirstOrDefault(x => x.Id == userId);
            if (u is null) return (false, 0);
            u.IsOnline = false; return (true, 0);
        }, ct);

    /// <summary>
    /// Local-only recovery: there is no email infrastructure, so the one-time token is returned to the caller.
    /// Known limitation: this reveals whether an email is registered.
    /// </summary>
    public async Task<OperationResult<string>> RequestRecoveryAsync(string? email, CancellationToken ct = default)
    {
        var ev = EmailValidator.Validate(email);
        if (!ev.IsValid) return OperationResult<string>.Fail(ErrorCodes.Invalid, ev.FirstError);
        var key = EmailValidator.Normalize(email);
        var now = Now;
        var user = await _s.Users.ReadAsync(d => d.Users.FirstOrDefault(u => u.Email == key)?.Clone(), ct);
        if (user is null) return OperationResult<string>.Fail("RECOVERY_UNAVAILABLE", "No se pudo generar un token para ese correo.");
        var token = TokenHelper.NewToken();
        var result = await _s.Sessions.UpdateAsync(d =>
        {
            var recent = d.RecoveryTokens.Count(t => t.UserId == user.Id && t.CreatedUtc > now.AddHours(-1));
            if (recent >= _o.MaxRecoveryRequestsPerHour)
                return (false, OperationResult<string>.Fail(ErrorCodes.RateLimit, "Demasiadas solicitudes de recuperación. Inténtalo más tarde."));
            d.RecoveryTokens.RemoveAll(t => t.ExpiresUtc < now.AddDays(-1));
            d.RecoveryTokens.Add(new RecoveryToken
            {
                UserId = user.Id, TokenHash = TokenHelper.Hash(token), CreatedUtc = now,
                ExpiresUtc = now.AddMinutes(_o.RecoveryTokenMinutes)
            });
            return (true, OperationResult<string>.Ok(token));
        }, ct);
        if (result.Success) _log.LogInformation("Recovery token issued for {UserId}", user.Id);
        return result;
    }

    public async Task<OperationResult> ResetPasswordAsync(string? token, string? newPassword, string? confirm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return OperationResult.Fail(ErrorCodes.Invalid, "Token inválido o caducado.");
        var h = TokenHelper.Hash(token);
        var now = Now;
        var userId = await _s.Sessions.ReadAsync(d =>
            d.RecoveryTokens.FirstOrDefault(t => t.TokenHash == h && !t.Used && t.ExpiresUtc > now)?.UserId, ct);
        if (userId is null) return OperationResult.Fail(ErrorCodes.Invalid, "Token inválido o caducado.");
        var user = await _s.Users.ReadAsync(d => d.Users.FirstOrDefault(u => u.Id == userId)?.Clone(), ct);
        if (user is null) return OperationResult.Fail(ErrorCodes.Invalid, "Token inválido o caducado.");

        var v = PasswordValidator.Validate(newPassword, user.UserName);
        if (newPassword != confirm) v.Add("Las contraseñas no coinciden.");
        if (!v.IsValid) return OperationResult.Fail(ErrorCodes.Invalid, v.AllErrors);

        var newHash = await Task.Run(() => _hasher.Hash(newPassword!), ct);
        var consumed = await _s.Sessions.UpdateAsync(d =>
        {
            var t = d.RecoveryTokens.FirstOrDefault(x => x.TokenHash == h && !x.Used && x.ExpiresUtc > now);
            if (t is null) return (false, false);
            t.Used = true;
            foreach (var s in d.Sessions.Where(s => s.UserId == t.UserId)) s.Revoked = true;
            return (true, true);
        }, ct);
        if (!consumed) return OperationResult.Fail(ErrorCodes.Invalid, "Token inválido o caducado.");

        await _s.Users.UpdateAsync(d =>
        {
            var u = d.Users.FirstOrDefault(x => x.Id == user.Id);
            if (u is null) return (false, 0);
            u.PasswordHash = newHash; u.FailedAttempts = 0; u.LockedUntilUtc = null; u.IsOnline = false;
            return (true, 0);
        }, ct);
        _currentToken = null;
        await _activity.LogAsync(user.Id, "Contraseña", "Contraseña restablecida; sesiones revocadas", ct);
        return OperationResult.Ok("Contraseña actualizada. Inicia sesión de nuevo.");
    }
}
