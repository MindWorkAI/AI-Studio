namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data structure for URL annotation in chat completions.
/// </summary>
/// <remarks>
/// Although this class is not directly intended for the Responses API, it is
/// used there as a fallback solution. One day, one of the open source LLM
/// drivers may use this data structure for their responses API.
/// </remarks>
/// <param name="Type">The type of annotation, typically "url_citation".</param>
/// <param name="UrlCitation">The URL citation details.</param>
public sealed record ChatCompletionAnnotatingURL(
    string Type,
    ChatCompletionUrlCitationData UrlCitation
) : Annotation(Type);