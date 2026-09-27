namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

/// <summary>
/// Whether Read Web Page may open web addresses the AI chose on its own.
/// </summary>
/// <remarks>
/// Stored and configured by name, so a member must never be renamed: an organization addresses
/// these in its configuration, and a user has one of them saved. The numbers behind the names are
/// not persisted anywhere.<br/><br/>
/// Both modes are instructions to the model, not a technical check of where an address came from.
/// OFF is the default, because an address a model makes up is at best a page that does not exist
/// and at worst one that carries parts of the conversation to a server nobody chose.
/// </remarks>
public enum BraveMode
{
    /// <summary>
    /// Read only addresses which appear in the chat or which a tool returned.
    /// </summary>
    OFF,

    /// <summary>
    /// Also read addresses the AI chose itself.
    /// </summary>
    ON,
}