namespace AIStudio.Tools.Rust;

/// <summary>
/// What became of a request to open the share sheet of the operating system. Keep in sync with
/// ShareOutcome in runtime/src/share_sheet.rs.
/// </summary>
public enum ShareSheetOutcome
{
    /// <summary>
    /// The runtime sent an outcome the app does not know, or none at all. The app reads this as a
    /// failure, never as an opened share sheet.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// The operating system opened its share sheet for the file.
    /// </summary>
    OPENED,

    /// <summary>
    /// The share sheet could not be opened, or the request was invalid.
    /// </summary>
    FAILED,

    /// <summary>
    /// Windows accepted the share request, but its share sheet never asked for the file. Windows
    /// reports no error then, and the share sheet simply does not appear.
    /// </summary>
    UNRESPONSIVE,
}