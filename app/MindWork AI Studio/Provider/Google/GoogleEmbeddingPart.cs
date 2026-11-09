namespace AIStudio.Provider.Google;

/// <summary>
/// One text in a request for an embedding to the Gemini API.
/// </summary>
/// <param name="Text">The text to embed.</param>
public sealed record GoogleEmbeddingPart(string Text);