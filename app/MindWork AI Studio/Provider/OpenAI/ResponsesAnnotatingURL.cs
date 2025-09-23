namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data structure for URL citation annotations in the OpenAI Responses API.
/// </summary>
/// <param name="Type">The type of annotation, typically "url_citation".</param>
/// <param name="EndIndex">The end index of the annotated text in the response.</param>
/// <param name="StartIndex">The start index of the annotated text in the response.</param>
/// <param name="Title">The title of the cited URL.</param>
/// <param name="URL">The URL being cited.</param>
public sealed record ResponsesAnnotatingUrlCitationData(
    string Type,
    int EndIndex,
    int StartIndex,
    string Title,
    string URL) : Annotation(Type);