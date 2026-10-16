namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores a safe structural diagnostic without model output or user content.
/// </summary>
public sealed class VisualBriefingStructuredResponseDiagnostic
{
    /// <summary>
    /// Gets or sets the stable structural issue kind.
    /// </summary>
    public VisualBriefingStructuredResponseIssueKind IssueKind { get; set; }

    /// <summary>
    /// Gets or sets the envelope containing the selected candidate.
    /// </summary>
    public VisualBriefingStructuredResponseEnvelope Envelope { get; set; }

    /// <summary>
    /// Gets or sets the one-based candidate index.
    /// </summary>
    public int CandidateIndex { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of eligible candidates in the response.
    /// </summary>
    public int CandidateCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets a safe JSON path containing only contract property names, indices, and wildcards.
    /// </summary>
    public string JsonPath { get; set; } = "$";

    /// <summary>
    /// Gets or sets the one-based line in the complete model response.
    /// </summary>
    public long? LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the zero-based UTF-8 byte position in the line.
    /// </summary>
    public long? BytePositionInLine { get; set; }

    /// <summary>
    /// Gets or sets a sanitized contract field name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a content-free expected contract shape.
    /// </summary>
    public string Expected { get; set; } = string.Empty;

    /// <summary>
    /// Formats the diagnostic for content-free technical details.
    /// </summary>
    /// <returns>A stable semicolon-separated diagnostic.</returns>
    internal string ToTechnicalDetails()
    {
        var details = new List<string>
        {
            $"StructuredIssue={this.IssueKind}",
            $"Envelope={this.Envelope}",
            $"Candidate={this.CandidateIndex}/{this.CandidateCount}",
            $"JsonPath={this.JsonPath}",
        };
        if (this.LineNumber is not null)
            details.Add($"Line={this.LineNumber}");
        if (this.BytePositionInLine is not null)
            details.Add($"BytePositionInLine={this.BytePositionInLine}");
        if (!string.IsNullOrEmpty(this.FieldName))
            details.Add($"Field={this.FieldName}");
        if (!string.IsNullOrEmpty(this.Expected))
            details.Add($"Expected={this.Expected}");
        return string.Join("; ", details);
    }
}