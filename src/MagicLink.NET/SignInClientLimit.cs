using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace MagicLink;

/// <summary>
/// Counts the sign-ins each client IP address started in a sliding window, so nobody can mail or probe many addresses
/// from one machine (spec "confirm sign-in with a link or a code", criterion 8).
/// </summary>
/// <remarks>
/// Every sign-in started counts, for a known or an unknown address, so the count tells nobody which addresses exist.
/// </remarks>
internal sealed class SignInClientLimit(TimeProvider clock) : SlidingWindowLimit(clock)
{
    /// <summary>
    /// The key a client counts under: an IPv4 address as itself, also when a dual-stack socket reports it as an
    /// IPv4-mapped IPv6 address, and an IPv6 address by its /64 network, since one machine usually holds a whole /64.
    /// </summary>
    public static string Key(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return string.Create(CultureInfo.InvariantCulture, $"{new IPAddress(bytes)}/64");
    }
}
