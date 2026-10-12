using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the tools of a collection are switched off and trusted as one.
/// </summary>
/// <remarks>
/// The model sees each tool of a collection on its own, so every tool is checked on its own when a
/// request is prepared. What these tests guard is that each of those checks asks the collection:
/// a tool which needed less trust than the others of its collection, or which an organization could
/// not switch off with them, would let the content of a mailbox through by a side door.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolCollectionTests : ToolRegistryTestBase
{
    private const string COLLECTION_ID = "test_collection";
    private const string SEARCH_TOOL_ID = "test_search";
    private const string READ_TOOL_ID = "test_read";

    [Test]
    public void EveryToolNeedsTheConfidenceOfItsCollection()
    {
        var registry = this.CreateCollection(ConfidenceLevel.MEDIUM);

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMinimumProviderConfidence(SEARCH_TOOL_ID), Is.EqualTo(ConfidenceLevel.MEDIUM), "The search asks for HIGH itself, which the collection overrules.");
            Assert.That(registry.GetMinimumProviderConfidence(READ_TOOL_ID), Is.EqualTo(ConfidenceLevel.MEDIUM), "The reader asks for nothing itself, which the collection overrules.");
            Assert.That(registry.GetMinimumProviderConfidence(COLLECTION_ID), Is.EqualTo(ConfidenceLevel.MEDIUM));
        });
    }

    [Test]
    public void AToolOutsideACollectionFormsOneOfItsOwn()
    {
        var registry = this.CreateRegistry([new TestCollection(COLLECTION_ID, ConfidenceLevel.LOW, SEARCH_TOOL_ID)], new TestTool(Definition(SEARCH_TOOL_ID)), new TestTool(Definition(minimumConfidence: ConfidenceLevel.HIGH)));

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetCollectionId(TOOL_ID), Is.EqualTo(TOOL_ID));
            Assert.That(registry.GetMinimumProviderConfidence(TOOL_ID), Is.EqualTo(ConfidenceLevel.HIGH), "Without a collection, the tool's own minimum counts.");
            Assert.That(registry.GetCollectionId(SEARCH_TOOL_ID), Is.EqualTo(COLLECTION_ID));
            Assert.That(registry.GetCollectionId(COLLECTION_ID), Is.EqualTo(COLLECTION_ID), "The ID of a collection stands for itself.");
        });
    }

    [Test]
    public void ChoosingALevelForOneToolChoosesItForItsWholeCollection()
    {
        var registry = this.CreateCollection(ConfidenceLevel.LOW);
        registry.SetMinimumProviderConfidence(READ_TOOL_ID, ConfidenceLevel.HIGH);

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMinimumProviderConfidence(SEARCH_TOOL_ID), Is.EqualTo(ConfidenceLevel.HIGH));
            Assert.That(this.StoredLevels, Is.EquivalentTo(new Dictionary<string, string> { [COLLECTION_ID] = nameof(ConfidenceLevel.HIGH) }), "The level belongs to the collection, not to the tool it was chosen for.");
        });
    }

    [Test]
    public void AnEntryForOneToolCountsForItsWholeCollection()
    {
        var registry = this.CreateCollection(ConfidenceLevel.LOW);
        this.StoredLevels[READ_TOOL_ID] = nameof(ConfidenceLevel.MEDIUM);

        Assert.That(registry.GetMinimumProviderConfidence(SEARCH_TOOL_ID), Is.EqualTo(ConfidenceLevel.MEDIUM), "An entry made before the tool joined the collection still counts.");
    }

    [Test]
    public void TheStrictestEntryOfACollectionWins()
    {
        var registry = this.CreateCollection(ConfidenceLevel.NONE);
        this.StoredLevels[COLLECTION_ID] = nameof(ConfidenceLevel.LOW);
        this.StoredLevels[READ_TOOL_ID] = nameof(ConfidenceLevel.HIGH);

        Assert.That(registry.GetMinimumProviderConfidence(SEARCH_TOOL_ID), Is.EqualTo(ConfidenceLevel.HIGH), "Somebody raised the level of the reader, and no other entry may lower it again.");
    }

    [Test]
    public void ChoosingALevelRemovesTheEntriesOfTheTools()
    {
        var registry = this.CreateCollection(ConfidenceLevel.NONE);
        this.StoredLevels[READ_TOOL_ID] = nameof(ConfidenceLevel.HIGH);
        registry.SetMinimumProviderConfidence(COLLECTION_ID, ConfidenceLevel.LOW);

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMinimumProviderConfidence(READ_TOOL_ID), Is.EqualTo(ConfidenceLevel.LOW), "The old, stricter entry of the reader would outvote the level just chosen.");
            Assert.That(this.StoredLevels.Keys, Is.EquivalentTo(new[] { COLLECTION_ID }));
        });
    }

    [Test]
    public void ChoosingTheLevelOfTheCollectionRemovesTheOverride()
    {
        var registry = this.CreateCollection(ConfidenceLevel.MEDIUM);
        registry.SetMinimumProviderConfidence(SEARCH_TOOL_ID, ConfidenceLevel.HIGH);
        registry.SetMinimumProviderConfidence(SEARCH_TOOL_ID, ConfidenceLevel.MEDIUM);

        Assert.That(this.StoredLevels, Is.Empty, "The collection asks for that level anyway.");
    }

    [Test]
    public async Task AProviderBelowTheCollectionGetsNoneOfItsTools()
    {
        var registry = this.CreateCollection(ConfidenceLevel.HIGH);

        Assert.That(await registry.GetOfferBlockReasonAsync(READ_TOOL_ID, LessTrustedProvider(), AIStudio.Tools.Components.CHAT), Is.EqualTo(ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW), "The reader asks for nothing itself, yet it shows what the search found.");
    }

    [TestCase(COLLECTION_ID)]
    [TestCase(READ_TOOL_ID)]
    public async Task SwitchingOffTheCollectionOrOneOfItsToolsSwitchesOffEveryTool(string disabledId)
    {
        var registry = this.CreateCollection(ConfidenceLevel.NONE);
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add(disabledId);

        var reason = await registry.GetOfferBlockReasonAsync(SEARCH_TOOL_ID, ToolCapableProvider(), AIStudio.Tools.Components.CHAT);
        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [SEARCH_TOOL_ID, READ_TOOL_ID], mayRunTools: true);

        Assert.Multiple(() =>
        {
            Assert.That(reason, Is.EqualTo(ToolOfferBlockReason.TOOL_SWITCHED_OFF));
            Assert.That(runnableTools, Is.Empty, "An administrator who names one tool would rather lose the collection than keep the others.");
            Assert.That(registry.IsToolActive(COLLECTION_ID), Is.False);
        });
    }

    [Test]
    public void ACollectionCannotTakeTheIdOfATool()
    {
        var registry = this.CreateRegistry([new TestCollection(TOOL_ID, ConfidenceLevel.HIGH, SEARCH_TOOL_ID)], new TestTool(Definition()), new TestTool(Definition(SEARCH_TOOL_ID)));

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetCollectionId(SEARCH_TOOL_ID), Is.EqualTo(SEARCH_TOOL_ID), "Settings for the tool and for the collection would share their key.");
            Assert.That(registry.GetMinimumProviderConfidence(TOOL_ID), Is.EqualTo(ConfidenceLevel.NONE));
        });
    }

    [Test]
    public void AToolBelongsToTheFirstCollectionWhichNamesIt()
    {
        var registry = this.CreateRegistry([new TestCollection(COLLECTION_ID, ConfidenceLevel.LOW, READ_TOOL_ID), new TestCollection("other_collection", ConfidenceLevel.HIGH, SEARCH_TOOL_ID, READ_TOOL_ID)], new TestTool(Definition(SEARCH_TOOL_ID)), new TestTool(Definition(READ_TOOL_ID)));

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetCollectionId(READ_TOOL_ID), Is.EqualTo(COLLECTION_ID), "Switching off one collection would otherwise take a tool of another one along.");
            Assert.That(registry.GetCollectionId(SEARCH_TOOL_ID), Is.EqualTo("other_collection"), "The second collection keeps its other tools.");
        });
    }

    [Test]
    public void AToolNobodySelectsStaysOutOfEveryCollection()
    {
        var registry = this.CreateRegistry([new TestCollection(COLLECTION_ID, ConfidenceLevel.HIGH, TOOL_ID)], new TestTool(Definition(activation: ToolActivation.CONTEXT)));

        Assert.That(registry.GetCollectionId(TOOL_ID), Is.EqualTo(TOOL_ID), "A tool which offers itself from the context of a chat is never selected, so it cannot be selected as part of a collection either.");
    }

    [Test]
    public void AnUnknownToolIsLeftOutOfItsCollection()
    {
        var registry = this.CreateRegistry([new TestCollection(COLLECTION_ID, ConfidenceLevel.NONE, "unknown_tool", SEARCH_TOOL_ID)], new TestTool(Definition(SEARCH_TOOL_ID)));
        this.SettingsManager.ConfigurationData.Tools.DisabledToolIds.Add("unknown_tool");

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetCollectionId(SEARCH_TOOL_ID), Is.EqualTo(COLLECTION_ID), "The known tool still forms the collection.");
            Assert.That(registry.IsToolActive(SEARCH_TOOL_ID), Is.True, "A tool which is not part of the collection cannot switch it off.");
        });
    }

    [Test]
    public void TheMailboxCollectionGathersTheToolsWhichReadMailboxes()
    {
        var definition = new MailboxToolCollection().GetDefinition();

        Assert.Multiple(() =>
        {
            Assert.That(definition.Id, Is.EqualTo(ToolSelectionRules.MAILBOXES_COLLECTION_ID));
            Assert.That(definition.ToolIds, Is.EqualTo(new[] { ToolSelectionRules.SEARCH_MAILS_TOOL_ID, ToolSelectionRules.READ_MAIL_TOOL_ID, ToolSelectionRules.COUNT_MAILS_TOOL_ID }));
            Assert.That(definition.MinimumProviderConfidence, Is.EqualTo(ConfidenceLevel.VERY_LOW), "Each mailbox asks for its own level, and none may ask for less.");
        });
    }

    private Dictionary<string, string> StoredLevels => this.SettingsManager.ConfigurationData.Tools.MinimumProviderConfidenceByToolId;

    /// <summary>
    /// A search and a reader in one collection. The search asks for HIGH itself, the reader for nothing.
    /// </summary>
    private ToolRegistry CreateCollection(ConfidenceLevel collectionConfidence) => this.CreateRegistry(
        [new TestCollection(COLLECTION_ID, collectionConfidence, SEARCH_TOOL_ID, READ_TOOL_ID)],
        new TestTool(Definition(SEARCH_TOOL_ID, ConfidenceLevel.HIGH)),
        new TestTool(Definition(READ_TOOL_ID)));
}