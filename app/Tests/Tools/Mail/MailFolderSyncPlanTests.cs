using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks what a sync pass plans to do in one folder.
/// </summary>
[TestFixture]
public sealed class MailFolderSyncPlanTests
{
    private const long UID_VALIDITY = 1_700_000_000;

    private static readonly MailFlags UNREAD = new(IsSeen: false, IsFlagged: false, IsAnswered: false);
    private static readonly MailFlags READ = new(IsSeen: true, IsFlagged: false, IsAnswered: false);

    private static readonly IReadOnlyDictionary<long, MailFlags> STORED_LOCATIONS = new Dictionary<long, MailFlags>
    {
        [10] = READ,
        [11] = UNREAD,
        [12] = READ,
    };

    [Test]
    public void TheFirstPassFindsEverythingNewTheNewestFirst()
    {
        var plan = MailFolderSyncPlan.Create(null, new Dictionary<long, MailFlags>(), State(highestModSeq: 900), [3, 1, 2]);
        Assert.Multiple(() =>
        {
            Assert.That(plan.UidValidityChanged, Is.False);
            Assert.That(plan.NewUids, Is.EqualTo(new long[] { 3, 2, 1 }), "The newest mails are not indexed first.");
            Assert.That(plan.GoneUids, Is.Empty);
            Assert.That(plan.KeptUids, Is.Empty);
            Assert.That(plan.ChecksFlags, Is.False);
        });
    }

    [Test]
    public void TheDifferenceSaysWhatIsNewAndWhatIsGone()
    {
        var plan = MailFolderSyncPlan.Create(Stored(highestModSeq: null), STORED_LOCATIONS, State(highestModSeq: null), [11, 12, 13, 14]);
        Assert.Multiple(() =>
        {
            Assert.That(plan.NewUids, Is.EqualTo(new long[] { 14, 13 }));
            Assert.That(plan.GoneUids, Is.EqualTo(new long[] { 10 }), "A mail which no longer belongs into the index stays.");
            Assert.That(plan.KeptUids, Is.EqualTo(new long[] { 11, 12 }));
            Assert.That(plan.ChecksFlags, Is.True, "Without CONDSTORE only fetching every flag tells.");
            Assert.That(plan.FlagsChangedSinceModSeq, Is.Null);
        });
    }

    [Test]
    public void ChangeTrackingAsksOnlyAboutChangedMails()
    {
        var changed = MailFolderSyncPlan.Create(Stored(highestModSeq: 900), STORED_LOCATIONS, State(highestModSeq: 950), [10, 11, 12]);
        var unchanged = MailFolderSyncPlan.Create(Stored(highestModSeq: 900), STORED_LOCATIONS, State(highestModSeq: 900), [10, 11, 12]);
        Assert.Multiple(() =>
        {
            Assert.That(changed.ChecksFlags, Is.True);
            Assert.That(changed.FlagsChangedSinceModSeq, Is.EqualTo(900));
            Assert.That(unchanged.ChecksFlags, Is.False, "The flags are fetched although nothing changed.");
        });
    }

    [Test]
    public void APassWhichNeverCompletedChecksEveryFlag()
    {
        var plan = MailFolderSyncPlan.Create(Stored(highestModSeq: null), STORED_LOCATIONS, State(highestModSeq: 950), [10, 11, 12]);
        Assert.Multiple(() =>
        {
            Assert.That(plan.ChecksFlags, Is.True);
            Assert.That(plan.FlagsChangedSinceModSeq, Is.Null);
        });
    }

    [Test]
    public void ANewUidValidityVoidsEveryStoredUid()
    {
        var plan = MailFolderSyncPlan.Create(Stored(highestModSeq: 900), STORED_LOCATIONS, State(highestModSeq: 950, uidValidity: UID_VALIDITY + 1), [1, 2]);
        Assert.Multiple(() =>
        {
            Assert.That(plan.UidValidityChanged, Is.True);
            Assert.That(plan.NewUids, Is.EqualTo(new long[] { 2, 1 }), "The mails under their new UIDs are not looked at.");
            Assert.That(plan.GoneUids, Is.Empty, "The stored UIDs are void, not gone: their mails are linked anew by their key.");
            Assert.That(plan.KeptUids, Is.Empty, "A void UID is kept.");
            Assert.That(plan.ChecksFlags, Is.False);
        });
    }

    [Test]
    public void OnlyChangedFlagsOfStoredMailsAreTaken()
    {
        var fetched = new Dictionary<long, MailFlags>
        {
            [10] = READ,
            [11] = READ,
            [99] = UNREAD,
        };

        Assert.That(MailFolderSyncPlan.GetChangedFlags(STORED_LOCATIONS, fetched), Is.EqualTo(new Dictionary<long, MailFlags> { [11] = READ }));
    }

    private static MailFolderRecord Stored(long? highestModSeq) => new("INBOX", MailFolderSpecialUse.NONE, UID_VALIDITY, 13, highestModSeq, 3, 1, DateTimeOffset.UnixEpoch);

    private static MailFolderState State(long? highestModSeq, long uidValidity = UID_VALIDITY) => new(uidValidity, 15, highestModSeq, 4, 1);
}