using System.Text.Json.Serialization;

namespace AIStudio.Tools.Security;

/// <summary>
/// One passage the runtime identified as a prompt-injection attempt and filtered out.
/// </summary>
/// <remarks>
/// The property names are spelled out because the content stream is deserialized without a
/// naming policy, so the names have to match what the runtime sends verbatim.
/// </remarks>
public sealed record PromptInjectionFinding
{
    /// <summary>
    /// Which rule matched, e.g. "instruction_override".
    /// </summary>
    [JsonPropertyName("rule_id")]
    public string RuleId { get; init; } = string.Empty;

    /// <summary>
    /// The rule's family, e.g. "exfiltration".
    /// </summary>
    [JsonPropertyName("category")]
    public PromptInjectionFindingCategory Category { get; init; } = PromptInjectionFindingCategory.UNKNOWN;

    /// <summary>
    /// The passage as it appeared in the content, so the user can see what was removed.
    /// </summary>
    [JsonPropertyName("snippet")]
    public string Snippet { get; init; } = string.Empty;
}