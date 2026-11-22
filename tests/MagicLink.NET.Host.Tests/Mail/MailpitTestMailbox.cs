using System.Text.Json;

namespace MagicLink.Host.Tests.Mail;

/// <summary>
/// The emails that reached Mailpit, the SMTP server CI gives the container, read through its API.
/// </summary>
internal sealed class MailpitTestMailbox(HttpClient mailpit) : ITestMailbox
{
    public async Task<IReadOnlyList<string>> ReadAsync(string email)
    {
        var cancel = TestContext.Current.CancellationToken;
        using var search = JsonDocument.Parse(await mailpit.GetStringAsync("/api/v1/search?limit=1000&query=" + Uri.EscapeDataString("to:\"" + email + "\""), cancel));

        // Mailpit lists the newest first.
        var ids = search.RootElement.GetProperty("messages").EnumerateArray().Select(static message => message.GetProperty("ID").GetString()!).Reverse().ToList();
        var texts = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            using var message = JsonDocument.Parse(await mailpit.GetStringAsync("/api/v1/message/" + id, cancel));
            texts.Add(message.RootElement.GetProperty("Text").GetString()!);
        }

        return texts;
    }
}
