using AIStudio.Tools.RAG;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// One page of a search of the mailboxes, as the model and the user receive it.
/// </summary>
/// <param name="Outcomes">What the model learns about each mailbox searched, its mails included, in the order they were searched.</param>
/// <param name="Sources">The sources of the mails which reached the model, listed below the answer.</param>
/// <param name="Passages">What the user is shown of these mails.</param>
internal sealed record SearchMailsResult(IReadOnlyList<MailboxSearchOutcome> Outcomes, IReadOnlyList<Source> Sources, IReadOnlyList<SourcePassage> Passages);