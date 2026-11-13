using AIStudio.Chat;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how the data a chat has seen tightens the providers which may continue it.
/// </summary>
/// <remarks>
/// A chat which once held data for self-hosted providers only must never be sent to any other
/// provider again, whatever it brings in afterwards. The RAG process and Semantic Search both go
/// through the same rule, so every combination of what a chat holds and what arrives is checked.
/// </remarks>
[TestFixture]
public sealed class ChatThreadDataSecurityTests
{
    [TestCase(DataSourceSecurity.NOT_SPECIFIED, DataSourceSecurity.SELF_HOSTED, DataSourceSecurity.SELF_HOSTED)]
    [TestCase(DataSourceSecurity.ALLOW_ANY, DataSourceSecurity.SELF_HOSTED, DataSourceSecurity.SELF_HOSTED)]
    [TestCase(DataSourceSecurity.SELF_HOSTED, DataSourceSecurity.SELF_HOSTED, DataSourceSecurity.SELF_HOSTED)]
    public void DataForSelfHostedProvidersOnlyRestrictsTheChat(DataSourceSecurity held, DataSourceSecurity arriving, DataSourceSecurity expected)
    {
        Assert.That(Tightened(held, arriving), Is.EqualTo(expected));
    }

    [TestCase(DataSourceSecurity.SELF_HOSTED, DataSourceSecurity.ALLOW_ANY)]
    [TestCase(DataSourceSecurity.SELF_HOSTED, DataSourceSecurity.NOT_SPECIFIED)]
    public void ARestrictionStays(DataSourceSecurity held, DataSourceSecurity arriving)
    {
        Assert.That(Tightened(held, arriving), Is.EqualTo(DataSourceSecurity.SELF_HOSTED), "The restricted data was seen by this chat. What arrives later cannot undo that.");
    }

    [TestCase(DataSourceSecurity.NOT_SPECIFIED)]
    [TestCase(DataSourceSecurity.ALLOW_ANY)]
    public void DataForAnyProviderMarksTheChat(DataSourceSecurity held)
    {
        Assert.That(Tightened(held, DataSourceSecurity.ALLOW_ANY), Is.EqualTo(DataSourceSecurity.ALLOW_ANY));
    }

    [TestCase(DataSourceSecurity.NOT_SPECIFIED)]
    [TestCase(DataSourceSecurity.ALLOW_ANY)]
    public void ResultsWhichDemandNothingChangeNothing(DataSourceSecurity held)
    {
        Assert.That(Tightened(held, DataSourceSecurity.NOT_SPECIFIED), Is.EqualTo(held), "A web search, say, says nothing about data sources and must leave the chat as it was.");
    }

    private static DataSourceSecurity Tightened(DataSourceSecurity held, DataSourceSecurity arriving)
    {
        var thread = new ChatThread { DataSecurity = held };
        thread.RequireDataSecurity(arriving);
        return thread.DataSecurity;
    }
}