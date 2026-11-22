using System.Diagnostics;

namespace MagicLink.Host.Tests;

// The container runs the check-sign-in command of issue #9 when its arguments start with check-sign-in, as in
// docker run magiclink-net check-sign-in ann@example.com: it answers from the source in its settings and exits with the
// command's code, without starting the server.
public sealed class CheckSignInCommandTests : IDisposable
{
    private readonly TestSetup _setup = new();

    public void Dispose() => _setup.Dispose();

    [Fact]
    public async Task A_listed_address_may_sign_in_with_its_groups()
    {
        var (exitCode, output) = await RunAsync("check-sign-in", TestSetup.Person);

        Assert.Equal(CheckSignInCommand.MaySignIn, exitCode);
        Assert.Contains(TestSetup.Group, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_address_that_is_not_listed_may_not_sign_in()
    {
        var (exitCode, _) = await RunAsync("check-sign-in", TestSetup.Outsider);

        Assert.Equal(CheckSignInCommand.MayNotSignIn, exitCode);
    }

    /// <summary>Runs the host's binary with <paramref name="arguments"/> and the settings of <see cref="TestSetup"/>.</summary>
    private async Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "MagicLink.NET.Host.dll"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Not started, so the store is never reached.
        foreach (var (key, value) in _setup.Settings("Host=localhost;Database=unused", smtpPort: 25))
        {
            start.Environment[key.Replace(":", "__", StringComparison.Ordinal)] = value;
        }

        start.Environment["DOTNET_hostBuilder__reloadConfigOnChange"] = "false";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await output + await error);
    }
}
