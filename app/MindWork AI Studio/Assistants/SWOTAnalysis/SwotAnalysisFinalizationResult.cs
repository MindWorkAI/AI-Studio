using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

internal sealed class SwotAnalysisFinalizationResult
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

    [JsonPropertyName("strengths_label")]
    public string StrengthsLabel { get; set; } = string.Empty;

    [JsonPropertyName("weaknesses_label")]
    public string WeaknessesLabel { get; set; } = string.Empty;

    [JsonPropertyName("opportunities_label")]
    public string OpportunitiesLabel { get; set; } = string.Empty;

    [JsonPropertyName("threats_label")]
    public string ThreatsLabel { get; set; } = string.Empty;

    [JsonPropertyName("prioritized_actions")]
    public SwotAnalysisFinalizationActions PrioritizedActions { get; set; } = new();
}

internal sealed class SwotAnalysisFinalizationActions
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("empty_message")]
    public string EmptyMessage { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<SwotAnalysisFinalizationAction> Items { get; set; } = [];
}

internal sealed class SwotAnalysisFinalizationAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("addressed_factor_ids")]
    public List<string> AddressedFactorIds { get; set; } = [];
}