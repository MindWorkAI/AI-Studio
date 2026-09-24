using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the registry takes the definition of Semantic Search the way it is meant.
/// </summary>
/// <remarks>
/// The registry drops a definition it cannot accept with no more than a warning in the log. For
/// Semantic Search, that would quietly bring back the classic RAG process in every chat. The tool
/// is offered without anybody selecting it, and only where there are data sources to search: in a
/// chat, never in an assistant.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class SemanticSearchToolDefinitionTests : ToolRegistryTestBase
{
    [Test]
    public async Task AChatIsOfferedTheToolWithoutSelectingIt()
    {
        var registry = this.CreateRegistry(new TestTool(Tool().GetDefinition()));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [], mayRunTools: true);

        Assert.That(runnableTools.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID }));
    }

    [Test]
    public async Task AnAssistantIsNeverOfferedTheTool()
    {
        var registry = this.CreateRegistry(new TestTool(Tool().GetDefinition()));
        var provider = ToolCapableProvider();
        var context = new ToolResolutionContext
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.REWRITE_ASSISTANT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = new ChatThread(),
        };

        var runnableTools = await registry.GetRunnableToolsAsync(context, [ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools, Is.Empty, "An assistant has no data sources to search, even when something names the tool.");
    }

    // Stating its definition needs none of the services the tool searches with. The test tool
    // around it offers the function as registered, since resolving it asks those services:
    private static SemanticSearchTool Tool() => new(null!, null!, null!);
}