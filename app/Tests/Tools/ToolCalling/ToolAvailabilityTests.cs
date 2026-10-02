using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that a tool which does not exist right now appears nowhere, and comes back with its preview.
/// </summary>
/// <remarks>
/// The mail tools belong to a preview. While it is switched off, a tool in the selection or in the
/// settings would promise something AI Studio does not offer yet, and a request would hand the
/// model a tool for mailboxes nobody can configure. A selection which names such a tool must not
/// lose it, though: switching the preview on again brings the tool back where it was selected.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolAvailabilityTests : ToolRegistryTestBase
{
    private const string PREVIEW_TOOL_ID = "preview_tool";

    [Test]
    public async Task AnUnavailableToolAppearsInNoList()
    {
        var previewTool = PreviewTool(isAvailable: false);
        var registry = this.CreateRegistry(new TestTool(Definition()), previewTool);

        var forTheChat = await registry.GetCatalogAsync(AIStudio.Tools.Components.CHAT);
        var forTheSettings = await registry.GetCatalogAsync(registry.GetAllDefinitions());
        var handedInDirectly = await registry.GetCatalogAsync([previewTool.GetDefinition()]);

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetDefinitionsForComponent(AIStudio.Tools.Components.CHAT).Select(definition => definition.Id), Is.EqualTo(new[] { TOOL_ID }));
            Assert.That(registry.GetAllDefinitions().Select(definition => definition.Id), Is.EqualTo(new[] { TOOL_ID }));
            Assert.That(forTheChat.Select(item => item.Definition.Id), Is.EqualTo(new[] { TOOL_ID }), "The selection below the message field.");
            Assert.That(forTheSettings.Select(item => item.Definition.Id), Is.EqualTo(new[] { TOOL_ID }), "The tool list of the app settings.");
            Assert.That(handedInDirectly, Is.Empty, "Whoever hands in the definition directly does not get it listed either.");
        });
    }

    [Test]
    public void TheTokenCountLeavesAnUnavailableToolOut()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()), PreviewTool(isAvailable: false));

        var counted = registry.FilterToolIdsForProvider(ToolCapableProvider(), [TOOL_ID, PREVIEW_TOOL_ID], OutboundDataRestriction.UNRESTRICTED);

        Assert.That(counted, Is.EquivalentTo(new[] { TOOL_ID }));
    }

    [Test]
    public async Task ASelectedToolComesBackWithItsPreview()
    {
        var previewTool = PreviewTool(isAvailable: false);
        var registry = this.CreateRegistry(previewTool);
        string[] selection = [PREVIEW_TOOL_ID];

        var whileSwitchedOff = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), selection, mayRunTools: true);
        previewTool.IsAvailable = true;
        var afterSwitchingOn = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), selection, mayRunTools: true);

        Assert.Multiple(() =>
        {
            Assert.That(whileSwitchedOff, Is.Empty);
            Assert.That(afterSwitchingOn.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { PREVIEW_TOOL_ID }), "The selection kept the tool all along.");
        });
    }

    private static TestTool PreviewTool(bool isAvailable) => new(Definition(PREVIEW_TOOL_ID)) { IsAvailable = isAvailable };
}