namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// What sort of content a chat received, apart from its messages.
/// </summary>
/// <remarks>
/// Stored by name, like the kinds of mail parts in the index: a new member may then go anywhere
/// in this list, and the database reads on its own.
/// </remarks>
public enum ProvenanceKind
{
    /// <summary>
    /// The content of a file the user attached to a message.
    /// </summary>
    ATTACHMENT,

    /// <summary>
    /// A passage a data source handed to the AI: a local one or an ERI server, through the classic
    /// process or the semantic search.
    /// </summary>
    DATA_SOURCE_PASSAGE,

    /// <summary>
    /// A mail, or a part of one, which the AI received from a mailbox.
    /// </summary>
    MAIL,

    /// <summary>
    /// A web page the AI received, e.g., which a tool read for it.
    /// </summary>
    WEB_PAGE,

    /// <summary>
    /// The whole result of one tool call, exactly as the AI received it.
    /// </summary>
    TOOL_RESULT,
}