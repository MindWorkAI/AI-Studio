using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that a tool collection the user switched off disappears from every selection and from every request, and comes back when switched on.
/// </summary>
/// <remarks>
/// Somebody without a Confluence wiki never needs its search, and a tool that stays in every
/// selection with a warning about its missing settings only gets in the way. Switching it off has
/// to hide it everywhere a tool can be picked, and the model must neither get nor call it. The
/// settings still list it, or the user could never switch it on again, and selections keep naming
/// it, so switching it on brings it back wherever it was selected.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolSwitchedOffByUserTests : ToolRegistryTestBase
{
    private const string OTHER_TOOL_ID = "other_tool";
    private const string COLLECTION_ID = "test_collection";
    private const string SEARCH_TOOL_ID = "test_search";
    private const string READ_TOOL_ID = "test_read";

    [Test]
    public async Task ASwitchedOffToolAppearsInNoSelectionButInTheSettings()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()), new TestTool(Definition(OTHER_TOOL_ID)));
        registry.SetSwitchedOffByUser(TOOL_ID, true);

        var forTheChat = await registry.GetCatalogAsync(AIStudio.Tools.Components.CHAT);
        var forTheSettings = await registry.GetCatalogAsync(registry.GetAllDefinitions());
        var forItsDialog = await registry.GetCatalogItemAsync(TOOL_ID);

        Assert.Multiple(() =>
        {
            Assert.That(forTheChat.Select(item => item.Id), Is.EqualTo(new[] { OTHER_TOOL_ID }), "Every selection is built from this list.");
            Assert.That(forTheSettings.Single(item => item.Id == TOOL_ID).SwitchOffReason, Is.EqualTo(ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER), "Otherwise, the user could never switch it on again.");
            Assert.That(forItsDialog?.IsActive, Is.False, "The settings dialog of the tool still opens.");
        });
    }

    [Test]
    public async Task AToolTheOrganizationSwitchedOffStaysInTheSelection()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()));
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(TOOL_ID);

        var forTheChat = await registry.GetCatalogAsync(AIStudio.Tools.Components.CHAT);

        Assert.That(forTheChat.Single().SwitchOffReason, Is.EqualTo(ToolOfferBlockReason.TOOL_SWITCHED_OFF), "Shown locked, so the user learns why the tool does not work.");
    }

    [Test]
    public async Task ASwitchedOffToolIsNeitherCountedNorPreselected()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()), new TestTool(Definition(OTHER_TOOL_ID)));
        registry.SetSwitchedOffByUser(TOOL_ID, true);

        var counted = registry.FilterToolIdsForProvider(ToolCapableProvider(), [TOOL_ID, OTHER_TOOL_ID], OutboundDataRestriction.UNRESTRICTED);
        var preselected = await registry.FilterSelectableToolIdsAsync(AIStudio.Tools.Components.CHAT, [TOOL_ID, OTHER_TOOL_ID]);

        Assert.Multiple(() =>
        {
            Assert.That(counted, Is.EquivalentTo(new[] { OTHER_TOOL_ID }), "The token count below the message field.");
            Assert.That(preselected, Is.EquivalentTo(new[] { OTHER_TOOL_ID }), "A chat template or a launcher which names the tool.");
        });
    }

    [Test]
    public async Task AModelCannotCallASwitchedOffTool()
    {
        var wasCalled = false;
        var registry = this.CreateRegistry(new TestTool(Definition(), execute: _ =>
        {
            wasCalled = true;
            return new ToolExecutionResult();
        }));

        registry.SetSwitchedOffByUser(TOOL_ID, true);
        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [TOOL_ID], mayRunTools: true);

        // A model which calls the tool anyway, e.g., because an earlier answer in the chat named it:
        var executor = new ToolExecutor(this.CreateToolSettingsService(), NullLogger<ToolExecutor>.Instance);
        var outcome = await executor.ExecuteAsync("call-1", TOOL_ID, "{}", runnableTools, new NoProvider(), new ChatThread(), order: 1);

        Assert.Multiple(() =>
        {
            Assert.That(runnableTools, Is.Empty, "The request does not offer the tool.");
            Assert.That(outcome.Trace.Status, Is.EqualTo(ToolInvocationTraceStatus.BLOCKED));
            Assert.That(wasCalled, Is.False);
        });
    }

    [Test]
    public async Task ASelectedToolComesBackWhenSwitchedOnAgain()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()));
        string[] selection = [TOOL_ID];

        registry.SetSwitchedOffByUser(TOOL_ID, true);
        var whileSwitchedOff = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), selection, mayRunTools: true);
        registry.SetSwitchedOffByUser(TOOL_ID, false);
        var afterSwitchingOn = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), selection, mayRunTools: true);

        Assert.Multiple(() =>
        {
            Assert.That(whileSwitchedOff, Is.Empty);
            Assert.That(afterSwitchingOn.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { TOOL_ID }), "The selection kept the tool all along.");
            Assert.That(this.StoredIds, Is.Empty);
        });
    }

    [Test]
    public async Task SwitchingOffOneToolSwitchesOffItsWholeCollection()
    {
        var registry = this.CreateCollection();
        registry.SetSwitchedOffByUser(READ_TOOL_ID, true);

        var reason = await registry.GetOfferBlockReasonAsync(SEARCH_TOOL_ID, ToolCapableProvider(), AIStudio.Tools.Components.CHAT);

        Assert.Multiple(() =>
        {
            Assert.That(this.StoredIds, Is.EquivalentTo(new[] { COLLECTION_ID }), "The choice belongs to the collection, not to the tool it was made for.");
            Assert.That(registry.IsSwitchedOffByUser(SEARCH_TOOL_ID), Is.True);
            Assert.That(reason, Is.EqualTo(ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER));
        });
    }

    [Test]
    public void SwitchingACollectionOnRemovesTheEntriesOfItsTools()
    {
        var registry = this.CreateCollection();
        this.StoredIds.Add(READ_TOOL_ID);

        var whileSwitchedOff = registry.IsToolActive(SEARCH_TOOL_ID);
        registry.SetSwitchedOffByUser(COLLECTION_ID, false);

        Assert.Multiple(() =>
        {
            Assert.That(whileSwitchedOff, Is.False, "An entry for one tool counts for its whole collection.");
            Assert.That(registry.IsToolActive(SEARCH_TOOL_ID), Is.True, "The old entry of the reader would keep the collection switched off.");
            Assert.That(this.StoredIds, Is.Empty);
        });
    }

    [Test]
    public void WhatTheUserSwitchedOffStaysOffWhenTheOrganizationLetsGo()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()));
        registry.SetSwitchedOffByUser(TOOL_ID, true);
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(TOOL_ID);

        var whileBothApply = registry.IsSwitchedOffByUser(TOOL_ID);
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Remove(TOOL_ID);

        Assert.Multiple(() =>
        {
            Assert.That(whileBothApply, Is.True);
            Assert.That(registry.IsToolActive(TOOL_ID), Is.False);
        });
    }

    private HashSet<string> StoredIds => this.SettingsManager.ConfigurationData.Tools.UserDisabledToolIds;

    private ToolRegistry CreateCollection() => this.CreateRegistry(
        [new TestCollection(COLLECTION_ID, ConfidenceLevel.NONE, SEARCH_TOOL_ID, READ_TOOL_ID)],
        new TestTool(Definition(SEARCH_TOOL_ID)),
        new TestTool(Definition(READ_TOOL_ID)));
}