namespace AIStudio.Tools.Databases.VectorStore;

/// <summary>
/// Thrown when a vector store is there on disk, but cannot be opened.
/// </summary>
/// <remarks>
/// Separate from every other database failure, because it is the one which no retry heals and which
/// the app must not heal on its own: building the index anew sends every document to the embedding
/// provider once more, which costs real money and, for a large data source, hours. So this failure
/// travels as its own type up to the places which can say so and offer the rebuild, and the decision
/// stays with the user.
/// </remarks>
public sealed class VectorStoreUnreadableException(string message) : Exception(message);