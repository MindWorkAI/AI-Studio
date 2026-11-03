using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the allowed private hosts count as services configured in AI Studio once a chat
/// may no longer send data to third parties.
/// </summary>
/// <remarks>
/// Without this, a chat which read one intranet page could open a second one only if its address
/// already stood in the chat. A host pattern such as *.example.org matches public hosts as well,
/// though, and an intranet server may redirect anywhere. An address the model chose on such a
/// host may therefore only reach non-public addresses, on every request, redirects included. This
/// holds for every source of the restriction, a mailbox included.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ReadWebPageAllowedPrivateHostTests : ToolRegistryTestBase
{
    private const string ALLOWED_PRIVATE_HOSTS = "intranet.example.org, *.corp.example.org";
    private const string WIKI = "https://wiki.corp.example.org/confluence/";
    private const string GIVEN = "https://intranet.example.org/newsletter/2026-10";

    // A public address written as an IP address, so the check needs no name resolution, and the
    // request is refused before anything is sent:
    private const string PUBLIC_ADDRESS = "http://93.184.215.14/";

    private static readonly Uri WIKI_URL = new(WIKI);

    private static readonly Func<string, bool> IS_ALLOWED_PRIVATE_HOST = ReadWebPageTool.ReadAllowedPrivateHosts(ALLOWED_PRIVATE_HOSTS, out _);

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, "https://intranet.example.org/handbook/travel")]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, "https://intranet.example.org/handbook/travel")]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, "https://projects.corp.example.org/apollo")]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, "https://projects.corp.example.org/apollo")]
    public void AnAddressTheModelChoseOnAnAllowedPrivateHostIsReadUnderEveryRestriction(OutboundDataRestriction restriction, string address)
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(address), restriction, ChatWithTheUserWriting(GIVEN), WIKI_URL, IS_ALLOWED_PRIVATE_HOST, out var redirectLimit);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allowed, Is.True, "The allowed private hosts are services configured in AI Studio, as the wiki is.");
            Assert.That(redirectLimit, Is.EqualTo(ReadWebPageTool.RedirectLimit.NON_PUBLIC_ONLY), "A matching name is not enough: the request may only reach non-public addresses.");
        }
    }

    [Test]
    public void AChatWithoutRestrictionIsNotLimited()
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri("https://intranet.example.org/handbook/travel"), OutboundDataRestriction.UNRESTRICTED, new ChatThread(), WIKI_URL, IS_ALLOWED_PRIVATE_HOST, out var redirectLimit);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allowed, Is.True);
            Assert.That(redirectLimit, Is.EqualTo(ReadWebPageTool.RedirectLimit.NONE));
        }
    }

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, "https://example.org/chosen-by-the-model")]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, "https://example.org/chosen-by-the-model")]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, "https://corp.example.org/")]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, "https://intranet.example.org.attacker.example/")]
    public void AnAddressOnAnyOtherHostIsStillRefused(OutboundDataRestriction restriction, string address)
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(address), restriction, ChatWithTheUserWriting(GIVEN), WIKI_URL, IS_ALLOWED_PRIVATE_HOST, out _);
        Assert.That(allowed, Is.False, "A wildcard pattern matches the hosts below its domain only, not the domain itself or a host which merely starts with it.");
    }

    [Test]
    public void AWikiOnAnAllowedPrivateHostStaysConfinedToTheWiki()
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri("https://wiki.corp.example.org/confluence/display/TEAM/Budget"), OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, new ChatThread(), WIKI_URL, IS_ALLOWED_PRIVATE_HOST, out var redirectLimit);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allowed, Is.True);
            Assert.That(redirectLimit, Is.EqualTo(ReadWebPageTool.RedirectLimit.WITHIN_WIKI), "The stricter of both limits: a redirect out of the wiki is refused even when it stays in the intranet.");
        }
    }

    [Test]
    public void ALinkFromTheChatOnAnAllowedPrivateHostMayBeRedirectedAnywhere()
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri(GIVEN), OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, ChatWithTheUserWriting(GIVEN), WIKI_URL, IS_ALLOWED_PRIVATE_HOST, out var redirectLimit);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allowed, Is.True);
            Assert.That(redirectLimit, Is.EqualTo(ReadWebPageTool.RedirectLimit.NONE), "The model did not write the address, so whatever a redirect carries came from the server.");
        }
    }

    [Test]
    public void AChatRestrictedByAMailboxNowReadsAllowedPrivateHosts()
    {
        //
        // A deliberate loosening: mail content may now reach the allowed intranet hosts through
        // an address the model chose, as it already could reach the wiki:
        //
        var chat = new ChatThread();
        chat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.MAILBOX, "4b8e2c6a-1d3f-4a7e-9c5b-2e8d1f6a3c7b"));

        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri("https://intranet.example.org/handbook/travel"), chat.RequiredOutboundDataRestriction.Restriction, chat, wiki: null, IS_ALLOWED_PRIVATE_HOST, out _);

        Assert.That(allowed, Is.True);
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("intranet.example.org", true)]
    [TestCase("https://intranet.example.org/", false)]
    public void AnAllowlistWhichCannotBeReadAllowsNoHost(string? rawValue, bool expectedHasAny)
    {
        var isAllowedPrivateHost = ReadWebPageTool.ReadAllowedPrivateHosts(rawValue, out var hasAny);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hasAny, Is.EqualTo(expectedHasAny));
            Assert.That(isAllowedPrivateHost("intranet.example.org"), Is.EqualTo(expectedHasAny));
        }
    }

    [Test]
    public async Task WithAllowedPrivateHostsTheToolIsOfferedForConfiguredServicesOnly()
    {
        this.SettingsManager.ConfigurationData.Tools.Settings[ToolSelectionRules.READ_WEB_PAGE_TOOL_ID] = new() { ["allowedPrivateHosts"] = ALLOWED_PRIVATE_HOSTS };
        var tool = new ReadWebPageTool(null!, null!, this.CreateToolSettingsService(), NullLogger<ReadWebPageTool>.Instance);
        var chat = new ChatThread();
        chat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.INTERNAL_WEB_PAGE, "intranet.example.org"));
        var provider = ToolCapableProvider();

        var function = await tool.ResolveFunctionAsync(tool.GetDefinition(), new()
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = chat,
        });

        Assert.That(function, Is.Not.Null, "Without a wiki, the allowed private hosts are what the chat can still read.");
    }

    [Test]
    public async Task AnAllowedNameWhichResolvesToAPublicAddressIsRefused()
    {
        var service = new WebPageRetrievalService(new HTMLParser());

        var exception = await Assert.ThrowsAsync<WebPageAccessBlockedException>(() => service.RetrieveAsync(new Uri(PUBLIC_ADDRESS), new WebPageRetrievalOptions
        {
            TimeoutSeconds = 10,
            ProviderConfidence = ConfidenceLevel.HIGH,
            IsPrivateHostAllowed = _ => true,
            NonPublicTargetsOnly = true,
        }));

        Assert.That(exception!.Reason, Is.EqualTo(WebPageAccessBlockReason.PUBLIC_TARGET_NOT_ALLOWED));
    }

    [Test]
    public async Task ARedirectFromAnAllowedPrivateHostToAPublicAddressIsRefused()
    {
        using var server = new LoopbackWebServer(redirectLocation: PUBLIC_ADDRESS);
        var service = new WebPageRetrievalService(new HTMLParser());

        //
        // The loopback address stands in for the intranet. It is reachable for an address the user
        // chose only, which the limit holds for all the same, because it guards what the address
        // may carry:
        //
        var exception = await Assert.ThrowsAsync<WebPageAccessBlockedException>(() => service.RetrieveAsync(server.Address("/redirect"), new WebPageRetrievalOptions
        {
            TimeoutSeconds = 10,
            TargetChosenByUser = true,
            NonPublicTargetsOnly = true,
        }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Reason, Is.EqualTo(WebPageAccessBlockReason.PUBLIC_TARGET_NOT_ALLOWED));
            Assert.That(exception.Message, Does.Not.Contain("93.184.215.14"), "The refusal reaches the model and must not hand it an address.");
        }
    }

    [Test]
    public async Task APageWhichStaysOnNonPublicAddressesIsRead()
    {
        using var server = new LoopbackWebServer();
        var service = new WebPageRetrievalService(new HTMLParser());

        var retrievedPage = await service.RetrieveAsync(server.Address("/redirect"), new WebPageRetrievalOptions
        {
            TimeoutSeconds = 10,
            TargetChosenByUser = true,
            NonPublicTargetsOnly = true,
        });

        Assert.That(retrievedPage.Page.FinalUrl.AbsolutePath, Is.EqualTo("/page"));
    }

    private static ChatThread ChatWithTheUserWriting(string address) => new()
    {
        Blocks =
        [
            new ContentBlock
            {
                Time = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero),
                ContentType = ContentType.TEXT,
                Content = new ContentText { Text = $"What does {address} say about the budget?" },
                Role = ChatRole.USER,
            },
        ],
    };
}