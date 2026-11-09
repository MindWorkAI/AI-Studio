using AIStudio.Agents;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Assistants.Builder;
using AIStudio.Assistants.ERI;
using AIStudio.Assistants.PromptOptimizer;
using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.PowerPoint;
using AIStudio.Tools.Services;

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

        // The segments of a document, e.g., translated by the Translation assistant:
        yield return Case(new(typeof(SegmentBatch), SegmentJson.ANSWER_OPTIONS));
    }

    /// <summary>
    /// The JSON which the Assistant Builder, the moderation of a SWOT analysis, and the work on the segments of a
    /// document put into their prompts.
    /// </summary>
    public static IEnumerable<TestCaseData> ModelPrompts()
    {
        TestHost.PrepareStaticState();

        var untrusted = AssistantPluginGenerationService.UNTRUSTED_PROMPT_JSON_OPTIONS;
        yield return Case(new(typeof(AssistantBuilderDraftPromptData), untrusted));
        yield return Case(new(typeof(AssistantBuilderGenerationPromptData), untrusted));
        yield return Case(new(typeof(AssistantBuilderLauncherPromptData), untrusted));
        yield return Case(new(typeof(AssistantBuilderRevisionPromptData), untrusted));

        yield return Case(new(typeof(Dictionary<string, SwotModerationCategoryInput>), SwotAnalysisModeration.JSON_OPTIONS));

        // The segments of a document, which a model works on:
        yield return Case(new(typeof(SegmentBatch), SegmentJson.PROMPT_OPTIONS));
    }
}