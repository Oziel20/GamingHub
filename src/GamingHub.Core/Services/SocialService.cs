using GamingHub.Core.Models;
using GamingHub.Core.Storage;
using GamingHub.Core.Validation;

namespace GamingHub.Core.Services;

/// <summary>Friends and private messages. LOCAL ONLY: messages never leave this machine.</summary>
public sealed class SocialService
{
    public const int MaxMessageLength = 2000;
    private const int MaxMessagesPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private readonly AuthService _auth; private readonly DataStores _s; private readonly NotificationHub _notes;
    private readonly ActivityLog _act; private readonly TimeProvider _time;
    private readonly Dictionary<Guid, Queue<DateTime>> _sent = new();
    private readonly object _rateLock = new();

    public SocialService(AuthService auth, DataStores s, NotificationHub notes, ActivityLog act, TimeProvider time)
    { _auth = auth; _s = s; _notes = notes; _act = act; _time = time; }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<OperationResult<List<FriendDto>>> GetFriendsAsync(CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult<List<FriendDto>>.AuthRequired();
        var rels = await _s.Friends.ReadAsync(d => d.Friendships.Where(f => f.RequesterId == me.Id || f.TargetId == me.Id)
            .Select(f => new Friendship { Id = f.Id, RequesterId = f.RequesterId, TargetId = f.TargetId, Status = f.Status, Utc = f.Utc }).ToList(), ct);
        var users = await _s.Users.ReadAsync(d => d.Users.ToDictionary(u => u.Id, u => u.Clone()), ct);
        var list = new List<FriendDto>();
        foreach (var f in rels)
        {
            var otherId = f.RequesterId == me.Id ? f.TargetId : f.RequesterId;
            // A block set by the other person is invisible to the blocked user.
            if (f.Status == FriendStatus.Blocked && f.RequesterId != me.Id) continue;
            if (!users.TryGetValue(otherId, out var o)) continue;
            list.Add(new FriendDto(f.Id, o.Id, o.UserName, o.DisplayName, o.IsOnline, f.Status, f.TargetId == me.Id));
        }
        return OperationResult<List<FriendDto>>.Ok(list.OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<OperationResult> SendRequestAsync(string? userName, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        var v = UserNameValidator.Validate(userName);
        if (!v.IsValid) return OperationResult.Fail(ErrorCodes.Invalid, v.FirstError);
        var key = UserNameValidator.Normalize(userName);
        var target = await _s.Users.ReadAsync(d => d.Users.FirstOrDefault(u => u.UserNameKey == key)?.Clone(), ct);
        if (target is null) return OperationResult.Fail(ErrorCodes.NotFound, "No se encontró ese usuario.");
        if (target.Id == me.Id) return OperationResult.Fail(ErrorCodes.Invalid, "No puedes agregarte a ti mismo.");
        var now = Now;
        var result = await _s.Friends.UpdateAsync(d =>
        {
            var ex = d.Friendships.FirstOrDefault(f => (f.RequesterId == me.Id && f.TargetId == target.Id) || (f.RequesterId == target.Id && f.TargetId == me.Id));
            if (ex is not null)
            {
                var msg = ex.Status switch
                {
                    FriendStatus.Accepted => "Ya son amigos.",
                    FriendStatus.Pending => "Ya existe una solicitud pendiente.",
                    _ => "No se puede enviar la solicitud."
                };
                return (false, OperationResult.Fail(ex.Status == FriendStatus.Blocked ? ErrorCodes.Forbidden : ErrorCodes.Duplicate, msg));
            }
            d.Friendships.Add(new Friendship { RequesterId = me.Id, TargetId = target.Id, Status = FriendStatus.Pending, Utc = now });
            return (true, OperationResult.Ok("Solicitud enviada."));
        }, ct);
        if (result.Success)
        {
            await _notes.NotifyAsync(target.Id, "Solicitud de amistad", $"{me.UserName} quiere ser tu amigo.", ct);
            await _act.LogAsync(me.Id, "Amigos", $"Solicitud enviada a {target.UserName}", ct);
        }
        return result;
    }

    public async Task<OperationResult> AcceptAsync(Guid friendshipId, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        return await _s.Friends.UpdateAsync(d =>
        {
            var f = d.Friendships.FirstOrDefault(x => x.Id == friendshipId && x.TargetId == me.Id && x.Status == FriendStatus.Pending);
            if (f is null) return (false, OperationResult.Fail(ErrorCodes.NotFound, "Solicitud no encontrada."));
            f.Status = FriendStatus.Accepted;
            return (true, OperationResult.Ok("Solicitud aceptada."));
        }, ct);
    }

    public async Task<OperationResult> RejectAsync(Guid friendshipId, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        return await _s.Friends.UpdateAsync(d =>
        {
            var n = d.Friendships.RemoveAll(x => x.Id == friendshipId && x.TargetId == me.Id && x.Status == FriendStatus.Pending);
            return n == 0 ? (false, OperationResult.Fail(ErrorCodes.NotFound, "Solicitud no encontrada."))
                          : (true, OperationResult.Ok("Solicitud rechazada."));
        }, ct);
    }

    public async Task<OperationResult> RemoveAsync(Guid friendshipId, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        return await _s.Friends.UpdateAsync(d =>
        {
            // Only parties of an Accepted/Pending relation may remove it; an unblock is done by the blocker.
            var n = d.Friendships.RemoveAll(x => x.Id == friendshipId && (x.RequesterId == me.Id || x.TargetId == me.Id)
                && (x.Status != FriendStatus.Blocked || x.RequesterId == me.Id));
            return n == 0 ? (false, OperationResult.Fail(ErrorCodes.NotFound, "Relación no encontrada."))
                          : (true, OperationResult.Ok("Eliminado."));
        }, ct);
    }

    public async Task<OperationResult> BlockAsync(Guid otherUserId, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        if (otherUserId == me.Id) return OperationResult.Fail(ErrorCodes.Invalid, "No puedes bloquearte a ti mismo.");
        if (!await _s.Users.ReadAsync(d => d.Users.Any(u => u.Id == otherUserId), ct))
            return OperationResult.Fail(ErrorCodes.NotFound, "Usuario no encontrado.");
        var now = Now;
        return await _s.Friends.UpdateAsync(d =>
        {
            d.Friendships.RemoveAll(f => (f.RequesterId == me.Id && f.TargetId == otherUserId) || (f.RequesterId == otherUserId && f.TargetId == me.Id));
            d.Friendships.Add(new Friendship { RequesterId = me.Id, TargetId = otherUserId, Status = FriendStatus.Blocked, Utc = now });
            return (true, OperationResult.Ok("Usuario bloqueado."));
        }, ct);
    }

    public static ValidationResult ValidateMessage(string? text)
    {
        var r = new ValidationResult();
        if (string.IsNullOrWhiteSpace(text)) { r.Add("El mensaje no puede estar vacío."); return r; }
        if (text.Length > MaxMessageLength) { r.Add($"El mensaje no puede superar {MaxMessageLength} caracteres."); return r; }
        if (text.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')) r.Add("El mensaje contiene caracteres no permitidos.");
        return r;
    }

    private async Task<bool> AreFriendsAsync(Guid a, Guid b, CancellationToken ct) =>
        await _s.Friends.ReadAsync(d => d.Friendships.Any(f => f.Status == FriendStatus.Accepted &&
            ((f.RequesterId == a && f.TargetId == b) || (f.RequesterId == b && f.TargetId == a))), ct);

    public async Task<OperationResult> SendMessageAsync(Guid toUserId, string? text, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult.AuthRequired();
        var v = ValidateMessage(text);
        if (!v.IsValid) return OperationResult.Fail(ErrorCodes.Invalid, v.FirstError);
        if (!await AreFriendsAsync(me.Id, toUserId, ct)) return OperationResult.Fail(ErrorCodes.Forbidden, "Solo puedes escribir a tus amigos (o el usuario te bloqueó).");
        var now = Now;
        lock (_rateLock)
        {
            if (!_sent.TryGetValue(me.Id, out var q)) _sent[me.Id] = q = new Queue<DateTime>();
            while (q.Count > 0 && now - q.Peek() > Window) q.Dequeue();
            if (q.Count >= MaxMessagesPerWindow) return OperationResult.Fail(ErrorCodes.RateLimit, "Estás enviando mensajes demasiado rápido.");
            q.Enqueue(now);
        }
        await _s.Messages.UpdateAsync(d =>
        {
            d.Messages.Add(new ChatMessage { FromId = me.Id, ToId = toUserId, Text = text!, Utc = now });
            return (true, 0);
        }, ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult<List<MessageDto>>> GetConversationAsync(Guid otherUserId, int take = 200, CancellationToken ct = default)
    {
        var me = await _auth.GetCurrentUserAsync(ct);
        if (me is null) return OperationResult<List<MessageDto>>.AuthRequired();
        if (!await AreFriendsAsync(me.Id, otherUserId, ct)) return OperationResult<List<MessageDto>>.Forbidden();
        take = Math.Clamp(take, 1, 500);
        var list = await _s.Messages.ReadAsync(d => d.Messages
            .Where(m => (m.FromId == me.Id && m.ToId == otherUserId) || (m.FromId == otherUserId && m.ToId == me.Id))
            .OrderByDescending(m => m.Utc).Take(take).OrderBy(m => m.Utc)
            .Select(m => new MessageDto(m.Id, m.FromId == me.Id, m.Text, m.Utc)).ToList(), ct);
        return OperationResult<List<MessageDto>>.Ok(list);
    }
}
