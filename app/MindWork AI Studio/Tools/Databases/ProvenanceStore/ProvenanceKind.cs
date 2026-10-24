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
    /// A passage a local data source handed to the AI.
    /// </summary>
    DATA_SOURCE_PASSAGE,

    /// <summary>
    /// A passage an ERI server handed to the AI, possibly with content around it.
    /// </summary>
    ERI_PASSAGE,

    /// <summary>
    /// A mail, or a part of one, which a tool handed to the AI.
    /// </summary>
    MAIL,

    /// <summary>
    /// A web page which a tool read for the AI.
    /// </summary>
    WEB_PAGE,

    /// <summary>
    /// The whole result of one tool call, exactly as the AI received it.
    /// </summary>
    TOOL_RESULT,
}