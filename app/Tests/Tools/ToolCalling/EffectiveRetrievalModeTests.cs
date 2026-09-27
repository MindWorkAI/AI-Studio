using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how the data sources of a chat are actually searched, compared to how the user wants them searched.
/// </summary>
/// <remarks>
/// Semantic search is a preference: whenever the tool cannot be offered, AI Studio searches with
/// every message instead, and says why. A wrong answer in one direction leaves a chat searching
/// nothing, in the other direction searching twice. Whether the chain of checks itself holds is
/// the business of ToolRegistryOfferTests; these tests check what the answer makes of it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class EffectiveRetrievalModeTests : ToolRegistryTestBase
{
    [Test]
    public async Task TheUserMayWantEveryMessageSearched()
    {
        var mode = await this.GetModeAsync(DataSourceRetrievalMode.EVERY_MESSAGE, ToolCapableProvider());

        Assert.That(mode, Is.EqualTo(new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.NONE)), "The user chose this, so there is no fallback to explain.");
    }

    [Test]
    public async Task AModelWhichCanUseToolsSearchesItself()
    {
        var mode = await this.GetModeAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolCapableProvider());

        Assert.That(mode, Is.EqualTo(new EffectiveRetrievalMode(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolOfferBlockReason.NONE)));
    }

    [Test]
    public async Task AModelWithoutToolsGetsEveryMessageSearched()
    {
        var provider = ToolCapableProvider() with { CapabilityOverrides = new() { FunctionCalling = false } };

        var mode = await this.GetModeAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, provider);

        Assert.That(mode, Is.EqualTo(new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS)));
    }

    [Test]
    public async Task SwitchingTheToolOffGetsEveryMessageSearched()
    {
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID);

        var mode = await this.GetModeAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolCapableProvider());

        Assert.That(mode, Is.EqualTo(new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.TOOL_SWITCHED_OFF)), "An organization which switches the tool off must not switch off the data sources along with it.");
    }

    [Test]
    public async Task OutsideTheChatEveryMessageIsSearched()
    {
        var mode = await this.GetModeAsync(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolCapableProvider(), AIStudio.Tools.Components.REWRITE_ASSISTANT);

        Assert.That(mode, Is.EqualTo(new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.NOT_AVAILABLE_HERE)));
    }

    // Stating its definition needs none of the services the tool searches with:
    private Task<EffectiveRetrievalMode> GetModeAsync(DataSourceRetrievalMode preference, AIStudio.Settings.Provider provider, AIStudio.Tools.Components component = AIStudio.Tools.Components.CHAT)
    {
        var registry = this.CreateRegistry(new TestTool(new SemanticSearchTool(null!, null!, null!, null!, null!).GetDefinition()));
        var options = new DataSourceOptions { DisableDataSources = false, AutomaticDataSourceSelection = true, RetrievalMode = preference };
        return registry.GetEffectiveRetrievalModeAsync(options, provider, component);
    }
}