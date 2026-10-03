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
}