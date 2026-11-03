using AIStudio.Agents;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Assistants.Builder;
using AIStudio.Assistants.ERI;
using AIStudio.Assistants.PromptOptimizer;
using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Serialization;

public static partial class JsonTestRoots
{
    /// <summary>
    /// The JSON which models write for the agents and assistants of AI Studio, apart from the Visual Briefing
    /// assistant.
    /// </summary>
    public static IEnumerable<TestCaseData> ModelAnswers()
    {
        TestHost.PrepareStaticState();

        // The agents, which select data sources, check retrieved passages, and audit assistant plugins:
        yield return Case(new(typeof(List<SelectedDataSource>), AgentBase.JSON_SERIALIZER_OPTIONS));
        yield return Case(new(typeof(RetrievalContextValidationResult), AgentBase.JSON_SERIALIZER_OPTIONS));
        yield return Case(new(typeof(AssistantAuditResult), AgentBase.JSON_SERIALIZER_OPTIONS));

        // The Assistant Builder, for assistant plugins and for direct chat launchers:
        yield return Case(new(typeof(LuaResponse), LuaResponse.JSON_OPTIONS));
        yield return Case(new(typeof(LauncherTextsResponse), LauncherTextsResponse.JSON_OPTIONS));

        yield return Case(new(typeof(PromptOptimizationResult), AssistantPromptOptimizer.JSON_OPTIONS));
        yield return Case(new(typeof(SwotCategoryAnalysisResult), SwotModelAnswer.JSON_OPTIONS));
        yield return Case(new(typeof(SwotModerationResult), SwotModelAnswer.JSON_OPTIONS));
        yield return Case(new(typeof(ERIServerFileList), ERIServerFileList.JSON_OPTIONS));
        yield return Case(new(typeof(SlideDeckSpec.DeckDto), SlideDeckSpec.JSON_OPTIONS));
    }
}