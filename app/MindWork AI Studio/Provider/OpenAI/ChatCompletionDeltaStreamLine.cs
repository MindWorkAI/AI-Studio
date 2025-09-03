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
    
    /// <inheritdoc />
    public bool ContainsContent() => this.Choices.Count > 0;

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Choices[0].Delta.Content, []);

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