using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroAlloc.Results;

namespace MagicLink;

/// <summary>Reads the file that says who may sign in and with which groups; see <see cref="MagicLinkSignInFileOptions"/>.</summary>
internal static class SignInFile
{
    /// <summary>The people in the file at <paramref name="path"/>, by normalized email address, or why it cannot be used.</summary>
    public static Result<FrozenDictionary<string, SignInAccess>> Read(string path)
    {
        byte[] json;
        try
        {
            json = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Failure($"The file of who may sign in, {path}, cannot be read: {exception.Message}");
        }

        var people = Parse(json);
        return people.IsSuccess ? people : Failure($"The file of who may sign in, {path}, {people.Error}");
    }

    /// <summary>The people in <paramref name="json"/>, by normalized email address, or what is wrong with it.</summary>
    public static Result<FrozenDictionary<string, SignInAccess>> Parse(byte[] json)
    {
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize(json, SignInFileJson.Default.Document);
        }
        catch (JsonException exception)
        {
            return Failure($"does not have the expected format: {exception.Message}");
        }

        if (document?.People is not { } entries)
        {
            return Failure("has no \"people\" list.");
        }

        var people = new Dictionary<string, SignInAccess>(entries.Count, StringComparer.Ordinal);
        for (var index = 0; index < entries.Count; index++)
        {
            var number = index + 1;
            if (entries[index] is not { } person)
            {
                return Failure($"has an error in entry {number}: it is empty.");
            }

            if (string.IsNullOrWhiteSpace(person.Email))
            {
                return Failure($"has an error in entry {number}: it has no email address.");
            }

            if (!SignInPage.IsEmailAddress(person.Email.Trim()))
            {
                return Failure($"has an error in entry {number}: '{person.Email}' is not an email address.");
            }

            var groups = new List<string>(person.Groups?.Count ?? 0);
            foreach (var group in person.Groups ?? [])
            {
                if (string.IsNullOrWhiteSpace(group))
                {
                    return Failure($"has an error in entry {number}: it has an empty group.");
                }

                groups.Add(group);
            }

            var email = MagicLinkSession.Normalize(person.Email);
            if (!people.TryAdd(email, SignInAccess.Allowed(groups)))
            {
                return Failure($"has an error in entry {number}: {email} is listed more than once.");
            }
        }

        return Result<FrozenDictionary<string, SignInAccess>>.Success(people.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static Result<FrozenDictionary<string, SignInAccess>> Failure(string error) => Result<FrozenDictionary<string, SignInAccess>>.Failure(error);

    /// <summary>The file as a whole.</summary>
    internal sealed class Document
    {
        /// <summary>Everyone who may sign in.</summary>
        [JsonPropertyName("people")]
        public List<Person?>? People { get; set; }
    }

    /// <summary>One person who may sign in.</summary>
    internal sealed class Person
    {
        /// <summary>The person's email address, in any case.</summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>The person's groups; none when left out.</summary>
        [JsonPropertyName("groups")]
        public List<string?>? Groups { get; set; }
    }
}
