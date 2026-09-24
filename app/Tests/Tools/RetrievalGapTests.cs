using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks who hears about what kept a search from covering a local data source.
/// </summary>
/// <remarks>
/// With Semantic Search, the model writes the query, not the user. A warning that the message was
/// too long to search with would then blame the user for a query they never wrote, while the model,
/// which could search with a shorter one, would learn nothing. Problems of the data source itself
/// stay with the user either way, since nobody else can fix a missing embedding provider.
/// </remarks>
[TestFixture]
public sealed class RetrievalGapTests
{
    [Test]
    public void AQueryTheModelWroteIsNotTheUsersProblem()
    {
        Assert.That(DataSourceLocalRetrievalService.IsForTheUser(RetrievalGap.QUERY_NOT_SEARCHABLE, queryWrittenByUser: false), Is.False, "The model learns about it from the page and can search with a shorter query.");
    }

    [Test]
    public void AMessageTheUserWroteIsTheirsToShorten()
    {
        Assert.That(DataSourceLocalRetrievalService.IsForTheUser(RetrievalGap.QUERY_NOT_SEARCHABLE, queryWrittenByUser: true), Is.True);
    }

    [TestCase(RetrievalGap.NOT_SEARCHED, false)]
    [TestCase(RetrievalGap.NOT_SEARCHED, true)]
    [TestCase(RetrievalGap.PARTLY_SEARCHED, false)]
    [TestCase(RetrievalGap.PARTLY_SEARCHED, true)]
    public void ProblemsOfTheDataSourceAreAlwaysForTheUser(RetrievalGap gap, bool queryWrittenByUser)
    {
        Assert.That(DataSourceLocalRetrievalService.IsForTheUser(gap, queryWrittenByUser), Is.True, "Only the user can fix an index or an embedding provider.");
    }
}