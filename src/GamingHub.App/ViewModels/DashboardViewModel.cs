using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _dash;
    public DashboardViewModel(DashboardService dash, ILogger<DashboardViewModel> log) : base(log) { _dash = dash; }

    public ObservableCollection<ActivityDto> Activity { get; } = new();
    public ObservableCollection<NotificationDto> Notifications { get; } = new();
    [ObservableProperty] private string _welcome = "";
    [ObservableProperty] private string _stats = "";

    public Task LoadAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var s = await _dash.GetSummaryAsync();
        if (!s.Success) { Fail(s.Message); return; }
        Welcome = $"Hola, {s.Value!.DisplayName}";
        Stats = $"{s.Value.GamesCount} juego(s) · {s.Value.PlayedText} jugadas · {s.Value.Friends} amigo(s) · {s.Value.UnreadNotifications} notificación(es) sin leer";
        var a = await _dash.GetActivityAsync(50);
        Activity.Clear(); if (a.Success) foreach (var x in a.Value!) Activity.Add(x);
        var n = await _dash.GetNotificationsAsync(50);
        Notifications.Clear(); if (n.Success) foreach (var x in n.Value!) Notifications.Add(x);
    }

    [RelayCommand]
    private Task MarkReadAsync() => RunAsync(async () =>
    {
        var r = await _dash.MarkNotificationsReadAsync();
        await LoadCoreAsync();
        if (r.Success) Succeed("Notificaciones marcadas como leídas."); else Fail(r.Message);
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadCoreAsync);
}
