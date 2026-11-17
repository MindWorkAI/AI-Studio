using System.Text.Json.Nodes;

using AIStudio.Tools.RAG;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// What a search tells the model about one mailbox.
/// </summary>
/// <param name="Search">The mailbox, as it was searched.</param>
/// <param name="Mails">Its mails which reached the model, as the model reads them.</param>
/// <param name="HasMore">Whether a further page of this mailbox holds mails.</param>
/// <param name="Gaps">What kept the search from covering the whole mailbox.</param>
/// <param name="LeftOutCount">How many of its mails a search of several mailboxes found, but had no room for.</param>
/// <param name="ListedFolders">The folders of the mailbox, filtered, when it has none of those the model named; otherwise null.</param>
internal sealed record MailboxSearchOutcome(MailboxSearch Search, IReadOnlyList<JsonObject> Mails, bool HasMore, IReadOnlyList<RetrievalGap> Gaps, int LeftOutCount, IReadOnlyList<string>? ListedFolders);