using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected ILogger Log { get; }
    protected ViewModelBase(ILogger log) { Log = log; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string _successMessage = "";

    public bool HasError => ErrorMessage.Length > 0;
    public bool HasSuccess => SuccessMessage.Length > 0;

    protected void Fail(string message) { SuccessMessage = ""; ErrorMessage = message; }
    protected void Succeed(string message) { ErrorMessage = ""; SuccessMessage = message; }
    protected void ClearMessages() { ErrorMessage = ""; SuccessMessage = ""; }

    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex)
        {
            Log.LogError("Unhandled error in view model: {Type}", ex.GetType().Name);
            Fail("Ocurrió un error inesperado. Inténtalo de nuevo.");
        }
        finally { IsBusy = false; }
    }
}
