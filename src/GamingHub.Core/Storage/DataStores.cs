using GamingHub.Core.Models;
using Microsoft.Extensions.Logging;

namespace GamingHub.Core.Storage;

public sealed record StoragePaths(string Root)
{
    public string FilePath(string name) => Path.Combine(Root, name);
    public string AvatarsDir => Path.Combine(Root, "avatars");
}

public sealed class DataStores
{
    public JsonFileStore<UsersFile> Users { get; }
    public JsonFileStore<GamesFile> Games { get; }
    public JsonFileStore<LibraryFile> Library { get; }
    public JsonFileStore<FriendsFile> Friends { get; }
    public JsonFileStore<MessagesFile> Messages { get; }
    public JsonFileStore<NotificationsFile> Notifications { get; }
    public JsonFileStore<PurchasesFile> Purchases { get; }
    public JsonFileStore<SessionsFile> Sessions { get; }
    public JsonFileStore<SettingsFile> Settings { get; }
    public JsonFileStore<ActivityFile> Activity { get; }

    public DataStores(StoragePaths paths, ILoggerFactory lf)
    {
        var log = lf.CreateLogger("JsonStore");
        JsonFileStore<T> Make<T>(string n) where T : class, new() => new(paths.FilePath(n), log);
        Users = Make<UsersFile>("users.json");
        Games = Make<GamesFile>("games.json");
        Library = Make<LibraryFile>("library.json");
        Friends = Make<FriendsFile>("friends.json");
        Messages = Make<MessagesFile>("messages.json");
        Notifications = Make<NotificationsFile>("notifications.json");
        Purchases = Make<PurchasesFile>("purchases.json");
        Sessions = Make<SessionsFile>("sessions.json");
        Settings = Make<SettingsFile>("settings.json");
        Activity = Make<ActivityFile>("activity.json");
    }
}
