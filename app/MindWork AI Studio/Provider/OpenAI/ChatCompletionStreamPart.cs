namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What one line of a streamed Chat Completions answer has to show to the user.
/// </summary>
/// <param name="TextDelta">The text this line carried, empty when it carried none.</param>
public readonly record struct ChatCompletionStreamPart(string TextDelta)
{
    /// <summary>
    /// The part of a line which says nothing to the user, such as a fragment of a tool call.
    /// </summary>
    public static ChatCompletionStreamPart Nothing => new(string.Empty);
    
    /// <summary>
    /// Whether this part has anything to show at all.
    /// </summary>
    public bool HasContent => this.TextDelta.Length > 0;
}