using System.Reflection;
using System.Text.Json;

using AIStudio.Assistants.VisualBriefing;
using AIStudio.Tools.Metadata;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that the stages of the Visual Briefing assistant send their models the same JSON as before, and that an
/// exported briefing gets the same protected data.
/// </summary>
/// <remarks>
/// The files in the folder Fixtures were written with the contracts of reflection, before AI Studio got
/// contracts of its own. A model reads the JSON of a prompt as it is, so a field under another name or one which
/// went missing changes what it builds. The protected data of an exported briefing is checked when the briefing
/// gets imported again. The fixture of the protected data names the version of AI Studio as a placeholder,
/// because every release changes it.
/// </remarks>
[TestFixture]
public sealed class VisualBriefingPromptTests
{
    private const string VERSION_PLACEHOLDER = "__VERSION__";

    private static IEnumerable<TestCaseData> Prompts()
    {
        yield return Case("visual-briefing-evidence-prompt.txt", () =>
        {
            var manifest = Manifest();
            return VisualBriefingEvidenceStage.BuildPrompt(manifest, VisualBriefingSamples.PreparedSources(manifest));
        });

        yield return Case("visual-briefing-plan-prompt.txt", () => VisualBriefingPlanStage.BuildPrompt(Manifest(), Evidence()));
        yield return Case("visual-briefing-content-prompt.txt", () => VisualBriefingContentStage.BuildPrompt(Manifest(), Evidence(), VisualBriefingSamples.Plan()));
        yield return Case("visual-briefing-presentation-prompt.txt", () => VisualBriefingPresentationStage.BuildPrompt(Manifest(), VisualBriefingSamples.Plan(), Presentation()));
    }

    [TestCaseSource(nameof(Prompts))]
    public void APromptCarriesTheSameJsonAsBefore(string fileName, Func<string> buildPrompt) => Assert.That(buildPrompt(), Is.EqualTo(JsonGoldenFileTests.ReadFixture(fileName)));

    [Test]
    public void AnExportedBriefingGetsTheSameProtectedData()
    {
        var manifest = Manifest();
        var data = VisualBriefingArtifactService.AddProtectedArtifactData(manifest, VisualBriefingSamples.RevisionRequest(manifest));
        var version = typeof(VisualBriefingArtifactService).Assembly.GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown";

        Assert.That(data.GetRawText(), Is.EqualTo(JsonGoldenFileTests.ReadFixture("visual-briefing-protected-data.json").Replace(VERSION_PLACEHOLDER, version, StringComparison.Ordinal)));
    }

    private static TestCaseData Case(string fileName, Func<string> buildPrompt) => new TestCaseData(fileName, buildPrompt).SetArgDisplayNames(fileName);

    private static VisualBriefingManifest Manifest() => Read<VisualBriefingManifest>("visual-briefing-manifest.json");

    private static VisualBriefingEvidenceArtifact Evidence() => Read<VisualBriefingEvidenceArtifact>("visual-briefing-evidence-artifact.json");

    private static VisualBriefingPresentationArtifact Presentation() => Read<VisualBriefingPresentationArtifact>("visual-briefing-presentation-artifact.json");

    private static T Read<T>(string fileName) => JsonSerializer.Deserialize<T>(JsonGoldenFileTests.ReadFixture(fileName), VisualBriefingJson.Persistence)!;
}