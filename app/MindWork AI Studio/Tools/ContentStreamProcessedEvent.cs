namespace AIStudio.Tools;

/// <summary>
/// The outcome of processing one content stream event: either content to append, or a reported
/// failure.
/// </summary>
/// <remarks>
/// Content and error are kept apart on purpose. A reported failure must never be appended as
/// content, because that would hand the failure to the AI as if it were part of the document.
/// </remarks>
/// <param name="Content">The content to append, or null when this event carries none.</param>
/// <param name="Error">The reported failure, or null when the event was processed successfully.</param>
/// <param name="PromptInjection">What the runtime filtered out of the content, or null when it filtered nothing.</param>
public readonly record struct ContentStreamProcessedEvent(string? Content, ContentStreamErrorDetails? Error, ContentStreamPromptInjectionDetails? PromptInjection = null)
{
    /// <summary>
    /// An event which neither produced content nor reported a failure.
    /// </summary>
    public static readonly ContentStreamProcessedEvent NOTHING = new(null, null);

    public static ContentStreamProcessedEvent FromContent(string? content) => new(content, null);

    public static ContentStreamProcessedEvent FromError(ContentStreamErrorDetails? error) => new(null, error);

    /// <summary>
    /// An event reporting that suspicious passages were filtered out of the content.
    /// </summary>
    /// <remarks>
    /// Carries no content and no error: the content was delivered by the events before it, and
    /// filtering is a notice rather than a failure.
    /// </remarks>
    public static ContentStreamProcessedEvent FromPromptInjection(ContentStreamPromptInjectionDetails? promptInjection) => new(null, null, promptInjection);
}