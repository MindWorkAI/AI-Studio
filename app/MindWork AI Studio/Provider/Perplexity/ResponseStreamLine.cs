using AIStudio.Provider.OpenAI;

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

    /// <summary>
    /// What Perplexity says the request cost, on the one line which carries it.
    /// </summary>
    /// <remarks>
    /// The same block the OpenAI chat completion API sends, because this is that wire format.
    /// Not a positional parameter: the struct is built from JSON, and a further parameter would
    /// only be a value nobody passes.
    /// </remarks>
    public ChatCompletionUsage? Usage { get; init; }

    /// <inheritdoc />
    public bool ContainsUsage() => this.GetUsage().IsKnown;

    /// <inheritdoc />
    public TokenUsage GetUsage() => this.Usage is null ? TokenUsage.UNKNOWN : TokenUsage.OfReported(this.Usage.PromptTokens, this.Usage.CompletionTokens);
    
    /// <inheritdoc />
    public bool ContainsSources() => this != default && this.SearchResults.Count > 0;

    /// <inheritdoc />
    public IList<ISource> GetSources() => this.SearchResults.Cast<ISource>().ToList();
}