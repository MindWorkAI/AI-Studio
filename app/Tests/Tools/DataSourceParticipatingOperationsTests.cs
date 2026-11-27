using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which AI operations count as seeing the data of the data sources.
/// </summary>
/// <remarks>
/// Every provider which sees the data must be trusted enough for every data source it sees. That
/// cuts both ways: leaving out an operation which does run would send data to a provider trusted too
/// little, while counting an operation which does not run holds back data sources for no reason. The
/// classic RAG process runs its AI operations; Semantic Search runs none, since the chat model picks the
/// data sources and judges the passages itself.
/// </remarks>
[TestFixture]
public sealed class DataSourceParticipatingOperationsTests
{
    [Test]
    public void SemanticSearchRunsNoOperation()
    {
        var operations = DataSourceService.GetParticipatingOperations(Options(automaticSelection: true, automaticValidation: true), DataSourceRetrievalMode.SEMANTIC_SEARCH, retrievalContextValidationEnabled: true);

        Assert.That(operations, Is.Empty, "The chat provider alone sees the data, so its trust alone decides which data sources it may search.");
    }

    [Test]
    public void TheClassicRAGProcessCountsTheOperationsItRuns()
    {
        var operations = DataSourceService.GetParticipatingOperations(Options(automaticSelection: true, automaticValidation: true), DataSourceRetrievalMode.EVERY_MESSAGE, retrievalContextValidationEnabled: true);

        Assert.That(operations.Select(operation => operation.Component), Is.EqualTo(new[] { AIStudio.Tools.Components.AI_TASK_DATA_SOURCE_SELECTION, AIStudio.Tools.Components.AI_TASK_RETRIEVAL_CONTEXT_VALIDATION }));
    }

    [Test]
    public void TheClassicRAGProcessCountsNoOperationItDoesNotRun()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceService.GetParticipatingOperations(Options(automaticSelection: false, automaticValidation: false), DataSourceRetrievalMode.EVERY_MESSAGE, retrievalContextValidationEnabled: true), Is.Empty);
            Assert.That(DataSourceService.GetParticipatingOperations(Options(automaticSelection: false, automaticValidation: true), DataSourceRetrievalMode.EVERY_MESSAGE, retrievalContextValidationEnabled: false), Is.Empty, "The validation of this chat is on, but the settings switch it off everywhere.");
        }
    }

    private static DataSourceOptions Options(bool automaticSelection, bool automaticValidation) => new()
    {
        DisableDataSources = false,
        AutomaticDataSourceSelection = automaticSelection,
        AutomaticValidation = automaticValidation,
    };
}