namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data model for a delta line in the Response API chat completion stream.
/// </summary>
/// <param name="Type">The type of the response.</param>
/// <param name="Delta">The delta content of the response.</param>
public record ResponsesDeltaStreamLine(
    string Type,
    string? Delta) : IResponseStreamLine
{
    #region Implementation of IResponseStreamLine

    /// <inheritdoc />
    public bool ContainsContent() => this.Delta is not null;

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Delta ?? string.Empty, this.GetSources());

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