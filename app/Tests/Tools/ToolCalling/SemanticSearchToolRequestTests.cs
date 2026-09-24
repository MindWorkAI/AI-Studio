using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Semantic Search reads the search a model asks for, and what it refuses.
/// </summary>
/// <remarks>
/// The model may only search the data sources offered to it, and it may only turn pages where
/// that means something: in one data source at a time, and not beyond the window the data sources
/// fetch at most. Every refusal says what would have been right, so that the model can correct
/// itself with its next call.
/// </remarks>
[TestFixture]
public sealed class SemanticSearchToolRequestTests
{
    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10 };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 2, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10 };
    private static readonly IReadOnlyList<IDataSource> OFFERED = [HANDBOOK, INTRANET];

    [Test]
    public void ASearchWithoutDataSourcesSearchesAllOfferedOnTheFirstPage()
    {
        var request = SemanticSearchTool.ReadRequest(Arguments("""{"query":"  travel expenses  "}"""), OFFERED);

        Assert.Multiple(() =>
        {
            Assert.That(request.Query, Is.EqualTo("travel expenses"));
            Assert.That(request.DataSources, Is.EqualTo(OFFERED));
            Assert.That(request.Page, Is.EqualTo(1));
        });
    }

    [Test]
    public void NamedDataSourcesAreSearchedInTheOrderOffered()
    {
        var request = SemanticSearchTool.ReadRequest(Arguments($$"""{"query":"travel expenses","data_source_ids":["{{INTRANET.Id}}","{{HANDBOOK.Id}}"]}"""), OFFERED);
        Assert.That(request.DataSources, Is.EqualTo(OFFERED));
    }

    [Test]
    public void ADataSourceNotOfferedIsRefusedWithTheOnesThatAre()
    {
        var message = Refusal(() => SemanticSearchTool.ReadRequest(Arguments("""{"query":"travel expenses","data_source_ids":["44444444-4444-4444-4444-444444444444"]}"""), OFFERED));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain(HANDBOOK.Id).And.Contain(INTRANET.Id), "A data source which dropped out since the request was prepared is refused the same way, so the model learns which ones are left.");
            Assert.That(message, Does.Contain("Leave it out to search all listed data sources."));
        });
    }

    [Test]
    public void ATooLongQueryIsRefused()
    {
        var message = Refusal(() => SemanticSearchTool.ReadRequest(Query(new string('x', 501)), OFFERED));
        Assert.That(message, Does.Contain("'query'").And.Contain("at most 500 characters").And.Contain("but had 501"));
    }

    [Test]
    public void AQueryOfSeveralLinesIsRefused()
    {
        var message = Refusal(() => SemanticSearchTool.ReadRequest(Query($"travel expenses{Environment.NewLine}hotels"), OFFERED));
        Assert.That(message, Does.Contain("'query'").And.Contain("single line"));
    }

    [Test]
    public void APageAfterTheFirstNeedsExactlyOneDataSource()
    {
        var message = Refusal(() => SemanticSearchTool.ReadRequest(Arguments("""{"query":"travel expenses","page":2}"""), OFFERED));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'page'").And.Contain("exactly one data source").And.Contain("but was 2 for 2"));
            Assert.That(message, Does.Contain("leave 'page' out"), "The way out when the model wanted the first page of each.");
        });
    }

    [Test]
    public void APageAfterTheFirstComesThroughForOneDataSource()
    {
        var named = SemanticSearchTool.ReadRequest(Arguments($$"""{"query":"travel expenses","data_source_ids":["{{INTRANET.Id}}"],"page":2}"""), OFFERED);
        var onlyOneOffered = SemanticSearchTool.ReadRequest(Arguments("""{"query":"travel expenses","page":2}"""), [HANDBOOK]);

        Assert.Multiple(() =>
        {
            Assert.That(named.DataSources, Is.EqualTo(new[] { INTRANET }));
            Assert.That(named.Page, Is.EqualTo(2));
            Assert.That(onlyOneOffered.Page, Is.EqualTo(2), "With a single data source offered, leaving it unnamed still means that one.");
        });
    }

    [Test]
    public void APageBeyondTheWindowIsRefusedWithTheLastPage()
    {
        var message = Refusal(() => SemanticSearchTool.ReadRequest(Arguments($$"""{"query":"travel expenses","data_source_ids":["{{HANDBOOK.Id}}"],"page":10}"""), OFFERED));
        Assert.That(message, Does.Contain("at most 9").And.Contain("but was 10").And.Contain("Rephrase the query"));
    }

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static JsonElement Query(string query) => JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["query"] = query });

    /// <summary>
    /// Reads a search which has to be refused and returns what the refusal said.
    /// </summary>
    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}