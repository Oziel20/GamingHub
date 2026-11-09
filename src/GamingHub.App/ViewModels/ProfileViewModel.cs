using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class ProfileViewModel : ViewModelBase
{
    private readonly ProfileService _profile;
    public ProfileViewModel(ProfileService profile, ILogger<ProfileViewModel> log) : base(log) { _profile = profile; }

    public ObservableCollection<string> Favorites { get; } = new();
    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _bio = "";
    [ObservableProperty] private string _stats = "";
    [ObservableProperty] private Uri? _avatarUri;

    public Task LoadAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var r = await _profile.GetMyProfileAsync();
        if (!r.Success) { Fail(r.Message); return; }
        var p = r.Value!;
        UserName = p.UserName; DisplayName = p.DisplayName; Bio = p.Bio;
        Stats = $"Nivel {p.Level} · {p.GamesCount} juego(s) · {p.PlayedText} jugadas · Rol: {p.Role}";
        AvatarUri = p.AvatarPath is not null && File.Exists(p.AvatarPath) ? new Uri(p.AvatarPath) : null;
        Favorites.Clear(); foreach (var f in p.FavoriteTitles) Favorites.Add(f);
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var r = await _profile.UpdateProfileAsync(DisplayName, Bio);
        if (r.Success) { await LoadCoreAsync(); Succeed(r.Message); } else Fail(r.Message);
    });

    [RelayCommand]
    private Task SetAvatarAsync(string? path) => RunAsync(async () =>
    {
        var r = await _profile.SetAvatarAsync(path);
        if (r.Success) { await LoadCoreAsync(); Succeed(r.Message); } else Fail(r.Message);
    });
}
