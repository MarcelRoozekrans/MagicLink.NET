namespace MagicLink.Host.Tests.Mail;

internal static class TestMailboxExtensions
{
    private const string CodeIntro = "Or type this code there:";

    /// <summary>The code of the first email to <paramref name="email"/> after the <paramref name="sentBefore"/> emails before it.</summary>
    public static async Task<string> WaitForCodeAsync(this ITestMailbox mailbox, string email, int sentBefore)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var emails = await mailbox.ReadAsync(email);
            if (emails.Count > sentBefore)
            {
                return CodeIn(emails[sentBefore]);
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No sign-in email to {email} arrived within 10 seconds.");
    }

    private static string CodeIn(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var intro = Array.FindIndex(lines, static line => string.Equals(line.Trim(), CodeIntro, StringComparison.Ordinal));
        Assert.True(intro >= 0 && intro + 1 < lines.Length, "The email has no code: " + text);
        return lines[intro + 1].Trim();
    }
}
