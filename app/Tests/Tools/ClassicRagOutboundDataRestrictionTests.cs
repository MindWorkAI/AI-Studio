using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.ToolCalling;
using AIStudio.Tools.RAG.RAGProcesses;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks where a chat may still send data once the classic RAG process found something in its data sources.
/// </summary>
/// <remarks>
/// The classic process brings passages into the chat without any tool. Afterwards, a web search
/// or an address the model writes itself could carry them to a third party, just as after Semantic
/// Search. Both ways have to demand the same of the same data sources, or one of them stays open.
/// The process runs before the request puts its tools together, so the restriction it sets already
/// holds for the answer it prepares.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ClassicRagOutboundDataRestrictionTests : ToolRegistryTestBase
{
    private const string WEB_SEARCH = "web_search_test";

    private static readonly IDataSource PROJECT_FILES = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Project files", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.MEDIUM };
    private static readonly IDataSource MINUTES = new DataSourceLocalFile { Num = 2, Id = "22222222-2222-2222-2222-222222222222", Name = "Minutes", MaxMatches = 10, ConfidenceLevel = ConfidenceLevel.LOW };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 3, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10, SecurityPolicy = DataSourceSecurity.SELF_HOSTED, OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED };
    private static readonly IDataSource PRESS_RELEASES = new DataSourceLocalFile { Num = 4, Id = "44444444-4444-4444-4444-444444444444", Name = "Press releases", MaxMatches = 10, OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED };

    private static IEnumerable<TestCaseData> Searches
    {
        get
        {
            yield return new TestCaseData(new[] { PROJECT_FILES, MINUTES }, new[] { 0, 0 }, OutboundDataRestriction.UNRESTRICTED).SetName("Nothing found");
            yield return new TestCaseData(new[] { PROJECT_FILES, PRESS_RELEASES }, new[] { 0, 3 }, OutboundDataRestriction.UNRESTRICTED).SetName("Found only content anybody may read");
            yield return new TestCaseData(new[] { PROJECT_FILES, MINUTES }, new[] { 2, 1 }, OutboundDataRestriction.UNRESTRICTED).SetName("Two equally strict data sources");
            yield return new TestCaseData(new[] { PRESS_RELEASES, INTRANET, PROJECT_FILES }, new[] { 1, 1, 1 }, OutboundDataRestriction.UNRESTRICTED).SetName("A data source for self-hosted providers only");
            yield return new TestCaseData(new[] { PRESS_RELEASES, MINUTES }, new[] { 2, 0 }, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES).SetName("A minimum of the organization");
        }
    }

    [Test]
    public void ARetrievalWhichFoundNothingLeavesTheChatAsItWas()
    {
        var chat = new ChatThread();
        chat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.CONFLUENCE_WIKI, string.Empty));

        AISrcSelWithRetCtxVal.RequireOutboundDataRestriction(chat, [PROJECT_FILES, INTRANET], [0, 0], OutboundDataRestriction.UNRESTRICTED);

        Assert.That(chat.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.CONFLUENCE_WIKI, string.Empty)), "Neither data source brought anything into the chat.");
    }

    [Test]
    public void OnlyTheDataSourcesWithPassagesRestrictTheChat()
    {
        var chat = new ChatThread();

        AISrcSelWithRetCtxVal.RequireOutboundDataRestriction(chat, [INTRANET, MINUTES], [0, 4], OutboundDataRestriction.UNRESTRICTED);

        Assert.That(chat.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, MINUTES.Id)), "The intranet found nothing, so it demands nothing either.");
    }

    [TestCaseSource(nameof(Searches))]
    public void BothWaysOfSearchingRestrictTheChatAlike(IDataSource[] dataSources, int[] passageCounts, OutboundDataRestriction organizationMinimum)
    {
        //
        // Semantic Search offers the data sources in a fixed order, while the selection agent of the
        // classic process may return them in any order. Both have to name the same data source:
        //
        var classicChat = new ChatThread();
        AISrcSelWithRetCtxVal.RequireOutboundDataRestriction(classicChat, dataSources.Reverse().ToArray(), passageCounts.Reverse().ToArray(), organizationMinimum);

        var semanticSearchChat = new ChatThread();
        semanticSearchChat.RequireOutboundDataRestriction(SemanticSearchTool.GetRequirements(dataSources, passageCounts, organizationMinimum).OutboundData);

        Assert.That(classicChat.RequiredOutboundDataRestriction, Is.EqualTo(semanticSearchChat.RequiredOutboundDataRestriction));
    }

    [Test]
    public async Task TheAnswerWhoseRetrievalRestrictedTheChatIsNotOfferedTheWebSearch()
    {
        var webSearch = new TestTool(Definition(WEB_SEARCH)) { OutboundData = ToolOutboundData.THIRD_PARTY_QUERIES };
        var registry = this.CreateRegistry(webSearch);

        //
        // The chat puts the tools of the answer together before the classic process runs, while
        // the chat is still unrestricted. Then the process finds something:
        //
        var chat = new ChatThread { RuntimeSelectedToolIds = [WEB_SEARCH] };
        AISrcSelWithRetCtxVal.RequireOutboundDataRestriction(chat, [PROJECT_FILES], [2], OutboundDataRestriction.UNRESTRICTED);

        var provider = ToolCapableProvider();
        var context = new ToolResolutionContext
        {
            Provider = provider,
            Component = AIStudio.Tools.Components.CHAT,
            ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
            ChatThread = chat,
        };
        var runnableTools = await registry.GetRunnableToolsAsync(context, chat.RuntimeSelectedToolIds, mayRunTools: true);

        Assert.That(runnableTools.Select(tool => tool.Definition.Id), Does.Not.Contain(WEB_SEARCH), "The request reads the restriction when it puts its tools together, after the classic process.");
    }
}