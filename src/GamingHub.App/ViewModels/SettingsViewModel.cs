using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AuthService _auth; private readonly SettingsService _settings; private readonly AdminService _admin;
    public SettingsViewModel(AuthService auth, SettingsService settings, AdminService admin, ILogger<SettingsViewModel> log) : base(log)
    { _auth = auth; _settings = settings; _admin = admin; }

    public event Action? LoggedOut;
    public ObservableCollection<string> Roots { get; } = new();
    public ObservableCollection<UserAdminDto> Users { get; } = new();
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _newRoot = "";

    public Task LoadAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        IsAdmin = await _auth.GetCurrentAdminAsync() is not null;
        var roots = await _settings.GetRootsAsync();
        Roots.Clear(); if (roots.Success) foreach (var r in roots.Value!) Roots.Add(r);
        Users.Clear();
        if (IsAdmin)
        {
            var u = await _admin.ListUsersAsync();
            if (u.Success) foreach (var x in u.Value!) Users.Add(x);
        }
    }

    private Task Act(Func<Task<OperationResult>> op) => RunAsync(async () =>
    {
        var r = await op();
        await LoadCoreAsync();
        if (r.Success) Succeed(r.Message.Length > 0 ? r.Message : "Listo."); else Fail(r.Message);
    });

    [RelayCommand]
    private Task AddRootAsync() => RunAsync(async () =>
    {
        var r = await _settings.AddRootAsync(NewRoot);
        if (r.Success) NewRoot = "";
        await LoadCoreAsync();
        if (r.Success) Succeed(r.Message); else Fail(r.Message);
    });

    [RelayCommand] private Task RemoveRootAsync(string? path) => Act(() => _settings.RemoveRootAsync(path));
    [RelayCommand] private Task UnlockAsync(Guid id) => Act(() => _admin.UnlockUserAsync(id));
    [RelayCommand] private Task RevokeAsync(Guid id) => Act(() => _admin.RevokeSessionsAsync(id));

    [RelayCommand]
    private Task LogoutEverywhereAsync() => RunAsync(async () =>
    {
        var r = await _auth.LogoutEverywhereAsync();
        if (r.Success) LoggedOut?.Invoke(); else Fail(r.Message);
    });
}
