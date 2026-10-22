using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that Read Web Page treats the intranet configured for Search Intranet like the wiki.
/// </summary>
/// <remarks>
/// The search finds pages and documents, and Read Web Page opens them. Both count as internal,
/// whatever the address of the intranet, and a chat restricted by internal content may still open
/// them, as long as an address the model chose stays within the intranet.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ReadWebPageIntranetTests : ToolRegistryTestBase
{
    private const string INTRANET = "https://intranet.example.org/";

    private static readonly Uri INTRANET_URL = new(INTRANET);
    private static readonly Func<string, bool> NO_ALLOWED_PRIVATE_HOST = _ => false;

    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void AnIntranetPageTheModelChoseIsReadUnderEveryRestriction(OutboundDataRestriction restriction)
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri("https://intranet.example.org/sites/hr/Documents/form.pdf"), restriction, new ChatThread(), wiki: null, NO_ALLOWED_PRIVATE_HOST, out var redirectLimit, INTRANET_URL);

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.True, "The intranet is a service configured in AI Studio, as the wiki is.");
            Assert.That(redirectLimit, Is.EqualTo(ReadWebPageTool.RedirectLimit.WITHIN_INTRANET), "An address the model chose must stay within the intranet, redirects included.");
        });
    }

    [Test]
    public void AnotherHostIsStillRefused()
    {
        var allowed = ReadWebPageTool.IsAllowedByOutboundDataRestriction(new Uri("https://intranet.example.org.attacker.example/"), OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, new ChatThread(), wiki: null, NO_ALLOWED_PRIVATE_HOST, out _, INTRANET_URL);
        Assert.That(allowed, Is.False);
    }

    [Test]
    public void AnIntranetPageIsInternalWhateverItsAddress()
    {
        var requirement = ReadWebPageTool.GetOutboundDataRequirement(new Uri("https://intranet.example.org/news"), reachedNonPublicAddress: false, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, wiki: null, INTRANET_URL);

        Assert.Multiple(() =>
        {
            Assert.That(requirement.Origin, Is.EqualTo(OutboundDataOrigin.INTERNAL_WEB_PAGE));
            Assert.That(ReadWebPageTool.IsInternalAddress(new Uri("https://intranet.example.org/files/form.pdf"), false, null, INTRANET_URL), Is.True, "Its documents are internal, so the default document access reads them.");
        });
    }

    [Test]
    public void TheInstructionsNameTheIntranet()
    {
        var instructions = ReadWebPageTool.BuildSystemPromptInstructions(FreeAddressChoice.OFF, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, wiki: null, intranet: INTRANET_URL);

        Assert.Multiple(() =>
        {
            Assert.That(instructions, Does.Contain($"pages of the intranet at {INTRANET} whose URL appears word for word in this conversation, such as the hits of an intranet search"));
            Assert.That(instructions, Does.Contain("only when they come from the intranet configured in AI Studio"));
        });
    }

    [Test]
    public void TheIntranetCanEndTheWebSearch()
    {
        Assert.That(ReadWebPageTool.InternalPageEndsWebSearch(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, true, wiki: null, allowedPrivateHosts: null, INTRANET_URL), Is.True);
    }

    [Test]
    public async Task WithAnIntranetTheToolIsOfferedForConfiguredServicesOnly()
    {
        this.SettingsManager.ConfigurationData.Tools.Settings[ToolSelectionRules.SEARCH_INTRANET_TOOL_ID] = new() { ["baseUrl"] = INTRANET };
        var tool = new ReadWebPageTool(null!, null!, this.CreateToolSettingsService(), NullLogger<ReadWebPageTool>.Instance);
        var chat = new ChatThread();
        chat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.INTRANET, string.Empty));
        var provider = ToolCapableProvider();

        var function = await tool.ResolveFunctionAsync(tool.GetDefinition(), new()
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = chat,
        });

        Assert.That(function, Is.Not.Null, "Without a wiki and allowed hosts, the intranet is what the chat can still read.");
    }
}