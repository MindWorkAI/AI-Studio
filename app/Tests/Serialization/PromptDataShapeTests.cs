using System.Text.Json;

using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that the Assistant Builder and the moderation of a SWOT analysis send their models the same JSON as before.
/// </summary>
/// <remarks>
/// The files in the folder Fixtures were written by the anonymous shapes which the records replaced. A model reads the
/// JSON of a prompt as it is, so a field under another name or one which went missing changes what it does. Both
/// write their JSON indented, with the line breaks of the OS, so the comparison ignores which line breaks they are.
/// </remarks>
[TestFixture]
public sealed class PromptDataShapeTests
{
    private static IEnumerable<TestCaseData> AssistantBuilderData()
    {
        yield return Case("assistant-builder-draft-data.json", () => Serialize(new AssistantBuilderDraftPromptData(
            PromptDataSamples.DESCRIPTION,
            null,
            PromptDataSamples.TITLE,
            null,
            PromptDataSamples.EXPECTED_OUTPUT,
            null,
            PromptDataSamples.OUTPUT_LANGUAGE,
            true,
            null,
            PromptDataSamples.EXAMPLE_REQUEST,
            null)));

        yield return Case("assistant-builder-generation-data.json", () => Serialize(new AssistantBuilderGenerationPromptData(PromptDataSamples.DRAFT, null)));
        yield return Case("assistant-builder-launcher-data.json", () => Serialize(new AssistantBuilderLauncherPromptData(PromptDataSamples.DRAFT, PromptDataSamples.REVIEW_NOTES, PromptDataSamples.ChatLaunch())));
        yield return Case("assistant-builder-revision-data.json", () => Serialize(new AssistantBuilderRevisionPromptData(
            PromptDataSamples.PLUGIN_ID,
            PromptDataSamples.PLUGIN_NAME,
            PromptDataSamples.TITLE,
            PromptDataSamples.CHANGE_REQUEST,
            null)));
    }

    [TestCaseSource(nameof(AssistantBuilderData))]
    public void TheAssistantBuilderSendsTheSameJsonAsBefore(string fileName, Func<string> serialize) => Assert.That(Normalized(serialize()), Is.EqualTo(Normalized(JsonGoldenFileTests.ReadFixture(fileName))));

    [Test]
    public void TheModeratorOfASwotAnalysisGetsTheSameRequestAsBefore()
    {
        var request = SwotAnalysisModeration.BuildUserRequest(PromptDataSamples.SwotCategories(), PromptDataSamples.SWOT_GOAL, string.Empty, PromptDataSamples.SWOT_RESEARCH);
        Assert.That(Normalized(request), Is.EqualTo(Normalized(JsonGoldenFileTests.ReadFixture("swot-moderation-request.txt"))));
    }

    private static TestCaseData Case(string fileName, Func<string> serialize) => new TestCaseData(fileName, serialize).SetArgDisplayNames(fileName);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, AssistantPluginGenerationService.UNTRUSTED_PROMPT_JSON_OPTIONS);

    private static string Normalized(string text) => text.ReplaceLineEndings("\n");
}