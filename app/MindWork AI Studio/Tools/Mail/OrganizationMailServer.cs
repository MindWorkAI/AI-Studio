namespace AIStudio.Tools.Mail;

/// <summary>
/// What MailServerPolicy needs to know about a mail server which a configuration plugin offers.
/// </summary>
/// <param name="Host">The host of the server.</param>
/// <param name="PasswordEncoding">The name of the encoding the server expects for usernames and passwords, cf. DataMailboxProvider.PasswordEncoding, or null when the organization did not say.</param>
public sealed record OrganizationMailServer(string Host, string? PasswordEncoding);