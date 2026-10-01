using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Mail;

/// <summary>
/// The IMAP settings of well-known providers, read from their own documentation.
/// </summary>
/// <remarks>
/// A fixed table on purpose: AI Studio never asks a third party which server belongs to an
/// address, since that would tell it whose mailbox is being set up. Each entry was checked against
/// the help pages of the provider on 2026-10-01. When the provider documents more than one way to
/// connect, the entry takes the one it recommends, or TLS from the first byte on.<br/><br/>
/// Microsoft 365 and Outlook.com are missing on purpose: they no longer accept a password over
/// IMAP, only a sign-in with Microsoft.
/// </remarks>
public static class MailboxProviderTemplates
{
    private const int IMAPS_PORT = 993;

    public static readonly IReadOnlyList<MailboxProviderTemplate> ALL =
    [
        // Google allows app passwords only with 2-Step Verification, and recommends a sign-in with Google instead:
        new("Gmail", "imap.gmail.com", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.APP_PASSWORD, "https://support.google.com/accounts/answer/185833"),

        new("GMX", "imap.gmx.net", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.IMAP_ACTIVATION | MailboxProviderRequirements.APP_PASSWORD_WITH_TWO_FACTOR, "https://hilfe.gmx.net/pop-imap/einschalten.html"),
        new("WEB.DE", "imap.web.de", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.IMAP_ACTIVATION | MailboxProviderRequirements.APP_PASSWORD_WITH_TWO_FACTOR, "https://hilfe.web.de/pop-imap/einschalten.html"),
        new("IONOS", "imap.ionos.de", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.NONE, "https://www.ionos.de/hilfe/e-mail/allgemeine-themen/serverinformationen-fuer-imap-pop3-und-smtp/"),
        new("STRATO", "imap.strato.de", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.NONE, "https://www.strato.de/faq/mail/so-lauten-die-strato-e-mail-server"),
        new("mailbox.org", "imap.mailbox.org", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.APP_PASSWORD_WITH_TWO_FACTOR, "https://kb.mailbox.org/en/private/e-mail/e-mail-configuration/"),
        new("Hetzner (konsoleH)", "mail.your-server.de", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.NONE, "https://docs.hetzner.com/de/konsoleh/account-management/email/setting-up-an-email-account/"),

        // The host is the same for every address at Posteo, whatever its domain:
        new("Posteo", "posteo.de", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.EMAIL_ADDRESS, MailboxProviderRequirements.NONE, "https://posteo.de/en/help/how-do-i-set-up-posteo-in-an-email-client-pop3-imap-and-smtp"),

        new("iCloud Mail", "imap.mail.me.com", IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.ADDRESS_NAME_PART, MailboxProviderRequirements.APP_PASSWORD, "https://support.apple.com/en-us/102654"),

        // Every organization runs a server of its own, and IMAP ships switched off:
        new("Microsoft Exchange Server", string.Empty, IMAPS_PORT, MailboxTransportSecurity.SSL_ON_CONNECT, MailboxUsernameFormat.DOMAIN_ACCOUNT, MailboxProviderRequirements.ADMIN_ACTIVATION, "https://learn.microsoft.com/en-us/exchange/clients/pop3-and-imap4/configure-imap4"),
    ];
}