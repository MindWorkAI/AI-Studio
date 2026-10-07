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
/// <param name="TokenCount">The number of tokens of the content, or null when it is unknown.</param>
/// <param name="PageNumber">The page the content came from, or null when it has none.</param>
public readonly record struct ContentStreamProcessedEvent(string? Content, ContentStreamErrorDetails? Error, ContentStreamPromptInjectionDetails? PromptInjection = null, int? TokenCount = null, int? PageNumber = null)
{
    /// <summary>
    /// An event which neither produced content nor reported a failure.
    /// </summary>
    public static readonly ContentStreamProcessedEvent NOTHING = new(null, null);

    /// <summary>
    /// An event which produced content, with the token count and the page of that very content.
    /// </summary>
    /// <remarks>
    /// The count travels with the content because a reader may hold content back across several
    /// events: pairing it with the count of the event which released it would size it by the
    /// wrong text. The page travels along for the same reason, and so that whoever indexes the
    /// content is told where it came from instead of having to read it back out of the text.
    /// </remarks>
    /// <param name="content">The content to append.</param>
    /// <param name="tokenCount">The number of tokens of that content, or null when it is unknown.</param>
    /// <param name="pageNumber">The page that content came from, or null when it has none.</param>
    public static ContentStreamProcessedEvent FromContent(string? content, int? tokenCount = null, int? pageNumber = null) => new(content, null, TokenCount: tokenCount, PageNumber: pageNumber);

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