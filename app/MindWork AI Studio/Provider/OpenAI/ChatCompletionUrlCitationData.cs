namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Represents citation data for a URL in a chat completion response.
/// </summary>
/// <param name="EndIndex">The end index of the citation in the response text.</param>
/// <param name="StartIndex">The start index of the citation in the response text.</param>
/// <param name="Title">The title of the cited source.</param>
/// <param name="URL">The URL of the cited source.</param>
public sealed record ChatCompletionUrlCitationData(
    int EndIndex,
    int StartIndex,
    string Title,
    string URL);