namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

/// <summary>
/// From where Read Web Page reads documents, such as PDFs, Word files, presentations, or spreadsheets.
/// </summary>
/// <remarks>
/// Stored and configured by name, so a member must never be renamed: an organization addresses
/// these in its configuration, and a user has one of them saved.<br/><br/>
/// INTERNAL_ONLY is the default. Much of what an organization knows lies in the documents of its
/// intranet and its wiki, while a public document is one more way for a stranger to reach the
/// model with text of their choosing.
/// </remarks>
public enum DocumentAccess
{
    /// <summary>
    /// Read no documents, only pages and text formats.
    /// </summary>
    OFF,

    /// <summary>
    /// Read documents of the configured wiki and of the allowed private hosts.
    /// </summary>
    INTERNAL_ONLY,

    /// <summary>
    /// Read documents from every address the tool may read.
    /// </summary>
    ALL_ADDRESSES,
}