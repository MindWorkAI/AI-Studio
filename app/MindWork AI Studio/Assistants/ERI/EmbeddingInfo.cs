namespace AIStudio.Assistants.ERI;

/// <summary>
/// Represents information about the used embedding for a data source.
/// </summary>
/// <param name="EmbeddingType">What kind of embedding is used. For example, "Transformer Embedding," "Contextual Word
/// Embedding," "Graph Embedding," etc.</param>
/// <param name="EmbeddingName">Name the embedding used. This can be a library, a framework, or the name of the used
/// algorithm.</param>
/// <param name="Description">A short description of the embedding. Describe what the embedding is doing.</param>
/// <param name="UsedWhen">Describe when the embedding is used. For example, when the user prompt contains certain
/// keywords, or anytime?</param>
/// <param name="Link">A link to the embedding's documentation or the source code. Might be null.</param>
public readonly record struct EmbeddingInfo(
    string EmbeddingType,
    string EmbeddingName,
    string Description,
    string UsedWhen,
    string? Link);