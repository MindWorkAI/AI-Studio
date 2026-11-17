using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the files of visual briefings, built at compile time.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(VisualBriefingManifest))]
[JsonSerializable(typeof(VisualBriefingEvidenceArtifact))]
[JsonSerializable(typeof(VisualBriefingPlanArtifact))]
[JsonSerializable(typeof(VisualBriefingContentArtifact))]
[JsonSerializable(typeof(VisualBriefingPresentationArtifact))]
[JsonSerializable(typeof(VisualBriefingBuildRecord))]
[JsonSerializable(typeof(VisualBriefingOperationDiagnostics))]

// The briefing selected last:
[JsonSerializable(typeof(Guid?))]
internal sealed partial class VisualBriefingPersistenceJsonContext : JsonSerializerContext;