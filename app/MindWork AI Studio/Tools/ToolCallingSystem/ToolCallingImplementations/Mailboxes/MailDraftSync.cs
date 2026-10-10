using System.Text.Json.Nodes;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

/// <summary>
/// Keeps the drafts which a mail tool reads up to date.
/// </summary>
/// <remarks>
/// A user who saved a draft a moment ago and asks the AI to improve it expects the AI to find it,
/// but the next sync at the interval may be a quarter of an hour away. A tool which asks for drafts
/// therefore starts a sync, unless the last one is just as recent, and its result says that drafts
/// may be missing or outdated until then. Received mails keep to the interval: nobody waits for a
/// mail they are writing themselves there.
/// </remarks>
internal static class MailDraftSync
{
    /// <summary>
    /// How long after a complete sync the drafts count as current.
    /// </summary>
    public static readonly TimeSpan CURRENT_FOR = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether the conditions ask for drafts: by the special folder, or by the path of a folder the server marks for drafts.
    /// </summary>
    /// <remarks>
    /// Asking by the special folder counts even when the mailbox has no folder for drafts in the
    /// index yet: a server may create it only when the first draft is saved.
    /// </remarks>
    /// <param name="conditions">The conditions the model set.</param>
    /// <param name="filter">The conditions for the mailbox, with the folder turned into its paths.</param>
    /// <param name="folders">The folders of the mailbox.</param>
    public static bool AsksForDrafts(MailConditions conditions, MailFilter filter, IReadOnlyList<MailFolderRecord> folders) =>
        conditions.SpecialFolder is MailFolderSpecialUse.DRAFTS
        || (filter.FolderPaths is { } folderPaths && folders.Any(folder => folder.SpecialUse is MailFolderSpecialUse.DRAFTS && folderPaths.Contains(folder.Path, StringComparer.Ordinal)));

    /// <summary>
    /// Decides what to do about drafts which may have changed since the last complete sync.
    /// </summary>
    /// <remarks>
    /// A refused sign-in is reported anyway, and a sync would not sign in. A first sync which is
    /// still running gets a follow-up, since it may have looked at the drafts before the change.
    /// </remarks>
    /// <param name="asksForDrafts">Whether the tool asks for drafts, see AsksForDrafts.</param>
    /// <param name="automaticRefresh">Whether the user lets the data sources refresh on their own.</param>
    /// <param name="coverage">How far the index of the mailbox reaches.</param>
    /// <param name="now">The current point in time.</param>
    /// <returns>What to do.</returns>
    public static MailDraftSyncDecision Decide(bool asksForDrafts, bool automaticRefresh, MailboxCoverage coverage, DateTimeOffset now)
    {
        if (!asksForDrafts || coverage.SignInRefusedAtUtc is not null)
            return MailDraftSyncDecision.NOT_NEEDED;

        if (coverage.LastCompleteSyncUtc is { } lastSync && now - lastSync < CURRENT_FOR)
            return MailDraftSyncDecision.NOT_NEEDED;

        return automaticRefresh ? MailDraftSyncDecision.SYNC_REQUESTED : MailDraftSyncDecision.AUTOMATIC_REFRESH_OFF;
    }

    /// <summary>
    /// Requests a sync of a mailbox when the conditions ask for its drafts and they may be outdated.
    /// </summary>
    /// <param name="embeddingService">The service which syncs the mailbox.</param>
    /// <param name="settingsManager">The settings, which say whether the data sources refresh on their own.</param>
    /// <param name="mailbox">The mailbox.</param>
    /// <param name="conditions">The conditions the model set.</param>
    /// <param name="filter">The conditions for the mailbox, with the folder turned into its paths.</param>
    /// <param name="coverage">How far the index of the mailbox reaches.</param>
    /// <returns>What was done, for the result to tell the model. NOT_NEEDED as well when the service declined the sync after all.</returns>
    public static async Task<MailDraftSyncDecision> RequestIfNeededAsync(DataSourceEmbeddingService embeddingService, SettingsManager settingsManager, DataSourceMailbox mailbox, MailConditions conditions, MailFilter filter, MailboxCoverage coverage)
    {
        var decision = Decide(AsksForDrafts(conditions, filter, coverage.Folders), settingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh, coverage, DateTimeOffset.UtcNow);
        if (decision is MailDraftSyncDecision.SYNC_REQUESTED && !await embeddingService.RequestMailboxSyncAsync(mailbox.Id))
            return MailDraftSyncDecision.NOT_NEEDED;

        return decision;
    }

    /// <summary>
    /// Adds what the model has to know about the drafts of a mailbox to what it learns about the mailbox, and to its issues.
    /// </summary>
    /// <param name="description">What the model learns about the mailbox.</param>
    /// <param name="issues">What kept the result from covering the whole mailbox.</param>
    /// <param name="decision">What was done about the drafts.</param>
    public static void Describe(JsonObject description, JsonArray issues, MailDraftSyncDecision decision)
    {
        switch (decision)
        {
            case MailDraftSyncDecision.SYNC_REQUESTED:
                description["sync_requested"] = true;
                issues.Add("Drafts saved or changed since the last complete sync of this mailbox may be missing or outdated in this result. AI Studio started a sync to fetch them. When a draft the user expects is missing, search again later, or tell the user to ask again shortly.");
                break;

            case MailDraftSyncDecision.AUTOMATIC_REFRESH_OFF:
                issues.Add("Drafts saved or changed since the last complete sync of this mailbox may be missing or outdated in this result. The user switched off that AI Studio refreshes its data sources on its own, so they appear only once the user syncs the mailbox in AI Studio, on the page of the background embeddings.");
                break;
        }
    }
}