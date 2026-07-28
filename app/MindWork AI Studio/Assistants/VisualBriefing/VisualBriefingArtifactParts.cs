using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingArtifactParts</c> for the visual briefing feature.
/// </summary>
public sealed record VisualBriefingArtifactParts(
    VisualBriefingExportManifest ExportManifest,
    JsonElement Data,
    string TemplateHtml,
    string Css,
    string RuntimeScript,
    string? EChartsScript,
    string PayloadHash);
