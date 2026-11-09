namespace GamingHub.Core.Models;

public sealed record OperationResult(bool Success, string Code, string Message)
{
    public static OperationResult Ok(string message = "") => new(true, "OK", message);
    public static OperationResult Fail(string code, string message) => new(false, code, message);
    public static OperationResult AuthRequired() => Fail(ErrorCodes.AuthRequired, "Debes iniciar sesión para continuar.");
}

public sealed record OperationResult<T>(bool Success, string Code, string Message, T? Value)
{
    public static OperationResult<T> Ok(T value, string message = "") => new(true, "OK", message, value);
    public static OperationResult<T> Fail(string code, string message) => new(false, code, message, default);
    public static OperationResult<T> AuthRequired() => Fail(ErrorCodes.AuthRequired, "Debes iniciar sesión para continuar.");
    public static OperationResult<T> Forbidden() => Fail(ErrorCodes.Forbidden, "No tienes permisos para realizar esta operación.");
}

public static class ErrorCodes
{
    public const string Invalid = "VAL_INVALID";
    public const string AuthFailed = "AUTH_FAILED";
    public const string AuthRequired = "AUTH_REQUIRED";
    public const string Forbidden = "AUTH_FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Duplicate = "DUPLICATE";
    public const string RateLimit = "RATE_LIMIT";
    public const string Io = "IO_ERROR";
    public const string State = "INVALID_STATE";
}
