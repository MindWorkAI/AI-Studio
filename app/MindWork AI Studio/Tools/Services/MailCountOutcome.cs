using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.RAG;

namespace AIStudio.Tools.Services;

/// <summary>
/// How many mails of a mailbox meet some conditions, as far as the index could tell.
/// </summary>
/// <param name="Count">The number of mails, or null when the mailbox could not be counted.</param>
/// <param name="Gaps">What kept the count from covering the whole mailbox, empty when nothing did.</param>
public sealed record MailCountOutcome(MailCountResult? Count, IReadOnlyList<RetrievalGap> Gaps);