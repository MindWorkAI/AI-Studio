// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Represents a response stream line.
/// </summary>
/// <param name="Type">The type of the response line.</param>
/// <param name="Index">The index of the response line.</param>
/// <param name="Delta">The delta of the response line.</param>
public readonly record struct ResponseStreamLine(string Type, int Index, Delta Delta) : IResponseStreamLine
{
    /// <summary>
    /// The message the stream opens with, on the message start event only.
    /// </summary>
    /// <remarks>
    /// Not a positional parameter, because nobody but the serializer ever builds this line with
    /// one. Only the opening event carries a message, which makes it the only line with a usage.
    /// </remarks>
    public AnthropicStreamMessage? Message { get; init; }

    /// <inheritdoc />
    public bool ContainsContent() => this != default && !string.IsNullOrWhiteSpace(this.Delta.Text);

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Delta.Text, []);

    /// <inheritdoc />
    /// <remarks>
    /// Read off the message start event, the first line of the stream, and never off the message
    /// delta at its end. That one carries a usage as well, but a cumulative one: once the model
    /// used a server tool, it holds what the tool fed back into the same request. The example in
    /// the streaming documentation, read on 2026-09-24 at
    /// https://platform.claude.com/docs/en/build-with-claude/streaming, shows 2,679 input tokens at
    /// the start of a message with a web search and 10,682 at its end. No later request carries
    /// those search results; the start states exactly what this one carried.
    ///
    /// The number arriving before the answer is no problem, because it describes the request, not
    /// the answer. A stream which breaks off afterward leaves the answer with whatever arrived up
    /// to then, nothing at all included, and the next request carries exactly that -- which is
    /// what the answer's text is counted as.
    /// </remarks>
    public TokenUsage GetUsage() => this.Message?.Usage?.ToTokenUsage() ?? TokenUsage.UNKNOWN;

    #region Implementation of IAnnotationStreamLine

    //
    // Please note: Anthropic's API does not currently support sources in their
    // OpenAI-compatible response stream.
    //

    /// <inheritdoc />
    public bool ContainsSources() => false;

    /// <inheritdoc />
    public IList<ISource> GetSources() => [];

    #endregion
}