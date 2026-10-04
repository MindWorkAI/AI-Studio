using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the configuration of a tool collection exports as one, with the settings of each of its tools.
/// </summary>
/// <remarks>
/// An administrator exports what they configured for one entry of the tool settings. The tools of a
/// collection keep their settings to themselves, so the export has to keep them apart, while the
/// confidence belongs to the collection and goes under its ID.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolSettingsExportTests : ToolRegistryTestBase
{
    private const string COLLECTION_ID = "test_collection";
    private const string SEARCH_TOOL_ID = "test_search";
    private const string READ_TOOL_ID = "test_read";

    [Test]
    public async Task EachToolOfACollectionBringsItsOwnAreas()
    {
        var item = await this.CreateCollectionItemAsync();
        var areas = ToolSettingsService.GetExportAreas(item.Tools);

        Assert.Multiple(() =>
        {
            Assert.That(areas.Select(area => area.Id), Is.EqualTo(new[] { $"{SEARCH_TOOL_ID}/", $"{READ_TOOL_ID}/" }), "Both tools put their settings into the nameless area, which only the tool tells apart; the order is the collection's.");
            Assert.That(areas[0].Label, Does.StartWith($"{SEARCH_TOOL_ID}: "), "With two tools to choose from, each area names its tool.");
            Assert.That(areas[1].Label, Does.StartWith($"{READ_TOOL_ID}: "));
        });
    }

    [Test]
    public async Task ASingleToolNamesNoTool()
    {
        var registry = this.CreateRegistry(new TestTool(Definition(requiresSetting: true)));
        var item = await registry.GetCatalogItemAsync(TOOL_ID);

        Assert.That(ToolSettingsService.GetExportAreas(item!.Tools).Single().Label, Does.Not.StartWith(TOOL_ID), "The dialog names the tool already.");
    }

    [Test]
    public async Task TheExportHoldsTheSettingsOfEveryToolAndTheConfidenceOfTheCollection()
    {
        var item = await this.CreateCollectionItemAsync();
        var areas = ToolSettingsService.GetExportAreas(item.Tools);

        var result = await this.CreateToolSettingsService().ExportAsync(item.Tools, new() { SelectedAreaIds = areas.Select(area => area.Id).ToHashSet() }, COLLECTION_ID, ConfidenceLevel.HIGH);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.LuaCode, Does.Contain($"[\"{SEARCH_TOOL_ID}.{REQUIRED_SETTING}\"] = \"https://search.example.org\""));
            Assert.That(result.LuaCode, Does.Contain($"[\"{READ_TOOL_ID}.{REQUIRED_SETTING}\"] = \"https://read.example.org\""));
            Assert.That(result.LuaCode, Does.Contain($"[\"{COLLECTION_ID}\"] = \"HIGH\""), "The confidence belongs to the collection.");
        });
    }

    [Test]
    public async Task OnlyTheSelectedAreasAreExported()
    {
        var item = await this.CreateCollectionItemAsync();

        var result = await this.CreateToolSettingsService().ExportAsync(item.Tools, new() { SelectedAreaIds = new HashSet<string> { $"{READ_TOOL_ID}/" }, IncludeMinimumProviderConfidence = false }, COLLECTION_ID, ConfidenceLevel.HIGH);

        Assert.Multiple(() =>
        {
            Assert.That(result.LuaCode, Does.Contain(READ_TOOL_ID));
            Assert.That(result.LuaCode, Does.Not.Contain(SEARCH_TOOL_ID), "The area of the search was not selected.");
        });
    }

    /// <summary>
    /// A search and a reader in one collection, each with an address saved for it.
    /// </summary>
    private async Task<ToolCatalogItem> CreateCollectionItemAsync()
    {
        var registry = this.CreateRegistry([new TestCollection(COLLECTION_ID, ConfidenceLevel.NONE, SEARCH_TOOL_ID, READ_TOOL_ID)], new TestTool(Definition(READ_TOOL_ID, requiresSetting: true)), new TestTool(Definition(SEARCH_TOOL_ID, requiresSetting: true)));
        this.SettingsManager.ConfigurationData.Tools.Settings[SEARCH_TOOL_ID] = new() { [REQUIRED_SETTING] = "https://search.example.org" };
        this.SettingsManager.ConfigurationData.Tools.Settings[READ_TOOL_ID] = new() { [REQUIRED_SETTING] = "https://read.example.org" };

        var item = await registry.GetCatalogItemAsync(READ_TOOL_ID);
        Assert.That(item, Is.Not.Null);
        return item!;
    }
}