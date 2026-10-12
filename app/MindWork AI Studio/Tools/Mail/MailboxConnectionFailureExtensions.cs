using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Mail;

public static class MailboxConnectionFailureExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxConnectionFailureExtensions).Namespace, nameof(MailboxConnectionFailureExtensions));

    /// <summary>
    /// What went wrong, and what the user can do about it.
    /// </summary>
    public static string GetDescription(this MailboxConnectionFailure failure) => failure switch
    {
        MailboxConnectionFailure.AUTHENTICATION_FAILED => TB("The server rejected the username or the password. Some providers require an app password instead of your usual password."),
        MailboxConnectionFailure.NETWORK_UNAVAILABLE => TB("The server could not be reached. Please check the host and the port, and whether you need a VPN connection."),
        MailboxConnectionFailure.TLS_FAILED => TB("No encrypted connection to the server could be established. When your organization uses a certificate authority of its own, enable the additional root certificates in the app settings, select the bundle with its root certificate, and add the host of the server to the allowed hosts. Your IT department can also configure this for you."),
        MailboxConnectionFailure.SERVER_ERROR => TB("The server reported an error. Please try again later."),
        MailboxConnectionFailure.INVALID_SETTINGS => TB("The settings of this mailbox are incomplete, or they were made by a newer version of AI Studio."),
        MailboxConnectionFailure.SERVER_NOT_ALLOWED => TB("Your organization allows mailboxes only on its own mail servers, and this server is none of them. AI Studio does not connect to it, and the AI does not read this mailbox."),

        _ => TB("The connection to the server failed for an unknown reason."),
    };
}