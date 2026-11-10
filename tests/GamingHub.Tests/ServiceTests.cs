using FluentAssertions;
using GamingHub.Core.Models;
using GamingHub.Core.Security;
using GamingHub.Core.Services;
using Xunit;

namespace GamingHub.Tests;

public class AuthServiceTests
{
    private static RegisterRequest Req(string name = "player_one", string? email = null, string pw = TestEnv.Pw, string? confirm = null,
        DateOnly? birth = null, bool terms = true) =>
        new(name, email ?? name + "@example.com", pw, confirm ?? pw, birth ?? new DateOnly(1995, 5, 5), terms);

    [Fact] public async Task Register_valid_succeeds_and_first_user_is_admin()
    {
        using var e = new TestEnv();
        var a = await e.Auth.RegisterAsync(Req("first_user"));
        var b = await e.Auth.RegisterAsync(Req("second_user"));
        a.Success.Should().BeTrue(); a.Value!.Role.Should().Be(UserRole.Admin);
        b.Value!.Role.Should().Be(UserRole.User);
    }

    [Fact] public async Task Register_rejects_duplicates_case_insensitively()
    {
        using var e = new TestEnv(); await e.RegisterAsync("player_one");
        (await e.Auth.RegisterAsync(Req("PLAYER_ONE", "other@example.com"))).Code.Should().Be(ErrorCodes.Duplicate);
        (await e.Auth.RegisterAsync(Req("another_one", "PLAYER_ONE@example.com"))).Code.Should().Be(ErrorCodes.Duplicate);
    }

    [Fact] public async Task Register_rejects_mismatch_terms_and_ages()
    {
        using var e = new TestEnv();
        (await e.Auth.RegisterAsync(Req(confirm: "Different!Pass1"))).Success.Should().BeFalse();
        (await e.Auth.RegisterAsync(Req(terms: false))).Success.Should().BeFalse();
        (await e.Auth.RegisterAsync(Req(birth: new DateOnly(2030, 1, 1)))).Success.Should().BeFalse();
        (await e.Auth.RegisterAsync(Req(birth: new DateOnly(2020, 1, 1)))).Success.Should().BeFalse();   // under minimum age
        (await e.Auth.RegisterAsync(Req(birth: new DateOnly(1850, 1, 1)))).Success.Should().BeFalse();
        (await e.Auth.RegisterAsync(Req(birth: null))).Success.Should().BeFalse();
    }

    [Fact] public async Task Register_rejects_65_char_password_even_if_ui_is_bypassed()
    {
        using var e = new TestEnv();
        var pw = "Aa1!" + new string('x', 61);
        (await e.Auth.RegisterAsync(Req(pw: pw))).Success.Should().BeFalse();
    }

    [Fact] public async Task Login_success_wrong_password_and_generic_message()
    {
        using var e = new TestEnv(); await e.RegisterAsync("player_one");
        var bad = await e.Auth.LoginAsync("player_one", "Wrong!Password1");
        var unknown = await e.Auth.LoginAsync("nobody_here", "Wrong!Password1");
        bad.Success.Should().BeFalse(); bad.Message.Should().Be(unknown.Message);
        (await e.Auth.LoginAsync("player_one", TestEnv.Pw)).Success.Should().BeTrue();
        (await e.Auth.GetCurrentUserAsync()).Should().NotBeNull();
    }

    [Fact] public async Task Login_rejects_oversized_input()
    {
        using var e = new TestEnv(); await e.RegisterAsync("player_one");
        (await e.Auth.LoginAsync("player_one", new string('a', 65))).Success.Should().BeFalse();
        (await e.Auth.LoginAsync(null, null)).Success.Should().BeFalse();
    }

    [Fact] public async Task Brute_force_triggers_temporary_lock_that_expires()
    {
        using var e = new TestEnv(); await e.RegisterAsync("player_one");
        for (var i = 0; i < 5; i++) await e.Auth.LoginAsync("player_one", "Wrong!Password1");
        (await e.Auth.LoginAsync("player_one", TestEnv.Pw)).Success.Should().BeFalse();   // locked even with right password
        e.Clock.Advance(TimeSpan.FromSeconds(31));
        (await e.Auth.LoginAsync("player_one", TestEnv.Pw)).Success.Should().BeTrue();   // not a permanent lock
    }

    [Fact] public async Task Logout_invalidates_session_and_sessions_expire()
    {
        using var e = new TestEnv(); await e.RegisterAndLoginAsync("player_one");
        await e.Auth.LogoutAsync();
        (await e.Auth.GetCurrentUserAsync()).Should().BeNull();
        await e.Auth.LoginAsync("player_one", TestEnv.Pw);
        e.Clock.Advance(TimeSpan.FromHours(9));
        (await e.Auth.GetCurrentUserAsync()).Should().BeNull();
    }

    [Fact] public async Task Recovery_token_is_single_use_and_revokes_sessions()
    {
        using var e = new TestEnv(); await e.RegisterAndLoginAsync("player_one");
        var t = await e.Auth.RequestRecoveryAsync("player_one@example.com");
        t.Success.Should().BeTrue();
        const string newPw = "N3w!Secure#Pass";
        (await e.Auth.ResetPasswordAsync(t.Value, newPw, newPw)).Success.Should().BeTrue();
        (await e.Auth.GetCurrentUserAsync()).Should().BeNull();
        (await e.Auth.ResetPasswordAsync(t.Value, "An0ther!Pass#1", "An0ther!Pass#1")).Success.Should().BeFalse();
        (await e.Auth.LoginAsync("player_one", newPw)).Success.Should().BeTrue();
        (await e.Auth.LoginAsync("player_one", TestEnv.Pw)).Success.Should().BeFalse();
    }

    [Fact] public async Task Recovery_token_expires_and_weak_password_does_not_burn_it()
    {
        using var e = new TestEnv(); await e.RegisterAsync("player_one");
        var t = await e.Auth.RequestRecoveryAsync("player_one@example.com");
        (await e.Auth.ResetPasswordAsync(t.Value, "weak", "weak")).Success.Should().BeFalse();
        e.Clock.Advance(TimeSpan.FromMinutes(16));
        (await e.Auth.ResetPasswordAsync(t.Value, "N3w!Secure#Pass", "N3w!Secure#Pass")).Success.Should().BeFalse();
    }

    [Fact] public async Task Recovery_requests_are_rate_limited()
    {
        using var e = new TestEnv(); await e.RegisterAsync("player_one");
        for (var i = 0; i < 3; i++) (await e.Auth.RequestRecoveryAsync("player_one@example.com")).Success.Should().BeTrue();
        (await e.Auth.RequestRecoveryAsync("player_one@example.com")).Code.Should().Be(ErrorCodes.RateLimit);
    }

    [Fact] public async Task Services_require_authentication()
    {
        using var e = new TestEnv();
        (await e.Library().SearchAsync("", null, LibrarySort.TitleAsc, 1)).Code.Should().Be(ErrorCodes.AuthRequired);
        (await e.Store().CheckoutAsync()).Code.Should().Be(ErrorCodes.AuthRequired);
        (await e.Social().GetFriendsAsync()).Code.Should().Be(ErrorCodes.AuthRequired);
    }

    [Fact] public async Task Admin_operations_are_denied_to_normal_users()
    {
        using var e = new TestEnv(); await e.RegisterAsync("first_user"); await e.RegisterAndLoginAsync("normal_user");
        var admin = new AdminService(e.Auth, e.Stores, e.Clock, e.Activity);
        (await admin.ListUsersAsync()).Code.Should().Be(ErrorCodes.Forbidden);
        (await admin.UnlockUserAsync(Guid.NewGuid())).Success.Should().BeFalse();
        (await e.Settings.AddRootAsync(Path.GetTempPath())).Code.Should().Be(ErrorCodes.Forbidden);
    }

    [Fact] public async Task Admin_can_block_and_unblock_account()
    {
        using var e = new TestEnv();
        await e.RegisterAsync("first_user");
        var target = await e.RegisterAsync("normal_user");
        await e.Auth.LoginAsync("first_user", TestEnv.Pw);
        var admin = new AdminService(e.Auth, e.Stores, e.Clock, e.Activity);

        (await admin.ToggleUserBlockAsync(target.Id)).Success.Should().BeTrue();
        (await admin.ListUsersAsync()).Value!.Single(x => x.Id == target.Id).BlockActionText.Should().Be("Desbloquear");
        (await e.Auth.LoginAsync("normal_user", TestEnv.Pw)).Success.Should().BeFalse();

        await e.Auth.LoginAsync("first_user", TestEnv.Pw);
        (await admin.ToggleUserBlockAsync(target.Id)).Success.Should().BeTrue();
        (await admin.ListUsersAsync()).Value!.Single(x => x.Id == target.Id).BlockActionText.Should().Be("Bloquear");
        (await e.Auth.LoginAsync("normal_user", TestEnv.Pw)).Success.Should().BeTrue();
    }
}

public class LibraryServiceTests
{
    private static string MakeExe(string dir, string name = "game.exe")
    {
        Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, name);
        var b = new byte[256]; b[0] = 0x4D; b[1] = 0x5A; File.WriteAllBytes(p, b);
        return p;
    }

    private static async Task<(TestEnv env, LibraryService lib, string dir)> SetupAsync()
    {
        var e = new TestEnv(); await e.RegisterAndLoginAsync("first_user");   // first user = admin
        var dir = Path.Combine(e.Dir, "games"); Directory.CreateDirectory(dir);
        (await e.Settings.AddRootAsync(dir)).Success.Should().BeTrue();
        return (e, e.Library(), dir);
    }

    [Fact] public async Task Add_search_sort_and_remove()
    {
        var (e, lib, dir) = await SetupAsync(); using var _ = e;
        (await lib.AddLocalGameAsync("Zeta Quest", "RPG", MakeExe(dir, "z.exe"))).Success.Should().BeTrue();
        (await lib.AddLocalGameAsync("alpha Run", "Acción", MakeExe(dir, "a.exe"))).Success.Should().BeTrue();

        var all = (await lib.SearchAsync("", null, LibrarySort.TitleAsc, 1)).Value!;
        all.Items.Select(i => i.Title).Should().Equal("alpha Run", "Zeta Quest");
        (await lib.SearchAsync("ZETA", null, LibrarySort.TitleAsc, 1)).Value!.Items.Should().HaveCount(1);
        (await lib.SearchAsync("", "RPG", LibrarySort.TitleAsc, 1)).Value!.Items.Should().HaveCount(1);
        (await lib.SearchAsync("zzzz", null, LibrarySort.TitleAsc, 1)).Value!.TotalCount.Should().Be(0);

        (await lib.RemoveAsync("nope")).Code.Should().Be(ErrorCodes.NotFound);
        (await lib.RemoveAsync(all.Items[0].GameId)).Success.Should().BeTrue();
        (await lib.SearchAsync("", null, LibrarySort.TitleAsc, 1)).Value!.TotalCount.Should().Be(1);
    }

    [Fact] public async Task Search_boundaries_are_enforced_by_the_service()
    {
        var (e, lib, _) = await SetupAsync(); using var _e = e;
        (await lib.SearchAsync(new string('a', 1), null, LibrarySort.TitleAsc, 1)).Success.Should().BeFalse();
        (await lib.SearchAsync(new string('a', 2), null, LibrarySort.TitleAsc, 1)).Success.Should().BeTrue();
        (await lib.SearchAsync(new string('a', 100), null, LibrarySort.TitleAsc, 1)).Success.Should().BeTrue();
        (await lib.SearchAsync(new string('a', 101), null, LibrarySort.TitleAsc, 1)).Success.Should().BeFalse();
        (await lib.SearchAsync(null, null, LibrarySort.TitleAsc, 1)).Success.Should().BeFalse();
    }

    [Fact] public async Task Duplicates_outside_roots_and_fake_exes_are_rejected()
    {
        var (e, lib, dir) = await SetupAsync(); using var _ = e;
        var exe = MakeExe(dir);
        (await lib.AddLocalGameAsync("Game One", "RPG", exe)).Success.Should().BeTrue();
        (await lib.AddLocalGameAsync("Game One Again", "RPG", exe)).Code.Should().Be(ErrorCodes.Duplicate);
        (await lib.AddLocalGameAsync("Outside", "RPG", MakeExe(Path.Combine(e.Dir, "elsewhere")))).Success.Should().BeFalse();
        File.WriteAllBytes(Path.Combine(dir, "fake.exe"), new byte[200]);
        (await lib.AddLocalGameAsync("Fake", "RPG", Path.Combine(dir, "fake.exe"))).Success.Should().BeFalse();
        (await lib.AddLocalGameAsync("", "RPG", exe)).Success.Should().BeFalse();
    }

    [Fact] public async Task Paging_limits_results_per_page()
    {
        var (e, lib, dir) = await SetupAsync(); using var _ = e;
        for (var i = 0; i < 5; i++) await lib.AddLocalGameAsync($"Game {i}", "RPG", MakeExe(dir, $"g{i}.exe"));
        var p = (await lib.SearchAsync("", null, LibrarySort.TitleAsc, 2, 2)).Value!;
        p.Items.Should().HaveCount(2); p.TotalCount.Should().Be(5); p.TotalPages.Should().Be(3);
    }

    [Fact] public async Task Libraries_are_isolated_between_users()
    {
        var (e, lib, dir) = await SetupAsync(); using var _ = e;
        await lib.AddLocalGameAsync("Private Game", "RPG", MakeExe(dir));
        await e.Auth.LogoutAsync();
        await e.RegisterAndLoginAsync("other_user");
        (await e.Library().SearchAsync("", null, LibrarySort.TitleAsc, 1)).Value!.TotalCount.Should().Be(0);
    }
}

public class StoreServiceTests
{
    [Fact] public async Task Catalog_cart_checkout_and_duplicate_protection()
    {
        using var e = new TestEnv(); await e.RegisterAndLoginAsync("buyer_one");
        var store = e.Store();
        var cat = (await store.SearchCatalogAsync("", null, 1, 50)).Value!;
        cat.TotalCount.Should().BeGreaterThan(5);
        var g = cat.Items.First(i => i.Price > 0);

        (await store.AddToCartAsync(g.Id)).Success.Should().BeTrue();
        (await store.AddToCartAsync(g.Id)).Code.Should().Be(ErrorCodes.Duplicate);
        (await store.AddToCartAsync("does-not-exist")).Code.Should().Be(ErrorCodes.NotFound);

        var res = await store.CheckoutAsync();
        res.Success.Should().BeTrue(); res.Value!.Total.Should().Be(g.Price);
        (await store.AddToCartAsync(g.Id)).Code.Should().Be(ErrorCodes.Duplicate);        // already owned
        (await store.CheckoutAsync()).Code.Should().Be(ErrorCodes.State);                 // empty cart: retry is harmless
        (await store.GetPurchasesAsync()).Value!.Should().HaveCount(1);
        (await e.Library().SearchAsync("", null, LibrarySort.TitleAsc, 1)).Value!.TotalCount.Should().Be(1);
    }

    [Fact] public async Task Store_search_enforces_limits()
    {
        using var e = new TestEnv(); await e.RegisterAndLoginAsync("buyer_one");
        (await e.Store().SearchCatalogAsync(new string('a', 101), null, 1)).Success.Should().BeFalse();
        (await e.Store().SearchCatalogAsync("a", null, 1)).Success.Should().BeFalse();
    }
}

public class SocialServiceTests
{
    private static async Task<(TestEnv e, SocialService s, User a, User b)> SetupFriendsAsync()
    {
        var e = new TestEnv(); var a = await e.RegisterAsync("alice_one"); var b = await e.RegisterAsync("bobby_two");
        await e.Auth.LoginAsync("alice_one", TestEnv.Pw);
        var s = e.Social();
        (await s.SendRequestAsync("bobby_two")).Success.Should().BeTrue();
        await e.Auth.LogoutAsync(); await e.Auth.LoginAsync("bobby_two", TestEnv.Pw);
        var req = (await s.GetFriendsAsync()).Value!.Single();
        (await s.AcceptAsync(req.FriendshipId)).Success.Should().BeTrue();
        return (e, s, a, b);
    }

    [Fact] public async Task Self_and_duplicate_requests_are_rejected()
    {
        using var e = new TestEnv(); await e.RegisterAsync("alice_one"); await e.RegisterAsync("bobby_two");
        await e.Auth.LoginAsync("alice_one", TestEnv.Pw); var s = e.Social();
        (await s.SendRequestAsync("alice_one")).Success.Should().BeFalse();
        (await s.SendRequestAsync("bobby_two")).Success.Should().BeTrue();
        (await s.SendRequestAsync("bobby_two")).Code.Should().Be(ErrorCodes.Duplicate);
        (await s.SendRequestAsync("ghost_user")).Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact] public async Task Only_the_target_can_accept_a_request()
    {
        using var e = new TestEnv(); await e.RegisterAsync("alice_one"); await e.RegisterAsync("bobby_two");
        await e.Auth.LoginAsync("alice_one", TestEnv.Pw); var s = e.Social();
        await s.SendRequestAsync("bobby_two");
        var id = (await s.GetFriendsAsync()).Value!.Single().FriendshipId;
        (await s.AcceptAsync(id)).Success.Should().BeFalse();   // requester cannot accept their own request
    }

    [Fact] public async Task Messaging_rules()
    {
        var (e, s, a, b) = await SetupFriendsAsync(); using var _ = e;
        (await s.SendMessageAsync(a.Id, "<script>alert(1)</script>")).Success.Should().BeTrue();   // stored as plain text
        (await s.SendMessageAsync(a.Id, "")).Success.Should().BeFalse();
        (await s.SendMessageAsync(a.Id, new string('a', 2001))).Success.Should().BeFalse();
        (await s.SendMessageAsync(a.Id, new string('a', 2000))).Success.Should().BeTrue();
        (await s.SendMessageAsync(Guid.NewGuid(), "hola")).Code.Should().Be(ErrorCodes.Forbidden);
        (await s.GetConversationAsync(a.Id)).Value!.Should().HaveCount(2);
    }

    [Fact] public async Task Messages_are_rate_limited()
    {
        var (e, s, a, _) = await SetupFriendsAsync(); using var _e = e;
        for (var i = 0; i < 10; i++) (await s.SendMessageAsync(a.Id, "hola " + i)).Success.Should().BeTrue();
        (await s.SendMessageAsync(a.Id, "one too many")).Code.Should().Be(ErrorCodes.RateLimit);
        e.Clock.Advance(TimeSpan.FromSeconds(31));
        (await s.SendMessageAsync(a.Id, "ok again")).Success.Should().BeTrue();
    }

    [Fact] public async Task Blocked_users_cannot_message_or_read()
    {
        var (e, s, a, b) = await SetupFriendsAsync(); using var _e = e;
        (await s.BlockAsync(a.Id)).Success.Should().BeTrue();
        (await s.SendMessageAsync(a.Id, "hi")).Success.Should().BeFalse();
        await e.Auth.LogoutAsync(); await e.Auth.LoginAsync("alice_one", TestEnv.Pw);
        (await s.SendMessageAsync(b.Id, "hi")).Success.Should().BeFalse();
        (await s.GetConversationAsync(b.Id)).Success.Should().BeFalse();
    }
}
