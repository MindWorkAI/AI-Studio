namespace AIStudio.Provider;

/// <summary>
/// A chunk of content from a content stream, along with its associated sources.
/// </summary>
/// <remarks>
/// The usage rides along on the chunk rather than being reported next to the stream, because a
/// provider states it as one more line of that same stream. It is unknown on every chunk but the
/// one which carries it, and unknown on all of them at the providers which report nothing.
/// </remarks>
/// <param name="Content">The text content of the chunk.</param>
/// <param name="Sources">The list of sources associated with the chunk.</param>
/// <param name="Usage">What the provider said the request cost, where it said anything.</param>
public sealed record ContentStreamChunk(string Content, IList<ISource> Sources, TokenUsage Usage = default)
{
    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    /// <param name="chunk">The content stream chunk.</param>
    /// <returns>The text content of the chunk.</returns>
    public static implicit operator string(ContentStreamChunk chunk) => chunk.Content;
}