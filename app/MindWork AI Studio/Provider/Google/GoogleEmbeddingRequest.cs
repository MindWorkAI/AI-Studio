namespace AIStudio.Provider.Google;

/// <summary>
/// A request for an embedding to the Gemini API.
/// </summary>
/// <param name="Content">The texts to embed.</param>
public sealed record GoogleEmbeddingRequest(GoogleEmbeddingContent Content)
{
    /// <summary>
    /// Asks for vectors which tell how similar texts are.
    /// </summary>
    /// <remarks>
    /// Written as task_type, like every other field of our requests. The API reads both its field names in
    /// snake case and in camel case.
    /// </remarks>
    public string TaskType => "SEMANTIC_SIMILARITY";

    /// <summary>
    /// Creates the request for the given texts, each of them as a part of its own.
    /// </summary>
    public static GoogleEmbeddingRequest Of(IEnumerable<string> texts) => new(new GoogleEmbeddingContent([..texts.Select(text => new GoogleEmbeddingPart(text))]));
}