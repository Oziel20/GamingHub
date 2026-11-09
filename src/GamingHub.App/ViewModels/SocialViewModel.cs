using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class SocialViewModel : ViewModelBase
{
    private readonly SocialService _social;
    public SocialViewModel(SocialService social, ILogger<SocialViewModel> log) : base(log) { _social = social; }

    public ObservableCollection<FriendDto> Friends { get; } = new();
    public ObservableCollection<MessageDto> Messages { get; } = new();

    [ObservableProperty] private string _newFriendName = "";
    [ObservableProperty] private string _messageText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChat), nameof(ChatTitle))]
    private FriendDto? _selectedFriend;

    public bool CanChat => SelectedFriend is { IsAccepted: true };
    public string ChatTitle => CanChat ? $"Chat local con {SelectedFriend!.UserName}" : "Selecciona un amigo aceptado para chatear";

    partial void OnSelectedFriendChanged(FriendDto? value) => _ = LoadMessagesAsync();

    public Task LoadAsync() => RunAsync(LoadFriendsCoreAsync);

    private async Task LoadFriendsCoreAsync()
    {
        var selectedId = SelectedFriend?.FriendshipId;
        var r = await _social.GetFriendsAsync();
        if (!r.Success) { Fail(r.Message); return; }
        Friends.Clear();
        foreach (var f in r.Value!) Friends.Add(f);
        SelectedFriend = selectedId is null ? null : Friends.FirstOrDefault(f => f.FriendshipId == selectedId);
    }

    private async Task LoadMessagesAsync()
    {
        try
        {
            Messages.Clear();
            var f = SelectedFriend;
            if (f is null || !f.IsAccepted) return;
            var r = await _social.GetConversationAsync(f.UserId);
            if (!r.Success) { Fail(r.Message); return; }
            foreach (var m in r.Value!) Messages.Add(m);
        }
        catch (Exception ex) { Log.LogError("Loading conversation failed: {Type}", ex.GetType().Name); }
    }

    private Task Act(Func<Task<OperationResult>> op) => RunAsync(async () =>
    {
        var r = await op();
        await LoadFriendsCoreAsync();
        if (r.Success) Succeed(r.Message.Length > 0 ? r.Message : "Listo."); else Fail(r.Message);
    });

    [RelayCommand]
    private Task AddFriendAsync() => RunAsync(async () =>
    {
        var r = await _social.SendRequestAsync(NewFriendName);
        if (r.Success) NewFriendName = "";
        await LoadFriendsCoreAsync();
        if (r.Success) Succeed(r.Message); else Fail(r.Message);
    });

    [RelayCommand] private Task AcceptAsync(Guid id) => Act(() => _social.AcceptAsync(id));
    [RelayCommand] private Task RejectAsync(Guid id) => Act(() => _social.RejectAsync(id));
    [RelayCommand] private Task RemoveAsync(Guid id) => Act(() => _social.RemoveAsync(id));
    [RelayCommand] private Task BlockAsync(Guid userId) => Act(() => _social.BlockAsync(userId));
    [RelayCommand] private Task RefreshAsync() => RunAsync(async () => { await LoadFriendsCoreAsync(); await LoadMessagesAsync(); });

    [RelayCommand]
    private Task SendAsync() => RunAsync(async () =>
    {
        if (SelectedFriend is not { IsAccepted: true } f) { Fail("Selecciona un amigo aceptado."); return; }
        var r = await _social.SendMessageAsync(f.UserId, MessageText);
        if (!r.Success) { Fail(r.Message); return; }
        MessageText = "";
        ClearMessages();
        await LoadMessagesAsync();
    });
}
