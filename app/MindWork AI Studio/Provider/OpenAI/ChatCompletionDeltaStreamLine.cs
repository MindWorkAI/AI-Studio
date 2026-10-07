namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data model for a delta line in the chat completion response stream.
/// </summary>
/// <param name="Id">The id of the response.</param>
/// <param name="Object">The object describing the response.</param>
/// <param name="Created">The timestamp of the response.</param>
/// <param name="Model">The model used for the response.</param>
/// <param name="SystemFingerprint">The system fingerprint; together with the seed, this allows you to reproduce the response.</param>
/// <param name="Choices">The choices made by the AI.</param>
public record ChatCompletionDeltaStreamLine(string Id, string Object, uint Created, string Model, string SystemFingerprint, IList<ChatCompletionChoice> Choices) : IResponseStreamLine
{
    public ChatCompletionDeltaStreamLine() : this(string.Empty, string.Empty, 0, string.Empty, string.Empty, [])
    {
    }
    
    /// <summary>
    /// What the provider says the request cost, on the one line which carries it.
    /// </summary>
    /// <remarks>
    /// Not a positional parameter: every provider builds an empty line through the constructor
    /// above, and a further parameter would change all of those call sites for a value none of
    /// them has. Providers send this block only when the request asked for it, and then on a final
    /// line of its own which carries no choices -- which is why the usage is read apart from the
    /// content rather than next to it.
    /// </remarks>
    public ChatCompletionUsage? Usage { get; init; }

    /// <inheritdoc />
    public bool ContainsContent() => this.Choices.Count > 0;

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Choices[0].Delta.Content, []);

    /// <inheritdoc />
    public TokenUsage GetUsage() => this.Usage?.ToTokenUsage() ?? TokenUsage.UNKNOWN;

    #region Implementation of IAnnotationStreamLine

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