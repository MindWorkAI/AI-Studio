using System.Text.Json;
using System.Text.Json.Nodes;

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

        // The briefing selected last:
        yield return Case(new(typeof(Guid?), options), "selected briefing");

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

        // The data of a briefing, with what the compilers and the stages put into it:
        yield return Case(new(typeof(JsonElement), options));
        yield return Case(new(typeof(JsonObject), options));
        yield return Case(new(typeof(VisualBriefingCompiledData), options));
        yield return Case(new(typeof(VisualBriefingContentData), options));
        yield return Case(new(typeof(VisualBriefingInteractionData), options));
        yield return Case(new(typeof(VisualBriefingProtectedData), options));
        yield return Case(new(typeof(string), options), "value of a control");

        // The position inside a repeated element, which the data bindings write:
        yield return Case(new(typeof(int), options), "index");

        // What the prompts of the stages carry:
        yield return Case(new(typeof(IEnumerable<VisualBriefingSourcePrompt>), options));
        yield return Case(new(typeof(string[]), options));
        yield return Case(new(typeof(VisualBriefingRequiredSlot[]), options));
        yield return Case(new(typeof(VisualBriefingControlRequirement[]), options));
        yield return Case(new(typeof(VisualBriefingEvidencePrompt), options));
        yield return Case(new(typeof(VisualBriefingParentDesign), options));

        // The fingerprint which decides whether the editor holds unsaved changes:
        yield return Case(new(typeof(VisualBriefingPersistenceFingerprint), options));

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