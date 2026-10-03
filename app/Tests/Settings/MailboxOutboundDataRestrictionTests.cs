using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how the least strict outbound data restriction an organization allows applies to the mailboxes.
/// </summary>
/// <remarks>
/// An organization may rule out the less strict levels for all mailboxes. A mailbox which still
/// holds one of them must not open a way out for its mails, and its dialog must not offer one again.
/// A mailbox which is stricter on its own must stay so.
/// </remarks>
[TestFixture]
public sealed class MailboxOutboundDataRestrictionTests
{
    private static readonly DataSourceMailbox OPEN = new()
    {
        Id = "5d2a8f1c-6e3b-4c7d-9a0e-1b4f2c8d6e39",
        Name = "Open",
        OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
    };

    private static readonly DataSourceMailbox CLOSED = new()
    {
        Id = "8b1e4c7a-2f5d-4a9b-b3c6-7d0e9f2a1c58",
        Name = "Closed",
        OutboundDataRestriction = OutboundDataRestriction.ONLY_CONFIGURED_SERVICES,
    };

    [Test]
    public void WithoutAnOrganizationTheUserDecides() => Assert.That(new DataMailboxes().MinimumOutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.UNRESTRICTED));

    [Test]
    public void TheMinimumTightensALooserMailbox()
    {
        var (_, outboundData) = MailToolResults.GetRequirements([OPEN], OutboundDataRestriction.ONLY_LINKS_FROM_CHAT);

        Assert.That(outboundData, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OPEN.Id)), "The chat names the mailbox whose mails it read, even though the level comes from the organization.");
    }

    [Test]
    public void TheMinimumNeverLoosensAStricterMailbox()
    {
        var (_, outboundData) = MailToolResults.GetRequirements([CLOSED], OutboundDataRestriction.ONLY_LINKS_FROM_CHAT);

        Assert.That(outboundData.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));
    }

    [Test]
    public void TheDialogOffersOnlyTheLevelsAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetOfferedLevels(OutboundDataRestriction.UNRESTRICTED), Is.EqualTo(new[] { OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataRestriction.UNRESTRICTED }));
            Assert.That(GetOfferedLevels(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT), Is.EqualTo(new[] { OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT }));
            Assert.That(GetOfferedLevels(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), Is.EqualTo(new[] { OutboundDataRestriction.ONLY_CONFIGURED_SERVICES }));
        });
    }

    private static IEnumerable<OutboundDataRestriction> GetOfferedLevels(OutboundDataRestriction minimumRestriction) => ConfigurationSelectDataFactory.GetOutboundDataRestrictionData(minimumRestriction).Select(option => option.Value);
}