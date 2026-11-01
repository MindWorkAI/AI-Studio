using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how a thread takes over the restrictions of another thread whose result it shows.
/// </summary>
[TestFixture]
public sealed class ChatThreadRestrictionsOfTests
{
    private const string DATA_SOURCE_ID = "3e8b1d6f-9a2c-4f7e-b5d1-6c4a9e2f8b17";
    private const string WIKI_HOST = "wiki.example.org";

    [Test]
    public void AllThreeRestrictionsComeAlong()
    {
        var researchThread = new ChatThread();
        researchThread.RequireProviderConfidence(ConfidenceLevel.HIGH);
        researchThread.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);
        researchThread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, DATA_SOURCE_ID));
        var assistantThread = new ChatThread();

        assistantThread.RequireRestrictionsOf(researchThread);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(assistantThread.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));
            Assert.That(assistantThread.DataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED));
            Assert.That(assistantThread.RequiredOutboundDataRestriction.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));
            Assert.That(assistantThread.RequiredOutboundDataRestriction.Reference, Is.EqualTo(DATA_SOURCE_ID));
        }
    }

    [Test]
    public void TheAddressesItsModelsWroteComeAlong()
    {
        // The moderation of the SWOT analysis carries what the models of the four categories wrote:
        var researchThread = new ChatThread();
        researchThread.RequireRestrictions(ChatRestrictions.NONE.WithWebAddressesWrittenByModelIn("A competitor is on https://example.org/competitor."));
        var assistantThread = new ChatThread();

        assistantThread.RequireRestrictionsOf(researchThread);

        Assert.That(assistantThread.WebAddressesWrittenByModels, Is.EquivalentTo(researchThread.WebAddressesWrittenByModels));
    }

    [Test]
    public void ALooserThreadLoosensNothing()
    {
        var assistantThread = new ChatThread();
        assistantThread.RequireProviderConfidence(ConfidenceLevel.HIGH);
        assistantThread.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);
        assistantThread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.CONFLUENCE_WIKI, WIKI_HOST));
        var researchThread = new ChatThread();
        researchThread.RequireProviderConfidence(ConfidenceLevel.LOW);
        researchThread.RequireDataSecurity(DataSourceSecurity.ALLOW_ANY);
        researchThread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, DATA_SOURCE_ID));

        assistantThread.RequireRestrictionsOf(researchThread);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(assistantThread.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));
            Assert.That(assistantThread.DataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED));
            Assert.That(assistantThread.RequiredOutboundDataRestriction.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));
        }
    }

    [Test]
    public void AThreadWhichReadNothingDemandsNothing()
    {
        var assistantThread = new ChatThread();

        assistantThread.RequireRestrictionsOf(new ChatThread());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(assistantThread.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.NONE));
            Assert.That(assistantThread.DataSecurity, Is.EqualTo(DataSourceSecurity.NOT_SPECIFIED));
            Assert.That(assistantThread.RequiredOutboundDataRestriction, Is.EqualTo(OutboundDataRequirement.NONE));
            Assert.That(assistantThread.WebAddressesWrittenByModels, Is.Empty);
        }
    }
}