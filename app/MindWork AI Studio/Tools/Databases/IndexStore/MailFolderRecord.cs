using AIStudio.Tools.Mail;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One folder of a mailbox and how far its sync got.
/// </summary>
/// <param name="Path">The full path of the folder, as the server names it.</param>
/// <param name="SpecialUse">What the server says the folder is for.</param>
/// <param name="UidValidity">The UIDVALIDITY the stored locations of this folder belong to.</param>
/// <param name="UidNext">The UIDNEXT at the end of the last complete pass, or null until one completed.</param>
/// <param name="HighestModSeq">The HIGHESTMODSEQ at the end of the last complete pass, or null until one completed or without CONDSTORE.</param>
/// <param name="ServerMessageCount">How many mails the folder holds on the server, or null when not read yet.</param>
/// <param name="ServerUnseenCount">How many of them are unread, or null when not read yet.</param>
/// <param name="InitialSyncCompletedUtc">When the first pass over the folder completed, or null while it runs.</param>
public sealed record MailFolderRecord(
    string Path,
    MailFolderSpecialUse SpecialUse,
    long UidValidity,
    long? UidNext,
    long? HighestModSeq,
    long? ServerMessageCount,
    long? ServerUnseenCount,
    DateTimeOffset? InitialSyncCompletedUtc);