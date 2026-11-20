namespace MagicLink.AotProof.Tests;

/// <summary>The flows on the in-memory application store, which needs no database.</summary>
public sealed class InMemoryFlowTests(InMemoryProofHost host) : FlowTests(host), IClassFixture<InMemoryProofHost>;
