namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// How a tool comes to be offered to a model.
/// </summary>
public enum ToolActivation
{
    /// <summary>
    /// Offered when it was selected: by the user, a chat template, a policy, or an assistant.
    /// </summary>
    SELECTION,

    /// <summary>
    /// Offered whenever the chat calls for it, without anybody selecting it.
    /// </summary>
    /// <remarks>
    /// For a tool whose use is already decided somewhere else. Semantic Search is such a tool: the
    /// user picks the data sources of a chat, and a second switch for searching them would only be
    /// a way to contradict the first one. Such a tool never appears in a selection, and it decides
    /// on each request whether it has anything to offer.
    /// </remarks>
    CONTEXT,
}