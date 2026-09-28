namespace AIStudio.Provider.Anthropic;

/// <summary>
/// What one line of a streamed Anthropic messages call has to show to the user.
/// </summary>
/// <remarks>
/// Only text ever shows. Thinking does not: neither of the two paths has ever put it on screen,
/// and doing so would be a feature of its own rather than a side effect of streaming.
/// </remarks>
/// <param name="TextDelta">The text this line carried, empty when it carried none.</param>
/// <param name="Usage">What the provider said the request carried, unknown on every line but the message start.</param>
public readonly record struct AnthropicStreamPart(string TextDelta, TokenUsage Usage = default)
{
    /// <summary>
    /// The part of a line that says nothing to the user, such as an opening or closing block.
    /// </summary>
    public static AnthropicStreamPart Nothing => new(string.Empty);

    /// <summary>
    /// Whether this part has anything to show at all.
    /// </summary>
    /// <remarks>
    /// The usage is not part of that: it is nothing to show, and whether it reaches the answer at
    /// all is the tool calling loop's decision, which knows which round this is.
    /// </remarks>
    public bool HasContent => this.TextDelta.Length > 0;
}