namespace AIStudio.Tools.Rust;

/// <summary>
/// What became of the page a document was to be shown at, as the runtime reports it.
/// </summary>
/// <remarks>
/// The runtime names these values in PageOutcome in runtime/src/file_actions.rs. The app tells the user
/// from them why a document opened without its page, so that they know AI Studio could not do otherwise.
/// </remarks>
public enum DocumentPageOutcome
{
    /// <summary>A value of a newer runtime which the app does not know.</summary>
    UNKNOWN,

    /// <summary>The document was handed to its program together with the page.</summary>
    APPLIED,

    /// <summary>No page was asked for.</summary>
    NOT_REQUESTED,

    /// <summary>AI Studio runs as a Flatpak, whose sandbox lets no page through to the program.</summary>
    SANDBOXED,

    /// <summary>The program which shows the document cannot be told a page, or the document is no PDF.</summary>
    PROGRAM_TAKES_NO_PAGE,

    /// <summary>The program takes a page, but it could not be started with one.</summary>
    PROGRAM_FAILED,

    /// <summary>The document did not open at all.</summary>
    DOCUMENT_NOT_OPENED
}