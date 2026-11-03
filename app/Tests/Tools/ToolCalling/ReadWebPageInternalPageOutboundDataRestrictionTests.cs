
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks where a chat may still send data once Read Web Page brought an internal page into it.
/// </summary>
/// <remarks>
/// A page in the intranet or behind a VPN is internal to the organization, and so is every page
/// of its wiki, whatever its address. A web search or an address the model writes itself would
/// carry it to a third party, so such a page restricts the chat as a mailbox does. Both kinds
/// count alike, and only the page as it was read in the end decides. A public page demands nothing.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ReadWebPageInternalPageOutboundDataRestrictionTests : ToolRegistryTestBase
{
    private const string INTRANET_HOST = "intranet.example.org";
    private const string INTRANET_PAGE = "https://intranet.example.org/handbook/travel";
    private const string PUBLIC_PAGE = "https://example.org/newsletter/2026-10";
    private const string WIKI_PAGE = "https://wiki.example.org/confluence/display/TEAM/Budget";

    private static readonly Uri WIKI = new("https://wiki.example.org/confluence/");

    [Test]
    public void APublicPageDemandsNothing()
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(PUBLIC_PAGE), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, WIKI);
        Assert.That(requirement, Is.EqualTo(OutboundDataRequirement.NONE));
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void AnIntranetPageRestrictsTheChatAndNamesItsHost(OutboundDataRestriction level)
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(INTRANET_PAGE), reachedNonPublicAddress: true, level, WIKI);
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(level, OutboundDataOrigin.INTERNAL_WEB_PAGE, INTRANET_HOST)));
    }

    [Test]
    public void APublicAddressWhichRedirectsIntoTheIntranetRestrictsTheChat()
    {
        //
        // The model asked for a public address, and the server sent it on to the intranet. What
        // the model reads came from there, which the retrieval reports for the page as read:
        //
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(INTRANET_PAGE), reachedNonPublicAddress: true, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, wiki: null);
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.INTERNAL_WEB_PAGE, INTRANET_HOST)));
    }

    [Test]
    public void AnIntranetAddressWhichRedirectsToAPublicPageDemandsNothing()
    {
        // A sign-in page outside the intranet, say. Its content is public:
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(PUBLIC_PAGE), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, wiki: null);
        Assert.That(requirement, Is.EqualTo(OutboundDataRequirement.NONE), "Only the page as it was read in the end decides.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AWikiPageCountsAsAnInternalPageWhateverItsAddress(bool reachedNonPublicAddress)
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri(WIKI_PAGE), reachedNonPublicAddress, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, WIKI);
        Assert.That(requirement, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.INTERNAL_WEB_PAGE, WIKI.Host)), "The wiki holds internal content whatever its address, and it is restricted like any other internal page.");
    }

    [Test]
    public void APageBesideTheWikiOnItsPublicHostDemandsNothing()
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri("https://wiki.example.org/blog/"), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, WIKI);
        Assert.That(requirement, Is.EqualTo(OutboundDataRequirement.NONE), "Only the path below the base URL is the wiki.");
    }

    [TestCase(null, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase("", OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(nameof(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase(nameof(OutboundDataRestriction.UNRESTRICTED), OutboundDataRestriction.UNRESTRICTED)]
    [TestCase("ONLY_LINKS", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase("2", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase("UNRESTRICTED, ONLY_LINKS_FROM_CHAT", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void TheLevelOfInternalPagesIsReadFromItsSetting(string? configuredValue, OutboundDataRestriction expected)
    {
        Assert.That(ReadWebPageTool.ReadInternalPageOutboundDataRestriction(configuredValue), Is.EqualTo(expected), "A value which names no level counts as the strictest, because a level guessed too loosely would let internal content out.");
    }

    [Test]
    public async Task AValueWhichNamesNoLevelIsReported()
    {
        var tool = new ReadWebPageTool(null!, null!, null!, NullLogger<ReadWebPageTool>.Instance);
        var state = await tool.ValidateConfigurationAsync(tool.GetDefinition(), new Dictionary<string, string> { ["internalPageOutboundDataRestriction"] = "ONLY_LINKS" });

        Assert.That(state?.IsConfigured, Is.False, "Guessing would decide on the organization's behalf where internal content may go.");
    }

    [Test]
    public async Task TheRetrievalReportsANonPublicAddressOfThePageAsRead()
    {
        //
        // The loopback address stands in for the intranet: it is the one non-public address a test
        // can serve from. A redirect between a public address and it needs a public server, which
        // a test cannot rely on; the service judges the last request the same way, whatever came
        // before, and this one follows a redirect on the way:
        //
        using var server = new LoopbackWebServer();
        var service = new WebPageRetrievalService(new HTMLParser());

        var retrievedPage = await service.RetrieveAsync(server.Address("/redirect"), new WebPageRetrievalOptions
        {
            TimeoutSeconds = 10,
            TargetChosenByUser = true,
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(retrievedPage.Page.FinalUrl.AbsolutePath, Is.EqualTo("/page"), "The redirect was followed.");
            Assert.That(retrievedPage.ReachedNonPublicAddress, Is.True);
        }
    }
}