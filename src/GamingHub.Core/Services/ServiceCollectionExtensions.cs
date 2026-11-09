using GamingHub.Core.Security;
using GamingHub.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GamingHub.Core.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGamingHubCore(this IServiceCollection s, string dataRoot, SecurityOptions? options = null)
    {
        s.AddSingleton(new StoragePaths(dataRoot));
        s.AddSingleton(options ?? new SecurityOptions());
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton<IPasswordHasher>(_ => new Pbkdf2PasswordHasher());
        s.AddSingleton<DataStores>();
        s.AddSingleton<ActivityLog>();
        s.AddSingleton<NotificationHub>();
        s.AddSingleton<AuthService>();
        s.AddSingleton<SettingsService>();
        s.AddSingleton<LibraryService>();
        s.AddSingleton<StoreService>();
        s.AddSingleton<TicketFiles>();
        s.AddSingleton<SocialService>();
        s.AddSingleton<ProfileService>();
        s.AddSingleton<DashboardService>();
        s.AddSingleton<AdminService>();
        return s;
    }
}
