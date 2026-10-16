namespace AIStudio.Provider.OpenAI;

/// <summary>
/// One line of a streamed Chat Completions answer, as the tool calling rounds read it.
/// </summary>
/// <remarks>
/// The plain text path reads the very same lines through its own provider-specific type, which
/// knows about text and about the sources some providers put in it. Reading a line twice costs
/// nothing next to the request it arrived on, and it keeps the tool calls out of a type every
/// provider implements -- including those which never call a tool.
/// </remarks>
/// <param name="Id">The ID of the answer.</param>
/// <param name="Choices">The choices this line adds to.</param>
public sealed record ChatCompletionToolStreamLine(string? Id, IList<ChatCompletionToolStreamChoice?>? Choices)
{
    /// <summary>
    /// What the provider says the request cost, on the one line which carries it.
    /// </summary>
    /// <remarks>
    /// The same block the plain text path reads, and asked for the same way: every streamed
    /// ChatCompletionAPIRequest asks for it, the requests of the tool rounds included. Not a
    /// positional parameter, because nobody but the serializer ever builds this line.
    /// </remarks>
    public ChatCompletionUsage? Usage { get; init; }
}