using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MagicLink.Tests.Sources;

// docs/specs/magiclink-net-decide-who-may-sign-in-and-with-which-groups.md: a file in git says who may sign in and with
// which groups (issue #7).
public sealed class SignInFileSourceTests : IDisposable
{
    private const string TwoPeople = """
        {
          // Comments and trailing commas are allowed: the file is kept by people, in git.
          "people": [
            { "email": "Ann@Example.com", "groups": ["admins", "developers", "admins"] },
            { "email": "bob@example.com" },
          ],
        }
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory("magiclink-signin-file-").FullName;
    private readonly ListLogger<SignInFileSource> _logger = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private string FilePath => Path.Combine(_directory, "people.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task A_listed_address_may_sign_in_with_its_groups_once_each()
    {
        var source = await SourceWith(TwoPeople);

        var access = await source.LookUpAsync("ann@example.com", Cancel);

        Assert.True(access.MaySignIn);
        Assert.Equal(["admins", "developers"], access.Groups);
    }

    [Fact]
    public async Task A_listed_address_without_groups_may_sign_in_with_none()
    {
        var source = await SourceWith(TwoPeople);

        var access = await source.LookUpAsync("bob@example.com", Cancel);

        Assert.True(access.MaySignIn);
        Assert.Empty(access.Groups);
    }

    [Fact]
    public async Task An_address_not_in_the_file_may_not_sign_in()
    {
        var source = await SourceWith(TwoPeople);

        var access = await source.LookUpAsync("carol@example.com", Cancel);

        Assert.False(access.MaySignIn);
        Assert.Empty(access.Groups);
    }

    [Fact]
    public async Task A_changed_file_is_read_again_on_the_next_look_up()
    {
        var source = await SourceWith(TwoPeople);
        Assert.True((await source.LookUpAsync("bob@example.com", Cancel)).MaySignIn);

        await WriteAsync("""{ "people": [ { "email": "carol@example.com", "groups": ["readers"] } ] }""");

        Assert.False((await source.LookUpAsync("bob@example.com", Cancel)).MaySignIn);
        Assert.Equal(["readers"], (await source.LookUpAsync("carol@example.com", Cancel)).Groups);
    }

    [Fact]
    public async Task A_file_broken_while_running_lets_nobody_in_and_says_why()
    {
        var source = await SourceWith(TwoPeople);
        Assert.True((await source.LookUpAsync("bob@example.com", Cancel)).MaySignIn);

        await WriteAsync("""{ "people": [ { "email": "bob@example.com", "groups": ["admins"] """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.LookUpAsync("bob@example.com", Cancel));
        Assert.Contains(FilePath, exception.Message, StringComparison.Ordinal);
        var logged = Assert.Single(_logger.Entries, static entry => entry.Level == LogLevel.Error);
        Assert.Contains(FilePath, logged.Message, StringComparison.Ordinal);

        // Logged once for the broken file, not on every look-up.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.LookUpAsync("bob@example.com", Cancel));
        Assert.Single(_logger.Entries, static entry => entry.Level == LogLevel.Error);

        await WriteAsync(TwoPeople);
        Assert.True((await source.LookUpAsync("bob@example.com", Cancel)).MaySignIn);
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_passing_read_error_ends_without_a_change_to_the_file()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "File modes are a Unix feature.");
        var source = await SourceWith(TwoPeople);
        var stamp = File.GetLastWriteTimeUtc(FilePath);

        // Unreadable without a change of time or size, as a lock held by another process would make it.
        File.SetUnixFileMode(FilePath, UnixFileMode.None);
        try
        {
            Assert.SkipWhen(CanRead(FilePath), "This user can read any file.");
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.LookUpAsync("bob@example.com", Cancel));
        }
        finally
        {
            File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(FilePath));
        Assert.True((await source.LookUpAsync("bob@example.com", Cancel)).MaySignIn);
    }

    [Fact]
    public async Task A_file_removed_while_running_lets_nobody_in()
    {
        var source = await SourceWith(TwoPeople);
        File.Delete(FilePath);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.LookUpAsync("bob@example.com", Cancel));

        Assert.Contains(FilePath, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "people": [ { "email": "ann@example.com" } """, "does not have the expected format")]
    [InlineData("""null""", "\"people\"")]
    [InlineData("""{ }""", "\"people\"")]
    [InlineData("""{ "people": null }""", "\"people\"")]
    [InlineData("""[]""", "does not have the expected format")]
    [InlineData("""{ "people": [ null ] }""", "entry 1")]
    [InlineData("""{ "people": [ { "groups": ["admins"] } ] }""", "entry 1: it has no email address")]
    [InlineData("""{ "people": [ { "email": "ann@example.com" }, { "email": "not an address" } ] }""", "entry 2: 'not an address' is not an email address")]
    [InlineData("""{ "people": [ { "email": "ann@example.com" }, { "email": " ANN@example.com " } ] }""", "ann@example.com is listed more than once")]
    [InlineData("""{ "people": [ { "email": "ann@example.com", "groups": ["admins", " "] } ] }""", "an empty group")]
    [InlineData("""{ "people": [ { "email": "ann@example.com", "groups": [null] } ] }""", "an empty group")]
    [InlineData("""{ "people": [ { "email": "ann@example.com", "group": ["admins"] } ] }""", "does not have the expected format")]
    public void A_file_that_does_not_say_clearly_who_may_sign_in_is_refused_with_the_reason(string json, string reason)
    {
        var result = SignInFile.Parse(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.True(result.IsFailure);
        Assert.Contains(reason, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_does_not_exist_is_refused_with_its_path()
    {
        var result = SignInFile.Read(FilePath);

        Assert.True(result.IsFailure);
        Assert.Contains(FilePath, result.Error, StringComparison.Ordinal);
        Assert.Contains("cannot be read", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_start_check_passes_a_good_file_and_refuses_a_broken_one_with_its_path_and_the_reason()
    {
        await WriteAsync(TwoPeople);
        Assert.True(SignInFileCheck.Check(new MagicLinkSignInFileOptions { Path = FilePath }).Succeeded);

        await WriteAsync("""{ "people": [ { "email": "ann@example.com" }, { "email": "ann@example.com" } ] }""");
        var broken = SignInFileCheck.Check(new MagicLinkSignInFileOptions { Path = FilePath });
        Assert.True(broken.Failed);
        Assert.Contains(FilePath, broken.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("listed more than once", broken.FailureMessage, StringComparison.Ordinal);

        var unset = SignInFileCheck.Check(new MagicLinkSignInFileOptions { Path = " " });
        Assert.True(unset.Failed);
        Assert.Contains("MagicLinkSignInFileOptions.Path", unset.FailureMessage, StringComparison.Ordinal);
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task<SignInFileSource> SourceWith(string json)
    {
        await WriteAsync(json);
        return new SignInFileSource(Options.Create(new MagicLinkSignInFileOptions { Path = FilePath }), _logger);
    }

    /// <summary>Writes the file and moves its time forward, as a new commit checked out would.</summary>
    private async Task WriteAsync(string json)
    {
        var previous = File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : DateTime.MinValue;
        await File.WriteAllTextAsync(FilePath, json, Cancel);
        File.SetLastWriteTimeUtc(FilePath, previous == DateTime.MinValue ? DateTime.UtcNow : previous.AddSeconds(1));
    }
}
