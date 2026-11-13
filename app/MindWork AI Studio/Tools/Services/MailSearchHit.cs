using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.Services;

/// <summary>
/// One mail a search of a mailbox found.
/// </summary>
/// <param name="Summary">What a list of mails shows about the mail.</param>
/// <param name="Passage">The passage of the mail which matched the query best, or null when the mails were listed without a query.</param>
/// <param name="PassageLocation">Where that passage is in the mail, e.g., on which page of which attachment, or null without a passage.</param>
public sealed record MailSearchHit(MailSummary Summary, string? Passage, ChunkLocation? PassageLocation = null);