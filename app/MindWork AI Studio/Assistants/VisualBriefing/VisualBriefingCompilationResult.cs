using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains deterministic compiler output before standalone artifact assembly.
/// </summary>
/// <param name="Data">The compiled declarative runtime data.</param>
/// <param name="TemplateHtml">The compiled safe HTML template.</param>
/// <param name="Css">The compiled safe stylesheet.</param>
/// <param name="TemplateHash">The deterministic template hash.</param>
/// <param name="CssHash">The deterministic stylesheet hash.</param>
public sealed record VisualBriefingCompilationResult(
    JsonElement Data,
    string TemplateHtml,
    string Css,
    string TemplateHash,
    string CssHash);