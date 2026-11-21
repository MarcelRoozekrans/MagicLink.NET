namespace MagicLink.Tests.SignIn;

/// <summary>Catches the sign-in emails MagicLink.NET would send, so a test can read the link and the code.</summary>
internal sealed class Mailbox : ISignInMessageSender
{
    private readonly Lock _lock = new();
    private readonly List<SignInMessage> _messages = [];

    /// <summary>How many emails were sent so far.</summary>
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

    /// <summary>The email sent as number <paramref name="index"/>, counting from 0.</summary>
    public SignInMessage this[int index]
    {
        get
        {
            lock (_lock)
            {
                return _messages[index];
            }
        }
    }

    public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }

        return ValueTask.CompletedTask;
    }
}
