namespace AIStudio.Provider.Perplexity;

/// <summary>
/// Data model for a line in the response stream, for streaming completions.
/// </summary>
/// <param name="Id">The id of the response.</param>
/// <param name="Object">The object describing the response.</param>
/// <param name="Created">The timestamp of the response.</param>
/// <param name="Model">The model used for the response.</param>
/// <param name="SystemFingerprint">The system fingerprint; together with the seed, this allows you to reproduce the response.</param>
/// <param name="Choices">The choices made by the AI.</param>
public readonly record struct ResponseStreamLine(string Id, string Object, uint Created, string Model, string SystemFingerprint, IList<Choice> Choices, IList<SearchResult> SearchResults) : IResponseStreamLine
{
    /// <inheritdoc />
    public bool ContainsContent() => this != default && this.Choices.Count > 0;

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Choices[0].Delta.Content, this.GetSources());
    
    /// <inheritdoc />
    public bool ContainsSources() => this != default && this.SearchResults.Count > 0;

    /// <inheritdoc />
    public IList<ISource> GetSources() => this.SearchResults.Cast<ISource>().ToList();
}