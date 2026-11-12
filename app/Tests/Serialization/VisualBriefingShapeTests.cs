using System.Text.Json;

using AIStudio.Assistants.VisualBriefing;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that the records of the Visual Briefing assistant write the same JSON as the anonymous shapes they replaced.
/// </summary>
/// <remarks>
/// The files in the folder Fixtures were written by the anonymous shapes themselves, before the records existed. The
/// content data and the protected data for the validation reach the HTML of a briefing, and the fingerprint of the
/// editor decides whether a briefing gets saved: a field under another name or in another place changes all three.
/// </remarks>
[TestFixture]
public sealed class VisualBriefingShapeTests
{
    [Test]
    public void TheContentDataGetsWrittenAsBefore()
    {
        var data = JsonSerializer.SerializeToElement(VisualBriefingContentData.Of(VisualBriefingSamples.Content()), VisualBriefingJson.Canonical);
        Assert.That(data.GetRawText(), Is.EqualTo(JsonGoldenFileTests.ReadFixture("visual-briefing-content-data.json")));
    }

    [Test]
    public void TheProtectedDataForTheValidationGetsWrittenAsBefore()
    {
        var evidence = Read<VisualBriefingEvidenceArtifact>("visual-briefing-evidence-artifact.json");
        var data = JsonSerializer.SerializeToElement(VisualBriefingProtectedData.ForValidation(evidence.AssetPlan), VisualBriefingJson.Canonical);
        Assert.That(data.GetRawText(), Is.EqualTo(JsonGoldenFileTests.ReadFixture("visual-briefing-validation-protected-data.json")));
    }

    [Test]
    public void TheFingerprintOfTheEditorGetsWrittenAsBefore()
    {
        var manifest = Read<VisualBriefingManifest>("visual-briefing-manifest.json");
        var fingerprint = new VisualBriefingPersistenceFingerprint(
            manifest.Name,
            manifest.Author,
            manifest.Settings,
            [.. manifest.Sources.Select(source => new VisualBriefingSourceFingerprint(source.Path, source.Kind))]);

        Assert.That(JsonSerializer.Serialize(fingerprint, VisualBriefingJson.Canonical), Is.EqualTo(JsonGoldenFileTests.ReadFixture("visual-briefing-persistence-fingerprint.json")));
    }

    private static T Read<T>(string fileName) => JsonSerializer.Deserialize<T>(JsonGoldenFileTests.ReadFixture(fileName), VisualBriefingJson.Persistence)!;
}