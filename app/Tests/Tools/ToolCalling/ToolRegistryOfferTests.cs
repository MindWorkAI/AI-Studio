using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that asking whether a tool can be offered gets the same answer as preparing a request.
/// </summary>
/// <remarks>
/// The RAG process leaves the searching of the data sources to Semantic Search only when the
/// registry says the tool can be offered. If the question and the preparation of the request ever
/// disagreed, a chat would end up searching nothing at all: the RAG process would stand back, and
/// the request would offer no tool either. Each reason is therefore checked against both.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolRegistryOfferTests : ToolRegistryTestBase
{
    [Test]
    public async Task NothingInTheWay()
    {
        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition())), ToolCapableProvider(), ToolOfferBlockReason.NONE, "A tool-capable, highly trusted provider and a tool which needs nothing.");
    }

    [Test]
    public async Task ToolsSwitchedOffAltogether()
    {
        this.SettingsManager.ConfigurationData.Tools.EnableTools = false;

        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition())), ToolCapableProvider(), ToolOfferBlockReason.TOOLS_SWITCHED_OFF, "The organization turned all tools off.");
    }

    [Test]
    public async Task AModelWithoutTools()
    {
        var provider = ToolCapableProvider() with { CapabilityOverrides = new() { FunctionCalling = false } };

        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition())), provider, ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS, "The person said their model cannot call functions.");
    }

    [Test]
    public async Task NoProviderSelected()
    {
        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition())), AIStudio.Settings.Provider.NONE, ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS, "Without a provider there is no model that could call a tool.");
    }

    [Test]
    public async Task AToolNotMeantForTheChat()
    {
        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition(visibleInChat: false))), ToolCapableProvider(), ToolOfferBlockReason.NOT_AVAILABLE_HERE, "The tool belongs to the assistants only.");
    }

    [Test]
    public async Task AToolNobodyKnows()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()));

        Assert.That(await registry.GetOfferBlockReasonAsync("unknown_tool", ToolCapableProvider(), AIStudio.Tools.Components.CHAT), Is.EqualTo(ToolOfferBlockReason.NOT_AVAILABLE_HERE));
    }

    [Test]
    public async Task AToolSwitchedOffByTheOrganization()
    {
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(TOOL_ID);

        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition())), ToolCapableProvider(), ToolOfferBlockReason.TOOL_SWITCHED_OFF, "The organization turned this one tool off.");
    }

    [Test]
    public async Task AToolMissingASetting()
    {
        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition(requiresSetting: true))), ToolCapableProvider(), ToolOfferBlockReason.NOT_CONFIGURED, "The tool cannot work without a setting nobody filled in.");
    }

    [Test]
    public async Task AProviderTrustedTooLittle()
    {
        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition(minimumConfidence: ConfidenceLevel.HIGH))), LessTrustedProvider(), ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW, "The tool asks for high confidence, the provider has a moderate one.");
    }

    [Test]
    public async Task ARaisedRequirementCountsAsWell()
    {
        this.SettingsManager.SetMinimumProviderConfidenceForTool(TOOL_ID, ConfidenceLevel.HIGH, ConfidenceLevel.NONE);

        await this.AssertBothAgree(this.CreateRegistry(new TestTool(Definition())), LessTrustedProvider(), ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW, "The tool asks for nothing itself, but its requirement was raised in the settings.");
    }

    private async Task AssertBothAgree(ToolRegistry registry, AIStudio.Settings.Provider provider, ToolOfferBlockReason expected, string situation)
    {
        var reason = await registry.GetOfferBlockReasonAsync(TOOL_ID, provider, AIStudio.Tools.Components.CHAT);
        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(provider), [TOOL_ID], mayRunTools: true);

        Assert.Multiple(() =>
        {
            Assert.That(reason, Is.EqualTo(expected), situation);
            Assert.That(runnableTools.Any(x => x.Definition.Id == TOOL_ID), Is.EqualTo(expected is ToolOfferBlockReason.NONE), "Preparing the request has to come to the same answer as asking beforehand.");
        });
    }
}