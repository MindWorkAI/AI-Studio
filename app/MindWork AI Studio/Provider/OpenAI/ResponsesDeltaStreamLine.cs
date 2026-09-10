namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data model for a delta line in the Response API chat completion stream.
/// </summary>
/// <param name="Type">The type of the response.</param>
/// <param name="Delta">The delta content of the response.</param>
/// <param name="SummaryIndex">The reasoning summary part this line belongs to.</param>
public record ResponsesDeltaStreamLine(
    string Type,
    string? Delta,
    int SummaryIndex) : IResponseStreamLine
{
    private const string SUMMARY_PART_ADDED = "response.reasoning_summary_part.added";
    private const string SUMMARY_TEXT_DELTA = "response.reasoning_summary_text.delta";
    private const string REASONING_TEXT_DELTA = "response.reasoning_text.delta";

    /// <summary>
    /// Whether this line starts a reasoning summary part which follows an earlier one.
    /// </summary>
    /// <remarks>
    /// A reasoning summary arrives as several parts, and each one reads as its own paragraph.
    /// The stream carries no separator between them, so the boundary has to come from the
    /// event which announces the next part.
    /// </remarks>
    private bool IsFollowUpSummaryPart => this.Type is SUMMARY_PART_ADDED && this.SummaryIndex > 0;

    #region Implementation of IResponseStreamLine

    /// <inheritdoc />
    public bool ContainsContent() => this.Delta is not null || this.IsFollowUpSummaryPart;

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => this.Type switch
    {
        SUMMARY_PART_ADDED => new(string.Empty, this.GetSources(), this.IsFollowUpSummaryPart ? $"{Environment.NewLine}{Environment.NewLine}" : string.Empty),
        SUMMARY_TEXT_DELTA or REASONING_TEXT_DELTA => new(string.Empty, this.GetSources(), this.Delta ?? string.Empty),
        _ => new(this.Delta ?? string.Empty, this.GetSources()),
    };

    //
    // Please note that there are multiple options where LLM providers might stream sources:
    //
    // - As part of the delta content while streaming. That would be part of this class.
    // - By using a dedicated stream event and data structure. That would be another class implementing IResponseStreamLine.
    //
    // Right now, OpenAI uses the latter approach, so we don't have any sources here. And
    // because no other provider does it yet, we don't have any implementation here either.
    //
    // One example where sources are part of the delta content is the Perplexity provider.
    //
    
    /// <inheritdoc />
    public bool ContainsSources() => false;

    /// <inheritdoc />
    public IList<ISource> GetSources() => [];

    #endregion
}
