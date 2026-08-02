using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingFormulaNode</c> for the visual briefing feature.
/// </summary>
[CanonicalJsonShape("aa29e015")]
public sealed class VisualBriefingFormulaNode
{
    /// <summary>
    /// Defines <c>FormulaVersion</c> for the visual briefing feature.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FormulaVersion { get; set; }

    /// <summary>
    /// Defines <c>Operation</c> for the visual briefing feature.
    /// </summary>
    [JsonPropertyName("op")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Operation { get; set; }

    /// <summary>
    /// Defines <c>Path</c> for the visual briefing feature.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    /// <summary>
    /// Defines <c>Value</c> for the visual briefing feature.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; set; }

    /// <summary>
    /// Defines <c>Arguments</c> for the visual briefing feature.
    /// </summary>
    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VisualBriefingFormulaNode>? Arguments { get; set; }
}