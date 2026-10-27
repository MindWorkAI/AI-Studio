namespace AIStudio.Tools.Mail;

/// <summary>
/// What a provider expects as the username for its IMAP server.
/// </summary>
public enum MailboxUsernameFormat
{
    /// <summary>
    /// The full e-mail address.
    /// </summary>
    EMAIL_ADDRESS,

    /// <summary>
    /// The part of the e-mail address before the @ sign, e.g., at iCloud.
    /// </summary>
    ADDRESS_NAME_PART,

    /// <summary>
    /// The account in the directory of the organization, e.g., at an Exchange server.
    /// </summary>
    DOMAIN_ACCOUNT,
}