namespace AIStudio.Tools.Mail;

/// <summary>
/// How a folder stands on the server when the sync opens it.
/// </summary>
/// <param name="UidValidity">The UIDVALIDITY. When it differs from the stored one, every stored UID of the folder is void.</param>
/// <param name="UidNext">The UID the next mail will get, or null when the server does not say.</param>
/// <param name="HighestModSeq">The HIGHESTMODSEQ, or null without CONDSTORE.</param>
/// <param name="MessageCount">How many mails the folder holds (STATUS MESSAGES).</param>
/// <param name="UnseenCount">How many of them are unread (STATUS UNSEEN).</param>
public sealed record MailFolderState(long UidValidity, long? UidNext, long? HighestModSeq, long MessageCount, long UnseenCount);