namespace MagicLink.AotProof.Tests;

/// <summary>The database an in-process proof app runs on; not ready when no PostgreSQL server is configured.</summary>
public sealed record ProofDatabase(bool Ready, string? ConnectionString);
