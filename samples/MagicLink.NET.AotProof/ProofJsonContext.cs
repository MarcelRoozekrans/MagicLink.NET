using System.Text.Json.Serialization;

namespace MagicLink.AotProof;

/// <summary>Source-generated JSON for the proof's own responses, so no reflection-based serializer is needed.</summary>
[JsonSerializable(typeof(MeResponse))]
[JsonSerializable(typeof(MailboxResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class ProofJsonContext : JsonSerializerContext;
