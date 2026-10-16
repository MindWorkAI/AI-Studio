namespace AIStudio.Tools.Mail;

/// <summary>
/// What a provider requires before AI Studio can sign in to its IMAP server.
/// </summary>
[Flags]
public enum MailboxProviderRequirements
{
    NONE = 0,

    /// <summary>
    /// An app password instead of the usual password, in every case.
    /// </summary>
    APP_PASSWORD = 1,

    /// <summary>
    /// An app password instead of the usual password, once two-factor authentication is enabled.
    /// </summary>
    APP_PASSWORD_WITH_TWO_FACTOR = 2,

    /// <summary>
    /// The user enables the IMAP access in the settings of the webmail first.
    /// </summary>
    IMAP_ACTIVATION = 4,

    /// <summary>
    /// The IT department enables IMAP on the server first, as Exchange ships with it switched off.
    /// </summary>
    ADMIN_ACTIVATION = 8,
}