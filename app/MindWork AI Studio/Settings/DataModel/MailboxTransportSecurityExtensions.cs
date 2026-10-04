using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class MailboxTransportSecurityExtensions
{
    /// <summary>
    /// The port IMAP with TLS from the start usually listens on.
    /// </summary>
    public const int SSL_ON_CONNECT_PORT = 993;

    /// <summary>
    /// The port IMAP with STARTTLS usually listens on.
    /// </summary>
    public const int STARTTLS_PORT = 143;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxTransportSecurityExtensions).Namespace, nameof(MailboxTransportSecurityExtensions));

    public static string GetName(this MailboxTransportSecurity transportSecurity) => transportSecurity switch
    {
        MailboxTransportSecurity.SSL_ON_CONNECT => TB("TLS from the start (usually port 993)"),
        MailboxTransportSecurity.STARTTLS => TB("STARTTLS (usually port 143)"),

        _ => TB("Unknown encryption"),
    };

    /// <summary>
    /// The port the encryption usually comes with.
    /// </summary>
    /// <returns>The port, or null when there is no usual one.</returns>
    public static int? GetUsualPort(this MailboxTransportSecurity transportSecurity) => transportSecurity switch
    {
        MailboxTransportSecurity.SSL_ON_CONNECT => SSL_ON_CONNECT_PORT,
        MailboxTransportSecurity.STARTTLS => STARTTLS_PORT,

        _ => null,
    };
}