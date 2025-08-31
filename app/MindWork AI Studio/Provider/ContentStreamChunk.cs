namespace AIStudio.Provider;

/// <summary>
/// A chunk of content from a content stream, along with its associated sources.
/// </summary>
/// <param name="Content">The text content of the chunk.</param>
/// <param name="Sources">The list of sources associated with the chunk.</param>
public sealed record ContentStreamChunk(string Content, IList<ISource> Sources)
{
    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    /// <param name="chunk">The content stream chunk.</param>
    /// <returns>The text content of the chunk.</returns>
    public static implicit operator string(ContentStreamChunk chunk) => chunk.Content;
}