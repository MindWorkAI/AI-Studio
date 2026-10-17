using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks when a mail tool syncs a mailbox before it reads the drafts.
/// </summary>
/// <remarks>
/// A user who saved a draft a moment ago and asks the AI to improve it must not hear that there is
/// no such draft. A sync with every search would go too far, though, and a refused sign-in must never
/// be tried again on the way, or the account of the user gets locked.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class MailDraftSyncTests : ToolRegistryTestBase
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DataSourceMailbox WORK = new() { Id = "5a1e7c3d-9b2f-4d8e-a6c4-3f0b9d2e1c75", Name = "Work" };

    private static readonly IReadOnlyList<MailFolderRecord> FOLDERS = [Folder("INBOX"), Folder("Entwürfe", MailFolderSpecialUse.DRAFTS), Folder("Gesendet", MailFolderSpecialUse.SENT)];

    [TestCase(true, 120, MailDraftSyncDecision.SYNC_REQUESTED)]
    [TestCase(true, null, MailDraftSyncDecision.SYNC_REQUESTED)]
    [TestCase(true, 30, MailDraftSyncDecision.NOT_NEEDED)]
    [TestCase(false, 120, MailDraftSyncDecision.AUTOMATIC_REFRESH_OFF)]
    [TestCase(false, 30, MailDraftSyncDecision.NOT_NEEDED)]
    public void TheDraftsAreSyncedOnlyWhenTheyMayBeOutdated(bool automaticRefresh, int? secondsSinceLastSync, MailDraftSyncDecision expected)
    {
        var lastSync = secondsSinceLastSync is { } seconds ? NOW.AddSeconds(-seconds) : (DateTimeOffset?)null;

        Assert.That(MailDraftSync.Decide(true, automaticRefresh, Coverage(lastSync, signInRefusedAtUtc: null), NOW), Is.EqualTo(expected), "A first sync which is still running gets a follow-up, since it may have looked at the drafts before the change.");
    }

    [Test]
    public void ReceivedMailsKeepToTheInterval() =>
        Assert.That(MailDraftSync.Decide(false, true, Coverage(NOW.AddHours(-1), signInRefusedAtUtc: null), NOW), Is.EqualTo(MailDraftSyncDecision.NOT_NEEDED));

    [Test]
    public void ARefusedSignInIsNeverTriedOnTheWay() =>
        Assert.That(MailDraftSync.Decide(true, true, Coverage(NOW.AddHours(-1), NOW.AddMinutes(-30)), NOW), Is.EqualTo(MailDraftSyncDecision.NOT_NEEDED), "Every refused attempt brings the account of the user closer to being locked.");

    [Test]
    public void TheDraftsAreAskedForByTheSpecialFolderOrByTheirPath()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AsksForDrafts(new MailConditions(new MailFilter(), null, MailFolderSpecialUse.DRAFTS), FOLDERS), Is.True);
            Assert.That(AsksForDrafts(new MailConditions(new MailFilter(), "entwürfe", null), FOLDERS), Is.True, "A model which took the path over from a result asks for the drafts as well.");
            Assert.That(AsksForDrafts(new MailConditions(new MailFilter(), null, MailFolderSpecialUse.SENT), FOLDERS), Is.False);
            Assert.That(AsksForDrafts(new MailConditions(new MailFilter(), "INBOX", null), FOLDERS), Is.False);
            Assert.That(AsksForDrafts(new MailConditions(new MailFilter { IsUnread = true }, null, null), FOLDERS), Is.False, "A search across all folders keeps to the interval.");
        });
    }

    [Test]
    public void AMailboxWithoutAFolderForDraftsMayGetOneWithTheNextSync() =>
        Assert.That(AsksForDrafts(new MailConditions(new MailFilter(), null, MailFolderSpecialUse.DRAFTS), [Folder("INBOX")]), Is.True, "Some servers create the folder only when the first draft is saved.");

    [Test]
    public async Task ARequestedSyncIsQueued()
    {
        this.ConfigureMailbox();
        using var service = this.CreateService();

        var requested = await service.RequestMailboxSyncAsync(WORK.Id);

        Assert.Multiple(() =>
        {
            Assert.That(requested, Is.True);
            Assert.That(service.GetStatuses().Single(status => status.DataSourceId == WORK.Id).State, Is.EqualTo(DataSourceEmbeddingState.QUEUED));
        });
    }

    [Test]
    public async Task WithoutAutomaticRefreshNoSyncIsQueued()
    {
        this.ConfigureMailbox();
        this.SettingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh = false;
        using var service = this.CreateService();

        var requested = await service.RequestMailboxSyncAsync(WORK.Id);

        Assert.Multiple(() =>
        {
            Assert.That(requested, Is.False, "Only the user starts a sync then.");
            Assert.That(service.GetStatuses(), Is.Empty);
        });
    }

    [Test]
    public async Task AMailboxWhichIsNotConfiguredIsNotSynced()
    {
        this.ConfigureMailbox();
        using var service = this.CreateService();

        Assert.That(await service.RequestMailboxSyncAsync("0e4b8d2a-6c1f-4a9e-b3d7-5f2c8e1a9b64"), Is.False);
    }

    private static bool AsksForDrafts(MailConditions conditions, IReadOnlyList<MailFolderRecord> folders) => MailDraftSync.AsksForDrafts(conditions, conditions.ForMailbox(folders), folders);

    private static MailboxCoverage Coverage(DateTimeOffset? lastCompleteSyncUtc, DateTimeOffset? signInRefusedAtUtc) => new(null, lastCompleteSyncUtc, signInRefusedAtUtc, null, FOLDERS);

    private static MailFolderRecord Folder(string path, MailFolderSpecialUse specialUse = MailFolderSpecialUse.NONE) => new(path, specialUse, 1, null, null, null, null, null);

    private void ConfigureMailbox()
    {
        this.SettingsManager.ConfigurationData.Mailboxes.Add(WORK);
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_RAG_2024);
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_MAILBOXES_2026);
    }

    // Queueing a run needs none of the services which carry it out:
    private DataSourceEmbeddingService CreateService() => new(this.SettingsManager, null!, null!, null!, NullLogger<DataSourceEmbeddingService>.Instance);
}