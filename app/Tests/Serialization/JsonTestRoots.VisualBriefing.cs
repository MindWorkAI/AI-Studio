using System.Text.Json;

using AIStudio.Assistants.VisualBriefing;

namespace AIStudio.Tests.Serialization;

public static partial class JsonTestRoots
{
    /// <summary>
    /// What the Visual Briefing assistant writes and reads again: the files of a briefing, and the canonical
    /// JSON which it parses back from an exported briefing.
    /// </summary>
    public static IEnumerable<TestCaseData> VisualBriefingStored()
    {
        TestHost.PrepareStaticState();
        var options = VisualBriefingJson.Persistence;

        yield return Case(new(typeof(VisualBriefingManifest), options));
        yield return Case(new(typeof(VisualBriefingEvidenceArtifact), options));
        yield return Case(new(typeof(VisualBriefingPlanArtifact), options));
        yield return Case(new(typeof(VisualBriefingContentArtifact), options));
        yield return Case(new(typeof(VisualBriefingPresentationArtifact), options));
        yield return Case(new(typeof(VisualBriefingBuildRecord), options));

        // The briefing selected last, which gets written without options of its own:
        yield return Case(new(typeof(Guid?), JsonSerializerOptions.Default), "selected briefing");

        //
        // The canonical JSON which gets read back: the header and the data of an exported briefing, the version
        // of AI Studio inside its runtime script, and the copy of a build which the progress view shows:
        //
        var canonical = VisualBriefingJson.Canonical;
        yield return Case(new(typeof(VisualBriefingExportManifest), canonical));
        yield return Case(new(typeof(Dictionary<string, JsonElement>), canonical), "data");
        yield return Case(new(typeof(string), canonical), "version");
        yield return Case(new(typeof(VisualBriefingBuildRecord), canonical), "progress");
    }

    /// <summary>
    /// What the Visual Briefing assistant writes as canonical JSON without reading it again: what goes into the
    /// hashes of a briefing, into the data of a briefing, and into the prompts of its stages.
    /// </summary>
    public static IEnumerable<TestCaseData> VisualBriefingHashed()
    {
        TestHost.PrepareStaticState();
        var options = VisualBriefingJson.Canonical;

        // The payload hashes of the artifacts:
        yield return Case(new(typeof(List<VisualBriefingEvidenceFact>), options));
        yield return Case(new(typeof(List<VisualBriefingEvidenceMetric>), options));
        yield return Case(new(typeof(List<VisualBriefingEvidenceTable>), options));
        yield return Case(new(typeof(List<VisualBriefingSourceCoverage>), options));
        yield return Case(new(typeof(List<VisualBriefingAssetPlanItem>), options));
        yield return Case(new(typeof(List<VisualBriefingPlanSection>), options));
        yield return Case(new(typeof(List<VisualBriefingSlotValue>), options));
        yield return Case(new(typeof(List<VisualBriefingChartSpec>), options));
        yield return Case(new(typeof(List<VisualBriefingControlSpec>), options));
        yield return Case(new(typeof(List<VisualBriefingFormulaSpec>), options));
        yield return Case(new(typeof(Dictionary<string, string>), options));
        yield return Case(new(typeof(Dictionary<string, List<string>>), options));
        yield return Case(new(typeof(VisualBriefingLayoutNode), options));

        // The data of a briefing, and the IDs the prompts list:
        yield return Case(new(typeof(JsonElement), options));
        yield return Case(new(typeof(string[]), options));

        // The position inside a repeated element, which the data bindings write without options of their own:
        yield return Case(new(typeof(int), JsonSerializerOptions.Default), "index");

        // The diagnostics a user copies to the clipboard:
        yield return Case(new(typeof(VisualBriefingOperationDiagnostics), VisualBriefingJson.Persistence));
    }

    /// <summary>
    /// The answers of the models which build a visual briefing.
    /// </summary>
    public static IEnumerable<TestCaseData> VisualBriefingAnswers()
    {
        TestHost.PrepareStaticState();
        var options = VisualBriefingJson.Canonical;

        yield return Case(new(typeof(VisualBriefingEvidenceResponse), options));
        yield return Case(new(typeof(VisualBriefingPlanResponse), options));
        yield return Case(new(typeof(VisualBriefingContentResponse), options));
        yield return Case(new(typeof(VisualBriefingDesignResponse), options));
    }
}