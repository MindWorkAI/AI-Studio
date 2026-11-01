using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks which entries of the catalog the tool selection offers for selecting right now.
/// </summary>
/// <remarks>
/// The tool selection shows these entries first and keeps the others behind a button, together
/// with the reason. An entry wrongly counted as selectable would sit at the top without reacting to
/// a click; one wrongly counted as unavailable would hide a tool the user could have picked.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolSelectableTests : ToolRegistryTestBase
{
    private const string COLLECTION_ID = "test_collection";
    private const string SEARCH_TOOL_ID = "test_search";
    private const string READ_TOOL_ID = "test_read";

    [Test]
    public async Task NothingInTheWay()
    {
        var item = await this.GetItemAsync(new TestTool(Definition()));

        Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.UNRESTRICTED), Is.True);
    }

    [Test]
    public async Task ToolsSwitchedOffAltogether()
    {
        this.SettingsManager.ConfigurationData.Tools.EnableTools = false;
        var item = await this.GetItemAsync(new TestTool(Definition()));

        Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.UNRESTRICTED), Is.False, "The organization turned all tools off.");
    }

    [Test]
    public async Task AToolSwitchedOffByTheOrganization()
    {
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(TOOL_ID);
        var item = await this.GetItemAsync(new TestTool(Definition()));

        Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.UNRESTRICTED), Is.False);
    }

    [Test]
    public async Task AToolSwitchedOffByTheUser()
    {
        this.SettingsManager.ConfigurationData.Tools.UserDisabledToolIds.Add(TOOL_ID);
        var item = await this.GetItemAsync(new TestTool(Definition()));

        Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.UNRESTRICTED), Is.False, "The selection never lists such a tool, but the answer must not depend on that.");
    }

    [Test]
    public async Task AToolMissingASetting()
    {
        var item = await this.GetItemAsync(new TestTool(Definition(requiresSetting: true)));

        Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.UNRESTRICTED), Is.False, "The tool cannot work without a setting nobody filled in.");
    }

    [TestCase(ConfidenceLevel.UNKNOWN, false)]
    [TestCase(ConfidenceLevel.MEDIUM, false)]
    [TestCase(ConfidenceLevel.HIGH, true)]
    public async Task TheProviderNeedsTheConfidenceOfTheTool(ConfidenceLevel providerConfidence, bool expected)
    {
        var item = await this.GetItemAsync(new TestTool(Definition(minimumConfidence: ConfidenceLevel.HIGH)));

        Assert.That(ToolSelectionRules.IsSelectable(item, providerConfidence, OutboundDataRestriction.UNRESTRICTED), Is.EqualTo(expected), "The tool asks for high confidence.");
    }

    [Test]
    public async Task ARestrictionKeepingSomeToolsOfACollectionBack()
    {
        var item = await this.GetCollectionItemAsync(readToolOutboundData: ToolOutboundData.CONFIGURED_SERVICE);

        Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), Is.True, "The reader still runs, and the request leaves out the search on its own.");
    }

    [Test]
    public async Task ARestrictionKeepingEveryToolOfACollectionBack()
    {
        var item = await this.GetCollectionItemAsync(readToolOutboundData: ToolOutboundData.MODEL_CHOSEN_ADDRESSES);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES), Is.False, "Nothing of the collection is left to run.");
            Assert.That(ToolSelectionRules.IsSelectable(item, ConfidenceLevel.HIGH, OutboundDataRestriction.UNRESTRICTED), Is.True, "Without a restriction, both tools run.");
        }
    }

    private async Task<ToolCatalogItem> GetItemAsync(TestTool tool)
    {
        var item = await this.CreateRegistry(tool).GetCatalogItemAsync(TOOL_ID);
        Assert.That(item, Is.Not.Null);
        return item!;
    }

    /// <summary>
    /// A collection of a search which queries third parties and a reader whose outbound data the test chooses.
    /// </summary>
    private async Task<ToolCatalogItem> GetCollectionItemAsync(ToolOutboundData readToolOutboundData)
    {
        var registry = this.CreateRegistry(
            [new TestCollection(COLLECTION_ID, ConfidenceLevel.NONE, SEARCH_TOOL_ID, READ_TOOL_ID)],
            new TestTool(Definition(SEARCH_TOOL_ID)) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES },
            new TestTool(Definition(READ_TOOL_ID)) { OutboundData = readToolOutboundData });

        var item = await registry.GetCatalogItemAsync(COLLECTION_ID);
        Assert.That(item, Is.Not.Null);
        return item!;
    }
}