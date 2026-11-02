namespace AIStudio.Provider;

/// <summary>
/// A request for embeddings, in the shape of the OpenAI API, which most providers take.
/// </summary>
/// <param name="Model">The name of the embedding model.</param>
/// <param name="Input">The texts to embed.</param>
public sealed record EmbeddingRequest(string Model, IList<string> Input)
{
    /// <summary>
    /// Asks for the vectors as numbers, instead of as base64.
    /// </summary>
    public string EncodingFormat => "float";
}