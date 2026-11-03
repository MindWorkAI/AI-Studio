using System.Text.Json;

using AIStudio.Assistants.VisualBriefing;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that the Visual Briefing assistant computes the same hashes and compiles the same data as before.
/// </summary>
/// <remarks>
/// A stored briefing carries the hashes of its artifacts, and an exported one the hash of its document. The
/// assistant computes them anew from the canonical JSON of the same values and discards whatever no longer
/// matches, so a byte written otherwise makes every stored briefing fail its check. The expected hashes were
/// computed from the files in the folder Fixtures with the contracts of reflection, before AI Studio got
/// contracts of its own.
/// </remarks>
[TestFixture]
public sealed class VisualBriefingHashTests
{
    private static IEnumerable<TestCaseData> StoredHashes()
    {
        yield return Case("evidence", "aed455ba77468d47c30aba31a744df35639978c3d56fe30469b298aa34918752", () =>
        {
            var evidence = Read<VisualBriefingEvidenceArtifact>("visual-briefing-evidence-artifact.json", VisualBriefingJson.Persistence);
            return VisualBriefingPayloadHash.ForEvidence(evidence.Facts, evidence.Metrics, evidence.Tables, evidence.SourceCoverage, evidence.AssetPlan);
        });

        yield return Case("plan", "311c5e8c1202a0f473a9f615c3aa3bc8f1015f8a37859b7addfcab879fa23f0d", () =>
        {
            var plan = Read<VisualBriefingPlanArtifact>("visual-briefing-plan-artifact.json", VisualBriefingJson.Persistence);
            return VisualBriefingPayloadHash.ForPlan(plan.Sections, plan.StructuralSignature);
        });

        yield return Case("content", "1cc333a62da8a14d76c37b7eed0a1ece71b5cb48b914cde3fed1a55bf39950c2", () =>
        {
            var content = Read<VisualBriefingContentArtifact>("visual-briefing-content-artifact.json", VisualBriefingJson.Persistence);
            return VisualBriefingPayloadHash.ForContent(content.Slots, content.Charts, content.Controls, content.Formulas, content.AccessibilityTexts,
                content.SourceReferences, content.ResetLabel, content.SourceCoverage, content.AssetPlan, content.StructuralSignature);
        });

        yield return Case("presentation", "96fc7f856c4edfba2b823b83e5cc3b09b8939124d6c5a70d07669cf61219ac3e", () =>
        {
            var presentation = Read<VisualBriefingPresentationArtifact>("visual-briefing-presentation-artifact.json", VisualBriefingJson.Persistence);
            return VisualBriefingPayloadHash.ForPresentation(presentation.Layout, presentation.Profile, presentation.TemplateHash, presentation.CssHash);
        });

        yield return Case("header of an export", "a838f56ba91facf5186ecb63e790ed9a9419ad73c2669fd8bfed97bd8c876f99", () =>
        {
            var header = Read<VisualBriefingExportManifest>("visual-briefing-export-manifest.json", VisualBriefingJson.Canonical);
            return VisualBriefingHashing.Compute(VisualBriefingHashing.CanonicalJson(header));
        });
    }

    [TestCaseSource(nameof(StoredHashes))]
    public void AStoredBriefingGetsTheSameHash(Func<string> computeHash, string expectedHash) => Assert.That(computeHash(), Is.EqualTo(expectedHash));

    [Test]
    public void TheDataOfABriefingGetsCompiledAsBefore()
    {
        var compiled = VisualBriefingLayoutCompiler.Compile(VisualBriefingSamples.Plan(), VisualBriefingSamples.Content(), VisualBriefingSamples.Layout(), VisualBriefingDesignProfile.EDITORIAL);
        Assert.That(compiled.Data.GetRawText(), Is.EqualTo(JsonGoldenFileTests.ReadFixture("visual-briefing-compiled-data.json")));
    }

    private static TestCaseData Case(string name, string expectedHash, Func<string> computeHash) => new TestCaseData(computeHash, expectedHash).SetArgDisplayNames(name);

    private static T Read<T>(string fileName, JsonSerializerOptions options) => JsonSerializer.Deserialize<T>(JsonGoldenFileTests.ReadFixture(fileName), options)!;
}