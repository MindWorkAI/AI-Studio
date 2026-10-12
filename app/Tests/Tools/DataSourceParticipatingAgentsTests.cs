using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which agents count as seeing the data of the data sources.
/// </summary>
/// <remarks>
/// Every provider which sees the data must be trusted enough for every data source it sees. That
/// cuts both ways: leaving out an agent which does run would send data to a provider trusted too
/// little, while counting an agent which does not run holds back data sources for no reason. The
/// classic RAG process runs its agents; Semantic Search runs none, since the chat model picks the
/// data sources and judges the passages itself.
/// </remarks>
[TestFixture]
public sealed class DataSourceParticipatingAgentsTests
{
    [Test]
    public void SemanticSearchRunsNoAgent()
    {
        var agents = DataSourceService.GetParticipatingAgents(Options(automaticSelection: true, automaticValidation: true), DataSourceRetrievalMode.SEMANTIC_SEARCH, retrievalContextValidationEnabled: true);

        Assert.That(agents, Is.Empty, "The chat provider alone sees the data, so its trust alone decides which data sources it may search.");
    }

    [Test]
    public void TheClassicRAGProcessCountsTheAgentsItRuns()
    {
        var agents = DataSourceService.GetParticipatingAgents(Options(automaticSelection: true, automaticValidation: true), DataSourceRetrievalMode.EVERY_MESSAGE, retrievalContextValidationEnabled: true);

        Assert.That(agents.Select(agent => agent.Component), Is.EqualTo(new[] { AIStudio.Tools.Components.AGENT_DATA_SOURCE_SELECTION, AIStudio.Tools.Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION }));
    }

    [Test]
    public void TheClassicRAGProcessCountsNoAgentItDoesNotRun()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DataSourceService.GetParticipatingAgents(Options(automaticSelection: false, automaticValidation: false), DataSourceRetrievalMode.EVERY_MESSAGE, retrievalContextValidationEnabled: true), Is.Empty);
            Assert.That(DataSourceService.GetParticipatingAgents(Options(automaticSelection: false, automaticValidation: true), DataSourceRetrievalMode.EVERY_MESSAGE, retrievalContextValidationEnabled: false), Is.Empty, "The validation of this chat is on, but the settings switch it off everywhere.");
        });
    }

    private static DataSourceOptions Options(bool automaticSelection, bool automaticValidation) => new()
    {
        DisableDataSources = false,
        AutomaticDataSourceSelection = automaticSelection,
        AutomaticValidation = automaticValidation,
    };
}