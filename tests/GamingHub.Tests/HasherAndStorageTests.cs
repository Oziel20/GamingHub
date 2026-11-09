using FluentAssertions;
using GamingHub.Core.Security;
using GamingHub.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamingHub.Tests;

public class HasherTests
{
    private readonly Pbkdf2PasswordHasher _h = new(100_000);

    [Fact] public void Hash_is_not_plaintext_and_verifies()
    {
        var hash = _h.Hash("Str0ng!Passw0rd");
        hash.Should().NotContain("Str0ng").And.StartWith("pbkdf2-sha256$100000$");
        _h.Verify("Str0ng!Passw0rd", hash).Should().BeTrue();
        _h.Verify("Str0ng!Passw0rd2", hash).Should().BeFalse();
    }
    [Fact] public void Salts_are_unique() => _h.Hash("Same!Password1").Should().NotBe(_h.Hash("Same!Password1"));
    [Theory] [InlineData("")] [InlineData("garbage")] [InlineData("pbkdf2-sha256$1$a$b")] [InlineData("pbkdf2-sha256$100000$!!$!!")]
    public void Malformed_hash_never_verifies(string stored) => _h.Verify("x", stored).Should().BeFalse();
    [Fact] public void Weak_iteration_count_rejected() => FluentActions.Invoking(() => new Pbkdf2PasswordHasher(1000)).Should().Throw<ArgumentOutOfRangeException>();
    [Fact] public void Tokens_are_unique_and_hash_differs()
    {
        var a = TokenHelper.NewToken(); var b = TokenHelper.NewToken();
        a.Should().NotBe(b); TokenHelper.Hash(a).Should().NotBe(a);
    }
}

public class CounterDoc { public int Count { get; set; } public List<string> Items { get; set; } = new(); }

public class JsonFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gh-json-" + Guid.NewGuid().ToString("N"));
    private string PathOf => Path.Combine(_dir, "sub", "counter.json");
    private JsonFileStore<CounterDoc> Make() => new(PathOf, NullLogger.Instance);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static Task<int> Inc(JsonFileStore<CounterDoc> s) => s.UpdateAsync(d => { d.Count++; return (true, d.Count); });

    [Fact] public async Task Creates_directory_and_file_when_missing()
    {
        await Make().ReadAsync(d => d.Count);
        File.Exists(PathOf).Should().BeTrue();
    }

    [Fact] public async Task Persists_across_instances()
    {
        await Inc(Make());
        (await Make().ReadAsync(d => d.Count)).Should().Be(1);
    }

    [Fact] public async Task Empty_file_is_handled()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!); File.WriteAllText(PathOf, "");
        (await Make().ReadAsync(d => d.Count)).Should().Be(0);
    }

    [Fact] public async Task Corrupt_file_is_quarantined_and_backup_restored()
    {
        var s = Make(); await Inc(s); await Inc(s);          // .bak holds Count=1
        File.WriteAllText(PathOf, "{ not json");
        (await Make().ReadAsync(d => d.Count)).Should().Be(1);
        Directory.GetFiles(Path.GetDirectoryName(PathOf)!, "*.corrupt-*").Should().HaveCount(1);
    }

    [Fact] public async Task Corrupt_file_without_backup_starts_empty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!); File.WriteAllText(PathOf, "[[[");
        (await Make().ReadAsync(d => d.Count)).Should().Be(0);
    }

    [Fact] public async Task Missing_properties_use_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!); File.WriteAllText(PathOf, "{}");
        (await Make().ReadAsync(d => d.Items.Count)).Should().Be(0);
    }

    [Fact] public async Task Leftover_tmp_is_recovered_when_main_missing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!); File.WriteAllText(PathOf + ".tmp", "{\"count\":7}");
        (await Make().ReadAsync(d => d.Count)).Should().Be(7);
    }

    [Fact] public async Task Concurrent_updates_do_not_lose_writes()
    {
        var s = Make();
        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Inc(s)));
        (await Make().ReadAsync(d => d.Count)).Should().Be(50);
    }

    [Fact] public async Task Failed_update_does_not_corrupt_state()
    {
        var s = Make(); await Inc(s);
        await FluentActions.Awaiting(() => s.UpdateAsync<int>(d => { d.Count = 999; throw new InvalidOperationException(); })).Should().ThrowAsync<InvalidOperationException>();
        (await s.ReadAsync(d => d.Count)).Should().Be(1);
    }

    [Fact] public async Task Uncommitted_update_is_not_written()
    {
        var s = Make(); await Inc(s);
        await s.UpdateAsync(d => (false, 0));
        (await Make().ReadAsync(d => d.Count)).Should().Be(1);
    }

    [Fact] public async Task Large_file_loads_quickly()
    {
        var s = Make();
        await s.UpdateAsync(d => { d.Items.AddRange(Enumerable.Range(0, 50_000).Select(i => "item-" + i)); return (true, 0); });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        (await Make().ReadAsync(d => d.Items.Count)).Should().Be(50_000);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}
