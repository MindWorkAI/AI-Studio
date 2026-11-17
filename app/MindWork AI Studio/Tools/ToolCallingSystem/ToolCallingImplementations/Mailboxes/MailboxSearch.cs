using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// One mailbox, ready to be searched page by page.
/// </summary>
/// <remarks>
/// What a search has to know about a mailbox before it searches is read once per call, however
/// many pages of the mailbox the search turns.
/// </remarks>
/// <param name="Mailbox">The mailbox.</param>
/// <param name="Coverage">How far its index reaches, or null when that cannot be read.</param>
/// <param name="Filter">The conditions as they apply to this mailbox, or null when it could not be read any more, so that nothing is searched.</param>
/// <param name="FolderIsMissing">Whether the mailbox has none of the folders the model named, so that nothing is searched.</param>
/// <param name="DraftSync">What was done about drafts which may have changed since the last sync.</param>
internal sealed record MailboxSearch(DataSourceMailbox Mailbox, MailboxCoverage? Coverage, MailFilter? Filter, bool FolderIsMissing, MailDraftSyncDecision DraftSync);