using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
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
/// chat, never in an assistant, and only when the user lets the model search them itself.
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

    [Test]
    public async Task AChatWhichSearchesWithEveryMessageIsNotOfferedTheTool()
    {
        var provider = ToolCapableProvider();
        var options = new DataSourceOptions
        {
            DisableDataSources = false,
            AutomaticDataSourceSelection = true,
            RetrievalMode = DataSourceRetrievalMode.EVERY_MESSAGE,
        };

        // A chat gets its options as a copy of the chat defaults or of its template, so the copy has to keep the choice:
        var context = new ToolResolutionContext
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = new ChatThread { DataSourceOptions = options.CreateCopy() },
        };

        // The choice decides before any data source is checked, so the tool needs none of its services:
        var function = await Tool().ResolveFunctionAsync(Tool().GetDefinition(), context);

        Assert.That(function, Is.Null, "When AI Studio searches with every message, the model must not search a second time.");
    }

    // Stating its definition needs none of the services the tool searches with. The test tool
    // around it offers the function as registered, since resolving it asks those services:
    private static SemanticSearchTool Tool() => new(null!, null!, null!, null!, null!);
}