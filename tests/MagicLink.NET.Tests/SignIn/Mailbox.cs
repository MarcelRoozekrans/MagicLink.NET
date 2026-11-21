namespace MagicLink.Tests.SignIn;

/// <summary>
/// Catches the sign-in emails MagicLink.NET sends, so a test can read the link and the code. Sending happens in the
/// background, so a test waits for an email with <see cref="WaitForAsync"/>; <see cref="Hold"/> keeps emails from
/// arriving until <see cref="Release"/>.
/// </summary>
internal sealed class Mailbox : ISignInMessageSender
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private readonly List<SignInMessage> _messages = [];
    private TaskCompletionSource _arrived = NewSignal();
    private TaskCompletionSource? _held;

    /// <summary>How many emails arrived so far.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count;
            }
        }
    }

    /// <summary>Waits for the email number <paramref name="index"/>, counting from 0, and returns it.</summary>
    public async Task<SignInMessage> WaitForAsync(int index)
    {
        using var timeout = new CancellationTokenSource(Patience);
        while (true)
        {
            Task arrived;
            lock (_lock)
            {
                if (_messages.Count > index)
                {
                    return _messages[index];
                }

                arrived = _arrived.Task;
            }

            await arrived.WaitAsync(timeout.Token);
        }
    }

    /// <summary>Lets no email arrive until <see cref="Release"/>, as a slow mail server would.</summary>
    public void Hold()
    {
        lock (_lock)
        {
            _held ??= NewSignal();
        }
    }

    /// <summary>Lets the held emails arrive.</summary>
    public void Release()
    {
        TaskCompletionSource? held;
        lock (_lock)
        {
            held = _held;
            _held = null;
        }

        held?.TrySetResult();
    }

    public async ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
    {
        Task? held;
        lock (_lock)
        {
            held = _held?.Task;
        }

        if (held is not null)
        {
            await held.WaitAsync(cancellationToken);
        }

        TaskCompletionSource arrived;
        lock (_lock)
        {
            _messages.Add(message);
            arrived = _arrived;
            _arrived = NewSignal();
        }

        arrived.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
