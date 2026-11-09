using System.Security.Cryptography;
using System.Text;

namespace GamingHub.Core.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string stored);
}

/// <summary>PBKDF2-HMAC-SHA256 with per-password salt. Format: pbkdf2-sha256$iterations$salt$hash.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int MinimumIterations = 100_000;
    private const int SaltSize = 16, HashSize = 32;
    private readonly int _iterations;

    public Pbkdf2PasswordHasher(int iterations = 310_000)
    {
        if (iterations < MinimumIterations) throw new ArgumentOutOfRangeException(nameof(iterations));
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2-sha256${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string stored)
    {
        try
        {
            var p = stored.Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2-sha256") return false;
            var iterations = int.Parse(p[1]);
            if (iterations < MinimumIterations || iterations > 10_000_000) return false;
            var salt = Convert.FromBase64String(p[2]);
            var expected = Convert.FromBase64String(p[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException) { return false; }
    }
}

public static class TokenHelper
{
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class SecurityOptions
{
    public int MinimumAge { get; set; } = 13;
    public int SessionHours { get; set; } = 8;
    public int MaxFailedBeforeLock { get; set; } = 5;
    public int BaseLockSeconds { get; set; } = 30;
    public int MaxLockMinutes { get; set; } = 15;
    public int RecoveryTokenMinutes { get; set; } = 15;
    public int MaxRecoveryRequestsPerHour { get; set; } = 3;
}
