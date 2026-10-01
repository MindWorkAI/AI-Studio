using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Mail;

/// <summary>
/// The IMAP settings of a well-known provider, to fill in the mailbox dialog.
/// </summary>
/// <param name="Name">The name of the provider, the same in every language.</param>
/// <param name="Host">The host of its IMAP server, or empty when every organization runs a server of its own.</param>
/// <param name="Port">The port of its IMAP server.</param>
/// <param name="TransportSecurity">How the connection is encrypted, the way the provider recommends.</param>
/// <param name="UsernameFormat">What the provider expects as the username.</param>
/// <param name="Requirements">What the provider requires before AI Studio can sign in.</param>
/// <param name="HelpUrl">The page of the provider which explains what the user has to do first.</param>
public sealed record MailboxProviderTemplate(string Name, string Host, int Port, MailboxTransportSecurity TransportSecurity, MailboxUsernameFormat UsernameFormat, MailboxProviderRequirements Requirements, string HelpUrl);