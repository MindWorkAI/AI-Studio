using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Semantic Search describes the data sources it offers in a request.
/// </summary>
/// <remarks>
/// The model learns from the description which data sources there are, and the schema lets it
/// name exactly those. Both have to hold the same data sources, and nothing else: a data source
/// the provider may not search must not even be named. The function also has to come out the same
/// whenever the data sources are the same, because the providers cache a request from its
/// beginning, and the tools are part of that beginning.
/// </remarks>
[TestFixture]
public sealed class SemanticSearchToolDescriptionTests
{
    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10 };
    private static readonly IDataSource MINUTES = new DataSourceLocalFile { Num = 1, Id = "22222222-2222-2222-2222-222222222222", Name = "Minutes", MaxMatches = 20 };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 2, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10 };

    [Test]
    public void TheFunctionOffersExactlyTheDataSourcesGiven()
    {
        var function = Describe((HANDBOOK, "Our processes."), (INTRANET, string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(function.DescriptionForLLM, Does.Contain($"id={HANDBOOK.Id}, name='Handbook', type=local folder, results per page=10, last page=9, description='Our processes.'"));
            Assert.That(function.DescriptionForLLM, Does.Contain($"id={INTRANET.Id}, name='Intranet', type=external data source, results per page=10, last page=9"));
            Assert.That(function.DescriptionForLLM, Does.Not.Contain(MINUTES.Id).And.Not.Contain("Minutes"), "A data source which is not offered must not even be named.");
            Assert.That(OfferedIds(function), Is.EqualTo(new[] { HANDBOOK.Id, INTRANET.Id }), "The schema lets the model name exactly the data sources the description lists.");
        });
    }

    [Test]
    public void ADataSourceWithoutDescriptionGetsNoEmptyOne()
    {
        var function = Describe((INTRANET, "  "));
        Assert.That(function.DescriptionForLLM, Does.Not.Contain("description="));
    }

    [Test]
    public void TheSameDataSourcesAlwaysComeOutTheSame()
    {
        var descriptions = new Dictionary<string, string> { [HANDBOOK.Id] = "Our processes.", [MINUTES.Id] = "Meetings.", [INTRANET.Id] = "Everything else." };
        ToolFunctionDefinition DescribeInOfferOrder(params IDataSource[] dataSources) =>
            Describe(SemanticSearchTool.InOfferOrder(dataSources).Select(dataSource => (dataSource, descriptions[dataSource.Id])).ToArray());

        var first = DescribeInOfferOrder(INTRANET, MINUTES, HANDBOOK);
        var second = DescribeInOfferOrder(MINUTES, HANDBOOK, INTRANET);

        Assert.Multiple(() =>
        {
            Assert.That(OfferedIds(first), Is.EqualTo(new[] { HANDBOOK.Id, MINUTES.Id, INTRANET.Id }), "By number first, then by ID.");
            Assert.That(second.DescriptionForLLM, Is.EqualTo(first.DescriptionForLLM));
            Assert.That(second.Parameters.GetRawText(), Is.EqualTo(first.Parameters.GetRawText()));
        });
    }

    [Test]
    public void ALongDescriptionIsShortened()
    {
        var function = Describe((INTRANET, new string('x', 2000)));

        Assert.Multiple(() =>
        {
            Assert.That(function.DescriptionForLLM, Does.Contain($"description='{new string('x', 500)}...'"));
            Assert.That(function.DescriptionForLLM, Does.Not.Contain(new string('x', 501)), "The server of an ERI data source writes this, and the model reads it with every request.");
        });
    }

    [Test]
    public void AShortenedDescriptionKeepsItsCharactersWhole()
    {
        //
        // An emoji takes two chars. Cut between them, what is left is no valid text anymore, and a
        // JSON writer refuses it -- the whole request would fail:
        //
        var emoji = char.ConvertFromUtf32(0x1F600);
        var function = Describe((INTRANET, $"{new string('x', 499)}{emoji}{new string('x', 100)}"));

        Assert.That(function.DescriptionForLLM, Does.Contain($"description='{new string('x', 499)}...'"));
    }

    private static ToolFunctionDefinition Describe(params (IDataSource DataSource, string Description)[] dataSources)
    {
        // Stating its definition needs none of the services the tool searches with:
        var registered = new SemanticSearchTool(null!, null!, null!).GetDefinition().Function;
        return SemanticSearchTool.DescribeDataSources(registered, dataSources);
    }

    private static IReadOnlyList<string?> OfferedIds(ToolFunctionDefinition function) => function.Parameters
        .GetProperty("properties")
        .GetProperty("data_source_ids")
        .GetProperty("items")
        .GetProperty("enum")
        .EnumerateArray()
        .Select(id => id.GetString())
        .ToList();
}