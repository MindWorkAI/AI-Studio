using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what a search tells the chat and the model besides its passages.
/// </summary>
/// <remarks>
/// The chat raises what it requires from a provider as soon as confidential passages land in it,
/// and never lowers it again. A search which found nothing brought nothing in, so it must not raise
/// anything: otherwise, one search in the wrong data source would lock a chat to self-hosted
/// providers for good. The model, for its part, has to tell a data source which found nothing
/// from one which could not be searched, or it would take a failure for an answer.
/// </remarks>
[TestFixture]
public sealed class SemanticSearchToolResultTests
{
    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.HIGH };
    private static readonly IDataSource MINUTES = new DataSourceLocalFile { Num = 2, Id = "22222222-2222-2222-2222-222222222222", Name = "Minutes", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.LOW };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 3, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10, SecurityPolicy = DataSourceSecurity.SELF_HOSTED };

    [Test]
    public void ASearchWhichFoundNothingRequiresNothing()
    {
        var requirements = SemanticSearchTool.GetRequirements([HANDBOOK, INTRANET], [0, 0]);

        Assert.That(requirements, Is.EqualTo((ConfidenceLevel.NONE, DataSourceSecurity.NOT_SPECIFIED)), "NOT_SPECIFIED leaves the data security of the chat as it was.");
    }

    [Test]
    public void OnlyTheDataSourcesWithPassagesRaiseTheRequirements()
    {
        var requirements = SemanticSearchTool.GetRequirements([HANDBOOK, MINUTES, INTRANET], [0, 2, 0]);

        Assert.That(requirements, Is.EqualTo((ConfidenceLevel.LOW, DataSourceSecurity.ALLOW_ANY)), "Neither the highly confidential handbook nor the self-hosted intranet brought anything into the chat.");
    }

    [Test]
    public void ASelfHostedDataSourceWithPassagesRestrictsTheChat()
    {
        var requirements = SemanticSearchTool.GetRequirements([MINUTES, INTRANET], [1, 1]);

        Assert.That(requirements, Is.EqualTo((ConfidenceLevel.LOW, DataSourceSecurity.SELF_HOSTED)));
    }

    [Test]
    public void ADataSourceWhichCouldNotBeSearchedSaysSo()
    {
        var result = SemanticSearchTool.DescribeResult(INTRANET, RetrievalPage.EMPTY with { Gaps = [RetrievalGap.NOT_SEARCHED] }, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result["result_count"]!.GetValue<int>(), Is.Zero);
            Assert.That(Issues(result), Has.Count.EqualTo(1), "A result count of zero alone reads as finding nothing.");
        });
    }

    [Test]
    public void PassagesLeftOutForTheBudgetAreCountedWithAWayOut()
    {
        var result = SemanticSearchTool.DescribeResult(HANDBOOK, RetrievalPage.EMPTY with { HasMore = true }, 4, 3);

        Assert.Multiple(() =>
        {
            Assert.That(result["result_count"]!.GetValue<int>(), Is.EqualTo(4), "Only the passages which reached the model count as results.");
            Assert.That(result["has_more"]!.GetValue<bool>(), Is.True);
            Assert.That(Issues(result), Has.Count.EqualTo(1));
            Assert.That(Issues(result).FirstOrDefault(), Does.StartWith("3 further passages").And.Contain("narrower query"), "The model has to learn how many are missing, and how to get to them.");
        });
    }

    [Test]
    public void ACompleteSearchReportsNoIssues()
    {
        var result = SemanticSearchTool.DescribeResult(MINUTES, RetrievalPage.EMPTY, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.ContainsKey("issues"), Is.False, "An empty list of issues would cost tokens in every result for nothing.");
            Assert.That(result["id"]!.GetValue<string>(), Is.EqualTo(MINUTES.Id));
            Assert.That(result["has_more"]!.GetValue<bool>(), Is.False);
        });
    }

    private static IReadOnlyList<string> Issues(JsonObject result) => result["issues"] is JsonArray issues
        ? issues.Select(issue => issue!.GetValue<string>()).ToList()
        : [];
}