using System.Globalization;
using System.Text;

namespace MagicLink.Tests.Mail;

/// <summary>Reads the text body of an email as SmtpClient writes it: one part, quoted-printable or base64.</summary>
internal static class Mime
{
    public static string Body(string data)
    {
        var split = data.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = data[..split];
        var body = data[(split + 4)..];
        if (headers.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(body.Replace("\r\n", string.Empty, StringComparison.Ordinal)));
        }

        if (!headers.Contains("Content-Transfer-Encoding: quoted-printable", StringComparison.OrdinalIgnoreCase))
        {
            return body;
        }

        var bytes = new List<byte>();
        var text = body.Replace("=\r\n", string.Empty, StringComparison.Ordinal);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '=' && i + 2 < text.Length)
            {
                bytes.Add(byte.Parse(text.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            }
            else
            {
                bytes.Add((byte)text[i]);
            }
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }
}
