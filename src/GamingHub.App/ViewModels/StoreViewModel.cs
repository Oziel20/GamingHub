using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Models;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public sealed partial class StoreViewModel : ViewModelBase
{
    private readonly StoreService _store;
    private bool _ready, _suppress;

    public StoreViewModel(StoreService store, ILogger<StoreViewModel> log) : base(log) { _store = store; }

    public ObservableCollection<CatalogItemDto> Items { get; } = new();
    public ObservableCollection<CatalogItemDto> Cart { get; } = new();
    public ObservableCollection<PurchaseDto> Purchases { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { LibraryService.AllCategories };

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _selectedCategory = LibraryService.AllCategories;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private string _pageText = "";
    [ObservableProperty] private bool _noResults;
    [ObservableProperty] private bool _canPrev;
    [ObservableProperty] private bool _canNext;
    [ObservableProperty] private string _cartTotalText = "Carrito vacío";
    [ObservableProperty] private bool _hasCart;

    partial void OnSelectedCategoryChanged(string? value) { if (_ready && !_suppress) _ = SearchCommand.ExecuteAsync(null); }

    public async Task LoadAsync()
    {
        await RunAsync(LoadCoreAsync);
        _ready = true;
    }

    private async Task LoadCoreAsync()
    {
        var prev = SelectedCategory ?? LibraryService.AllCategories;
        var cats = await _store.GetCategoriesAsync();
        if (cats.Success && !cats.Value!.SequenceEqual(Categories))
        {
            _suppress = true;
            Categories.Clear();
            foreach (var c in cats.Value!) Categories.Add(c);
            SelectedCategory = Categories.Contains(prev) ? prev : LibraryService.AllCategories;
            _suppress = false;
        }
        var r = await _store.SearchCatalogAsync(SearchText, SelectedCategory, Page);
        if (!r.Success) { Fail(r.Message); return; }
        if (r.Value!.Items.Count == 0 && Page > 1) { Page = 1; await LoadCoreAsync(); return; }
        Items.Clear(); foreach (var i in r.Value.Items) Items.Add(i);
        NoResults = r.Value.TotalCount == 0;
        PageText = $"Página {r.Value.Page} de {r.Value.TotalPages} · {r.Value.TotalCount} juego(s)";
        CanPrev = Page > 1; CanNext = Page < r.Value.TotalPages;

        var cart = await _store.GetCartAsync();
        Cart.Clear();
        if (cart.Success) foreach (var c in cart.Value!) Cart.Add(c);
        HasCart = Cart.Count > 0;
        CartTotalText = Cart.Count == 0 ? "Carrito vacío" : $"Total estimado: {Cart.Sum(c => c.Price):0.00} {Cart[0].Currency} (el total final lo calcula el servicio)";
        var p = await _store.GetPurchasesAsync();
        Purchases.Clear(); if (p.Success) foreach (var x in p.Value!) Purchases.Add(x);
    }

    [RelayCommand] private Task SearchAsync() { Page = 1; return RunAsync(LoadCoreAsync); }
    [RelayCommand] private Task ClearSearchAsync() { SearchText = ""; SelectedCategory = LibraryService.AllCategories; Page = 1; return RunAsync(LoadCoreAsync); }
    [RelayCommand] private Task NextPageAsync() { Page++; return RunAsync(LoadCoreAsync); }
    [RelayCommand] private Task PrevPageAsync() { if (Page > 1) Page--; return RunAsync(LoadCoreAsync); }

    private Task Act(Func<Task<OperationResult>> op) => RunAsync(async () =>
    {
        var r = await op();
        await LoadCoreAsync();
        if (r.Success) Succeed(r.Message.Length > 0 ? r.Message : "Listo."); else Fail(r.Message);
    });

    [RelayCommand] private Task AddToCartAsync(string? id) => Act(() => _store.AddToCartAsync(id));
    [RelayCommand] private Task RemoveFromCartAsync(string? id) => Act(() => _store.RemoveFromCartAsync(id));

    /// <summary>Ticket de una compra del historial (null si no existe; el error queda en ErrorMessage).</summary>
    public async Task<Ticket?> GetTicketAsync(Guid orderId)
    {
        var r = await _store.GetTicketAsync(orderId);
        if (!r.Success) { Fail(r.Message); return null; }
        ClearMessages();
        return r.Value;
    }

    [RelayCommand]
    private Task CheckoutAsync() => RunAsync(async () =>
    {
        var r = await _store.CheckoutAsync();
        await LoadCoreAsync();
        if (r.Success) Succeed($"Compra SIMULADA completada: {r.Value!.Count} juego(s), {r.Value.Total:0.00} {r.Value.Currency}. No se realizó ningún pago real.");
        else Fail(r.Message);
    });
}
