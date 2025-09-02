namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data model for a line in the response stream, for streaming chat completions.
/// </summary>
/// <param name="Id">The id of the response.</param>
/// <param name="Object">The object describing the response.</param>
/// <param name="Created">The timestamp of the response.</param>
/// <param name="Model">The model used for the response.</param>
/// <param name="SystemFingerprint">The system fingerprint; together with the seed, this allows you to reproduce the response.</param>
/// <param name="Choices">The choices made by the AI.</param>
public record ChatCompletionResponseStreamLine(string Id, string Object, uint Created, string Model, string SystemFingerprint, IList<ChatCompletionChoice> Choices) : IResponseStreamLine
{
    public ChatCompletionResponseStreamLine() : this(string.Empty, string.Empty, 0, string.Empty, string.Empty, [])
    {
    }
    
    /// <inheritdoc />
    public bool ContainsContent() => this.Choices.Count > 0;

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Choices[0].Delta.Content, []);

    #region Implementation of IAnnotationStreamLine

    /// <inheritdoc />
    public bool ContainsSources() => false;

    /// <inheritdoc />
    public IList<ISource> GetSources() => [];

    #endregion
}