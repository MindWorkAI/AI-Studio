namespace AIStudio.Provider.Google;

/// <summary>
/// The content of a request for an embedding to the Gemini API.
/// </summary>
/// <param name="Parts">The texts, each of them as a part of its own.</param>
public sealed record GoogleEmbeddingContent(IList<GoogleEmbeddingPart> Parts);