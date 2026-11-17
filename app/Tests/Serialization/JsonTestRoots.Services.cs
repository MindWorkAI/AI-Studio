using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

namespace AIStudio.Tests.Serialization;

public static partial class JsonTestRoots
{
    /// <summary>
    /// What AI Studio sends to ERI servers and to search services.
    /// </summary>
    public static IEnumerable<TestCaseData> ServiceRequests()
    {
        TestHost.PrepareStaticState();
        yield return Case(new(typeof(RetrievalRequest), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(StaanSearchRequest), WebSearchJson.OPTIONS));
        yield return Case(new(typeof(TavilySearchRequest), WebSearchJson.OPTIONS));
    }

    /// <summary>
    /// What AI Studio receives from ERI servers and from search services.
    /// </summary>
    public static IEnumerable<TestCaseData> ServiceAnswers()
    {
        TestHost.PrepareStaticState();
        yield return Case(new(typeof(List<AuthScheme>), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(AuthResponse), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(DataSourceInfo), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(List<EmbeddingInfo>), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(List<RetrievalInfo>), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(List<Context>), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(SecurityRequirements), ERIClientBase.JSON_OPTIONS));
        yield return Case(new(typeof(StaanSearchResponse), WebSearchJson.OPTIONS));
        yield return Case(new(typeof(TavilySearchResponse), WebSearchJson.OPTIONS));
    }
}