namespace MagicLink.AotProof;

/// <summary>Who may sign in to the proof: only <see cref="ProofClients.TestPerson"/>, in one group. Proof-only.</summary>
internal sealed class ProofSignInSource : ISignInSource
{
    private static readonly SignInAccess Person = SignInAccess.Allowed([ProofClients.TestGroup]);

    public ValueTask<SignInAccess> LookUpAsync(string email, CancellationToken cancellationToken) =>
        ValueTask.FromResult(string.Equals(email, ProofClients.TestPerson, StringComparison.Ordinal) ? Person : SignInAccess.Refused);
}
