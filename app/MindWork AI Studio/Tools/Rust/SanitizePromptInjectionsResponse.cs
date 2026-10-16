using System.Text.Json.Serialization;

using AIStudio.Tools.Security;

namespace AIStudio.Tools.Rust;

/// <param name="SanitizedText">The content with the suspicious passages removed. Usable as it stands.</param>
/// <param name="Findings">The passages that were removed, capped by the runtime.</param>
/// <param name="RedactedCount">How many passages were removed in total, which may exceed the number of findings.</param>
public readonly record struct SanitizePromptInjectionsResponse(
    [property: JsonPropertyName("sanitized_text")] string SanitizedText,
    [property: JsonPropertyName("findings")] IReadOnlyList<PromptInjectionFinding> Findings,
    [property: JsonPropertyName("redacted_count")] int RedactedCount);