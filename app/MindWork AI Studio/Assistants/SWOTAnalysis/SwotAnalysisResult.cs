using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

public sealed class SwotAnalysisResult
{
    [JsonPropertyName("matrix_heading")]
    public string MatrixHeading { get; set; } = string.Empty;

    [JsonPropertyName("internal_label")]
    public string InternalLabel { get; set; } = string.Empty;

    [JsonPropertyName("external_label")]
    public string ExternalLabel { get; set; } = string.Empty;

    [JsonPropertyName("positive_label")]
    public string PositiveLabel { get; set; } = string.Empty;

    [JsonPropertyName("negative_label")]
    public string NegativeLabel { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public SwotCategory Strengths { get; set; } = new();

    [JsonPropertyName("weaknesses")]
    public SwotCategory Weaknesses { get; set; } = new();

    [JsonPropertyName("opportunities")]
    public SwotCategory Opportunities { get; set; } = new();

    [JsonPropertyName("threats")]
    public SwotCategory Threats { get; set; } = new();

    [JsonPropertyName("prioritized_actions")]
    public SwotPrioritizedActions PrioritizedActions { get; set; } = new();

    [JsonIgnore]
    public IReadOnlyList<SwotCategory> Categories =>
    [
        this.Strengths,
        this.Weaknesses,
        this.Opportunities,
        this.Threats,
    ];
}

public sealed class SwotCategory
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<SwotFinding> Findings { get; set; } = [];
}

public sealed class SwotFinding
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}

public sealed class SwotPrioritizedActions
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<SwotAction> Items { get; set; } = [];
}

public sealed class SwotAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("addressed_factors")]
    public List<string> AddressedFactors { get; set; } = [];
}