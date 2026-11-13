using System.Globalization;

namespace AIStudio.Tools.Mail;

public static class MailServerHosts
{
    /// <summary>
    /// The host in the form certificates and the allowed hosts for root certificates use.
    /// </summary>
    /// <param name="host">The host as somebody wrote it, e.g., with umlauts or surrounding white space.</param>
    /// <param name="idnHost">The host in its ASCII form, or an IP address as written.</param>
    /// <returns>True when the text is a host name or an IP address.</returns>
    public static bool TryGetIdnHost(string host, out string idnHost)
    {
        idnHost = host.Trim();
        switch (Uri.CheckHostName(idnHost))
        {
            case UriHostNameType.IPv4:
            case UriHostNameType.IPv6:
                return true;

            case UriHostNameType.Dns:
                try
                {
                    idnHost = new IdnMapping().GetAscii(idnHost);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether two hosts name the same server, however each was written.
    /// </summary>
    /// <remarks>
    /// Case, umlauts against their ASCII form, and a closing dot make no difference. A text which
    /// is no host at all is the same as nothing.
    /// </remarks>
    /// <param name="host">The one host.</param>
    /// <param name="otherHost">The other host.</param>
    /// <returns>True when both are hosts and name the same server.</returns>
    public static bool AreSame(string host, string otherHost) =>
        TryGetIdnHost(host, out var idnHost) &&
        TryGetIdnHost(otherHost, out var otherIdnHost) &&
        idnHost.TrimEnd('.').Equals(otherIdnHost.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);
}