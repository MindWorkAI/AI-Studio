namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// One row of the table provenance.
/// </summary>
/// <remarks>
/// The key is the chat together with the id. A chat which is copied, e.g., an assistant's
/// conversation which the user carries over into a chat, takes its rows along under the same ids,
/// so its references stay valid, and deleting one of the two chats leaves the other one whole.
/// </remarks>
internal sealed class ProvenanceEntity
{
    public string ChatId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The ProvenanceKind, stored by name.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The content around an ERI passage as a JSON array of strings, or null when there is none.
    /// </summary>
    public string? SurroundingContent { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}