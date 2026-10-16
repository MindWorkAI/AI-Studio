using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which provider confidence levels a mailbox may require, and which providers may read it.
/// </summary>
/// <remarks>
/// For the other data sources, NONE means that any provider may read them, and UNTRUSTED or UNKNOWN
/// let even a provider through which nobody rated. A mailbox gets none of that: it requires a level
/// from very low to high, and without one, no provider may read it, however the level got there.
/// </remarks>
[TestFixture]
public sealed class MailboxConfidenceTests
{
    private static readonly ConfidenceLevel[] MAILBOX_LEVELS = [ConfidenceLevel.VERY_LOW, ConfidenceLevel.LOW, ConfidenceLevel.MODERATE, ConfidenceLevel.MEDIUM, ConfidenceLevel.HIGH];

    private static readonly ConfidenceLevel[] FORBIDDEN_LEVELS = [ConfidenceLevel.NONE, ConfidenceLevel.UNTRUSTED, ConfidenceLevel.UNKNOWN];

    [Test]
    public void TheChoiceOffersTheLevelsFromVeryLowToHigh()
    {
        var offeredLevels = ConfigurationSelectDataFactory.GetMailboxConfidenceLevelsData().Select(option => option.Value);
        Assert.That(offeredLevels, Is.EqualTo(MAILBOX_LEVELS));
    }

    [Test]
    public void EveryLevelIsEitherAllowedOrForbidden()
    {
        //
        // A level added to ConfidenceLevel later has to be placed on purpose, rather than slip into
        // the mailboxes, or out of them, by its value.
        //
        Assert.That(Enum.GetValues<ConfidenceLevel>(), Is.EquivalentTo(MAILBOX_LEVELS.Concat(FORBIDDEN_LEVELS)));
    }

    [TestCaseSource(nameof(FORBIDDEN_LEVELS))]
    public void AMailboxWithAForbiddenLevelIsClosedToEveryProvider(ConfidenceLevel mailboxLevel)
    {
        var admittedLevels = Enum.GetValues<ConfidenceLevel>().Where(providerLevel => providerLevel.AllowsMailboxConfidenceLevel(mailboxLevel));
        Assert.That(admittedLevels, Is.Empty);
    }

    [TestCaseSource(nameof(MAILBOX_LEVELS))]
    public void AProviderHasToMeetTheLevelOfTheMailbox(ConfidenceLevel mailboxLevel)
    {
        var admittedLevels = Enum.GetValues<ConfidenceLevel>().Where(providerLevel => providerLevel.AllowsMailboxConfidenceLevel(mailboxLevel));
        Assert.That(admittedLevels, Is.EqualTo(MAILBOX_LEVELS.Where(level => level >= mailboxLevel)));
    }

    [TestCaseSource(nameof(FORBIDDEN_LEVELS))]
    public void NoEmbeddingProviderIndexesAMailboxWithAForbiddenLevel(ConfidenceLevel mailboxLevel)
    {
        var mailbox = new DataSourceMailbox { ConfidenceLevel = mailboxLevel };
        Assert.That(DataSourceEmbeddingService.AllowsEmbedding(mailbox, ConfidenceLevel.HIGH), Is.False, "The indexing run asked the rule of the other data sources, which lets every provider read a source without a level.");
    }

    [Test]
    public void AFolderWithoutALevelStaysOpenToEveryEmbeddingProvider()
    {
        var directory = new DataSourceLocalDirectory { ConfidenceLevel = ConfidenceLevel.NONE };
        Assert.That(DataSourceEmbeddingService.AllowsEmbedding(directory, ConfidenceLevel.UNTRUSTED), Is.True, "The stricter rule of the mailboxes reached the other data sources.");
    }

    [TestCaseSource(nameof(FORBIDDEN_LEVELS))]
    public void TheDialogRejectsAForbiddenLevel(ConfidenceLevel level)
    {
        Assert.That(new DataSourceValidation().ValidateMailboxConfidenceLevel(level), Is.Not.Null);
    }

    [TestCaseSource(nameof(MAILBOX_LEVELS))]
    public void TheDialogAcceptsAnAllowedLevel(ConfidenceLevel level)
    {
        Assert.That(new DataSourceValidation().ValidateMailboxConfidenceLevel(level), Is.Null);
    }
}