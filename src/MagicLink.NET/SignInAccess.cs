using System.Collections.Immutable;

namespace MagicLink;

/// <summary>What an <see cref="ISignInSource"/> says about an email address: whether it may sign in, and its groups.</summary>
public sealed class SignInAccess
{
    private SignInAccess(bool maySignIn, ImmutableArray<string> groups)
    {
        MaySignIn = maySignIn;
        Groups = groups;
    }

    /// <summary>The address may not sign in.</summary>
    public static SignInAccess Refused { get; } = new(maySignIn: false, []);

    /// <summary>Whether the address may sign in.</summary>
    public bool MaySignIn { get; }

    /// <summary>The person's groups, each once and in the source's order; empty when refused.</summary>
    public ImmutableArray<string> Groups { get; }

    /// <summary>The address may sign in, with <paramref name="groups"/>; a group listed twice counts once.</summary>
    public static SignInAccess Allowed(IEnumerable<string> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var group in groups)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(groups));
            if (seen.Add(group))
            {
                builder.Add(group);
            }
        }

        return new SignInAccess(maySignIn: true, builder.ToImmutable());
    }
}
