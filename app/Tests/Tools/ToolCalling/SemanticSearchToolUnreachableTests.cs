using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Semantic Search deals with ERI data sources whose server could not be reached.
/// </summary>
/// <remarks>
/// Such a data source is never searched, but the model learns about it, so that it does not take
/// its silence for finding nothing. It learns only about those it would have been offered, and it
/// gets no ID to pass for them, since there is nothing it could search.
/// </remarks>
[TestFixture]
public sealed class SemanticSearchToolUnreachableTests
{
    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10 };
    private static readonly IDataSource MINUTES = new DataSourceLocalFile { Num = 2, Id = "22222222-2222-2222-2222-222222222222", Name = "Minutes", MaxMatches = 10 };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 3, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10 };
    private static readonly IDataSource WIKI = new DataSourceERI_V1 { Num = 4, Id = "44444444-4444-4444-4444-444444444444", Name = "Wiki", MaxMatches = 10 };

    [Test]
    public void TheAutomaticSelectionNamesEveryUnreachableDataSource()
    {
        var offered = SemanticSearchTool.SelectOffered(new DataSourceOptions { AutomaticDataSourceSelection = true }, Checked([HANDBOOK], [HANDBOOK], [WIKI, INTRANET]));

        Assert.Multiple(() =>
        {
            Assert.That(offered.Searchable, Is.EqualTo(new[] { HANDBOOK }));
            Assert.That(offered.Unreachable, Is.EqualTo(new[] { INTRANET, WIKI }), "In the order they are offered, so that the function stays the same for the same data sources.");
        });
    }

    [Test]
    public void TheUserSelectionNamesOnlyTheSelectedUnreachableDataSources()
    {
        var options = new DataSourceOptions { AutomaticDataSourceSelection = false, PreselectedDataSourceIds = [HANDBOOK.Id, INTRANET.Id] };
        var offered = SemanticSearchTool.SelectOffered(options, Checked([HANDBOOK, MINUTES], [HANDBOOK], [INTRANET, WIKI]));

        Assert.Multiple(() =>
        {
            Assert.That(offered.Searchable, Is.EqualTo(new[] { HANDBOOK }));
            Assert.That(offered.Unreachable, Is.EqualTo(new[] { INTRANET }), "The model must not learn about a data source the user did not select for this chat.");
        });
    }

    [Test]
    public void AnUnreachableDataSourceWithoutMatchesIsNotNamed()
    {
        var silent = new DataSourceERI_V1 { Num = 5, Id = "55555555-5555-5555-5555-555555555555", Name = "Archive", MaxMatches = 0 };
        var offered = SemanticSearchTool.SelectOffered(new DataSourceOptions { AutomaticDataSourceSelection = true }, Checked([], [], [silent]));

        Assert.That(offered.IsEmpty, Is.True, "A data source configured to return no matches is not offered when it answers, either.");
    }

    [Test]
    public void OnlyUnreachableDataSourcesKeepTheToolOut()
    {
        var options = new DataSourceOptions { AutomaticDataSourceSelection = false, PreselectedDataSourceIds = [INTRANET.Id] };
        var offered = SemanticSearchTool.SelectOffered(options, Checked([HANDBOOK], [], [INTRANET]));

        Assert.Multiple(() =>
        {
            Assert.That(offered.IsSearchable, Is.False, "The model would search all the same, and every such call costs a round of the answer for nothing.");
            Assert.That(offered.Unreachable, Is.EqualTo(new[] { INTRANET }), "The model still has to learn what is missing, then through the system prompt.");
        });
    }

    [Test]
    public void ASearchOfAllDataSourcesReportsTheUnreachableOnes()
    {
        var offered = new OfferedDataSources([HANDBOOK, MINUTES], [INTRANET]);
        var request = new SemanticSearchRequest("travel expenses", [HANDBOOK, MINUTES], 1);

        Assert.That(SemanticSearchTool.GetUnreachableToReport(request, offered), Is.EqualTo(new[] { INTRANET }));
    }

    [Test]
    public void ASearchWithOnlyUnreachableDataSourcesReportsThem()
    {
        var offered = new OfferedDataSources([], [INTRANET]);
        var request = new SemanticSearchRequest("travel expenses", [], 1);

        Assert.That(SemanticSearchTool.GetUnreachableToReport(request, offered), Is.EqualTo(new[] { INTRANET }), "Otherwise, the model would answer as though the chat had no data sources.");
    }

    [Test]
    public void ASearchOfSomeDataSourcesLeavesTheUnreachableOnesOut()
    {
        var offered = new OfferedDataSources([HANDBOOK, MINUTES], [INTRANET]);
        var request = new SemanticSearchRequest("travel expenses", [HANDBOOK], 1);

        Assert.That(SemanticSearchTool.GetUnreachableToReport(request, offered), Is.Empty, "The model asked for the handbook alone.");
    }

    [Test]
    public void AFurtherPageLeavesTheUnreachableOnesOut()
    {
        var offered = new OfferedDataSources([HANDBOOK], [INTRANET]);
        var request = new SemanticSearchRequest("travel expenses", [HANDBOOK], 2);

        Assert.That(SemanticSearchTool.GetUnreachableToReport(request, offered), Is.Empty, "The first page reported them already.");
    }

    [Test]
    public void TheResultNamesAnUnreachableDataSourceWithoutItsId()
    {
        var result = SemanticSearchTool.DescribeUnreachableResult(INTRANET);

        Assert.Multiple(() =>
        {
            Assert.That(result.ContainsKey("id"), Is.False, "The model cannot search it, so an ID would only invite a call which is refused.");
            Assert.That(result["name"]!.GetValue<string>(), Is.EqualTo("Intranet"));
            Assert.That(result["result_count"]!.GetValue<int>(), Is.Zero);
            Assert.That(result["issues"]!.AsArray(), Has.Count.EqualTo(1), "A result count of zero alone reads as finding nothing.");
        });
    }

    private static AllowedSelectedDataSources Checked(IReadOnlyList<IDataSource> allowed, IReadOnlyList<IDataSource> selected, IReadOnlyList<IDataSource> unreachable) => new(allowed, selected, [], [], unreachable);
}