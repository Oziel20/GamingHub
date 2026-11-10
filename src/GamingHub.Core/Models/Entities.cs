namespace GamingHub.Core.Models;

public enum UserRole { User = 0, Admin = 1 }
public enum FriendStatus { Pending = 0, Accepted = 1, Blocked = 2 }
public enum PaymentMethod { None = 0, Card = 1, Transfer = 2 }

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "";
    public string UserNameKey { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string? AvatarPath { get; set; }
    public bool IsOnline { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime? LastFailedUtc { get; set; }
    public User Clone() => (User)MemberwiseClone();
}

public sealed class Game
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = "";
    public DateTime ReleaseDate { get; set; }
    public long SizeBytes { get; set; }
    public bool Available { get; set; } = true;
    /// <summary>Null for catalog games; set for games a user added from a local executable.</summary>
    public Guid? OwnerUserId { get; set; }
    public string? ExecutablePath { get; set; }
    // Ficha técnica (aparece en el ticket de compra). Valores de demostración para el catálogo local.
    public string Developer { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Requirements { get; set; } = "";
    public string? CoverPath { get; set; }
    public Game Clone() => (Game)MemberwiseClone();
}

public sealed class LibraryEntry
{
    public Guid UserId { get; set; }
    public string GameId { get; set; } = "";
    public DateTime AddedUtc { get; set; }
    public long PlayedSeconds { get; set; }
    public DateTime? LastPlayedUtc { get; set; }
    public bool Installed { get; set; }
    public bool Favorite { get; set; }
    public LibraryEntry Clone() => (LibraryEntry)MemberwiseClone();
}

public sealed class Purchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string GameId { get; set; } = "";
    public string GameTitle { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime Utc { get; set; }
    /// <summary>Always true: there is no payment gateway, purchases are local simulations.</summary>
    public bool Simulated { get; set; } = true;

    // ---- Ticket / pago (compras anteriores al ticket tienen OrderId vacío y PaymentMethod None) ----
    /// <summary>Agrupa las líneas de una misma compra (un ticket por OrderId).</summary>
    public Guid OrderId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    /// <summary>Titular de la tarjeta o nombre de quien hizo la transferencia.</summary>
    public string? PaymentHolder { get; set; }
    public string? CardBrand { get; set; }
    /// <summary>Solo los 4 últimos dígitos. El número completo y el CVV NUNCA se guardan.</summary>
    public string? CardLast4 { get; set; }
    public string? Bank { get; set; }
    /// <summary>Solo los 4 últimos dígitos de la cuenta/CLABE de origen.</summary>
    public string? AccountLast4 { get; set; }
    public string? TransferSubject { get; set; }
    public string? TrackingKey { get; set; }
    public Purchase Clone() => (Purchase)MemberwiseClone();
}

public sealed class SessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public bool Revoked { get; set; }
}

public sealed class RecoveryToken
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public bool Used { get; set; }
}

public sealed class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public Guid TargetId { get; set; }
    public FriendStatus Status { get; set; }
    public DateTime Utc { get; set; }
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    public string Text { get; set; } = "";
    public DateTime Utc { get; set; }
}

public sealed class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Utc { get; set; }
    public bool Read { get; set; }
}

public sealed class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime Utc { get; set; }
}

public sealed class AppSettings
{
    public List<string> AuthorizedGameRoots { get; set; } = new();
}

// One document type per JSON file.
public sealed class UsersFile { public List<User> Users { get; set; } = new(); }
public sealed class GamesFile { public List<Game> Games { get; set; } = new(); }
public sealed class LibraryFile { public List<LibraryEntry> Entries { get; set; } = new(); }
public sealed class FriendsFile { public List<Friendship> Friendships { get; set; } = new(); }
public sealed class MessagesFile { public List<ChatMessage> Messages { get; set; } = new(); }
public sealed class NotificationsFile { public List<AppNotification> Notifications { get; set; } = new(); }
public sealed class PurchasesFile { public List<Purchase> Purchases { get; set; } = new(); }
public sealed class SessionsFile
{
    public List<SessionRecord> Sessions { get; set; } = new();
    public List<RecoveryToken> RecoveryTokens { get; set; } = new();
}
public sealed class SettingsFile { public AppSettings Settings { get; set; } = new(); }
public sealed class ActivityFile { public List<ActivityEntry> Entries { get; set; } = new(); }
