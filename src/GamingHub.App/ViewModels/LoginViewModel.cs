using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamingHub.Core.Services;
using Microsoft.Extensions.Logging;

namespace GamingHub.App.ViewModels;

public enum AuthMode { Login, Register, Recover }

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    public LoginViewModel(AuthService auth, ILogger<LoginViewModel> log) : base(log) { _auth = auth; }

    public event Action? LoggedIn;
    public event Action? CredentialsCleared;

    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private DateTimeOffset? _birthDate;
    [ObservableProperty] private bool _acceptedTerms;
    [ObservableProperty] private string _recoveryToken = "";
    [ObservableProperty] private string _generatedToken = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginMode), nameof(IsRegisterMode), nameof(IsRecoverMode), nameof(Title),
        nameof(ShowUserName), nameof(ShowEmail), nameof(ShowConfirm), nameof(PasswordLabel))]
    private AuthMode _mode = AuthMode.Login;

    public bool IsLoginMode => Mode == AuthMode.Login;
    public bool IsRegisterMode => Mode == AuthMode.Register;
    public bool IsRecoverMode => Mode == AuthMode.Recover;
    public bool ShowUserName => Mode != AuthMode.Recover;
    public bool ShowEmail => Mode != AuthMode.Login;
    public bool ShowConfirm => Mode != AuthMode.Login;
    public bool HasGeneratedToken => GeneratedToken.Length > 0;
    public string PasswordLabel => IsRecoverMode ? "Nueva contraseña (12-64 caracteres)" : "Contraseña (12-64 caracteres)";
    public string Title => Mode switch { AuthMode.Login => "Inicia sesión", AuthMode.Register => "Crea tu cuenta", _ => "Recuperar contraseña (local)" };
    public string MatchText => ConfirmPassword.Length == 0 ? "" : Password == ConfirmPassword ? "✔ Las contraseñas coinciden" : "✖ Las contraseñas no coinciden";
    public bool PasswordsMatch => ConfirmPassword.Length > 0 && Password == ConfirmPassword;

    partial void OnPasswordChanged(string value) { OnPropertyChanged(nameof(MatchText)); OnPropertyChanged(nameof(PasswordsMatch)); ClearMessages(); }
    partial void OnConfirmPasswordChanged(string value) { OnPropertyChanged(nameof(MatchText)); OnPropertyChanged(nameof(PasswordsMatch)); ClearMessages(); }
    partial void OnUserNameChanged(string value) => ClearMessages();
    partial void OnEmailChanged(string value) => ClearMessages();
    partial void OnGeneratedTokenChanged(string value) => OnPropertyChanged(nameof(HasGeneratedToken));

    private void ClearSensitive()
    {
        Password = ""; ConfirmPassword = ""; RecoveryToken = "";
        CredentialsCleared?.Invoke();
    }

    [RelayCommand] private void ShowLogin() { Mode = AuthMode.Login; ClearSensitive(); ClearMessages(); GeneratedToken = ""; }
    [RelayCommand] private void ShowRegister() { Mode = AuthMode.Register; ClearSensitive(); ClearMessages(); }
    [RelayCommand] private void ShowRecover() { Mode = AuthMode.Recover; ClearSensitive(); ClearMessages(); }

    [RelayCommand]
    private Task LoginAsync() => RunAsync(async () =>
    {
        var r = await _auth.LoginAsync(UserName, Password);
        if (!r.Success) { Fail(r.Message); return; }
        ClearSensitive();
        LoggedIn?.Invoke();
    });

    [RelayCommand]
    private Task RegisterAsync() => RunAsync(async () =>
    {
        DateOnly? birth = BirthDate is { } d ? DateOnly.FromDateTime(d.Date) : null;
        var r = await _auth.RegisterAsync(new RegisterRequest(UserName, Email, Password, ConfirmPassword, birth, AcceptedTerms));
        if (!r.Success) { Fail(r.Message); return; }
        ClearSensitive();
        Mode = AuthMode.Login;
        Succeed("Cuenta creada. Ahora puedes iniciar sesión.");
    });

    [RelayCommand]
    private Task RequestTokenAsync() => RunAsync(async () =>
    {
        var r = await _auth.RequestRecoveryAsync(Email);
        if (!r.Success) { Fail(r.Message); return; }
        GeneratedToken = r.Value!;
        Succeed("Token local generado (válido 15 minutos, un solo uso). No se envió ningún correo: esta app no tiene servidor de correo.");
    });

    [RelayCommand]
    private Task ResetPasswordAsync() => RunAsync(async () =>
    {
        var r = await _auth.ResetPasswordAsync(RecoveryToken, Password, ConfirmPassword);
        if (!r.Success) { Fail(r.Message); return; }
        ClearSensitive(); GeneratedToken = "";
        Mode = AuthMode.Login;
        Succeed(r.Message);
    });
}
