namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// The number of mails in one group, e.g., one folder or one sender.
/// </summary>
/// <param name="Key">The folder path or the sender address.</param>
/// <param name="DisplayName">A name the sender uses, empty for folders and for senders without one.</param>
/// <param name="Count">How many mails belong to the group.</param>
public sealed record MailCountGroup(string Key, string DisplayName, long Count);