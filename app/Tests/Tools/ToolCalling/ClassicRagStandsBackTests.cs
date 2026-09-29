using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG.RAGProcesses;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks when the classic RAG process leaves the searching of the data sources to the model.
/// </summary>
/// <remarks>
/// Standing back where the request offers no tool leaves a chat searching nothing at all; not
/// standing back where it does has every message searched twice. The request knows its provider
/// only as a provider instance plus a model, so these tests hand over the provider in the same
/// form: the expert settings of the user have to survive that way, or the two decide differently.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ClassicRagStandsBackTests : ToolRegistryTestBase
{
    [Test]
    public async Task ItStandsBackWhenTheModelCanSearchItself()
    {
        Assert.That(await this.IsSearchedByTheModelAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolCapableProvider()), Is.True);
    }

    [Test]
    public async Task ItSearchesWhenTheUserWantsEveryMessageSearched()
    {
        Assert.That(await this.IsSearchedByTheModelAsync(DataSourceRetrievalMode.EVERY_MESSAGE, ToolCapableProvider()), Is.False);
    }

    [Test]
    public async Task ItSearchesWhenTheModelCannotUseTools()
    {
        var provider = ToolCapableProvider() with { CapabilityOverrides = new() { FunctionCalling = false } };

        Assert.That(await this.IsSearchedByTheModelAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, provider), Is.False, "The person said their model cannot call functions, so no request will offer it the tool.");
    }

    [Test]
    public async Task ItSearchesWhenTheOrganizationSwitchedTheToolOff()
    {
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID);

        Assert.That(await this.IsSearchedByTheModelAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolCapableProvider()), Is.False);
    }

    [Test]
    public async Task ItSearchesWithoutARegistry()
    {
        var providerSettings = ToolCapableProvider();
        var thread = ThreadSearching(DataSourceRetrievalMode.SEMANTIC_SEARCH);

        var isSearchedByTheModel = await AISrcSelWithRetCtxVal.IsSearchedByTheModelAsync(null, this.SettingsManager, providerSettings.CreateProvider(), providerSettings.Model, thread);

        Assert.That(isSearchedByTheModel, Is.False, "Without a registry, the request offers no tools either.");
    }

    private async Task<bool> IsSearchedByTheModelAsync(DataSourceRetrievalMode preference, AIStudio.Settings.Provider providerSettings)
    {
        // Stating its definition needs none of the services the tool searches with:
        var registry = this.CreateRegistry(new TestTool(new SemanticSearchTool(null!, null!, null!, null!, null!).GetDefinition()));
        return await AISrcSelWithRetCtxVal.IsSearchedByTheModelAsync(registry, this.SettingsManager, providerSettings.CreateProvider(), providerSettings.Model, ThreadSearching(preference));
    }

    private static ChatThread ThreadSearching(DataSourceRetrievalMode preference) => new()
    {
        DataSourceOptions = new() { DisableDataSources = false, AutomaticDataSourceSelection = true, RetrievalMode = preference },
    };
}