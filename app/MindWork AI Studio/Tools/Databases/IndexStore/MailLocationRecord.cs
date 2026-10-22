namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One place on the server where a mail lies.
/// </summary>
/// <param name="FolderPath">The full path of the folder, as the server names it.</param>
/// <param name="Uid">The UID of the mail within that folder.</param>
/// <param name="Flags">The flags of the mail there.</param>
public sealed record MailLocationRecord(string FolderPath, long Uid, MailFlags Flags);