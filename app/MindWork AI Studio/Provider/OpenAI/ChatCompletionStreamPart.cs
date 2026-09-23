namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What one line of a streamed Chat Completions answer has to show to the user.
/// </summary>
/// <param name="TextDelta">The text this line carried, empty when it carried none.</param>
/// <param name="Sources">The sources this line announced, empty when it announced none.</param>
/// <param name="Usage">What the provider said the request cost, unknown on every line but the one which carries it.</param>
public readonly record struct ChatCompletionStreamPart(string TextDelta, IList<ISource> Sources, TokenUsage Usage = default)
{
    /// <summary>
    /// The part of a line which says nothing to the user, such as a fragment of a tool call.
    /// </summary>
    public static ChatCompletionStreamPart Nothing => new(string.Empty, []);

    /// <summary>
    /// Whether this part has anything to show at all.
    /// </summary>
    /// <remarks>
    /// The usage is not part of that: it is nothing to show, and whether it is passed on at all is
    /// the adapter's decision, which knows which round this is.
    /// </remarks>
    public bool HasContent => this.TextDelta.Length > 0 || this.Sources.Count > 0;
}