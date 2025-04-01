namespace AIStudio.Tools.ERIClient.DataModel;

/// <summary>
/// The retrieval request sent by AI Studio.
/// </summary>
/// <remarks>
/// Images and other media are base64 encoded.
/// </remarks>
/// <param name="LatestUserPrompt">The latest user prompt that AI Studio received.</param>
/// <param name="LatestUserPromptType">The type of the latest user prompt, e.g., text, image, etc.</param>
/// <param name="Thread">The chat thread that the user is currently in.</param>
/// <param name="RetrievalProcessId">Optional. The ID of the retrieval process that the data source should use.
/// When null, the data source chooses an appropriate retrieval process. Selecting a retrieval process is optional
/// for AI Studio users. Most users do not specify a retrieval process.</param>
/// <param name="Parameters">A dictionary of parameters that the data source should use for the retrieval process.
/// Although each parameter will be sent as a string, the retrieval process specifies the expected type and range.</param>
/// <param name="MaxMatches">The maximum number of matches that the data source should return. AI Studio uses
/// any value below 1 to indicate that the data source should return as many matches as appropriate.</param>
public readonly record struct RetrievalRequest(
    string LatestUserPrompt,
    ContentType LatestUserPromptType,
    ChatThread Thread,
    string? RetrievalProcessId,
    Dictionary<string, string>? Parameters,
    int MaxMatches);