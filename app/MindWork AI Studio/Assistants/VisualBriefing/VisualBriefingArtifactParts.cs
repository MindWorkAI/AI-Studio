using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains the parsed and validated protected sections of one standalone briefing artifact.
/// </summary>
/// <param name="ExportManifest">The embedded export manifest.</param>
/// <param name="Data">The complete declarative runtime data.</param>
/// <param name="TemplateHtml">The safe declarative HTML template.</param>
/// <param name="Css">The safe presentation stylesheet.</param>
/// <param name="RuntimeScript">The embedded AI Studio runtime.</param>
/// <param name="EChartsScript">The optional embedded Apache ECharts runtime.</param>
/// <param name="DocumentHash">The SHA-256 hash of the complete standalone document.</param>
public sealed record VisualBriefingArtifactParts(
    VisualBriefingExportManifest ExportManifest,
    JsonElement Data,
    string TemplateHtml,
    string Css,
    string RuntimeScript,
    string? EChartsScript,
    string DocumentHash);