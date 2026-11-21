using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// Who may sign in and with which groups, from a file kept in git (ADR-014); see <see cref="MagicLinkSignInFileOptions"/>.
/// The file is read again when its time or size changes. While it cannot be read, nobody signs in: a look-up throws, and
/// the reason is logged once per change of the file.
/// </summary>
internal sealed partial class SignInFileSource(IOptions<MagicLinkSignInFileOptions> options, ILogger<SignInFileSource> logger) : ISignInSource
{
    private readonly Lock _lock = new();
    private Snapshot? _snapshot;

    /// <inheritdoc />
    public ValueTask<SignInAccess> LookUpAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        var people = People();
        return ValueTask.FromResult(people.TryGetValue(email, out var access) ? access : SignInAccess.Refused);
    }

    /// <summary>The people in the file as it is now; reads it again only when it changed.</summary>
    private FrozenDictionary<string, SignInAccess> People()
    {
        // Read lazily, not in the constructor: the start check of these options looks this source up.
        var path = Path.GetFullPath(options.Value.Path!);
        var stamp = Stamp.Of(path);
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is null || snapshot.Stamp != stamp)
        {
            lock (_lock)
            {
                snapshot = _snapshot;
                if (snapshot is null || snapshot.Stamp != stamp)
                {
                    // The stamp is taken before reading, so a change made while reading is read on the next look-up.
                    snapshot = new Snapshot(stamp, SignInFile.Read(path));
                    Volatile.Write(ref _snapshot, snapshot);
                    if (snapshot.People.IsFailure)
                    {
                        FileUnusable(logger, snapshot.People.Error);
                    }
                }
            }
        }

        return snapshot.People.IsSuccess ? snapshot.People.Value : throw new InvalidOperationException(snapshot.People.Error);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Reason} Nobody can sign in until the file is fixed.")]
    private static partial void FileUnusable(ILogger logger, string reason);

    /// <summary>The file as read at <paramref name="Stamp"/>.</summary>
    private sealed record Snapshot(Stamp Stamp, ZeroAlloc.Results.Result<FrozenDictionary<string, SignInAccess>> People);

    /// <summary>When the file was last written, and its size; <see cref="Missing"/> when it is not there.</summary>
    private readonly record struct Stamp(DateTime WrittenAt, long Length)
    {
        private static readonly Stamp Missing = new(DateTime.MinValue, -1);

        public static Stamp Of(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? new Stamp(file.LastWriteTimeUtc, file.Length) : Missing;
        }
    }
}
