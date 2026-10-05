using AIStudio.Tools.RAG;

namespace AIStudio.Tools.Services;

/// <summary>
/// One page of the mails a search found in a mailbox.
/// </summary>
/// <param name="Hits">The mails: the best matches first, or without a query the most recently received first.</param>
/// <param name="HasMore">Whether the next page is worth asking for.</param>
/// <param name="Gaps">What kept the search from covering the whole mailbox, empty when nothing did.</param>
public sealed record MailSearchPage(IReadOnlyList<MailSearchHit> Hits, bool HasMore, IReadOnlyList<RetrievalGap> Gaps)
{
    public static readonly MailSearchPage EMPTY = new([], false, []);
}