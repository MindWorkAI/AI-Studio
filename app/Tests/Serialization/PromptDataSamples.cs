using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Synthetic inputs for the JSON which the Assistant Builder and the SWOT analysis put into their prompts.
/// </summary>
/// <remarks>
/// The texts carry umlauts, quotes, and angle brackets, because the encoder of the options escapes them. The values of
/// the Assistant Builder are the ones its prompts get after trimming: a field the user left empty is null.
/// </remarks>
internal static class PromptDataSamples
{
    internal const string DESCRIPTION = "Summarizes <meeting notes> of any Größe.";

    internal const string TITLE = "Meeting \"Notes\" Summary";

    internal const string EXPECTED_OUTPUT = "A short summary & a list of decisions.";

    internal const string OUTPUT_LANGUAGE = "German";

    internal const string EXAMPLE_REQUEST = "Summarize the notes of Monday's meeting.";

    internal const string DRAFT = "# Assistant Draft\n## Name\nMeeting \"Notes\" Summary\n## Description\nSummarizes <notes> of any Größe.";

    internal const string REVIEW_NOTES = "Keep the title short.";

    internal const string PLUGIN_NAME = "Meeting Notes";

    internal const string CHANGE_REQUEST = "Add a <tone> dropdown with „formal“ and „casual“.";

    internal const string SWOT_GOAL = "Decide whether the company should grow to a new Größe.";

    internal const string SWOT_RESEARCH = "The market grew by 8 % in the \"last\" year.";

    internal static readonly Guid PLUGIN_ID = Guid.Parse("6f1c3a52-9d4e-4b8a-a2f7-0c5e81d94b36");

    internal static AssistantBuilderChatLaunchRequest ChatLaunch() => new(
        "Workspace <Größe>",
        "provider id sample",
        Guid.Empty.ToString(),
        null,
        ["data source 1", "data source 2"],
        null);

    internal static IReadOnlyDictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> SwotCategories() => new Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult>
    {
        [SwotAnalysisCategory.STRENGTHS] = Category(string.Empty, Finding("A loyal \"core\" team", "The team stayed together for years."), Finding("Low costs", "Costs are <10 %> below the market.")),
        [SwotAnalysisCategory.WEAKNESSES] = Category(string.Empty, Finding("Small Größe", "The company has only twelve employees.")),
        [SwotAnalysisCategory.OPPORTUNITIES] = Category("The sources name no external opportunities."),
        [SwotAnalysisCategory.THREATS] = Category(string.Empty, Finding("New competitors", "Two competitors entered the market.")),
    };

    private static SwotCategoryAnalysisResult Category(string emptyMessage, params List<SwotFinding> findings) => new()
    {
        EmptyMessage = emptyMessage,
        Findings = findings,
    };

    private static SwotFinding Finding(string summary, string explanation) => new()
    {
        Summary = summary,
        Explanation = explanation,
    };
}