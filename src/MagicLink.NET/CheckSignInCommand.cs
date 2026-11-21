using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// The command <c>check-sign-in &lt;email address&gt;</c>: shows whether an address may sign in and with which groups,
/// as the source in use answers for a real sign-in, without starting MagicLink.NET and without an email (ADR-014).
/// </summary>
/// <remarks>
/// It first runs the start check of the source, so it refuses what MagicLink.NET would refuse to start with. Exit codes:
/// 0 when the address may sign in, 1 when it may not, and 2 when that cannot be told, because the source cannot answer or
/// the arguments are not one email address.
/// </remarks>
internal static class CheckSignInCommand
{
    /// <summary>The name of the command, its first argument.</summary>
    public const string Name = "check-sign-in";

    /// <summary>The exit code when the address may sign in.</summary>
    public const int MaySignIn = 0;

    /// <summary>The exit code when the address may not sign in.</summary>
    public const int MayNotSignIn = 1;

    /// <summary>The exit code when it cannot be told whether the address may sign in.</summary>
    public const int CannotTell = 2;

    /// <summary>
    /// Runs the command when <paramref name="arguments"/> start with <see cref="Name"/>, and returns its exit code;
    /// returns <see langword="null"/> for any other arguments, which are the host's.
    /// </summary>
    /// <param name="services">The services of a host with MagicLink.NET, built but not started.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="output">Where the answer is written.</param>
    /// <param name="cancellationToken">Stops the look-up.</param>
    public static async ValueTask<int?> TryRunAsync(IServiceProvider services, IReadOnlyList<string> arguments, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        if (arguments.Count == 0 || !string.Equals(arguments[0], Name, StringComparison.Ordinal))
        {
            return null;
        }

        if (arguments.Count != 2 || !SignInPage.IsEmailAddress(arguments[1].Trim()))
        {
            await output.WriteLineAsync($"Usage: {Name} <email address>").ConfigureAwait(false);
            await output.WriteLineAsync("Shows whether the address may sign in, and with which groups, without sending an email.").ConfigureAwait(false);
            return CannotTell;
        }

        var email = MagicLinkSession.Normalize(arguments[1]);
        SignInAccess access;
        try
        {
            access = await LookUpAsync(services, email, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync($"MagicLink.NET cannot tell whether {email} may sign in, so nobody can sign in now: {exception.Message}").ConfigureAwait(false);
            return CannotTell;
        }

        if (!access.MaySignIn)
        {
            await output.WriteLineAsync($"{email} may not sign in.").ConfigureAwait(false);
            return MayNotSignIn;
        }

        await output.WriteLineAsync($"{email} may sign in.").ConfigureAwait(false);
        await output.WriteLineAsync("Groups: " + (access.Groups.IsEmpty ? "none" : string.Join(", ", access.Groups))).ConfigureAwait(false);
        return MaySignIn;
    }

    /// <summary>Runs the start check of the source in use, then asks it about <paramref name="email"/>.</summary>
    private static ValueTask<SignInAccess> LookUpAsync(IServiceProvider services, string email, CancellationToken cancellationToken)
    {
        var source = services.GetService<ISignInSource>()
            ?? throw new InvalidOperationException("No source of who may sign in is registered, for example a file with AddMagicLinkSignInFile.");

        // Reading the settings runs their start check, which is skipped for a source that is not in use.
        _ = services.GetRequiredService<IOptions<MagicLinkSignInFileOptions>>().Value;
        _ = services.GetRequiredService<IOptions<MagicLinkSignInDatabaseOptions>>().Value;
        return source.LookUpAsync(email, cancellationToken);
    }
}
