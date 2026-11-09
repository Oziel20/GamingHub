using FluentAssertions;
using GamingHub.Core.Validation;
using Xunit;

namespace GamingHub.Tests;

public class PasswordValidatorTests
{
    private static string Pw(int len) => "Aa1!" + new string('x', len - 4);

    [Theory]
    [InlineData(11, false)] [InlineData(12, true)] [InlineData(64, true)] [InlineData(65, false)]
    public void Length_boundaries(int len, bool valid) =>
        PasswordValidator.Validate(Pw(len), "player_one").IsValid.Should().Be(valid);

    [Theory]
    [InlineData(null)] [InlineData("")]
    [InlineData("alllowercase1!x")] [InlineData("ALLUPPERCASE1!X")] [InlineData("NoDigitsHere!!x")] [InlineData("NoSpecial12345A")]
    [InlineData(" LeadingSpace1!x")] [InlineData("TrailingSpace1!x ")]
    [InlineData("Password123!")] [InlineData("Tab\tInside1!Abc")]
    public void Rejects_weak_or_malformed(string? pw) => PasswordValidator.Validate(pw, "player_one").IsValid.Should().BeFalse();

    [Fact] public void Rejects_password_containing_username() =>
        PasswordValidator.Validate("Xx!player_one9", "player_one").IsValid.Should().BeFalse();

    [Fact] public void Rejects_password_equal_to_username() =>
        PasswordValidator.Validate("Player_One1!", "player_one1!").IsValid.Should().BeFalse();

    [Fact] public void Accepts_strong_password() => PasswordValidator.Validate("Str0ng!Passw0rd", "bob_smith").IsValid.Should().BeTrue();
}

public class UserNameValidatorTests
{
    [Theory]
    [InlineData(3, false)] [InlineData(4, true)] [InlineData(24, true)] [InlineData(25, false)]
    public void Length_boundaries(int len, bool valid) => UserNameValidator.Validate(new string('a', len)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("has space")] [InlineData("bad-char")] [InlineData("emoji😀name")]
    [InlineData("tab\tname")] [InlineData("admin")] [InlineData("ADMIN")] [InlineData("Root")] [InlineData("system")] [InlineData("administrator")]
    public void Rejects_invalid(string? name) => UserNameValidator.Validate(name).IsValid.Should().BeFalse();

    [Fact] public void Accepts_letters_digits_underscore() => UserNameValidator.Validate("Good_Name_9").IsValid.Should().BeTrue();
    [Fact] public void Rejects_huge_input() => UserNameValidator.Validate(new string('a', 5000)).IsValid.Should().BeFalse();
}

public class SearchValidatorTests
{
    [Fact] public void Null_is_invalid() => SearchValidator.Validate(null, out _).IsValid.Should().BeFalse();
    [Theory] [InlineData("")] [InlineData("   ")]
    public void Empty_means_show_all(string q) { SearchValidator.Validate(q, out var n).IsValid.Should().BeTrue(); n.Should().BeEmpty(); }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(100, true)] [InlineData(101, false)]
    public void Length_boundaries(int len, bool valid) => SearchValidator.Validate(new string('a', len), out _).IsValid.Should().Be(valid);

    [Fact] public void Trims_and_keeps_inner_spaces() { SearchValidator.Validate("  half life  ", out var n).IsValid.Should().BeTrue(); n.Should().Be("half life"); }
    [Fact] public void Rejects_control_chars() => SearchValidator.Validate("ab\u0007cd", out _).IsValid.Should().BeFalse();
    [Fact] public void Single_char_after_trim_is_invalid() => SearchValidator.Validate(" a ", out _).IsValid.Should().BeFalse();
}

public class EmailAndTextValidatorTests
{
    [Theory] [InlineData("a@b.co", true)] [InlineData("user.name+tag@example.com", true)] [InlineData("no-at.com", false)]
    [InlineData("a@b", false)] [InlineData("", false)] [InlineData(null, false)] [InlineData("a b@c.com", false)]
    public void Email_formats(string? e, bool valid) => EmailValidator.Validate(e).IsValid.Should().Be(valid);

    [Fact] public void Email_254_ok_255_rejected()
    {
        string Make(int total) => new string('a', 60) + "@" + new string('b', total - 60 - 1 - 4) + ".com";
        EmailValidator.Validate(Make(254)).IsValid.Should().BeTrue();
        EmailValidator.Validate(Make(255)).IsValid.Should().BeFalse();
    }

    [Fact] public void Message_limits()
    {
        GamingHub.Core.Services.SocialService.ValidateMessage(new string('a', 2000)).IsValid.Should().BeTrue();
        GamingHub.Core.Services.SocialService.ValidateMessage(new string('a', 2001)).IsValid.Should().BeFalse();
        GamingHub.Core.Services.SocialService.ValidateMessage("  ").IsValid.Should().BeFalse();
    }

    [Theory] [InlineData(-1, "USD", false)] [InlineData(0, "USD", true)] [InlineData(9.99, "EUR", true)]
    [InlineData(9.999, "USD", false)] [InlineData(5, "XXX", false)] [InlineData(1001, "USD", false)]
    public void Prices(double price, string cur, bool valid) => PriceValidator.Validate((decimal)price, cur).IsValid.Should().Be(valid);
}

public class FileValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gh-files-" + Guid.NewGuid().ToString("N"));
    public FileValidatorTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private string Write(string name, byte[] head, int size = 200)
    {
        var p = Path.Combine(_dir, name);
        var b = new byte[size]; head.CopyTo(b, 0); File.WriteAllBytes(p, b); return p;
    }

    [Fact] public void Valid_exe_inside_root() =>
        PathValidator.ValidateGameExecutable(Write("g.exe", new byte[] { 0x4D, 0x5A }), new[] { _dir }).IsValid.Should().BeTrue();

    [Fact] public void Exe_outside_root_rejected() =>
        PathValidator.ValidateGameExecutable(Write("g.exe", new byte[] { 0x4D, 0x5A }), new[] { Path.Combine(_dir, "other") }).IsValid.Should().BeFalse();

    [Fact] public void Traversal_rejected() =>
        PathValidator.ValidateGameExecutable(Path.Combine(_dir, "..", "x.exe"), new[] { _dir }).IsValid.Should().BeFalse();

    [Fact] public void Wrong_extension_rejected() =>
        PathValidator.ValidateGameExecutable(Write("g.txt", new byte[] { 0x4D, 0x5A }), new[] { _dir }).IsValid.Should().BeFalse();

    [Fact] public void Fake_exe_content_rejected() =>
        PathValidator.ValidateGameExecutable(Write("fake.exe", new byte[] { 1, 2 }), new[] { _dir }).IsValid.Should().BeFalse();

    [Fact] public void Missing_file_rejected() =>
        PathValidator.ValidateGameExecutable(Path.Combine(_dir, "none.exe"), new[] { _dir }).IsValid.Should().BeFalse();

    [Fact] public void Valid_png_and_jpg() 
    {
        ImageValidator.ValidateAvatar(Write("a.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })).IsValid.Should().BeTrue();
        ImageValidator.ValidateAvatar(Write("a.jpg", new byte[] { 0xFF, 0xD8, 0xFF })).IsValid.Should().BeTrue();
    }

    [Fact] public void Renamed_exe_as_png_rejected() =>
        ImageValidator.ValidateAvatar(Write("evil.png", new byte[] { 0x4D, 0x5A })).IsValid.Should().BeFalse();

    [Fact] public void Oversized_image_rejected() =>
        ImageValidator.ValidateAvatar(Write("big.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 2 * 1024 * 1024 + 1)).IsValid.Should().BeFalse();
}
