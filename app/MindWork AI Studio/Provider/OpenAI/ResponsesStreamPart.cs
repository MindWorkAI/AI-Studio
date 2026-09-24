namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What one line of a streamed Responses API call has to show to the user.
/// </summary>
/// <param name="TextDelta">The text this line carried, empty when it carried none.</param>
/// <param name="Sources">The sources this line announced, empty when it announced none.</param>
/// <param name="Usage">What the provider said the request cost, unknown on every line but the completed event.</param>
public readonly record struct ResponsesStreamPart(string TextDelta, IList<ISource> Sources, TokenUsage Usage = default)
{
    /// <summary>
    /// The part of a line which says nothing to the user, such as a bookkeeping event.
    /// </summary>
    public static ResponsesStreamPart Nothing => new(string.Empty, []);

    /// <summary>
    /// Whether this part has anything to show at all.
    /// </summary>
    /// <remarks>
    /// The usage is not part of that: it is nothing to show, and whether it reaches the answer at
    /// all is the tool calling loop's decision, which knows which round this is.
    /// </remarks>
    public bool HasContent => this.TextDelta.Length > 0 || this.Sources.Count > 0;
}