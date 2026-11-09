using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly LibraryService _lib;
    private string _lastKey = "";
    private bool _ready, _suppress;

    public LibraryViewModel(LibraryService lib, ILogger<LibraryViewModel> log) : base(log) { _lib = lib; }

    public ObservableCollection<LibraryItemDto> Items { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { LibraryService.AllCategories };

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _selectedCategory = LibraryService.AllCategories;
    [ObservableProperty] private int _sortIndex;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private string _pageText = "";
    [ObservableProperty] private bool _noResults;
    [ObservableProperty] private bool _canPrev;
    [ObservableProperty] private bool _canNext;
    [ObservableProperty] private string _newTitle = "";
    [ObservableProperty] private string _newCategory = "";
    [ObservableProperty] private string _newPath = "";

    partial void OnSortIndexChanged(int value) { if (_ready) _ = SearchCommand.ExecuteAsync(null); }
    partial void OnSelectedCategoryChanged(string? value) { if (_ready && !_suppress) _ = SearchCommand.ExecuteAsync(null); }

    public async Task LoadAsync()
    {
        await RunAsync(() => LoadCoreAsync(true));
        _ready = true;
    }

    private async Task LoadCoreAsync(bool force)
    {
        var prev = SelectedCategory ?? LibraryService.AllCategories;
        var cats = await _lib.GetCategoriesAsync();
        if (cats.Success && !cats.Value!.SequenceEqual(Categories))
        {
            _suppress = true;
            Categories.Clear();
            foreach (var c in cats.Value!) Categories.Add(c);
            SelectedCategory = Categories.Contains(prev) ? prev : LibraryService.AllCategories;
            _suppress = false;
        }
        var key = $"{SearchText}|{SelectedCategory}|{SortIndex}|{Page}";
        if (!force && key == _lastKey) return;   // avoid pointless repeated searches
        var r = await _lib.SearchAsync(SearchText, SelectedCategory, (LibrarySort)SortIndex, Page);
        if (!r.Success) { Fail(r.Message); return; }
        if (r.Value!.Items.Count == 0 && Page > 1) { Page = 1; await LoadCoreAsync(true); return; }
        _lastKey = key;
        Items.Clear();
        foreach (var i in r.Value.Items) Items.Add(i);
        NoResults = r.Value.TotalCount == 0;
        PageText = $"Página {r.Value.Page} de {r.Value.TotalPages} · {r.Value.TotalCount} juego(s)";
        CanPrev = Page > 1; CanNext = Page < r.Value.TotalPages;
        ClearMessages();
    }

    [RelayCommand] private Task SearchAsync() { Page = 1; return RunAsync(() => LoadCoreAsync(false)); }
    [RelayCommand] private Task ClearSearchAsync() { SearchText = ""; SelectedCategory = LibraryService.AllCategories; Page = 1; return RunAsync(() => LoadCoreAsync(false)); }
    [RelayCommand] private Task NextPageAsync() { Page++; return RunAsync(() => LoadCoreAsync(false)); }
    [RelayCommand] private Task PrevPageAsync() { if (Page > 1) Page--; return RunAsync(() => LoadCoreAsync(false)); }

    private Task Act(Func<Task<OperationResult>> op) => RunAsync(async () =>
    {
        var r = await op();
        await LoadCoreAsync(true);
        if (r.Success) Succeed(r.Message.Length > 0 ? r.Message : "Listo."); else Fail(r.Message);
    });

    [RelayCommand] private Task RemoveAsync(string? gameId) => Act(() => _lib.RemoveAsync(gameId));
    [RelayCommand] private Task PlayAsync(string? gameId) => Act(() => _lib.PlayAsync(gameId));
    [RelayCommand] private Task ToggleInstallAsync(string? gameId) => Act(() => _lib.ToggleInstalledAsync(gameId));
    [RelayCommand] private Task ToggleFavoriteAsync(string? gameId) => Act(() => _lib.ToggleFavoriteAsync(gameId));

    [RelayCommand]
    private Task AddLocalAsync() => RunAsync(async () =>
    {
        var r = await _lib.AddLocalGameAsync(NewTitle, NewCategory, NewPath);
        if (r.Success) { NewTitle = ""; NewCategory = ""; NewPath = ""; }
        await LoadCoreAsync(true);
        if (r.Success) Succeed(r.Message); else Fail(r.Message);
    });
}
