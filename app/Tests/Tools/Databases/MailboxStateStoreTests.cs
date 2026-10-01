using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// Checks what the index keeps about a mailbox as a whole: its sync and a refused sign-in.
/// </summary>
/// <remarks>
/// Both protect the user from something which cannot be taken back. A removal from the index the
/// user did not agree to costs them a new embedding of every mail which comes back, and a sign-in
/// tried again with a refused password may lock their directory account.
/// </remarks>
[TestFixture]
public sealed class MailboxStateStoreTests
{
    private const string DATA_SOURCE_ID = "0c3f9b52-7d4e-4a1b-9e6f-2b8c5d7a1e40";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    private TemporaryIndexStore store = null!;

    private IndexStoreClient Client => this.store.Client;

    [SetUp]
    public async Task CreateMailboxAsync()
    {
        this.store = await TemporaryIndexStore.CreateAsync();
        await this.Client.UpsertDataSourceAsync(DATA_SOURCE_ID, "MAILBOX", "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "signature", string.Empty, 3, TOKEN);
    }

    [TearDown]
    public async Task DeleteMailboxAsync() => await this.store.DisposeAsync();

    [Test]
    public async Task AMailboxWhichWasNeverSyncedKnowsNothingYet()
    {
        Assert.That(await this.Client.GetMailboxSyncStateAsync(DATA_SOURCE_ID, TOKEN), Is.EqualTo(new MailboxSyncState(null, null, null)));
    }

    [Test]
    public async Task ACompleteSyncEndsAHeldBackRemoval()
    {
        var completed = new DateTimeOffset(2026, 10, 1, 9, 16, 0, TimeSpan.Zero);
        await this.Client.HoldBackMailRemovalAsync(DATA_SOURCE_ID, 500, TOKEN);
        await this.Client.ApprovePendingMailRemovalAsync(DATA_SOURCE_ID, 500, TOKEN);

        await this.Client.CompleteMailboxSyncAsync(DATA_SOURCE_ID, completed, TOKEN);

        Assert.That(
            await this.Client.GetMailboxSyncStateAsync(DATA_SOURCE_ID, TOKEN),
            Is.EqualTo(new MailboxSyncState(completed, null, null)),
            "The approved removal took place in that sync, so the next large one has to be asked for again.");
    }

    [Test]
    public async Task AnApprovalCoversOnlyTheCountTheUserWasShown()
    {
        await this.Client.HoldBackMailRemovalAsync(DATA_SOURCE_ID, 500, TOKEN);

        var approvedAnotherCount = await this.Client.ApprovePendingMailRemovalAsync(DATA_SOURCE_ID, 400, TOKEN);
        var stateAfterAnotherCount = await this.Client.GetMailboxSyncStateAsync(DATA_SOURCE_ID, TOKEN);

        var approvedTheCount = await this.Client.ApprovePendingMailRemovalAsync(DATA_SOURCE_ID, 500, TOKEN);
        await this.Client.HoldBackMailRemovalAsync(DATA_SOURCE_ID, 500, TOKEN);
        var stateAfterTheSameCountAgain = await this.Client.GetMailboxSyncStateAsync(DATA_SOURCE_ID, TOKEN);

        await this.Client.HoldBackMailRemovalAsync(DATA_SOURCE_ID, 800, TOKEN);
        var stateAfterMoreMails = await this.Client.GetMailboxSyncStateAsync(DATA_SOURCE_ID, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(approvedAnotherCount, Is.False, "A sync changed the count while the user looked at the old one.");
            Assert.That(stateAfterAnotherCount.PendingRemovalApprovedUtc, Is.Null);
            Assert.That(approvedTheCount, Is.True);
            Assert.That(stateAfterTheSameCountAgain.PendingRemovalApprovedUtc, Is.Not.Null, "The next sync finds the same count and may go ahead.");
            Assert.That(stateAfterMoreMails, Is.EqualTo(new MailboxSyncState(null, 800, null)), "The user agreed to 500 mails, not to whatever comes.");
        });
    }

    [Test]
    public async Task NothingCanBeApprovedWhileNothingIsHeldBack()
    {
        var approved = await this.Client.ApprovePendingMailRemovalAsync(DATA_SOURCE_ID, 500, TOKEN);
        var state = await this.Client.GetMailboxSyncStateAsync(DATA_SOURCE_ID, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(approved, Is.False);
            Assert.That(state, Is.EqualTo(new MailboxSyncState(null, null, null)));
        });
    }

    [Test]
    public void ARemovalOfNoMailIsNothingToHoldBack()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => this.Client.HoldBackMailRemovalAsync(DATA_SOURCE_ID, 0, TOKEN));
    }

    [Test]
    public async Task ARefusedSignInStaysUntilItIsCleared()
    {
        var first = new MailboxAuthFailure(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), "AUTHENTICATIONFAILED Invalid credentials");
        var second = new MailboxAuthFailure(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero), string.Empty);

        await this.Client.UpsertMailboxAuthFailureAsync(DATA_SOURCE_ID, first, TOKEN);
        var afterFirst = await this.Client.GetMailboxAuthFailureAsync(DATA_SOURCE_ID, TOKEN);

        await this.Client.UpsertMailboxAuthFailureAsync(DATA_SOURCE_ID, second, TOKEN);
        var afterSecond = await this.Client.GetMailboxAuthFailureAsync(DATA_SOURCE_ID, TOKEN);

        await this.Client.ClearMailboxAuthFailureAsync(DATA_SOURCE_ID, TOKEN);
        var afterClearing = await this.Client.GetMailboxAuthFailureAsync(DATA_SOURCE_ID, TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(afterFirst, Is.EqualTo(first));
            Assert.That(afterSecond, Is.EqualTo(second), "The one try the user asked for failed again.");
            Assert.That(afterClearing, Is.Null);
        });
    }

    [Test]
    public async Task ASignInCanBeRefusedBeforeAnythingWasIndexed()
    {
        const string NEVER_INDEXED = "5a1e9c37-2b4d-4e8f-a6c0-3d7b9e1f2a48";
        var failure = new MailboxAuthFailure(DateTimeOffset.UtcNow, "AUTHENTICATIONFAILED Invalid credentials");

        await this.Client.UpsertMailboxAuthFailureAsync(NEVER_INDEXED, failure, TOKEN);

        Assert.That(await this.Client.GetMailboxAuthFailureAsync(NEVER_INDEXED, TOKEN), Is.EqualTo(failure), "The very first sign-in of a mailbox can fail, long before the index holds anything of it.");
    }
}