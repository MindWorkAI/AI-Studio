using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how a tool tailors what it offers to a single request.
/// </summary>
/// <remarks>
/// A tool may describe itself differently per request, as Semantic Search does with the data
/// sources of a chat. What it must never do on the way is become another tool, or decide whether it
/// is allowed: the name is what the model's calls are matched by, and the checks ran before it was
/// asked. A tool which fails to answer must cost the request that tool, not the whole request.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolRegistryResolutionTests : ToolRegistryTestBase
{
    private const string OTHER_TOOL_ID = "other_tool";

    [Test]
    public async Task ATailoredFunctionReachesTheRequest()
    {
        var parameters = ToolParameterSchemaBuilder.Create().RequiredEnum("choice", "What to pick.", "a", "b").Build();
        var tool = new TestTool(Definition(), registered => registered.Function with { DescriptionForLLM = "Tailored.", Parameters = parameters });

        var offered = await this.GetOfferedDefinition(tool);

        Assert.Multiple(() =>
        {
            Assert.That(offered?.Function.DescriptionForLLM, Is.EqualTo("Tailored."));
            Assert.That(offered?.Function.Parameters.GetRawText(), Is.EqualTo(parameters.GetRawText()));
            Assert.That(offered?.Id, Is.EqualTo(TOOL_ID), "Tailoring the function leaves the rest of the definition as registered.");
        });
    }

    [Test]
    public async Task NameAndStrictModeStayAsRegistered()
    {
        var tool = new TestTool(Definition(), registered => registered.Function with { Name = "another_name", Strict = false, DescriptionForLLM = "Tailored." });

        var offered = await this.GetOfferedDefinition(tool);

        Assert.Multiple(() =>
        {
            Assert.That(offered?.Function.Name, Is.EqualTo(TOOL_ID), "The model's calls find their tool by this name. Another one would reach nobody.");
            Assert.That(offered?.Function.Strict, Is.True, "Whether a tool can go strict is part of what was registered.");
            Assert.That(offered?.Function.DescriptionForLLM, Is.EqualTo("Tailored."), "What a tool may change still arrives.");
        });
    }

    [Test]
    public async Task AnUntailoredToolKeepsItsRegisteredDefinition()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools.Single().Definition, Is.SameAs(registry.GetDefinition(TOOL_ID)), "Most tools offer what they registered, and nothing needs to be copied for them.");
    }

    [Test]
    public async Task NothingToOfferLeavesTheToolOut()
    {
        var registry = this.CreateRegistry(new TestTool(Definition(), _ => null));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [TOOL_ID], mayRunTools: true);
        var reason = await registry.GetOfferBlockReasonAsync(TOOL_ID, ToolCapableProvider(), AIStudio.Tools.Components.CHAT);

        Assert.Multiple(() =>
        {
            Assert.That(runnableTools, Is.Empty, "A model should not learn about a tool which can only come back empty.");
            Assert.That(reason, Is.EqualTo(ToolOfferBlockReason.NONE), "Asking beforehand only covers the checks. Whether a tool has anything to offer depends on the chat and is left to the request.");
        });
    }

    [Test]
    public async Task ParametersWhichAreNoSchemaAreNotOffered()
    {
        var registry = this.CreateRegistry(new TestTool(Definition(), registered => registered.Function with { DescriptionForLLM = "Tailored.", Parameters = JsonSerializer.Deserialize<JsonElement>("[]") }));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools.Single().Definition, Is.SameAs(registry.GetDefinition(TOOL_ID)), "The registered definition passed validation; what came back instead did not.");
    }

    [Test]
    public async Task AFailingToolCostsOnlyItself()
    {
        var failing = new TestTool(Definition(), _ => throw new InvalidOperationException("The data sources could not be read."));
        var working = new TestTool(Definition(OTHER_TOOL_ID));
        var registry = this.CreateRegistry(failing, working);

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [TOOL_ID, OTHER_TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools.Select(x => x.Definition.Id), Is.EquivalentTo(new[] { OTHER_TOOL_ID }));
    }

    [Test]
    public async Task AContextToolRunsWithoutBeingSelected()
    {
        var registry = this.CreateRegistry(new TestTool(Definition(activation: ToolActivation.CONTEXT)), new TestTool(Definition(OTHER_TOOL_ID)));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [], mayRunTools: true);

        Assert.That(runnableTools.Select(x => x.Definition.Id), Is.EquivalentTo(new[] { TOOL_ID }), "The tool offering itself from the chat is a candidate without a selection; the other one waits to be selected.");
    }

    [Test]
    public async Task AToolIsOnlyAskedOnceItsChecksPassed()
    {
        var tool = new TestTool(Definition(minimumConfidence: ConfidenceLevel.HIGH));
        var registry = this.CreateRegistry(tool);

        await registry.GetRunnableToolsAsync(this.ContextFor(LessTrustedProvider()), [TOOL_ID], mayRunTools: true);

        Assert.That(tool.ResolveCount, Is.Zero, "A tool tailoring itself for a provider it is not allowed with would already be working for a request it cannot join.");
    }

    private async Task<ToolDefinition?> GetOfferedDefinition(TestTool tool)
    {
        var runnableTools = await this.CreateRegistry(tool).GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [TOOL_ID], mayRunTools: true);
        return runnableTools.SingleOrDefault().Definition;
    }
}