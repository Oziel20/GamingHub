using GamingHub.Core.Models;
using GamingHub.Core.Storage;

namespace GamingHub.Core.Services;

/// <summary>Writes activity entries. Reads are exposed only through DashboardService (permission-checked).</summary>
public sealed class ActivityLog
{
    private readonly DataStores _s; private readonly TimeProvider _time;
    public ActivityLog(DataStores s, TimeProvider time) { _s = s; _time = time; }

    public Task LogAsync(Guid userId, string action, string details, CancellationToken ct = default) =>
        _s.Activity.UpdateAsync(d =>
        {
            d.Entries.Add(new ActivityEntry { UserId = userId, Action = action, Details = details, Utc = _time.GetUtcNow().UtcDateTime });
            if (d.Entries.Count > 2000) d.Entries.RemoveRange(0, d.Entries.Count - 2000);
            return (true, 0);
        }, ct);

    internal Task<List<ActivityDto>> ReadForAsync(Guid userId, int take, CancellationToken ct) =>
        _s.Activity.ReadAsync(d => d.Entries.Where(e => e.UserId == userId).OrderByDescending(e => e.Utc)
            .Take(take).Select(e => new ActivityDto(e.Utc, e.Action, e.Details)).ToList(), ct);
}

public sealed class NotificationHub
{
    private readonly DataStores _s; private readonly TimeProvider _time;
    public NotificationHub(DataStores s, TimeProvider time) { _s = s; _time = time; }

    public Task NotifyAsync(Guid userId, string title, string message, CancellationToken ct = default) =>
        _s.Notifications.UpdateAsync(d =>
        {
            d.Notifications.Add(new AppNotification { UserId = userId, Title = title, Message = message, Utc = _time.GetUtcNow().UtcDateTime });
            if (d.Notifications.Count > 2000) d.Notifications.RemoveRange(0, d.Notifications.Count - 2000);
            return (true, 0);
        }, ct);

    internal Task<List<NotificationDto>> ReadForAsync(Guid userId, int take, CancellationToken ct) =>
        _s.Notifications.ReadAsync(d => d.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.Utc)
            .Take(take).Select(n => new NotificationDto(n.Id, n.Utc, n.Title, n.Message, n.Read)).ToList(), ct);

    internal Task MarkAllReadAsync(Guid userId, CancellationToken ct) =>
        _s.Notifications.UpdateAsync(d =>
        {
            foreach (var n in d.Notifications.Where(n => n.UserId == userId)) n.Read = true;
            return (true, 0);
        }, ct);
}

public sealed class DashboardService
{
    private readonly AuthService _auth; private readonly DataStores _s;
    private readonly ActivityLog _activity; private readonly NotificationHub _notes;

    public DashboardService(AuthService auth, DataStores s, ActivityLog activity, NotificationHub notes)
    { _auth = auth; _s = s; _activity = activity; _notes = notes; }

    public async Task<OperationResult<DashboardSummary>> GetSummaryAsync(CancellationToken ct = default)
    {
        var u = await _auth.GetCurrentUserAsync(ct);
        if (u is null) return OperationResult<DashboardSummary>.AuthRequired();
        var (games, secs) = await _s.Library.ReadAsync(d =>
        {
            var mine = d.Entries.Where(e => e.UserId == u.Id).ToList();
            return (mine.Count, mine.Sum(e => e.PlayedSeconds));
        }, ct);
        var friends = await _s.Friends.ReadAsync(d => d.Friendships.Count(f => f.Status == FriendStatus.Accepted &&
            (f.RequesterId == u.Id || f.TargetId == u.Id)), ct);
        var unread = await _s.Notifications.ReadAsync(d => d.Notifications.Count(n => n.UserId == u.Id && !n.Read), ct);
        return OperationResult<DashboardSummary>.Ok(new DashboardSummary(u.DisplayName, games, secs, friends, unread));
    }

    public async Task<OperationResult<List<ActivityDto>>> GetActivityAsync(int take = 50, CancellationToken ct = default)
    {
        var u = await _auth.GetCurrentUserAsync(ct);
        return u is null ? OperationResult<List<ActivityDto>>.AuthRequired()
            : OperationResult<List<ActivityDto>>.Ok(await _activity.ReadForAsync(u.Id, Math.Clamp(take, 1, 200), ct));
    }

    public async Task<OperationResult<List<NotificationDto>>> GetNotificationsAsync(int take = 50, CancellationToken ct = default)
    {
        var u = await _auth.GetCurrentUserAsync(ct);
        return u is null ? OperationResult<List<NotificationDto>>.AuthRequired()
            : OperationResult<List<NotificationDto>>.Ok(await _notes.ReadForAsync(u.Id, Math.Clamp(take, 1, 200), ct));
    }

    public async Task<OperationResult> MarkNotificationsReadAsync(CancellationToken ct = default)
    {
        var u = await _auth.GetCurrentUserAsync(ct);
        if (u is null) return OperationResult.AuthRequired();
        await _notes.MarkAllReadAsync(u.Id, ct);
        return OperationResult.Ok();
    }
}
