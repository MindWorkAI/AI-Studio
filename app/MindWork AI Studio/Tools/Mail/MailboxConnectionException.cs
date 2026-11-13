namespace AIStudio.Tools.Mail;

/// <summary>
/// A connection to the IMAP server of a mailbox failed, for the reason its failure code names.
/// </summary>
/// <remarks>
/// The message is meant for developers and names neither the user nor the server. The inner
/// exception may well do so, since servers tend to repeat the username in their answers. Log the
/// failure code and the type of the inner exception, never its message.
/// </remarks>
public sealed class MailboxConnectionException(MailboxConnectionFailure failure, string message, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Why the connection failed.
    /// </summary>
    public MailboxConnectionFailure Failure { get; } = failure;
}