using GamingHub.Core.Security;
using GamingHub.Core.Services;
using GamingHub.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GamingHub.Tests;

public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan t) => _now += t;
}

public sealed class TestEnv : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "gh-tests-" + Guid.NewGuid().ToString("N"));
    public TestClock Clock { get; } = new();
    public DataStores Stores { get; }
    public AuthService Auth { get; }
    public ActivityLog Activity { get; }
    public NotificationHub Notes { get; }
    public SettingsService Settings { get; }
    public StoragePaths Paths { get; }

    public TestEnv(SecurityOptions? options = null)
    {
        Paths = new StoragePaths(Dir);
        Stores = new DataStores(Paths, NullLoggerFactory.Instance);
        var hasher = Substitute.For<IPasswordHasher>(); // fast fake: real hashing is covered in HasherTests
        hasher.Hash(Arg.Any<string>()).Returns(ci => "h:" + ci.Arg<string>());
        hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(1) == "h:" + ci.ArgAt<string>(0));
        Activity = new ActivityLog(Stores, Clock);
        Notes = new NotificationHub(Stores, Clock);
        Auth = new AuthService(Stores, hasher, options ?? new SecurityOptions(), Clock, Activity, NullLogger<AuthService>.Instance);
        Settings = new SettingsService(Auth, Stores);
    }

    public const string Pw = "Str0ng!Passw0rd";

    public async Task<GamingHub.Core.Models.User> RegisterAsync(string name, string? email = null)
    {
        var r = await Auth.RegisterAsync(new RegisterRequest(name, email ?? $"{name.ToLower()}@example.com", Pw, Pw, new DateOnly(1995, 5, 5), true));
        if (!r.Success) throw new InvalidOperationException(r.Message);
        return r.Value!;
    }

    public async Task<GamingHub.Core.Models.User> RegisterAndLoginAsync(string name)
    {
        var u = await RegisterAsync(name);
        var l = await Auth.LoginAsync(name, Pw);
        if (!l.Success) throw new InvalidOperationException(l.Message);
        return u;
    }

    public LibraryService Library() => new(Auth, Stores, Settings, Activity, Clock, NullLogger<LibraryService>.Instance);
    public StoreService Store() => new(Auth, Stores, Activity, Notes, Clock, NullLogger<StoreService>.Instance);
    public SocialService Social() => new(Auth, Stores, Notes, Activity, Clock);

    public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }
}
