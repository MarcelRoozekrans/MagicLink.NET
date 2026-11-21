namespace MagicLink.Tests.SignIn;

/// <summary><see cref="SignInHosts"/> with the default limit on sign-in emails per address.</summary>
public sealed class DefaultLimitSignInHosts : SignInHosts
{
    protected override int? MaxEmailsPerAddress => null;
}
