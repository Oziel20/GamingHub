using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly AuthService _auth; private readonly DashboardService _dash;
    public ShellViewModel(AuthService auth, DashboardService dash, ILogger<ShellViewModel> log) : base(log) { _auth = auth; _dash = dash; }

    public event Action? LoggedOut;
    [ObservableProperty] private string _userText = "";
    [ObservableProperty] private int _unread;

    /// <summary>Also acts as the session heartbeat: an expired/revoked session sends the user back to login.</summary>
    public async Task RefreshAsync()
    {
        var u = await _auth.GetCurrentUserAsync();
        if (u is null) { LoggedOut?.Invoke(); return; }
        UserText = $"{u.DisplayName} · {u.Role}";
        var s = await _dash.GetSummaryAsync();
        if (s.Success) Unread = s.Value!.UnreadNotifications;
    }

    [RelayCommand]
    private Task LogoutAsync() => RunAsync(async () => { await _auth.LogoutAsync(); LoggedOut?.Invoke(); });
}
