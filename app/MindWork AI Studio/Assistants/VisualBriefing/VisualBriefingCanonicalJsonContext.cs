using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The contracts with which System.Text.Json writes and reads the canonical JSON of visual briefings, built at
/// compile time.
/// </summary>
/// <remarks>
/// Much of what these contracts write goes into the hashes of stored briefings, see VisualBriefingJson.Canonical.
/// The hashes of the artifacts are generic: every list they hash needs a contract of its own. A test fails for a type
/// which is missing here.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]

// The header and the data of an exported briefing, and the build which the progress view shows:
[JsonSerializable(typeof(VisualBriefingExportManifest))]
[JsonSerializable(typeof(VisualBriefingBuildRecord))]
[JsonSerializable(typeof(VisualBriefingCompiledData))]
[JsonSerializable(typeof(VisualBriefingContentData))]
[JsonSerializable(typeof(VisualBriefingInteractionData))]
[JsonSerializable(typeof(VisualBriefingProtectedData))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]

// The payload hashes of the artifacts:
[JsonSerializable(typeof(List<VisualBriefingEvidenceFact>))]
[JsonSerializable(typeof(List<VisualBriefingEvidenceMetric>))]
[JsonSerializable(typeof(List<VisualBriefingEvidenceTable>))]
[JsonSerializable(typeof(List<VisualBriefingSourceCoverage>))]
[JsonSerializable(typeof(List<VisualBriefingAssetPlanItem>))]
[JsonSerializable(typeof(List<VisualBriefingPlanSection>))]
[JsonSerializable(typeof(List<VisualBriefingSlotValue>))]
[JsonSerializable(typeof(List<VisualBriefingChartSpec>))]
[JsonSerializable(typeof(List<VisualBriefingControlSpec>))]
[JsonSerializable(typeof(List<VisualBriefingFormulaSpec>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
[JsonSerializable(typeof(VisualBriefingLayoutNode))]

// The prompts of the stages:
[JsonSerializable(typeof(IEnumerable<VisualBriefingSourcePrompt>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(VisualBriefingRequiredSlot[]))]
[JsonSerializable(typeof(VisualBriefingControlRequirement[]))]
[JsonSerializable(typeof(VisualBriefingEvidencePrompt))]
[JsonSerializable(typeof(VisualBriefingParentDesign))]

// The answers of the models:
[JsonSerializable(typeof(VisualBriefingEvidenceResponse))]
[JsonSerializable(typeof(VisualBriefingPlanResponse))]
[JsonSerializable(typeof(VisualBriefingContentResponse))]
[JsonSerializable(typeof(VisualBriefingDesignResponse))]

// The fingerprint which decides whether the editor holds unsaved changes:
[JsonSerializable(typeof(VisualBriefingPersistenceFingerprint))]
internal sealed partial class VisualBriefingCanonicalJsonContext : JsonSerializerContext;