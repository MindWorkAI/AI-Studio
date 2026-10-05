namespace AIStudio.Tools.Mail;

/// <summary>
/// How important the sender marked a mail, read from the Importance, X-Priority and Priority headers.
/// </summary>
/// <remarks>
/// There is no value for an unknown importance on purpose: RFC 2156 says that a mail without any
/// such header is a normal one, so NORMAL is what the mail says, not a guess. It comes first so
/// that a value nobody set is the normal one as well, never LOW.
/// </remarks>
public enum MailImportance
{
    NORMAL,
    LOW,
    HIGH,
}