namespace AIStudio.Tools.Mail;

/// <summary>
/// Why a connection to the IMAP server of a mailbox failed.
/// </summary>
public enum MailboxConnectionFailure
{
    /// <summary>
    /// The server rejected the username or the password. AI Studio does not try again on its own, because every rejected attempt brings the account closer to being locked.
    /// </summary>
    AUTHENTICATION_FAILED,

    /// <summary>
    /// The server could not be reached or stopped answering, e.g., without a VPN connection. Says nothing about the password.
    /// </summary>
    NETWORK_UNAVAILABLE,

    /// <summary>
    /// No encrypted connection came about: the certificate of the server was not trusted, or the server does not offer STARTTLS.
    /// </summary>
    TLS_FAILED,

    /// <summary>
    /// The server refused a command, or answered in a way the IMAP client did not understand.
    /// </summary>
    SERVER_ERROR,

    /// <summary>
    /// The settings of the mailbox are incomplete, or this version of AI Studio does not know them. Nothing was sent to the server.
    /// </summary>
    INVALID_SETTINGS,
}