namespace AIStudio.Assistants.ERI;

/// <summary>
/// Information about a retrieval process, which this data source implements.
/// </summary>
/// <param name="Name">The name of the retrieval process, e.g., "Keyword-Based Wikipedia Article Retrieval".</param>
/// <param name="Description">A short description of the retrieval process. What kind of retrieval process is it?</param>
/// <param name="Link">A link to the retrieval process's documentation, paper, Wikipedia article, or the source code. Might be null.</param>
/// <param name="ParametersDescription">A dictionary that describes the parameters of the retrieval process. The key is the parameter name,
/// and the value is a description of the parameter. Although each parameter will be sent as a string, the description should indicate the
/// expected type and range, e.g., 0.0 to 1.0 for a float parameter.</param>
/// <param name="Embeddings">A list of embeddings used in this retrieval process. It might be empty in case no embedding is used.</param>
public readonly record struct RetrievalInfo(
    string Name,
    string Description,
    string? Link,
    Dictionary<string, string>? ParametersDescription,
    List<EmbeddingInfo>? Embeddings);