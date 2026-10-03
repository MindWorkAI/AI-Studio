namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// How many mails of a mailbox meet some conditions.
/// </summary>
/// <param name="TotalCount">How many mails meet them, each mail counted once.</param>
/// <param name="Groups">The largest groups, largest first; empty without a grouping.</param>
public sealed record MailCountResult(long TotalCount, IReadOnlyList<MailCountGroup> Groups);