using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicLink;

/// <summary>
/// Reads the file without reflection, so it works under Native AOT. A property it does not know, such as <c>group</c>
/// for <c>groups</c>, is an error rather than silently ignored: a typo must not drop someone's groups.
/// </summary>
[JsonSourceGenerationOptions(
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(SignInFile.Document))]
internal sealed partial class SignInFileJson : JsonSerializerContext;
