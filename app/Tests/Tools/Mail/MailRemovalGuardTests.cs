using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks when a sync has to ask before it removes mails from the index.
/// </summary>
[TestFixture]
public sealed class MailRemovalGuardTests
{
    private static readonly MailboxSyncState NOTHING_PENDING = new(null, null, null);

    [TestCase(10_000, 99, ExpectedResult = false)]
    [TestCase(400, 99, ExpectedResult = false)]
    [TestCase(10_000, 1_999, ExpectedResult = false)]
    [TestCase(10_000, 2_000, ExpectedResult = true)]
    [TestCase(400, 100, ExpectedResult = true)]
    [TestCase(100, 100, ExpectedResult = true)]
    public bool OnlyAManyAndALargeShareAreAskedAbout(int indexedCount, int removalCount) => MailRemovalGuard.IsMassRemoval(indexedCount, removalCount);

    [Test]
    public void AMassRemovalIsHeldBackUntilExactlyItWasApproved()
    {
        var approvedNow = DateTimeOffset.UtcNow;
        Assert.Multiple(() =>
        {
            Assert.That(MailRemovalGuard.Decide(1_000, 500, NOTHING_PENDING), Is.EqualTo(MailRemovalDecision.HOLD_BACK));
            Assert.That(MailRemovalGuard.Decide(1_000, 500, new(null, 500, null)), Is.EqualTo(MailRemovalDecision.HOLD_BACK), "A removal nobody approved went ahead.");
            Assert.That(MailRemovalGuard.Decide(1_000, 500, new(null, 500, approvedNow)), Is.EqualTo(MailRemovalDecision.PROCEED));
            Assert.That(MailRemovalGuard.Decide(1_000, 600, new(null, 500, approvedNow)), Is.EqualTo(MailRemovalDecision.HOLD_BACK), "The approval counted for another number.");
            Assert.That(MailRemovalGuard.Decide(1_000, 10, NOTHING_PENDING), Is.EqualTo(MailRemovalDecision.PROCEED));
        });
    }
}