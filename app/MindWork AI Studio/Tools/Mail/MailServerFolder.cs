namespace AIStudio.Tools.Mail;

/// <summary>
/// A folder as the IMAP server lists it.
/// </summary>
/// <param name="FullName">The full path as the server names it, including the server's own hierarchy delimiter.</param>
/// <param name="Name">The last part of the path, as shown in a folder tree.</param>
/// <param name="ParentFullName">The full name of the folder this one lies in, or empty at the top level.</param>
/// <param name="DirectorySeparator">The hierarchy delimiter of the server, or the null character when it has none.</param>
/// <param name="SpecialUse">What the folder is for, as the server announces it.</param>
/// <param name="IsInbox">Whether this is the inbox, which IMAP names INBOX in any case.</param>
/// <param name="CanSelect">Whether the folder can hold mails. A folder which cannot only groups other folders.</param>
public sealed record MailServerFolder(string FullName, string Name, string ParentFullName, char DirectorySeparator, MailFolderSpecialUse SpecialUse, bool IsInbox, bool CanSelect);