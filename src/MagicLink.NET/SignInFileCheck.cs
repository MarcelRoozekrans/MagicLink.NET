using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// Reads the file of who may sign in when MagicLink.NET starts, while that file is the source in use, and refuses to
/// start when it cannot be read or is wrong, with the reason (ADR-014).
/// </summary>
/// <remarks>The source is looked up while validating, not injected, as <see cref="SmtpSettingsCheck"/> does with the sender.</remarks>
internal sealed class SignInFileCheck(IServiceProvider services) : IValidateOptions<MagicLinkSignInFileOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MagicLinkSignInFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.GetService<ISignInSource>() is SignInFileSource ? Check(options) : ValidateOptionsResult.Skip;
    }

    /// <summary>Whether the file <paramref name="options"/> point to can be read and says clearly who may sign in.</summary>
    internal static ValidateOptionsResult Check(MagicLinkSignInFileOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Path))
        {
            return ValidateOptionsResult.Fail("MagicLink.NET reads who may sign in from a file, so MagicLinkSignInFileOptions.Path must be set.");
        }

        var people = SignInFile.Read(Path.GetFullPath(options.Path));
        return people.IsSuccess ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(people.Error);
    }
}
