// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The delta object of a response line.
/// </summary>
/// <param name="Type">The type of the delta.</param>
/// <param name="Text">The text of the delta.</param>
public readonly record struct Delta(string Type, string Text)
{
    /// <summary>
    /// The thinking of the delta, on a thinking delta only.
    /// </summary>
    /// <remarks>
    /// Read only to tell that the model is thinking, see ContentStreamChunk.IsReasoning. Not a
    /// positional parameter, because nobody but the serializer ever builds a delta with one.
    /// </remarks>
    public string? Thinking { get; init; }
}