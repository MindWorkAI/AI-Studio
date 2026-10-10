using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.Services;

/// <summary>
/// How far the index of a mailbox reaches, so that an answer from it can say what it does not cover.
/// </summary>
/// <param name="ReceivedSinceUtc">Since when mails are indexed, or null when all of them are. Flagged mails and drafts are indexed regardless of their age.</param>
/// <param name="LastCompleteSyncUtc">When the last complete sync ended, or null while the first one is still running.</param>
/// <param name="SignInRefusedAtUtc">When the server refused to let AI Studio sign in, or null when it did not. Until the user deals with that, no new mail arrives in the index.</param>
/// <param name="PendingRemovalCount">How many mails the index still holds although a sync would have removed them, waiting for the user to agree, or null when there are none.</param>
/// <param name="Folders">The folders of the mailbox and how far their sync got, ordered by path.</param>
public sealed record MailboxCoverage(DateTimeOffset? ReceivedSinceUtc, DateTimeOffset? LastCompleteSyncUtc, DateTimeOffset? SignInRefusedAtUtc, int? PendingRemovalCount, IReadOnlyList<MailFolderRecord> Folders);