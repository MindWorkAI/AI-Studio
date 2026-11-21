using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what a selection of tools or data sources offers when several models work with it.
/// </summary>
/// <remarks>
/// A rule which lets one model have more than another would make a comparison unfair, or send data to a model which
/// may not see it. A rule which gives a single model less than before would change the chat.
/// </remarks>
[TestFixture]
public sealed class SharedSelectionRulesTests
{
    private static readonly IDataSource ALPHA = DataSource("alpha");
    private static readonly IDataSource BETA = DataSource("beta");
    private static readonly IDataSource GAMMA = DataSource("gamma");

    [Test]
    public void TheLowestConfidenceCounts()
    {
        Assert.That(SharedSelectionRules.LowestConfidence([ConfidenceLevel.HIGH, ConfidenceLevel.LOW, ConfidenceLevel.MEDIUM]), Is.EqualTo(ConfidenceLevel.LOW));
    }

    [Test]
    public void WithoutAModelThereIsNoConfidence()
    {
        Assert.That(SharedSelectionRules.LowestConfidence([]), Is.EqualTo(ConfidenceLevel.NONE));
    }

    [Test]
    public void ToolsNeedEveryModelToCallThem()
    {
        var unavailable = new ToolCallingAvailability(false, "The second model cannot call tools.");

        Assert.That(SharedSelectionRules.ToolCallingOfAll([ToolCallingAvailability.Available(), unavailable]), Is.EqualTo(unavailable), "The reason of the model which cannot call tools has to reach the user.");
    }

    [Test]
    public void ToolsAreAvailableWhenEveryModelCallsThem()
    {
        Assert.That(SharedSelectionRules.ToolCallingOfAll([ToolCallingAvailability.Available(), ToolCallingAvailability.Available()]).IsAvailable, Is.True);
    }

    [Test]
    public void SemanticSearchNeedsEveryModelToSearchItself()
    {
        var blocked = new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS);

        var mode = SharedSelectionRules.RetrievalModeOfAll([new(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolOfferBlockReason.NONE), blocked]);

        Assert.That(mode, Is.EqualTo(blocked), "One model searching itself while the other gets passages with every message would make the chats unequal.");
    }

    [Test]
    public void TheReasonComesAlongWhateverTheUserPrefers()
    {
        var blocked = new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW);

        var mode = SharedSelectionRules.RetrievalModeOfAll([new(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.NONE), blocked]);

        Assert.That(mode, Is.EqualTo(blocked), "The data source selection leaves out a choice which is none, which it can tell only by the reason.");
    }

    [Test]
    public void EveryModelSearchingItselfKeepsSemanticSearch()
    {
        var searchesItself = new EffectiveRetrievalMode(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolOfferBlockReason.NONE);

        Assert.That(SharedSelectionRules.RetrievalModeOfAll([searchesItself, searchesItself]), Is.EqualTo(searchesItself));
    }

    [Test]
    public void ThePreferenceOfTheUserStays()
    {
        var everyMessage = new EffectiveRetrievalMode(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.NONE);

        Assert.That(SharedSelectionRules.RetrievalModeOfAll([everyMessage, everyMessage]), Is.EqualTo(everyMessage));
    }

    [Test]
    public void ASingleModelKeepsItsOwnMode()
    {
        foreach (var mode in new EffectiveRetrievalMode[]
                 {
                     new(DataSourceRetrievalMode.SEMANTIC_SEARCH, ToolOfferBlockReason.NONE),
                     new(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.NONE),
                     new(DataSourceRetrievalMode.EVERY_MESSAGE, ToolOfferBlockReason.TOOLS_SWITCHED_OFF),
                 })
            Assert.That(SharedSelectionRules.RetrievalModeOfAll([mode]), Is.EqualTo(mode));
    }

    [Test]
    public void OnlyTheDataSourcesEveryModelMayUseRemain()
    {
        var first = new AllowedSelectedDataSources([ALPHA, BETA, GAMMA], [ALPHA, BETA], [], [], []);
        var second = new AllowedSelectedDataSources([GAMMA, ALPHA], [ALPHA], [], [], []);

        var shared = SharedSelectionRules.DataSourcesOfAll([first, second]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shared.AllowedDataSources, Is.EqualTo(new[] { ALPHA, GAMMA }), "The order of the first model counts.");
            Assert.That(shared.SelectedDataSources, Is.EqualTo(new[] { ALPHA }));
        }
    }

    [Test]
    public void AWaitingDataSourceStaysWaitingOnlyWhenEveryModelMayUseIt()
    {
        var first = new AllowedSelectedDataSources([], [], [ALPHA, BETA], [GAMMA], [BETA]);
        var second = new AllowedSelectedDataSources([], [], [ALPHA], [GAMMA], []);

        var shared = SharedSelectionRules.DataSourcesOfAll([first, second]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shared.DataSourcesAwaitingReindex, Is.EqualTo(new[] { ALPHA }));
            Assert.That(shared.DataSourcesNeedingRepair, Is.EqualTo(new[] { GAMMA }));
            Assert.That(shared.UnreachableDataSources, Is.Empty);
        }
    }

    [Test]
    public void ASingleModelKeepsItsDataSources()
    {
        var only = new AllowedSelectedDataSources([ALPHA, BETA], [BETA], [GAMMA], [], [ALPHA]);

        var shared = SharedSelectionRules.DataSourcesOfAll([only]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shared.AllowedDataSources, Is.EqualTo(only.AllowedDataSources));
            Assert.That(shared.SelectedDataSources, Is.EqualTo(only.SelectedDataSources));
            Assert.That(shared.DataSourcesAwaitingReindex, Is.EqualTo(only.DataSourcesAwaitingReindex));
            Assert.That(shared.DataSourcesNeedingRepair, Is.EqualTo(only.DataSourcesNeedingRepair));
            Assert.That(shared.UnreachableDataSources, Is.EqualTo(only.UnreachableDataSources));
        }
    }

    [Test]
    public void WithoutAModelThereIsNoDataSource()
    {
        Assert.That(SharedSelectionRules.DataSourcesOfAll([]).AllowedDataSources, Is.Empty);
    }

    private static IDataSource DataSource(string id) => new DataSourceLocalFile
    {
        Num = 1,
        Id = id,
        Name = id,
        Type = DataSourceType.LOCAL_FILE,
        ConfidenceLevel = ConfidenceLevel.LOW,
    };
}